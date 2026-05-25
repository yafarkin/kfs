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
    ///     Пытается переместить задачу из одной стадии в другую
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

            // Для рабочих стадий пытаемся найти исполнителя
            BoardWorker? worker = null;
            if (toStage.Stage.Type == StageType.Work)
            {
                worker = FindAvailableWorker(task, toStage);
                
                // Если задача требует конкретного воркера (AcceptableWorkers), но он недоступен — не перемещаем
                var requiredWorkerLogin = GetRequiredWorkerForStage(task, toStage);
                if (!string.IsNullOrEmpty(requiredWorkerLogin) && worker == null)
                {
                    continue; // Задача ждёт конкретного воркера
                }
                
                // Если воркер не найден — задача всё равно может переместиться в стадию,
                // но будет ждать доступного воркера
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
        // Проверяем, может ли задача работать на этой стадии (пересечение навыков задачи и стадии)
        if (!CanTaskMoveToStage(task, toStage))
        {
            return null;
        }

        // Проверяем есть ли требование к конкретному worker'у для этой стадии
        var requiredWorkerLogin = GetRequiredWorkerForStage(task, toStage);

        foreach (var worker in _simulation.Board.Workers)
        {
            // Если задача требует конкретного worker'а для этой стадии — пропускаем остальных
            if (!string.IsNullOrEmpty(requiredWorkerLogin) && worker.Worker.Login != requiredWorkerLogin)
            {
                continue;
            }

            // Проверяем, есть ли у воркера навыки для работы на этой стадии с этой задачей
            if (!HasSkillsForTaskOnStage(worker, task, toStage))
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
                .Where(w => HasSkillsForTaskOnStage(w, task, toStage))
                .ToList();

            if (workersWithSkills.Count == 1)
            {
                // Только один воркер - не может выполнить требование "другой ресурс"
                return null;
            }
        }

        // Если задача требует конкретного воркера (AcceptableWorkers), но он недоступен — не возвращаем null,
        // задача может переместиться в стадию и ждать
        // Но если это буферная стадия после Work - задача не должна перемещаться без воркера
        if (!string.IsNullOrEmpty(requiredWorkerLogin))
        {
            return null;
        }

        return null;
    }

    /// <summary>
    ///     Проверяет, может ли задача работать на данной стадии (пересечение навыков)
    ///     У задачи должен быть хотя бы один общий навык с требуемыми навыками стадии
    /// </summary>
    private bool CanTaskMoveToStage(BoardTask task, BoardStage toStage)
    {
        // Если у стадии нет требуемых навыков — задача может работать
        if (toStage.Stage.RequiredSkills.Count == 0)
        {
            return true;
        }

        // Если у задачи нет требуемых навыков — задача может работать (без ограничений)
        if (task.Task.RequiredSkills.Count == 0)
        {
            return true;
        }

        // Проверяем пересечение: есть ли хотя бы один общий навык между задачей и стадией
        return task.Task.RequiredSkills.Any(skill => toStage.Stage.RequiredSkills.Contains(skill));
    }

    /// <summary>
    ///     Проверяет, есть ли у воркера навыки для работы на данной стадии с данной задачей
    ///     У воркера должен быть хотя бы один общий навык И со стадией, И с задачей
    /// </summary>
    private bool HasSkillsForTaskOnStage(BoardWorker worker, BoardTask task, BoardStage toStage)
    {
        var stageSkills = toStage.Stage.RequiredSkills;
        var taskSkills = task.Task.RequiredSkills;

        // Если у стадии нет требуемых навыков — проверяем только навыки задачи
        if (stageSkills.Count == 0)
        {
            if (taskSkills.Count == 0)
            {
                return true; // Нет требований ни у стадии, ни у задачи
            }
            return worker.Worker.Skills.Any(skill => taskSkills.Contains(skill));
        }

        // Если у задачи нет требуемых навыков — проверяем только навыки стадии
        if (taskSkills.Count == 0)
        {
            return worker.Worker.Skills.Any(skill => stageSkills.Contains(skill));
        }

        // Проверяем, что у воркера есть навык И для стадии, И для задачи
        var workerSkills = worker.Worker.Skills;
        var hasStageSkill = workerSkills.Any(skill => stageSkills.Contains(skill));
        var hasTaskSkill = workerSkills.Any(skill => taskSkills.Contains(skill));

        return hasStageSkill && hasTaskSkill;
    }

    /// <summary>
    ///     Получает требуемые навыки для задачи на данной стадии
    ///     Упрощённая логика: используем навыки задачи, стадия сама решит подходит ли она
    /// </summary>
    private List<string> GetRequiredSkillsForStage(BoardTask task, BoardStage toStage)
    {
        // Используем навыки задачи
        if (task.Task.RequiredSkills.Count > 0)
        {
            return task.Task.RequiredSkills;
        }
        
        // Нет требований
        return [];
    }

    /// <summary>
    ///     Проверяет, есть ли у воркера хотя бы один требуемый навык (пересечение)
    ///     Логика: если у воркера есть ХОТЯ БЫ ОДИН навык из requiredSkills — он подходит
    /// </summary>
    private bool HasAllRequiredSkills(BoardWorker worker, List<string> requiredSkills)
    {
        if (requiredSkills.Count == 0)
        {
            return true; // Нет требований к навыкам
        }

        // Проверяем пересечение: есть ли у воркера хотя бы один требуемый навык
        return requiredSkills.Any(skill => worker.Worker.Skills.Contains(skill));
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
