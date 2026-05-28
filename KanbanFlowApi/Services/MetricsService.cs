using KanbanFlowApi.Dtos.Metrics;
using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Board;
using KanbanFlowSerivce.Dtos.History;
using KanbanFlowSerivce.Enums;

namespace KanbanFlowApi.Services;

/// <summary>
/// Сервис для расчёта метрик симуляции.
/// Использует историю активностей для расчёта метрик.
/// </summary>
public sealed class MetricsService
{
    private readonly Simulation _simulation;
    private readonly string _leadTimeStartStageName;

    public MetricsService(Simulation simulation, string leadTimeStartStageName = "Todo")
    {
        _simulation = simulation;
        _leadTimeStartStageName = leadTimeStartStageName;
    }

    /// <summary>
    /// Рассчитать все метрики симуляции.
    /// </summary>
    public ApiMetricsDto CalculateAllMetrics()
    {
        return new ApiMetricsDto
        {
            LeadTime = CalculateLeadTime(),
            Throughput = CalculateThroughput(),
            FlowEfficiency = CalculateFlowEfficiency(),
            Frequency = CalculateFrequency()
        };
    }

    /// <summary>
    /// Рассчитать Lead Time (p50, p85 перцентили).
    /// Lead Time считается от стадии isLeadTimeStart (Todo) до завершения задачи (Done).
    /// Использует историю активностей для расчёта.
    /// </summary>
    public ApiLeadTimeMetricsDto CalculateLeadTime()
    {
        var leadTimes = new List<decimal>();
        var taskKeys = _simulation.Board.Tasks.Select(t => t.Task.Key).ToList();

        foreach (var taskKey in taskKeys)
        {
            var leadTime = CalculateTaskLeadTimeFromHistory(taskKey);
            if (leadTime.HasValue)
            {
                leadTimes.Add(leadTime.Value);
            }
        }

        if (leadTimes.Count == 0)
        {
            return new ApiLeadTimeMetricsDto
            {
                P50 = 0,
                P85 = 0,
                TaskCount = 0
            };
        }

        leadTimes.Sort();

        return new ApiLeadTimeMetricsDto
        {
            P50 = CalculatePercentile(leadTimes, 50),
            P85 = CalculatePercentile(leadTimes, 85),
            TaskCount = leadTimes.Count
        };
    }

    /// <summary>
    /// Рассчитать Lead Time для задачи по истории активностей.
    /// Lead Time считается от первого перехода задачи (из Todo) до перехода в Done.
    /// </summary>
    private decimal? CalculateTaskLeadTimeFromHistory(string taskKey)
    {
        // Находим все активности TaskMoved для этой задачи
        var taskActivities = _simulation.History
            .SelectMany(d => d.Activities)
            .Where(a => a.Type == ActivityType.TaskMoved && GetTaskKeyFromActivity(a) == taskKey)
            .OrderBy(a => a.Tick)
            .ToList();

        if (taskActivities.Count == 0)
        {
            return null;
        }

        // Первый переход задачи - это начало Lead Time (выход из Todo)
        var startTick = taskActivities.First().Tick;

        // Находим переход в Done
        var enterDoneActivity = taskActivities
            .FirstOrDefault(a => a.StageName == "Done");

        if (enterDoneActivity == null)
        {
            // Задача ещё не завершена
            return null;
        }

        return enterDoneActivity.Tick - startTick;
    }

    /// <summary>
    /// Извлечь ключ задачи из активности (из TaskKey или из Description).
    /// </summary>
    private static string? GetTaskKeyFromActivity(HistoryActivity activity)
    {
        if (activity.TaskKey != null)
            return activity.TaskKey;

        // Пытаемся извлечь из описания (формат: "Задача TASK-1 перемещена...")
        var match = System.Text.RegularExpressions.Regex.Match(activity.Description, @"TASK-\d+");
        return match.Success ? match.Value : null;
    }

    /// <summary>
    /// Рассчитать перцентиль из отсортированного списка.
    /// </summary>
    private static decimal CalculatePercentile(List<decimal> sortedValues, int percentile)
    {
        if (sortedValues.Count == 0)
            return 0;

        var index = (percentile / 100.0) * (sortedValues.Count - 1);
        var lowerIndex = (int)Math.Floor(index);
        var upperIndex = (int)Math.Ceiling(index);

        if (lowerIndex == upperIndex)
        {
            return sortedValues[lowerIndex];
        }

        var fraction = (decimal)(index - lowerIndex);
        return sortedValues[lowerIndex] + fraction * (sortedValues[upperIndex] - sortedValues[lowerIndex]);
    }

    /// <summary>
    /// Рассчитать Throughput (пропускную способность).
    /// </summary>
    public ApiThroughputMetricsDto CalculateThroughput()
    {
        var dailyHistory = new List<ApiThroughputDayDto>();
        var totalCompleted = 0;

        foreach (var day in _simulation.History)
        {
            var completedCount = day.Activities
                .Count(a => a.Type == ActivityType.WorkerCompletedTask);

            totalCompleted += completedCount;

            dailyHistory.Add(new ApiThroughputDayDto
            {
                DayNumber = day.DayNumber,
                CompletedTasksCount = completedCount
            });
        }

        var totalDays = _simulation.History.Count > 0 ? _simulation.History.Count : 1;

        return new ApiThroughputMetricsDto
        {
            Overall = totalCompleted / (decimal)totalDays,
            DailyHistory = dailyHistory
        };
    }

