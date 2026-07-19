using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Enums;

namespace KanbanFlowSerivce.Services;

/// <summary>
/// Сервис валидации состояния симуляции.
/// Проверяет, можно ли продолжить симуляцию (есть ли возможные ходы).
/// </summary>
public class SimulationValidationService
{
    private readonly Simulation _simulation;

    public SimulationValidationService(Simulation simulation)
    {
        _simulation = simulation;
    }

    /// <summary>
    /// Проверяет, можно ли продолжить симуляцию.
    /// </summary>
    public ValidationResult ValidateCanContinue()
    {
        // Проверяем, есть ли задачи, которые ещё не в Done
        var tasksInDone = _simulation.Board.Tasks
            .Where(t => t.CurrentStage is not null && t.CurrentStage.NextStages.Count == 0)
            .ToList();

        if (tasksInDone.Count == _simulation.Board.Tasks.Count)
        {
            return ValidationResult.Invalid("Симуляция завершена: все задачи находятся в финальных стадиях");
        }

        var tasksNotInDone = _simulation.Board.Tasks
            .Where(t => t.CurrentStage is null || t.CurrentStage.NextStages.Count != 0)
            .ToList();

        // Если все задачи без стадии (новая симуляция) — это нормально
        var allTasksWithoutStage = _simulation.Board.Tasks.All(t => t.CurrentStage is null);
        if (allTasksWithoutStage)
        {
            return ValidationResult.Valid();
        }

        // Проверяем, есть ли воркеры для выполнения оставшихся задач
        var availableWorkers = _simulation.Board.Workers.Where(w => w.IsAvailable).ToList();
        if (!availableWorkers.Any())
        {
            // Проверяем, есть ли задачи в работе
            var tasksInProgress = _simulation.Board.Tasks
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
                // Задача ещё не в воркфлоу — может быть начата
                return ValidationResult.Valid();
            }

            // Если задача в Buffer и есть следующие стадии — можно двигаться
            if (currentStage.Stage.Type == StageType.Buffer &&
                currentStage.NextStages.Any())
            {
                return ValidationResult.Valid();
            }

            // Если задача в Work и ещё не завершена — можно продолжать работу
            if (currentStage.Stage.Type == StageType.Work &&
                !task.IsCompleted)
            {
                return ValidationResult.Valid();
            }

            // Если задача в Work и завершена — можно двигаться дальше
            if (currentStage.Stage.Type == StageType.Work &&
                task.IsCompleted &&
                currentStage.NextStages.Any())
            {
                return ValidationResult.Valid();
            }
        }

        // Если дошли сюда — проверяем, есть ли хоть какой-то путь продвижения
        // Задачи с CurrentStage == null ещё не вошли в воркфлоу и могут быть начаты
        var hasPossibleMove = tasksNotInDone.Any(t =>
        {
            var stage = t.CurrentStage;
            return stage == null || // Задача ещё не в воркфлоу (ждёт в Todo)
                   (stage.Stage.Type == StageType.Buffer && stage.NextStages.Any()) ||
                   (!t.IsCompleted && stage.NextStages.Any());
        });

        if (!hasPossibleMove)
        {
            return ValidationResult.Invalid(
                "Симуляция невозможна: задачи не могут быть продвинуты. " +
                "Проверьте конфигурацию переходов между стадиями.");
        }

        return ValidationResult.Valid();
    }

    public sealed record ValidationResult(bool IsValid, string? ErrorMessage)
    {
        public static ValidationResult Valid() => new(true, null);
        public static ValidationResult Invalid(string error) => new(false, error);
    }
}
