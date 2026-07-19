using KanbanFlowApi.Dtos.Metrics;
using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Board;
using KanbanFlowSerivce.Dtos.History;
using KanbanFlowSerivce.Enums;

namespace KanbanFlowApi.Services;

/// <summary>
/// Сервис для расчёта метрик работников.
/// Использует event-based подход: метрики рассчитываются на основе событий истории.
/// </summary>
public sealed class WorkerMetricsService
{
    private readonly Simulation _simulation;

    public WorkerMetricsService(Simulation simulation)
    {
        _simulation = simulation;
    }

    /// <summary>
    /// Рассчитать метрики для всех работников.
    /// </summary>
    public List<ApiWorkerMetricsDto> CalculateAllWorkersMetrics()
    {
        return _simulation.Board.Workers
            .Select(w => CalculateWorkerMetrics(w))
            .ToList();
    }

    /// <summary>
    /// Рассчитать метрики для конкретного работника.
    /// </summary>
    private ApiWorkerMetricsDto CalculateWorkerMetrics(BoardWorker boardWorker)
    {
        var workerLogin = boardWorker.Worker.Login;

        // 1. Найти все ценные стадии (Work-тип + создаёт ценность)
        var valuableStageNames = _simulation.Board.Stages
            .Where(s => s.Stage.Type == StageType.Work && s.Stage.CreatesValue)
            .Select(s => s.Stage.Name)
            .ToHashSet();

        // 2. Получить все активности работника
        var allActivities = _simulation.History
            .SelectMany(d => d.Activities)
            .Where(a => IsWorkerActivity(a, workerLogin))
            .OrderBy(a => a.DayNumber)
            .ToList();

        // 3. Throughput: задачи, где работник работал на ценной стадии И задача дошла до Done
        var valuableTaskKeys = GetTasksWhereWorkerWorkedOnValuableStage(allActivities, valuableStageNames);
        var completedValuableTaskKeys = valuableTaskKeys
            .Where(taskKey => IsTaskCompleted(taskKey))
            .ToHashSet();

        var totalDays = _simulation.History.Count > 0 ? _simulation.History.Count : 1;
        var throughput = completedValuableTaskKeys.Count / (decimal)totalDays;

        // 4. Lead Time: среднее время задач (от LeadTimeStarted/первого TaskMoved до Done/сейчас)
        var leadTimes = new List<decimal>();
        foreach (var taskKey in valuableTaskKeys)
        {
            var leadTime = CalculateTaskLeadTime(taskKey);
            if (leadTime.HasValue)
            {
                leadTimes.Add(leadTime.Value);
            }
        }
        var avgLeadTime = leadTimes.Count > 0 ? leadTimes.Average() : 0m;

        // 5. Flow Efficiency: (Active Work Time) / (Total Simulation Days)
        // Active Work Time = Σ(WorkerCompletedTask.DayNumber - WorkerTookTask.DayNumber) по всем парам
        // Total Simulation Days = количество дней симуляции
        // Показывает реальную утилизацию работника за всё время симуляции
        var (activeTime, waitTime) = CalculateFlowEfficiencyTimes(allActivities, workerLogin);
        var efficiencyPercent = (activeTime / totalDays) * 100;

        return new ApiWorkerMetricsDto
        {
            Login = workerLogin,
            Throughput = Math.Round(throughput, 2),
            LeadTime = Math.Round(avgLeadTime, 1),
            ValuableTasksCount = valuableTaskKeys.Count,
            EfficiencyPercent = Math.Round(efficiencyPercent, 1),
            WorkTimeDays = Math.Round(activeTime, 1),
            BufferTimeDays = Math.Round(waitTime, 1)
        };
    }

    /// <summary>
    /// Проверить, является ли активность активностью работника (WorkerTookTask или WorkerCompletedTask).
    /// </summary>
    private static bool IsWorkerActivity(HistoryActivity activity, string workerLogin)
    {
        if (activity.Type != ActivityType.WorkerTookTask &&
            activity.Type != ActivityType.WorkerCompletedTask)
        {
            return false;
        }

        return activity.WorkerLogin == workerLogin;
    }

    /// <summary>
    /// Получить задачи, где работник завершил ценную стадию.
    /// </summary>
    private HashSet<string> GetTasksWhereWorkerWorkedOnValuableStage(
        List<HistoryActivity> activities,
        HashSet<string> valuableStageNames)
    {
        var taskKeys = new HashSet<string>();

        foreach (var activity in activities)
        {
            if (activity.Type == ActivityType.WorkerCompletedTask &&
                activity.StageName != null &&
                valuableStageNames.Contains(activity.StageName))
            {
                var taskKey = GetTaskKeyFromActivity(activity);
                if (taskKey != null)
                {
                    taskKeys.Add(taskKey);
                }
            }
        }

        return taskKeys;
    }

    /// <summary>
    /// Проверить, дошла ли задача до Done.
    /// </summary>
    private bool IsTaskCompleted(string taskKey)
    {
        var activities = _simulation.History
            .SelectMany(d => d.Activities)
            .Where(a => a.Type == ActivityType.TaskMoved && GetTaskKeyFromActivity(a) == taskKey)
            .OrderBy(a => a.DayNumber)
            .ToList();

        return activities.Any(a => a.StageName == "Done");
    }

