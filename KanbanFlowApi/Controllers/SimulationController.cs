using KanbanFlowApi.Dtos;
using KanbanFlowApi.Dtos.Config;
using KanbanFlowApi.Dtos.Metrics;
using KanbanFlowApi.Dtos.Task;
using KanbanFlowApi.Mappers;
using KanbanFlowApi.Services;
using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Services;
using Microsoft.AspNetCore.Mvc;
using Simulation = KanbanFlowSerivce.Dtos.Simulation;

namespace KanbanFlowApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SimulationController : ControllerBase
{
    /// <summary>
    /// Запустить симуляцию из полной конфигурации.
    /// Возвращает состояние симуляции на день 0 (готово к началу работы).
    /// Если указан параметр DaysToSimulate - выполняет симуляцию на N дней или до завершения всех задач.
    /// </summary>
    /// <param name="request">Запрос с полной конфигурацией</param>
    [HttpPost("start")]
    public ActionResult<ApiSimulationStateDto> StartSimulation([FromBody] StartSimulationRequestDto request)
    {
        // Валидация конфигурации
        if (request == null)
        {
            return BadRequest(new { error = "Конфигурация не может быть пустой" });
        }

        if (request.Workers == null || request.Workers.Count == 0)
        {
            return BadRequest(new { error = "Конфигурация должна содержать хотя бы одного работника" });
        }

        // Логины воркеров используются как ключ при round-trip состояния симуляции
        // (ApiMapper.ToDomainBoard ищет воркера по логину через .Single) — дубликаты
        // проходят /start без ошибок, но валят первый же /simulate-day необработанным
        // исключением. Проверяем здесь, чтобы конфигурация с дублями вообще не запускалась.
        var duplicateWorkerLogins = request.Workers
            .GroupBy(w => w.Login)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateWorkerLogins.Count > 0)
        {
            return BadRequest(new
            {
                error = $"Логины воркеров должны быть уникальны. Дублируются: {string.Join(", ", duplicateWorkerLogins)}"
            });
        }

        if (request.Workflow == null || request.Workflow.Stages == null || request.Workflow.Stages.Count == 0)
        {
            return BadRequest(new { error = "Конфигурация должна содержать хотя бы одну стадию" });
        }

        if (request.Tasks == null || request.Tasks.Count == 0)
        {
            return BadRequest(new { error = "Конфигурация должна содержать хотя бы одну задачу" });
        }

        // Преобразуем в доменную конфигурацию
        var domainConfig = ApiMapper.ToDomainConfig(request);

        // Создаём и инициализируем симуляцию
        var simulation = new Simulation();
        simulation.InitFromConfig(domainConfig);

        // Если указан параметр DaysToSimulate - выполняем симуляцию
        if (request.DaysToSimulate.HasValue)
        {
            var daysToSimulate = request.DaysToSimulate.Value;
            var maxDays = daysToSimulate == 0 ? 10000 : daysToSimulate; // 0 = до конца (ограничиваем 10000 дней)

            var validationService = new SimulationValidationService(simulation);
            var movementService = new TaskMovementService(simulation);
            var workProgressService = new WorkProgressService(simulation);

            for (var day = 0; day < maxDays; day++)
            {
                // Проверяем, можно ли продолжить симуляцию
                var validationResult = validationService.ValidateCanContinue();
                if (!validationResult.IsValid)
                {
                    break;
                }

                // Начинаем новый день
                simulation.StartNewDay();

                // Обрабатываем перемещения задач (перед работой)
                movementService.ProcessMovements();

                // Симулируем выполнение работы и получаем список завершённых задач
                var completedTasks = workProgressService.SimulateWorkDay();

                // Обрабатываем перемещения завершённых задач (после работы)
                if (completedTasks.Count > 0)
                {
                    movementService.ProcessMovements(completedTasks);
                }

                // Проверяем, завершилась ли симуляция (все задачи в Done)
                var allTasksDone = simulation.Board.Tasks.All(t =>
                    t.CurrentStage?.Stage.Name == "Done" || t.CurrentStage == null);

                if (allTasksDone)
                {
                    break;
                }
            }
        }

        // Возвращаем состояние (на день 0 или после симуляции N дней)
        return Ok(ApiMapper.ToApiDto(simulation));
    }

    /// <summary>
    /// Выполнить расчёт одного дня симуляции.
    /// Принимает полное состояние симуляции (конфиг + доска + история) и возвращает обновлённое состояние.
    /// Можно использовать результат предыдущего вызова для симуляции следующего дня.
    /// </summary>
    /// <param name="state">Состояние симуляции</param>
    [HttpPost("simulate-day")]
    public ActionResult<ApiSimulationStateDto> SimulateDay([FromBody] ApiSimulationStateDto state)
    {
        // Восстанавливаем доменную симуляцию из DTO
        var simulation = ApiMapper.ToDomainSimulation(state);

        // Проверяем, можно ли продолжить симуляцию (сервисный слой)
        var validationService = new SimulationValidationService(simulation);
        var validationResult = validationService.ValidateCanContinue();
        if (!validationResult.IsValid)
        {
            return BadRequest(new { error = validationResult.ErrorMessage });
        }

        // Начинаем новый день (увеличиваем CurrentDay)
        simulation.StartNewDay();

        // Обрабатываем перемещения задач (перед работой)
        var movementService = new TaskMovementService(simulation);
        movementService.ProcessMovements();

        // Симулируем выполнение работы и получаем список завершённых задач
        var workProgressService = new WorkProgressService(simulation);
        var completedTasks = workProgressService.SimulateWorkDay();

        // Обрабатываем перемещения завершённых задач (после работы)
        if (completedTasks.Count > 0)
        {
            movementService.ProcessMovements(completedTasks);
        }

        // Возвращаем полное обновлённое состояние.
        // Если все задачи в Done — следующий вызов /simulate-day вернёт 400 из ValidateCanContinue.
        return Ok(ApiMapper.ToApiDto(simulation));
    }

    /// <summary>
    /// Рассчитать все метрики симуляции (общие, работников, задач, стадий).
    /// Принимает полное состояние симуляции и возвращает полный набор метрик.
    /// </summary>
    /// <param name="state">Состояние симуляции</param>
    [HttpPost("all-metrics")]
    public ActionResult<AllMetricsDto> CalculateAllMetrics([FromBody] ApiSimulationStateDto state)
    {
        // Восстанавливаем доменную симуляцию из DTO
        var simulation = ApiMapper.ToDomainSimulation(state);

        // Рассчитываем все метрики
        var metricsService = new MetricsService(simulation);
        var workerMetricsService = new WorkerMetricsService(simulation);
        var taskMetricsService = new TaskMetricsService(simulation);

        return Ok(new AllMetricsDto
        {
            SimulationMetrics = metricsService.CalculateAllMetrics(),
            WorkerMetrics = workerMetricsService.CalculateAllWorkersMetrics(),
            TaskMetrics = taskMetricsService.CalculateAllTasksMetrics(),
            StageMetrics = taskMetricsService.CalculateStageMetricsAggregated()
        });
    }
}
