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
        // Проверяем есть ли требование к конкретному worker'у для этой стадии
        var requiredWorkerLogin = GetRequiredWorkerForStage(task, toStage);

        // Получаем требуемые навыки для этой стадии
        var requiredSkillsForStage = GetRequiredSkillsForStage(task, toStage);

        foreach (var worker in _simulation.Board.Workers)
        {
            // Если задача требует конкретного worker'а для этой стадии — пропускаем остальных
            if (!string.IsNullOrEmpty(requiredWorkerLogin) && worker.Worker.Login != requiredWorkerLogin)
            {
                continue;
            }

            // Проверяем, есть ли у воркера все требуемые навыки
            if (!HasAllRequiredSkills(worker, requiredSkillsForStage))
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

        // Специальная проверка: если только один воркер с нужными навыками и стадия требует другого - пропускаем
        if (toStage.RequiresDifferentResource)
        {
            var workersWithSkills = _simulation.Board.Workers
                .Where(w => HasAllRequiredSkills(w, requiredSkillsForStage))
                .ToList();

            if (workersWithSkills.Count == 1)
            {
                // Только один воркер - не может выполнить требование "другой ресурс"
                return null;
            }
        }

        return null;
    }

    /// <summary>
    ///     Получает требуемые навыки для задачи на данной стадии
    ///     Логика:
    ///     1. Если для стадии указан RequiredSkillsPerStage — используем его (наибольший приоритет)
    ///     2. Если у стадии есть RequiredSkills И у задачи есть RequiredSkills — используем пересечение
    ///     3. Если у стадии есть RequiredSkills — используем их (для downstream-стадий типа Testing, Review)
    ///     4. Иначе используем RequiredSkills задачи (для production-стадии)
    /// </summary>
    private List<string> GetRequiredSkillsForStage(BoardTask task, BoardStage toStage)
    {
        // 1. Проверяем RequiredSkillsPerStage для конкретной стадии (наибольший приоритет)
        if (task.Task.RequiredSkillsPerStage.TryGetValue(toStage.Stage.Name, out var stageSkills))
        {
            return stageSkills;
        }

        var stageRequiredSkills = toStage.Stage.RequiredSkills;
        var taskRequiredSkills = task.Task.RequiredSkills;

        // 2. Если у стадии и задачи есть RequiredSkills — используем пересечение
        if (stageRequiredSkills.Count > 0 && taskRequiredSkills.Count > 0)
        {
            var intersection = stageRequiredSkills.Intersect(taskRequiredSkills).ToList();
            if (intersection.Count > 0)
            {
                return intersection;
            }
            // Если пересечения нет, используем навыки задачи (предполагаем, что задача специфична)
            return taskRequiredSkills;
        }

        // 3. Если у стадии есть RequiredSkills — используем их (для downstream-стадий типа Testing, Review)
        if (stageRequiredSkills.Count > 0)
        {
            return stageRequiredSkills;
        }

        // 4. Если у стадии нет RequiredSkills, пробуем AllowedRoles (для обратной совместимости)
        if (toStage.Stage.AllowedRoles.Length > 0)
        {
            return toStage.Stage.AllowedRoles.ToList();
        }

        // 5. Используем RequiredSkills задачи (для production-стадии)
        if (taskRequiredSkills.Count > 0)
        {
            return taskRequiredSkills;
        }

        // 6. Если у задачи нет RequiredSkills, пробуем унаследовать от Role (для обратной совместимости)
        if (!string.IsNullOrEmpty(task.Task.Role))
        {
            return [task.Task.Role];
        }

        // Нет требований к навыкам
        return [];
    }

    /// <summary>
    ///     Проверяет, есть ли у воркера все требуемые навыки
    ///     Если requiredSkills.Count > 1, проверяем наличие ХОТЯ БЫ ОДНОГО (ИЛИ)
    ///     Если requiredSkills.Count == 1, проверяем наличие этого навыка
    /// </summary>
    private bool HasAllRequiredSkills(BoardWorker worker, List<string> requiredSkills)
    {
        if (requiredSkills.Count == 0)
        {
            return true; // Нет требований к навыкам
        }

        // Собираем все навыки воркера (из Skills и Role для обратной совместимости)
        var workerSkills = new HashSet<string>(worker.Worker.Skills);
        if (!string.IsNullOrEmpty(worker.Worker.Role))
        {
            workerSkills.Add(worker.Worker.Role);
        }

        // Если требуется несколько навыков — это "ИЛИ" (любой подходит)
        // Если один навык — это строгое требование
        return requiredSkills.Any(skill => workerSkills.Contains(skill));
    }

    /// <summary>
    ///     Получает требуемого worker'а для задачи на конкретной стадии из AcceptableWorkers
    /// </summary>
    private static string? GetRequiredWorkerForStage(BoardTask task, BoardStage stage)
    {
        if (task.Task.AcceptableWorkers == null || task.Task.AcceptableWorkers.Count == 0)
        {
            return null;
        }

        // Ищем требование для текущей стадии
        if (task.Task.AcceptableWorkers.TryGetValue(stage.Stage.Name, out var requiredWorker))
        {
            return requiredWorker;
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
            // Задача перемещается в буферную стадию — освобождаем воркера
            var previousWorker = task.Worker;
            if (previousWorker != null)
            {
                var oldAssignments = previousWorker.Assignments.Where(a => a.Task == task).ToList();
                foreach (var oldAssignment in oldAssignments)
                {
                    previousWorker.Assignments.Remove(oldAssignment);
                }
            }
            task.Worker = null;
        }

        // Добавляем запись в историю
        var workerInfo = worker != null ? $" (worker: {worker.Worker.Login})" : "";
        var activity = new HistoryActivity
        {
            Type = ActivityType.TaskMoved,
            Description = $"Задача {task.Task.Key} перемещена из {fromStage.Stage.Name} в {toStage.Stage.Name}{workerInfo}",
            Task = task,
            Worker = worker,
            Stage = toStage
        };

        _simulation.LogActivity(activity);

        // Если задача перемещена на рабочую стадию с worker'ом — записываем что worker взял задачу
        if (toStage.Stage.Type == StageType.Work && worker != null)
        {
            var workerTookTaskActivity = new HistoryActivity
            {
                Type = ActivityType.WorkerTookTask,
                Description = $"Worker {worker.Worker.Login} взял задачу {task.Task.Key} на стадии {toStage.Stage.Name}",
                Task = task,
                Worker = worker,
                Stage = toStage,
                StartedAtTick = _simulation.CurrentTick
            };
            _simulation.LogActivity(workerTookTaskActivity);
        }

        // Если задача была завершена на предыдущей стадии — записываем что worker завершил задачу
        if (fromStage.Stage.Type == StageType.Work && task.Progress >= 100)
        {
            var workerCompletedTaskActivity = new HistoryActivity
            {
                Type = ActivityType.WorkerCompletedTask,
                Description = $"Worker {task.Worker?.Worker.Login} завершил задачу {task.Task.Key} на стадии {fromStage.Stage.Name}",
                Task = task,
                Worker = task.Worker,
                Stage = fromStage,
                CompletedAtTick = _simulation.CurrentTick
            };
            _simulation.LogActivity(workerCompletedTaskActivity);
        }

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