    /// <summary>
    /// Рассчитать Lead Time задачи (от LeadTimeStarted или первого TaskMoved до Done/сейчас) в днях.
    /// </summary>
    private decimal? CalculateTaskLeadTime(string taskKey)
    {
        var allTaskActivities = _simulation.History
            .SelectMany(d => d.Activities)
            .Where(a => GetTaskKeyFromActivity(a) == taskKey)
            .OrderBy(a => a.DayNumber)
            .ToList();

        if (allTaskActivities.Count == 0)
            return null;

        // Найти начало Lead Time (событие LeadTimeStarted или первый TaskMoved)
        var leadTimeStartEvent = allTaskActivities
            .FirstOrDefault(a => a.Type == ActivityType.LeadTimeStarted);

        if (leadTimeStartEvent == null)
        {
            // Если нет явного события, берём первый TaskMoved
            leadTimeStartEvent = allTaskActivities
                .FirstOrDefault(a => a.Type == ActivityType.TaskMoved);
        }

        if (leadTimeStartEvent == null)
            return null;

        var startDay = leadTimeStartEvent.DayNumber;

        // Найти конец (Done или текущий день)
        var doneActivity = allTaskActivities
            .FirstOrDefault(a => a.Type == ActivityType.TaskMoved && a.StageName == "Done");

        var endDay = doneActivity?.DayNumber ?? _simulation.CurrentDay;

        return (endDay - startDay);
    }

    /// <summary>
    /// Рассчитать активное время работы и время ожидания для работника (в днях).
    /// Active Time = Σ(WorkerCompletedTask.DayNumber - WorkerTookTask.DayNumber) по всем парам
    /// Wait Time = время простоя между задачами (только если между завершением и началом следующей задачи прошло > 1 дня)
    /// </summary>
    private (decimal ActiveTime, decimal WaitTime) CalculateFlowEfficiencyTimes(
        List<HistoryActivity> allActivities,
        string workerLogin)
    {
        var activeTime = 0m;
        var waitTime = 0m;

        // Получить все события работника, отсортированные по дням
        var workerEvents = allActivities
            .Where(a => a.WorkerLogin == workerLogin &&
                       (a.Type == ActivityType.WorkerTookTask || a.Type == ActivityType.WorkerCompletedTask))
            .OrderBy(a => a.DayNumber)
            .ToList();

        // Рассчитать активное время по парам WorkerTookTask -> WorkerCompletedTask
        var tookTasks = workerEvents
            .Where(a => a.Type == ActivityType.WorkerTookTask)
            .ToList();

        foreach (var tookTask in tookTasks)
        {
            // Найти соответствующее WorkerCompletedTask по CorrelationId
            var completedTask = workerEvents
                .FirstOrDefault(a =>
                    a.Type == ActivityType.WorkerCompletedTask &&
                    a.CorrelationId == tookTask.CorrelationId);

            if (completedTask != null)
            {
                // Активное время = завершение - начало + 1 (включая оба дня)
                // Пример: взял в День 1, завершил в День 1 = 1 день работы (а не 0)
                var duration = (completedTask.DayNumber - tookTask.DayNumber + 1);
                activeTime += duration;
            }
            else
            {
                // Задача ещё не завершена — считаем до текущего дня (включительно)
                var duration = (_simulation.CurrentDay - tookTask.DayNumber + 1);
                activeTime += duration;
            }
        }

        // Рассчитать время ожидания (простой)
        // Простой считается только если между завершением задачи и началом следующей прошло > 1 дня
        // Формула: BufferDays = NextTookTask.DayNumber - CompletedTask.DayNumber - 1
        // Если результат <= 0, то простоя не было (worker начал новую задачу на следующий день или в тот же день)
        var completedTasks = workerEvents
            .Where(a => a.Type == ActivityType.WorkerCompletedTask)
            .OrderBy(a => a.DayNumber)
            .ToList();

        for (var i = 0; i < completedTasks.Count; i++)
        {
            var completedDay = completedTasks[i].DayNumber;

            // Найти следующий WorkerTookTask
            var nextTookTask = tookTasks
                .FirstOrDefault(t => t.DayNumber > completedDay);

            if (nextTookTask != null)
            {
                // Простой = разница в днях минус 1 (следующий день не считается простоем)
                var waitDuration = (nextTookTask.DayNumber - completedDay - 1);
                if (waitDuration > 0)
                {
                    waitTime += waitDuration;
                }
            }
            else
            {
                // Если нет следующего WorkerTookTask — считаем до конца симуляции
                // Но последний день тоже не считаем простоем (worker мог быть занят до конца дня)
                var waitDuration = (_simulation.CurrentDay - completedDay - 1);
                if (waitDuration > 0)
                {
                    waitTime += waitDuration;
                }
            }
        }

        return (activeTime, waitTime);
    }

    /// <summary>
    /// Извлечь ключ задачи из активности.
    /// </summary>
    private static string? GetTaskKeyFromActivity(HistoryActivity activity)
    {
        if (activity.TaskKey != null)
            return activity.TaskKey;

        var match = System.Text.RegularExpressions.Regex.Match(activity.Description, @"TASK-\d+");
        return match.Success ? match.Value : null;
    }
}
