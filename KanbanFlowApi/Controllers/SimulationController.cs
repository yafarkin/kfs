using KanbanFlowApi.Dtos;
using KanbanFlowApi.Dtos.Config;
using KanbanFlowApi.Dtos.Metrics;
using KanbanFlowApi.Dtos.Task;
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
    /// Получить список доступных конфигураций симуляции.
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
    /// Рассчитать метрики симуляции (Lead Time, Throughput, Flow Efficiency, Frequency).
    /// Принимает полное состояние симуляции и возвращает рассчитанные метрики.
    /// </summary>
    /// <param name="state">Состояние симуляции</param>
    [HttpPost("calculate-metrics")]
    public ActionResult<ApiMetricsDto> CalculateMetrics([FromBody] ApiSimulationStateDto state)
    {
        // Восстанавливаем доменную симуляцию из DTO
        var simulation = ApiMapper.ToDomainSimulation(state);

        // Определяем стадию начала расчёта Lead Time (isLeadTimeStart = true)
        var leadTimeStartStage = simulation.Config.Workflow.Stages
            .FirstOrDefault(s => s.IsLeadTimeStart);

        var leadTimeStartStageName = leadTimeStartStage?.Name ?? "Todo";

        // Создаём сервис метрик и рассчитываем
        var metricsService = new MetricsService(simulation, leadTimeStartStageName);
        var metrics = metricsService.CalculateAllMetrics();

        return Ok(metrics);
    }

    /// <summary>
    /// Рассчитать метрики работников (Throughput, Lead Time, Efficiency).
    /// </summary>
    [HttpPost("workers/metrics")]
    public ActionResult<List<ApiWorkerMetricsDto>> GetWorkersMetrics([FromBody] ApiSimulationStateDto state)
    {
        // Восстанавливаем доменную симуляцию из DTO
        var simulation = ApiMapper.ToDomainSimulation(state);

        var metricsService = new WorkerMetricsService(simulation);
        var workerMetrics = metricsService.CalculateAllWorkersMetrics();

        return Ok(workerMetrics);
    }

    /// <summary>
    /// Рассчитать метрики по задачам (Lead Time, Flow Efficiency, время по стадиям, воркеры).
    /// </summary>
    [HttpPost("task-metrics")]
    public ActionResult<List<TaskMetricsDto>> GetTaskMetrics([FromBody] ApiSimulationStateDto state)
    {
        // Восстанавливаем доменную симуляцию из DTO
        var simulation = ApiMapper.ToDomainSimulation(state);

        var taskMetricsService = new TaskMetricsService(simulation);
        var taskMetrics = taskMetricsService.CalculateAllTasksMetrics();

        return Ok(taskMetrics);
    }

    /// <summary>
    /// Рассчитать агрегированные метрики по стадиям (P50, P85, P95, Avg, Max).
    /// </summary>
    [HttpPost("stage-metrics")]
    public ActionResult<List<StageMetricsAggregatedDto>> GetStageMetrics([FromBody] ApiSimulationStateDto state)
    {
        // Восстанавливаем доменную симуляцию из DTO
        var simulation = ApiMapper.ToDomainSimulation(state);

        var taskMetricsService = new TaskMetricsService(simulation);
        var stageMetrics = taskMetricsService.CalculateStageMetricsAggregated();

        return Ok(stageMetrics);
    }
}
