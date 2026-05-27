using KanbanFlowSerivce.Dtos.Board;
using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.History;
using KanbanFlowSerivce.Enums;

namespace KanbanFlowSerivce.Services;

/// <summary>
///     Сервис для симуляции выполнения работы воркерами
///     Увеличивает прогресс задач на рабочих стадиях
/// </summary>
public sealed class WorkProgressService
{
    private readonly Simulation _simulation;

    public WorkProgressService(Simulation simulation)
    {
        _simulation = simulation;
    }

    /// <summary>
    ///     Симулирует один день работы
    ///     Увеличивает прогресс задач на основе производительности воркеров
    /// </summary>
    public void SimulateWorkDay()
    {
        foreach (var worker in _simulation.Board.Workers)
        {
            foreach (var assignment in worker.Assignments.ToList())
            {
                if (assignment.Stage.Stage.Type != StageType.Work)
                {
                    continue;
                }

                var task = assignment.Task;
                var stage = assignment.Stage;

                // Рассчитываем сколько дней требуется для выполнения задачи на этой стадии
                var daysRequired = worker.Worker.GetDaysForTask(stage.Stage, task.Task.ShirtType);

                // Защита от деления на ноль: если daysRequired = 0, задача выполняется мгновенно
                if (daysRequired <= 0)
                {
                    task.Progress = 100;
                    continue;
                }

                // Прогресс за один день = 100% / количество дней
                // С учётом производительности воркера
                var progressPerDay = 100.0m / daysRequired;
                var performanceMultiplier = worker.Worker.Performance / 100.0;
                var actualProgress = progressPerDay * (decimal)performanceMultiplier;

                // Увеличиваем прогресс
                var newProgress = Math.Min(100, task.Progress + (int)actualProgress);

                // Проверяем не завершилась ли задача
                var wasCompleted = task.Progress < 100 && newProgress >= 100;

                task.Progress = newProgress;

                // Записываем в историю
                _simulation.LogActivity(new HistoryActivity
                {
                    Type = ActivityType.TaskProgressUpdated,
                    Description = $"Задача {task.Task.Key} выполняется на {task.Progress}% (worker: {worker.Worker.Login}, стадия: {stage.Stage.Name})",
                    Task = task,
                    Worker = worker,
                    Stage = stage,
                    Progress = task.Progress
                });

                // Если задача завершена, записываем событие
                if (wasCompleted)
                {
                    _simulation.LogActivity(new HistoryActivity
                    {
                        Type = ActivityType.WorkerCompletedTask,
                        Description = $"Worker {worker.Worker.Login} завершил задачу {task.Task.Key} на стадии {stage.Stage.Name}",
                        Task = task,
                        Worker = worker,
                        Stage = stage,
                        CompletedAtTick = _simulation.CurrentTick
                    });
                }
            }
        }
    }

    /// <summary>
    ///     Симулирует работу до завершения всех задач или достижения лимита дней
    /// </summary>
    /// <param name="maxDays">Максимальное количество дней для симуляции</param>
    /// <returns>Количество дней симуляции</returns>
    public int SimulateUntilCompletion(int maxDays = 100)
    {
        var day = 0;

        while (day < maxDays)
        {
            day++;
            _simulation.StartNewDay();

            // Сначала обрабатываем перемещения задач
            var movementService = new TaskMovementService(_simulation);
            movementService.ProcessMovements();

            // Проверяем, все ли задачи в Done
            var doneStage = _simulation.Board.Stages
                .SingleOrDefault(s => s.Stage.Name == "Done");
            
            if (doneStage != null && doneStage.Tasks.Count == _simulation.Board.Tasks.Count)
            {
                break;
            }

            // Симулируем выполнение работы
            SimulateWorkDay();

            // Увеличиваем тик
            _simulation.AdvanceTick(24); // 24 часа в дне
        }

        return day;
    }
}
