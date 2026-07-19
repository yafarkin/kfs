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
    private readonly HashSet<string> _finalStageNames;

    public WorkerMetricsService(Simulation simulation)
    {
        _simulation = simulation;
        _finalStageNames = MetricsHelpers.GetFinalStageNames(simulation);
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
        // Active Work Time = объединение интервалов работы (слияние перекрывающихся)
        // Total Simulation Days = количество дней симуляции
        // Показывает реальную утилизацию работника за всё время симуляции (≤ 100%)
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
                var taskKey = MetricsHelpers.GetTaskKeyFromActivity(activity);
                if (taskKey != null)
                {
                    taskKeys.Add(taskKey);
                }
            }
        }

        return taskKeys;
    }

    /// <summary>
    /// Проверить, дошла ли задача до финальной стадии.
    /// </summary>
    private bool IsTaskCompleted(string taskKey)
    {
        var activities = _simulation.History
            .SelectMany(d => d.Activities)
            .Where(a => a.Type == ActivityType.TaskMoved && MetricsHelpers.GetTaskKeyFromActivity(a) == taskKey)
            .OrderBy(a => a.DayNumber)
            .ToList();

        return activities.Any(a => a.StageName != null && _finalStageNames.Contains(a.StageName));
    }

    /// <summary>
    /// Рассчитать Lead Time задачи (от LeadTimeStarted до входа в финальную стадию) в днях.
    /// Если LeadTimeStarted отсутствует — возвращает null.
    /// </summary>
    private decimal? CalculateTaskLeadTime(string taskKey)
    {
        var allTaskActivities = _simulation.History
            .SelectMany(d => d.Activities)
            .Where(a => MetricsHelpers.GetTaskKeyFromActivity(a) == taskKey)
            .OrderBy(a => a.DayNumber)
            .ToList();

        return MetricsHelpers.CalculateLeadTimeFromStartEvent(allTaskActivities, _finalStageNames, _simulation.CurrentDay);
    }

    /// <summary>
    /// Рассчитать активное время работы и время ожидания для работника (в днях).
    /// Active Time = объединение интервалов [tookDay, completedDay] (слияние перекрывающихся).
    /// Wait Time = totalDays - activeTime(union).
    /// </summary>
    private (decimal ActiveTime, decimal WaitTime) CalculateFlowEfficiencyTimes(
        List<HistoryActivity> allActivities,
        string workerLogin)
    {
        // Получить все события работника
        var workerEvents = allActivities
            .Where(a => a.WorkerLogin == workerLogin &&
                       (a.Type == ActivityType.WorkerTookTask || a.Type == ActivityType.WorkerCompletedTask))
            .OrderBy(a => a.DayNumber)
            .ToList();

        // Собрать все интервалы [tookDay, completedDay]
        var intervals = new List<(int Start, int End)>();
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

            var endDay = completedTask?.DayNumber ?? _simulation.CurrentDay;
            intervals.Add((tookTask.DayNumber, endDay));
        }

        // Объединить перекрывающиеся интервалы
        var mergedIntervals = MergeIntervals(intervals);

        // Сумма длин слитых интервалов (включая оба конца)
        var activeTime = mergedIntervals.Sum(i => i.End - i.Start + 1);

        // Wait Time = общее время симуляции - activeTime
        var totalDays = _simulation.History.Count > 0 ? _simulation.History.Count : 1;
        var waitTime = totalDays - activeTime;

        return (activeTime, waitTime);
    }

    /// <summary>
    /// Объединить перекрывающиеся интервалы.
    /// Пример: [1,3], [2,5] → [1,5]
    /// </summary>
    private static List<(int Start, int End)> MergeIntervals(List<(int Start, int End)> intervals)
    {
        if (intervals.Count == 0)
            return [];

        // Сортируем по началу интервала
        var sorted = intervals.OrderBy(i => i.Start).ToList();
        var merged = new List<(int Start, int End)>();

        var currentStart = sorted[0].Start;
        var currentEnd = sorted[0].End;

        for (var i = 1; i < sorted.Count; i++)
        {
            var next = sorted[i];

            if (next.Start <= currentEnd + 1)
            {
                // Интервалы перекрываются или соприкасаются — объединяем
                currentEnd = Math.Max(currentEnd, next.End);
            }
            else
            {
                // Добавляем текущий интервал и начинаем новый
                merged.Add((currentStart, currentEnd));
                currentStart = next.Start;
                currentEnd = next.End;
            }
        }

        // Добавляем последний интервал
        merged.Add((currentStart, currentEnd));

        return merged;
    }

    /// <summary>
    /// Извлечь ключ задачи из активности.
    /// Возвращает TaskKey без regex-фоллбека.
    /// </summary>
    private static string? GetTaskKeyFromActivity(HistoryActivity activity)
    {
        return activity.TaskKey;
    }
}