    /// <summary>
    /// Рассчитать Flow Efficiency.
    /// Active Time - время в рабочих статусах (начиная с isLeadTimeStart стадии).
    /// Wait Time - время в буферных стадиях (ожидание).
    /// Использует историю активностей для расчёта.
    /// </summary>
    public ApiFlowEfficiencyMetricsDto CalculateFlowEfficiency()
    {
        var totalActiveTime = 0m;
        var totalWaitTime = 0m;

        var taskKeys = _simulation.Board.Tasks.Select(t => t.Task.Key).ToList();

        foreach (var taskKey in taskKeys)
        {
            var (active, wait) = CalculateTaskFlowEfficiencyFromHistory(taskKey);
            totalActiveTime += active;
            totalWaitTime += wait;
        }

        var totalTime = totalActiveTime + totalWaitTime;
        var efficiencyPercent = totalTime > 0 ? (totalActiveTime / totalTime) * 100 : 0;

        return new ApiFlowEfficiencyMetricsDto
        {
            ActiveTime = totalActiveTime,
            WaitTime = totalWaitTime,
            EfficiencyPercent = Math.Round(efficiencyPercent, 2)
        };
    }

    /// <summary>
    /// Рассчитать Active и Wait время для задачи по истории активностей.
    /// </summary>
    private (decimal ActiveTime, decimal WaitTime) CalculateTaskFlowEfficiencyFromHistory(string taskKey)
    {
        var activeTime = 0m;
        var waitTime = 0m;

        // Получаем все активности TaskMoved для конкретной задачи
        var taskActivities = _simulation.History
            .SelectMany(d => d.Activities)
            .Where(a => a.Type == ActivityType.TaskMoved && 
                        GetTaskKeyFromActivity(a) == taskKey && 
                        a.StageName != null)
            .OrderBy(a => a.Tick)
            .ToList();

        if (taskActivities.Count == 0)
        {
            return (0, 0);
        }

        // Первый переход - начало расчёта
        var startTick = taskActivities.First().Tick;

        // Проходим по всем переходам задачи
        for (var i = 0; i < taskActivities.Count - 1; i++)
        {
            var currentActivity = taskActivities[i];
            var nextActivity = taskActivities[i + 1];

            var duration = nextActivity.Tick - currentActivity.Tick;
            
            // Определяем тип стадии по имени
            var stageType = GetStageTypeByName(currentActivity.StageName);

            if (stageType == StageType.Work)
            {
                activeTime += duration;
            }
            else if (stageType == StageType.Buffer)
            {
                waitTime += duration;
            }
        }

        // Если задача ещё в процессе, добавляем время до текущего тика
        var lastActivity = taskActivities.LastOrDefault();
        if (lastActivity != null)
        {
            var remainingTime = _simulation.CurrentTick - lastActivity.Tick;
            if (remainingTime > 0)
            {
                // Определяем текущую стадию задачи
                var currentStageName = _simulation.Board.Tasks
                    .FirstOrDefault(t => t.Task.Key == taskKey)?
                    .CurrentStage?.Stage.Name;

                if (currentStageName != null)
                {
                    var currentStageType = GetStageTypeByName(currentStageName);
                    if (currentStageType == StageType.Work)
                    {
                        activeTime += remainingTime;
                    }
                    else if (currentStageType == StageType.Buffer)
                    {
                        waitTime += remainingTime;
                    }
                }
            }
        }

        return (activeTime, waitTime);
    }

    /// <summary>
    /// Получить тип стадии по имени.
    /// </summary>
    private StageType GetStageTypeByName(string stageName)
    {
        var stage = _simulation.Board.Stages
            .FirstOrDefault(s => s.Stage.Name == stageName);
        return stage?.Stage.Type ?? StageType.Buffer;
    }

    /// <summary>
    /// Рассчитать частотную метрику (распределение задач по времени выполнения).
    /// </summary>
    public ApiFrequencyMetricsDto CalculateFrequency()
    {
        var distribution = new Dictionary<string, int>();
        var completedTasksCount = 0;

        var taskKeys = _simulation.Board.Tasks.Select(t => t.Task.Key).ToList();

        foreach (var taskKey in taskKeys)
        {
            var leadTime = CalculateTaskLeadTimeFromHistory(taskKey);
            if (leadTime.HasValue)
            {
                completedTasksCount++;
                var bucket = GetTimeBucket(leadTime.Value);
                
                if (!distribution.ContainsKey(bucket))
                {
                    distribution[bucket] = 0;
                }
                distribution[bucket]++;
            }
        }

        return new ApiFrequencyMetricsDto
        {
            Distribution = distribution,
            TaskCount = completedTasksCount
        };
    }

    /// <summary>
    /// Получить диапазон времени (бакет) для значения.
    /// </summary>
    private static string GetTimeBucket(decimal hours)
    {
        var bucketSize = 24; // 24 часа
        var lower = (int)(hours / bucketSize) * bucketSize;
        var upper = lower + bucketSize;
        return $"{lower}-{upper}";
    }
}
