using KanbanFlowApi.Dtos;
using KanbanFlowApi.Dtos.Config;
using KanbanFlowApi.Dtos.Metrics;
using KanbanFlowApi.Dtos.Task;
using KanbanFlowApi.Factories;
using KanbanFlowApi.Mappers;
using KanbanFlowApi.Services;
using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Factories;
using KanbanFlowSerivce.Services;
using Microsoft.AspNetCore.Mvc;

namespace KanbanFlowApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SimulationController : ControllerBase
{
    /// <summary>
    /// Получить список доступных пресетов производственных процессов.
    /// </summary>
    [HttpGet("process-presets")]
    public ActionResult<List<ProcessPresetDto>> GetProcessPresets()
    {
        var presets = ProcessPresetsFactory.GetAllPresets();
        return Ok(presets);
    }

    /// <summary>
    /// Получить список доступных пресетов пулов работников.
    /// </summary>
    [HttpGet("worker-pools")]
    public ActionResult<List<WorkerPoolPresetDto>> GetWorkerPools()
    {
        var presets = WorkerPoolPresetsFactory.GetAllPresets();
        return Ok(presets);
    }

    /// <summary>
    /// Получить список доступных пресетов задач.
    /// </summary>
    [HttpGet("task-presets")]
    public ActionResult<List<TaskPresetDto>> GetTaskPresets()
    {
        var presets = TaskPresetsFactory.GetAllPresets();
        return Ok(presets);
    }

    /// <summary>
    /// Запустить симуляцию из комбинации пресетов.
    /// Возвращает состояние симуляции на день 0 (готово к началу работы).
    /// Если указан параметр DaysToSimulate - выполняет симуляцию на N дней или до завершения всех задач.
    /// </summary>
    /// <param name="request">Запрос с именами пресетов</param>
    [HttpPost("start")]
    public ActionResult<ApiSimulationStateDto> StartSimulation([FromBody] StartSimulationRequestDto request)
    {
        // Получаем пресет процесса
        var processPreset = ProcessPresetsFactory.GetPresetByName(request.ProcessPresetName);
        if (processPreset == null)
        {
            return BadRequest(new { error = $"Процесс '{request.ProcessPresetName}' не найден" });
        }

        // Получаем пресет работников
        var workerPoolPreset = WorkerPoolPresetsFactory.GetPresetByName(request.WorkerPoolPresetName);
        if (workerPoolPreset == null)
        {
            return BadRequest(new { error = $"Пул работников '{request.WorkerPoolPresetName}' не найден" });
        }

        // Получаем пресет задач (опционально)
        List<ApiTaskDto> tasks;
        if (!string.IsNullOrEmpty(request.TaskPresetName))
        {
            var taskPreset = TaskPresetsFactory.GetPresetByName(request.TaskPresetName);
            if (taskPreset == null)
            {
                return BadRequest(new { error = $"Пресет задач '{request.TaskPresetName}' не найден" });
            }
            tasks = taskPreset.Tasks;
        }
        else
        {
            // Используем задачи из процесса
            tasks = processPreset.Tasks;
        }

        // Собираем конфигурацию
        var config = new ApiSimulationConfigDto
        {
            Seed = request.Seed,
            UseVariability = request.UseVariability,
            Workflow = processPreset.Workflow,
            Workers = workerPoolPreset.Workers,
            Tasks = tasks
        };

        // Создаём доменную конфигурацию через маппер
        var domainConfig = ApiMapper.ToDomainConfig(config);

        // Инициализируем симуляцию
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
    /// Получить список доступных конфигураций симуляции (устаревший endpoint).
    /// </summary>
    [HttpGet("presets")]
    public ActionResult<List<ConfigPresetDto>> GetConfigPresets()
    {
        var presets = SimulationFactory.GetAvailablePresets()
            .Select(p => new ConfigPresetDto
            {
                Name = p.Name,
                DisplayName = p.DisplayName,
                Description = p.Description,
                IsDefault = p.IsDefault
            })
            .ToList();

        return Ok(presets);
    }

    /// <summary>
    /// Получить начальное состояние симуляции по умолчанию.
    /// Возвращает готовое состояние для передачи в simulate-day (день 0, задачи ещё не двигались).
    /// </summary>
    /// <param name="configName">Название конфигурации: "default", "twork" или "simple". По умолчанию "default".</param>
    [HttpGet("default-config")]
    public ActionResult<ApiSimulationStateDto> GetDefaultConfig([FromQuery] string configName = "default")
    {
        var config = configName.ToLower() switch
        {
            "twork" => SimulationFactory.CreateTWorkConfig(),
            "simple" => SimpleConfigFactory.CreateDefaultConfig(),
            _ => SimulationFactory.CreateDefaultConfig()
        };

        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        // Возвращаем начальное состояние (день 0, задачи ещё не распределены по стадиям)
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

        // Проверяем, завершилась ли симуляция (все задачи в Done)
        var allTasksDone = simulation.Board.Tasks.All(t =>
            t.CurrentStage?.Stage.Name == "Done" || t.CurrentStage == null);

        if (allTasksDone && simulation.CurrentDay > 0)
        {
            // Симуляция завершена — возвращаем результат, но следующий вызов вернёт 400
        }

        // Возвращаем полное обновлённое состояние
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

        // Определяем стадию начала расчёта Lead Time (isLeadTimeStart = true)
        var leadTimeStartStage = simulation.Config.Workflow.Stages
            .FirstOrDefault(s => s.IsLeadTimeStart);

        var leadTimeStartStageName = leadTimeStartStage?.Name ?? "Todo";

        // Рассчитываем все метрики
        var metricsService = new MetricsService(simulation, leadTimeStartStageName);
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
