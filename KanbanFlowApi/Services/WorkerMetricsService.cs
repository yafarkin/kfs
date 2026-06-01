using KanbanFlowApi.Dtos.Metrics;
using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Board;
using KanbanFlowSerivce.Dtos.History;
using KanbanFlowSerivce.Enums;

namespace KanbanFlowApi.Services;

/// <summary>
/// Сервис для расчёта метрик работников.
/// Использует stage-based подход: стадии делятся на ценные (создают ценность) и вспомогательные.
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
        // Буферные стадии всегда игнорируются, независимо от CreatesValue
        var valuableStageNames = _simulation.Board.Stages
            .Where(s => s.Stage.Type == StageType.Work && s.Stage.CreatesValue)
            .Select(s => s.Stage.Name)
            .ToHashSet();

        // 2. Получить все активности работника
        var allActivities = _simulation.History
            .SelectMany(d => d.Activities)
            .Where(a => IsWorkerActivity(a, workerLogin))
            .OrderBy(a => a.Tick)
            .ToList();

        // 3. Throughput: задачи, где работник работал на ценной стадии И задача дошла до Done
        var valuableTaskKeys = GetTasksWhereWorkerWorkedOnValuableStage(allActivities, valuableStageNames);
        var completedValuableTaskKeys = valuableTaskKeys
            .Where(taskKey => IsTaskCompleted(taskKey))
            .ToHashSet();

        var totalDays = _simulation.History.Count > 0 ? _simulation.History.Count : 1;
        var throughput = completedValuableTaskKeys.Count / (decimal)totalDays;

        // 4. Lead Time: среднее время задач (от isLeadTimeStart до Done/сейчас), где работник участвовал в ценной стадии
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

        // 5. Flow Efficiency: (Work Time) / (Work + Buffer Time) — все стадии
        var (workTime, bufferTime) = CalculateWorkerTime(allActivities, workerLogin);
        var totalTime = workTime + bufferTime;
        var efficiencyPercent = totalTime > 0 ? (workTime / totalTime) * 100 : 0m;

        return new ApiWorkerMetricsDto
        {
            Login = workerLogin,
            Throughput = Math.Round(throughput, 2),
            LeadTime = Math.Round(avgLeadTime, 1),
            ValuableTasksCount = valuableTaskKeys.Count,
            EfficiencyPercent = Math.Round(efficiencyPercent, 1),
            WorkTimeDays = Math.Round(workTime, 1),
            BufferTimeDays = Math.Round(bufferTime, 1)
        };
    }

    /// <summary>
    /// Проверить, является ли активность активностью работника (WorkerTookTask или WorkerCompletedTask).
    /// </summary>
    private static bool IsWorkerActivity(HistoryActivity activity, string workerLogin)
    {
        return activity.Type == ActivityType.WorkerTookTask || 
               activity.Type == ActivityType.WorkerCompletedTask;
    }

    /// <summary>
    /// Получить задачи, где работник завершил ценную стадию.
    /// </summary>
    private HashSet<string> GetTasksWhereWorkerWorkedOnValuableStage(
        List<HistoryActivity> activities, 
        HashSet<string> valuableStageNames)
    {
        var taskKeys = new HashSet<string>();

        // Ищем только WorkerCompletedTask на ценных стадиях
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
            .OrderBy(a => a.Tick)
            .ToList();

        return activities.Any(a => a.StageName == "Done");
    }

    /// <summary>
    /// Рассчитать Lead Time задачи (от isLeadTimeStart до Done/сейчас) в днях.
    /// </summary>
    private decimal? CalculateTaskLeadTime(string taskKey)
    {
        var activities = _simulation.History
            .SelectMany(d => d.Activities)
            .Where(a => a.Type == ActivityType.TaskMoved && GetTaskKeyFromActivity(a) == taskKey)
            .OrderBy(a => a.Tick)
            .ToList();

        if (activities.Count == 0)
            return null;

        // Найти начало (isLeadTimeStart стадия)
        var startStageName = _simulation.Board.Stages
            .FirstOrDefault(s => s.Stage.IsLeadTimeStart)?
            .Stage.Name;

        var startActivity = activities.FirstOrDefault(a => a.StageName == startStageName);
        if (startActivity == null)
            startActivity = activities.First(); // Если не нашли, берём первый переход

        var startTick = startActivity.Tick;

        // Найти конец (Done или текущий тик)
        var doneActivity = activities.FirstOrDefault(a => a.StageName == "Done");
        var endTick = doneActivity?.Tick ?? _simulation.CurrentTick;

        // Конвертировать в дни
        return (endTick - startTick) / 24m;
    }

    /// <summary>
    /// Рассчитать время работы (Work) и ожидания (Buffer) для работника в днях.
    /// </summary>
    private (decimal WorkTime, decimal BufferTime) CalculateWorkerTime(
        List<HistoryActivity> activities, 
        string workerLogin)
    {
        var workTime = 0m;
        var bufferTime = 0m;

        // Получить все TaskMoved активности для задач, которые брал этот работник
        var workerTaskKeys = activities
            .Where(a => a.Type == ActivityType.WorkerTookTask)
            .Select(a => GetTaskKeyFromActivity(a))
            .Where(k => k != null)
            .ToHashSet()!;

        var taskMovements = _simulation.History
            .SelectMany(d => d.Activities)
            .Where(a => a.Type == ActivityType.TaskMoved && 
                        GetTaskKeyFromActivity(a) != null &&
                        workerTaskKeys.Contains(GetTaskKeyFromActivity(a)!))
            .OrderBy(a => a.Tick)
            .ToList();

        if (taskMovements.Count == 0)
            return (0, 0);

        // Группировать по задачам
        foreach (var taskKey in workerTaskKeys)
        {
            var taskActivities = taskMovements
                .Where(a => GetTaskKeyFromActivity(a) == taskKey)
                .OrderBy(a => a.Tick)
                .ToList();

            if (taskActivities.Count == 0)
                continue;

            for (var i = 0; i < taskActivities.Count - 1; i++)
            {
                var currentActivity = taskActivities[i];
                var nextActivity = taskActivities[i + 1];

                if (currentActivity.StageName == null)
                    continue;

                var durationDays = (nextActivity.Tick - currentActivity.Tick) / 24m;

                var stage = _simulation.Board.Stages
                    .FirstOrDefault(s => s.Stage.Name == currentActivity.StageName);

                if (stage?.Stage.Type == StageType.Work)
                {
                    workTime += durationDays;
                }
                else if (stage?.Stage.Type == StageType.Buffer)
                {
                    bufferTime += durationDays;
                }
            }
        }

        return (workTime, bufferTime);
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
