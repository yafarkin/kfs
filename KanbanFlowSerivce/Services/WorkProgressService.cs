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
    /// <returns>Список задач, которые завершили работу (достигли 100%) в этот день</returns>
    public List<BoardTask> SimulateWorkDay()
    {
        var completedTasks = new List<BoardTask>();

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

                // Пропускаем уже завершённые задачи — они должны перемещаться дальше через ProcessMovements
                if (task.IsCompleted)
                {
                    continue;
                }

                // Рассчитываем сколько дней требуется для выполнения задачи на этой стадии
                var daysRequired = worker.Worker.GetDaysForTask(
                    stage.Stage, 
                    task.Task.ShirtType, 
                    _simulation.Config.UseVariability,
                    _simulation.Random
                );

                // Защита от деления на ноль: если daysRequired = 0, задача выполняется мгновенно
                if (daysRequired <= 0)
                {
                    task.Progress = 100;
                    completedTasks.Add(task);
                    continue;
                }

                // Прогресс за один день = 100% / количество дней
                // С учётом производительности воркера
                var progressPerDay = 100.0m / daysRequired;
                var performanceMultiplier = (decimal)worker.Worker.Performance / 100.0m;
                var actualProgress = progressPerDay * performanceMultiplier;

                // Увеличиваем прогресс
                var newProgress = Math.Min(100, task.Progress + (int)actualProgress);

                // Проверяем не завершилась ли задача
                var wasCompleted = !task.IsCompleted && newProgress >= 99;

                task.Progress = newProgress;

                // Записываем в историю
                _simulation.LogActivity(new HistoryActivity
                {
                    Type = ActivityType.TaskProgressUpdated,
                    Description = $"Задача {task.Task.Key} выполняется на {task.Progress}% (worker: {worker.Worker.Login}, стадия: {stage.Stage.Name})",
                    Task = task,
                    Worker = worker,
                    Stage = stage,
                    Progress = task.Progress,
                    WorkerLogin = worker.Worker.Login,
                    TaskKey = task.Task.Key,
                    StageName = stage.Stage.Name
                });

                // Если задача завершена, записываем событие и добавляем в список
                if (wasCompleted)
                {
                    // Находим событие WorkerTookTask для этой задачи на этой стадии чтобы получить CorrelationId
                    // Ищем в общей истории симуляции
                    var tookTaskEvent = _simulation.History
                        .SelectMany(d => d.Activities)
                        .Where(a => a.Type == ActivityType.WorkerTookTask)
                        .OrderByDescending(a => a.DayNumber)
                        .FirstOrDefault(a =>
                            a.TaskKey == task.Task.Key &&
                            a.StageName == stage.Stage.Name);

                    var correlationId = tookTaskEvent?.CorrelationId ?? Guid.NewGuid();

                    var completedActivity = new HistoryActivity
                    {
                        Type = ActivityType.WorkerCompletedTask,
                        Description = $"Worker {worker.Worker.Login} завершил задачу {task.Task.Key} на стадии {stage.Stage.Name}",
                        Task = task,
                        Worker = worker,
                        Stage = stage,
                        WorkerLogin = worker.Worker.Login,
                        TaskKey = task.Task.Key,
                        StageName = stage.Stage.Name,
                        CorrelationId = correlationId
                    };

                    _simulation.LogActivity(completedActivity);

                    completedTasks.Add(task);
                }
            }
        }

        return completedTasks;
    }
}
