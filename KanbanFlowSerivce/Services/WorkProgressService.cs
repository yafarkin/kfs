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
    private const decimal CompletionEpsilon = 0.001m;
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
            // Считаем только активные задачи на Work-стадиях (для корректного деления времени)
            var activeWorkCount = worker.Assignments
                .Count(a => a.Stage.Stage.Type == StageType.Work && !a.Task.IsCompleted);

            // Если нет активных задач, пропускаем этого воркера
            if (activeWorkCount == 0)
            {
                continue;
            }

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

                // Доля времени которую получает задача сегодня (1 / количество активных задач)
                var share = 1.0m / activeWorkCount;

                // Увеличиваем отработанное время
                assignment.DaysWorked += share;

                // Жёсткая проверка: DaysRequired должно быть установлено при взятии задачи
                if (assignment.DaysRequired <= 0)
                {
                    throw new InvalidOperationException(
                        $"assignment восстановлен без DaysRequired — ошибка сериализации состояния (задача {task.Task.Key}, воркер {worker.Worker.Login})");
                }

                // Рассчитываем прогресс как процент от отработанного времени
                task.Progress = (int)Math.Round(100.0m * assignment.DaysWorked / assignment.DaysRequired);

                // Проверяем завершение задачи с допуском (эпсилон) для случаев деления не на степени 2/5
                var wasCompleted = assignment.DaysWorked >= assignment.DaysRequired - CompletionEpsilon;
                if (wasCompleted)
                {
                    task.Progress = 100;
                }

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
