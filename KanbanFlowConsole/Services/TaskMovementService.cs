using KanbanFlowConsole.Dtos;
using KanbanFlowConsole.Dtos.Board;
using KanbanFlowConsole.Dtos.History;
using KanbanFlowConsole.Enums;

namespace KanbanFlowConsole.Services;

/// <summary>
///     Сервис для продвижения задач по доске (вытягивающий принцип)
/// </summary>
public sealed class TaskMovementService
{
    private readonly Simulation _simulation;

    public TaskMovementService(Simulation simulation)
    {
        _simulation = simulation;
    }

    /// <summary>
    ///     Обрабатывает все возможные перемещения задач по доске
    ///     Выполняется циклически, пока есть возможные действия
    /// </summary>
    public void ProcessMovements()
    {
        var hasMovements = true;
        while (hasMovements)
        {
            hasMovements = false;

            // Проходим по всем стадиям от конца к началу (reverse order)
            // Начинаем с тех, у которых есть предыдущие стадии
            foreach (var stage in _simulation.Board.Stages.AsEnumerable().Reverse())
            {
                if (stage.PrevStages.Count == 0)
                {
                    continue;
                }

                // Пытаемся переместить задачу из предыдущих стадий
                foreach (var prevStage in stage.PrevStages)
                {
                    var moved = TryMoveTask(prevStage, stage);
                    if (moved)
                    {
                        hasMovements = true;
                        break; // Начинаем цикл заново после успешного перемещения
                    }
                }
            }
        }
    }

    /// <summary>
    ///     Пытается переместить задачу из предыдущей стадии в текущую
    /// </summary>
    private bool TryMoveTask(BoardStage fromStage, BoardStage toStage)
    {
        // Проверяем, может ли стадия принять задачу (WIP лимит)
        if (!toStage.CanAcceptTaskWithLimit())
        {
            return false;
        }

        // Находим подходящую задачу в предыдущей стадии
        foreach (var task in fromStage.Tasks.ToList())
        {
            // Проверяем, готова ли задача к перемещению
            if (!IsTaskReadyForMove(task, fromStage))
            {
                continue;
            }

            // Для рабочих стадий нужен исполнитель
            BoardWorker? worker = null;
            if (toStage.Stage.Type == StageType.Work)
            {
                worker = FindAvailableWorker(task, toStage);
                if (worker == null)
                {
                    continue; // Нет доступного воркера
                }
            }

            // Выполняем перемещение
            MoveTask(task, fromStage, toStage, worker);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Проверяет, готова ли задача к перемещению из стадии
    /// </summary>
    private bool IsTaskReadyForMove(BoardTask task, BoardStage fromStage)
    {
        // Если предыдущая стадия рабочая - задача должна быть завершена (100%)
        if (fromStage.Stage.Type == StageType.Work)
        {
            return task.Progress >= 100;
        }

        // Для буферных стадий задача подходит всегда
        return true;
    }

    /// <summary>
    ///     Ищет доступного воркера для задачи с учётом всех ограничений
    /// </summary>
    private BoardWorker? FindAvailableWorker(BoardTask task, BoardStage toStage)
    {
        // Определяем требуемую роль из задачи
        var requiredRole = task.Task.Role;

        foreach (var worker in _simulation.Board.Workers)
        {
            // Проверяем роль воркера
            if (!string.IsNullOrEmpty(requiredRole) && worker.Worker.Role != requiredRole)
            {
                continue;
            }

            // Проверяем WIP лимит воркера
            if (!worker.IsAvailable)
            {
                continue;
            }

            // Проверяем ограничение RequiresDifferentResource
            if (toStage.RequiresDifferentResource && toStage.ExcludedStage != null)
            {
                if (IsWorkerInExcludedStage(worker, toStage.ExcludedStage, task))
                {
                    continue;
                }
            }

            // Воркер подходит
            return worker;
        }

        // Специальная проверка: если только один воркер в роли и стадия требует другого - пропускаем
        if (toStage.RequiresDifferentResource)
        {
            var workersInRole = _simulation.Board.Workers
                .Where(w => string.IsNullOrEmpty(requiredRole) || w.Worker.Role == requiredRole)
                .ToList();

            if (workersInRole.Count == 1)
            {
                // Только один воркер - не может выполнить требование "другой ресурс"
                return null;
            }
        }

        return null;
    }

    /// <summary>
    ///     Проверяет, работает ли воркер в исключаемой стадии над этой задачей
    /// </summary>
    private bool IsWorkerInExcludedStage(BoardWorker worker, BoardStage excludedStage, BoardTask task)
    {
        // Проверяем, есть ли у воркера назначения на задачу в исключаемой стадии
        foreach (var assignment in worker.Assignments)
        {
            if (assignment.Stage == excludedStage && assignment.Task == task)
            {
                return true;
            }
        }

        // Также проверяем историю переходов задачи
        if (task.TransitionHistory.Any(h => h.ToStage == excludedStage && h.Activity.Worker == worker))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Выполняет перемещение задачи между стадиями
    /// </summary>
    private void MoveTask(BoardTask task, BoardStage fromStage, BoardStage toStage, BoardWorker? worker)
    {
        // Удаляем задачу из предыдущей стадии
        fromStage.Tasks.Remove(task);

        // Добавляем задачу в новую стадию
        toStage.Tasks.Add(task);

        // Обновляем текущую стадию задачи
        task.CurrentStage = toStage;

        // Сбрасываем прогресс
        task.Progress = 0;

        // Обновляем воркера
        if (worker != null)
        {
            // Удаляем старые назначения этого воркера для этой задачи
            var oldAssignments = worker.Assignments.Where(a => a.Task == task).ToList();
            foreach (var oldAssignment in oldAssignments)
            {
                worker.Assignments.Remove(oldAssignment);
            }

            // Добавляем новое назначение
            worker.Assignments.Add(new BoardTaskAssignment
            {
                Task = task,
                Stage = toStage
            });

            task.Worker = worker;
        }
        else
        {
            task.Worker = null;
        }

        // Добавляем запись в историю
        var activity = new HistoryActivity
        {
            Type = ActivityType.TaskMoved,
            Description = $"Задача {task.Task.Key} перемещена из {fromStage.Stage.Name} в {toStage.Stage.Name}",
            Task = task,
            Worker = worker,
            Stage = toStage
        };

        _simulation.LogActivity(activity);

        // Добавляем запись в историю переходов задачи
        task.TransitionHistory.Add(new TaskTransitionHistory
        {
            Activity = activity,
            FromStage = fromStage,
            ToStage = toStage,
            Tick = _simulation.CurrentTick
        });
    }
}

/// <summary>
///     Метод расширения для проверки WIP лимита стадии
/// </summary>
public static class BoardStageExtensions
{
    /// <summary>
    ///     Проверяет, может ли стадия принять задачу с учётом WIP лимита
    ///     Конечные стадии (без лимита) всегда могут принять задачу
    /// </summary>
    public static bool CanAcceptTaskWithLimit(this BoardStage stage)
    {
        // Конечные стадии не имеют WIP лимита
        if (!stage.WipLimit.HasValue)
        {
            return true;
        }

        return stage.CanAcceptTasks;
    }
}
