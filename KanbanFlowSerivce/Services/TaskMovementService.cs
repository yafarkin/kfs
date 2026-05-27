using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Board;
using KanbanFlowSerivce.Dtos.History;
using KanbanFlowSerivce.Enums;

namespace KanbanFlowSerivce.Services;

/// <summary>
///     Сервис для продвижения задач по доске (вытягивающий принцип)
/// </summary>
public sealed class TaskMovementService
{
    private readonly Simulation _simulation;
    private readonly Random _random;

    public TaskMovementService(Simulation simulation, Random? random = null)
    {
        _simulation = simulation;
        _random = random ?? new Random();
    }

    /// <summary>
    ///     Обрабатывает все возможные перемещения задач по доске
    ///     Выполняется циклически, пока есть возможные действия
    /// </summary>
    /// <param name="completedTasks">Опционально: список задач, которые завершили работу в этот день. 
    /// Если указан, обрабатываются только эти задачи (для оптимизации).</param>
    public void ProcessMovements(List<BoardTask>? completedTasks = null)
    {
        // Сначала пытаемся назначить воркеров на задачи, которые уже в рабочих стадиях без воркера
        TryAssignWorkersToWaitingTasks();

        var hasMovements = true;
        while (hasMovements)
        {
            hasMovements = false;

            // Получаем стадии в топологическом порядке от стоков (конечных) к истокам (начальным)
            // Это позволяет задачам двигаться каскадом за один проход цикла
            // и корректно работает с DAG с несколькими ветками
            var stagesInOrder = GetStagesInTopologicalOrder();

            foreach (var stage in stagesInOrder)
            {
                if (stage.PrevStages.Count == 0)
                {
                    continue;
                }

                // Пытаемся переместить задачу из предыдущих стадий
                foreach (var prevStage in stage.PrevStages)
                {
                    // Проверяем вероятность перехода из prevStage в stage
                    if (!IsTransitionAllowed(prevStage, stage))
                    {
                        continue;
                    }

                    var moved = TryMoveTask(prevStage, stage, completedTasks);
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
    ///     Пытается назначить воркеров на задачи, которые находятся в рабочих стадиях без воркера
    /// </summary>
    private void TryAssignWorkersToWaitingTasks()
    {
        foreach (var stage in _simulation.Board.Stages)
        {
            if (stage.Stage.Type != StageType.Work)
            {
                continue;
            }

            foreach (var task in stage.Tasks.ToList())
            {
                // Пропускаем задачи, которые уже в работе
                if (task.Worker != null)
                {
                    continue;
                }

                // Если задача не требует эту стадию — пропускаем (она должна двигаться дальше)
                if (!TaskRequiresStage(task, stage))
                {
                    continue;
                }

                // Пытаемся найти воркера
                var worker = FindAvailableWorker(task, stage);
                if (worker != null)
                {
                    // Назначаем воркера на задачу
                    worker.RemoveTaskAssignment(task);
                    worker.Assignments.Add(new BoardTaskAssignment
                    {
                        Task = task,
                        Stage = stage
                    });
                    task.Worker = worker;

                    _simulation.LogActivity(new HistoryActivity
                    {
                        Type = ActivityType.WorkerTookTask,
                        Description = $"Worker {worker.Worker.Login} взял задачу {task.Task.Key} на стадии {stage.Stage.Name}",
                        Task = task,
                        Worker = worker,
                        Stage = stage,
                        StartedAtTick = _simulation.CurrentTick
                    });
                }
            }
        }
    }

    /// <summary>
    ///     Возвращает стадии в топологическом порядке от стоков (конечных стадий) к истокам (начальным)
    ///     Используется BFS от всех стоков для построения порядка обработки
    /// </summary>
    private List<BoardStage> GetStagesInTopologicalOrder()
    {
        // Находим все стоки (стадии без следующих стадий)
        var sinks = _simulation.Board.Stages.Where(s => s.NextStages.Count == 0).ToList();

        // BFS от стоков к истокам
        var result = new List<BoardStage>();
        var visited = new HashSet<BoardStage>();
        var queue = new Queue<BoardStage>();

        // Добавляем все стоки в очередь
        foreach (var sink in sinks)
        {
            queue.Enqueue(sink);
            visited.Add(sink);
        }

        while (queue.Count > 0)
        {
            var stage = queue.Dequeue();
            result.Add(stage);

            // Добавляем предыдущие стадии (обратное направление)
            foreach (var prevStage in stage.PrevStages)
            {
                if (!visited.Contains(prevStage))
                {
                    visited.Add(prevStage);
                    queue.Enqueue(prevStage);
                }
            }
        }

        return result;
    }

    /// <summary>
    ///     Проверяет, разрешён ли переход из одной стадии в другую на основе вероятности
    /// </summary>
    private bool IsTransitionAllowed(BoardStage fromStage, BoardStage toStage)
    {
        // Находим переход из fromStage в toStage
        var transition = fromStage.Stage.Transitions
            .FirstOrDefault(t => t.Stage.Name == toStage.Stage.Name);

        // Если перехода нет в конфигурации — запрещаем
        if (transition == null)
        {
            return false;
        }

        // Если вероятность 1.0 — переход всегда разрешён
        if (transition.Probability >= 1.0)
        {
            return true;
        }

        // Если вероятность 0.0 — переход запрещён
        if (transition.Probability <= 0.0)
        {
            return false;
        }

        // Проверяем вероятность через случайное число
        var roll = _random.NextDouble();
        var result = roll < transition.Probability;
        return result;
    }

    /// <summary>
    ///     Пытается переместить задачу из одной стадии в другую
    /// </summary>
    private bool TryMoveTask(BoardStage fromStage, BoardStage toStage, List<BoardTask>? completedTasks = null)
    {
        // Проверяем, может ли стадия принять задачу (WIP лимит)
        if (!toStage.CanAcceptTasks)
        {
            return false;
        }

        // Находим подходящую задачу в предыдущей стадии
        foreach (var task in fromStage.Tasks)
        {
            // Если указан список завершённых задач — обрабатываем только их
            if (completedTasks != null && !completedTasks.Contains(task))
            {
                continue;
            }

            // Проверяем, готова ли задача к перемещению
            if (!IsTaskReadyForMove(task, fromStage))
            {
                continue;
            }

            // Для рабочих стадий определяем, нужен ли воркер
            BoardWorker? worker = null;
            if (toStage.Stage.Type == StageType.Work)
            {
                // Если задача не требует навыков для этой стадии — помещаем без воркера
                // В следующем цикле while она автоматически продвинется дальше
                if (!TaskRequiresStage(task, toStage))
                {
                    worker = null;
                }
                else
                {
                    // Задача требует эту стадию — ищем воркера
                    worker = FindAvailableWorker(task, toStage);

                    // Если задача требует конкретного воркера (AcceptableWorkers) — не перемещаем без него
                    var requiredWorkerLogin = GetRequiredWorkerForStage(task, toStage);
                    if (!string.IsNullOrEmpty(requiredWorkerLogin) && worker is null)
                    {
                        continue;
                    }

                    // Если воркера нет — задача НЕ перемещается, остаётся в предыдущей стадии
                    if (worker is null)
                    {
                        continue;
                    }
                }
            }

            // Выполняем перемещение
            MoveTask(task, fromStage, toStage, worker);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Проверяет, требует ли задача работы на данной стадии (есть ли пересечение навыков)
    /// </summary>
    private static bool TaskRequiresStage(BoardTask task, BoardStage stage)
    {
        // Если у стадии нет требуемых навыков — задача проходит всегда
        if (stage.Stage.RequiredSkills.Count == 0)
        {
            return true;
        }

        // Если у задачи нет требуемых навыков — задача проходит все стадии
        if (task.Task.RequiredSkills.Count == 0)
        {
            return true;
        }

        // Задача требует эту стадию, если есть пересечение навыков задачи и стадии
        return task.Task.RequiredSkills.Any(skill => stage.Stage.RequiredSkills.Contains(skill));
    }

    /// <summary>
    ///     Проверяет, готова ли задача к перемещению из стадии
    /// </summary>
    private bool IsTaskReadyForMove(BoardTask task, BoardStage fromStage)
    {
        // Если предыдущая стадия рабочая — задача должна быть завершена (100%)
        // ИСКЛЮЧЕНИЕ: если задача не требует эту стадию (нет пересечения навыков) — она готова сразу
        if (fromStage.Stage.Type == StageType.Work)
        {
            if (!TaskRequiresStage(task, fromStage))
            {
                // Задача не требует эту стадию — готова к перемещению без ожидания прогресса
                return true;
            }
            
            return task.IsCompleted;
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

            // Проверяем WIP лимит воркера
            if (!worker.IsAvailable)
            {
                continue;
            }

            // Проверяем, есть ли у воркера навыки для работы на этой стадии с этой задачей
            if (!HasSkillsForTaskOnStage(worker, task, toStage))
            {
                continue;
            }

            // Проверяем ограничение RequiresDifferentResource
            if (toStage is {RequiresDifferentResource: true, ExcludedStage: not null})
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
            
            return worker.Worker.Skills.Any(taskSkills.Contains);
        }

        // Если у задачи нет требуемых навыков — проверяем только навыки стадии
        if (taskSkills.Count == 0)
        {
            return worker.Worker.Skills.Any(stageSkills.Contains);
        }

        // Проверяем, что у воркера есть навык И для стадии, И для задачи
        var workerSkills = worker.Worker.Skills;
        var hasStageSkill = workerSkills.Any(stageSkills.Contains);
        var hasTaskSkill = workerSkills.Any(taskSkills.Contains);

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
        if (task.Task.AcceptableWorkers is null || task.Task.AcceptableWorkers.Count == 0)
        {
            return null;
        }

        // Ищем требование для текущей стадии
        return task.Task.AcceptableWorkers.GetValueOrDefault(stage.Stage.Name);
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

        // Если задача была завершена на предыдущей стадии — записываем что worker завершил задачу
        if (fromStage.Stage.Type == StageType.Work && task.IsCompleted)
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
        
        // Добавляем запись в историю
        var workerInfo = worker is not null ? $" (worker: {worker.Worker.Login})" : string.Empty;
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
        if (toStage.Stage.Type == StageType.Work && worker is not null)
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
        
        // Сбрасываем прогресс
        task.Progress = 0;

        // Удаляем у старого воркера назначение на эту задачу
        task.Worker?.RemoveTaskAssignment(task);

        // Обновляем воркера
        if (worker is not null)
        {
            worker.RemoveTaskAssignment(task);

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