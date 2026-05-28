using KanbanFlowApi.Dtos;
using KanbanFlowApi.Mappers;
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
    /// Получить начальное состояние симуляции по умолчанию.
    /// Возвращает готовое состояние для передачи в simulate-day (день 0, задачи ещё не двигались).
    /// </summary>
    /// <param name="configName">Название конфигурации: "default" или "twork". По умолчанию "default".</param>
    [HttpGet("default-config")]
    public ActionResult<ApiSimulationStateDto> GetDefaultConfig([FromQuery] string configName = "default")
    {
        var config = configName.ToLower() switch
        {
            "twork" => SimulationFactory.CreateTWorkConfig(),
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
        
        // Сохраняем настройку вариативности из конфига
        simulation.UseVariability = state.Config.UseVariability;

        // Проверяем, можно ли продолжить симуляцию
        var validationResult = ValidateSimulationCanContinue(simulation);
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

        // Увеличиваем тик на 24 часа (день)
        simulation.AdvanceTick(24);

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
    /// Проверяет, можно ли продолжить симуляцию
    /// </summary>
    private static ValidationResult ValidateSimulationCanContinue(Simulation simulation)
    {
        // Проверяем, есть ли задачи, которые ещё не в Done
        var tasksInDone = simulation.Board.Tasks
            .Where(t => t.CurrentStage is not null && t.CurrentStage.NextStages.Count == 0)
            .ToList();
        if (tasksInDone.Count == simulation.Board.Tasks.Count)
        {
            return ValidationResult.Invalid("Симуляция завершена: все задачи находятся в финальных стадиях");
        }
        
        var tasksNotInDone = simulation.Board.Tasks
            .Where(t => t.CurrentStage is null || t.CurrentStage.NextStages.Count != 0)
            .ToList();

        // Если все задачи без стадии (новая симуляция) — это нормально
        var allTasksWithoutStage = simulation.Board.Tasks.All(t => t.CurrentStage is null);
        if (allTasksWithoutStage)
        {
            return ValidationResult.Valid();
        }

        // Проверяем, есть ли воркеры для выполнения оставшихся задач
        var availableWorkers = simulation.Board.Workers.Where(w => w.IsAvailable).ToList();
        if (!availableWorkers.Any())
        {
            // Проверяем, есть ли задачи в работе
            var tasksInProgress = simulation.Board.Tasks
                .Where(t => t.Worker != null && t.CurrentStage?.Stage.Name != "Done")
                .ToList();

            if (!tasksInProgress.Any())
            {
                return ValidationResult.Invalid(
                    "Симуляция невозможна: нет доступных воркеров и задач в работе. " +
                    "Возможно, задачи заблокированы или требуют воркеров с отсутствующими ролями.");
            }
        }

        // Проверяем, есть ли доступные переходы для задач
        foreach (var task in tasksNotInDone)
        {
            var currentStage = task.CurrentStage;
            if (currentStage == null)
            {
                continue;
            }

            // Если задача в Buffer и есть следующие стадии — можно двигаться
            if (currentStage.Stage.Type == KanbanFlowSerivce.Enums.StageType.Buffer &&
                currentStage.NextStages.Any())
            {
                return ValidationResult.Valid();
            }

            // Если задача в Work и есть прогресс — можно продолжать работу
            if (currentStage.Stage.Type == KanbanFlowSerivce.Enums.StageType.Work &&
                task.Progress < currentStage.Stage.StageProgressPercent)
            {
                return ValidationResult.Valid();
            }

            // Если задача в Work и прогресс 100% — можно двигаться дальше
            if (currentStage.Stage.Type == KanbanFlowSerivce.Enums.StageType.Work &&
                task.Progress >= currentStage.Stage.StageProgressPercent &&
                currentStage.NextStages.Any())
            {
                return ValidationResult.Valid();
            }
        }

        // Если дошли сюда — проверяем, есть ли хоть какой-то путь продвижения
        var hasPossibleMove = tasksNotInDone.Any(t =>
        {
            var stage = t.CurrentStage;
            return stage != null &&
                   (stage.Stage.Type == KanbanFlowSerivce.Enums.StageType.Buffer ||
                    t.Progress >= stage.Stage.StageProgressPercent) &&
                   stage.NextStages.Any();
        });

        if (!hasPossibleMove)
        {
            return ValidationResult.Invalid(
                "Симуляция невозможна: задачи не могут быть продвинуты. " +
                "Проверьте конфигурацию переходов между стадиями.");
        }

        return ValidationResult.Valid();
    }

    private sealed record ValidationResult(bool IsValid, string? ErrorMessage)
    {
        public static ValidationResult Valid() => new(true, null);
        public static ValidationResult Invalid(string error) => new(false, error);
    }
}
