using KanbanFlowApi.Dtos.Metrics;
using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Board;
using KanbanFlowSerivce.Dtos.History;
using KanbanFlowSerivce.Enums;

namespace KanbanFlowApi.Services;

/// <summary>
/// Сервис для расчёта метрик по задачам.
/// </summary>
public sealed class TaskMetricsService
{
    private readonly Simulation _simulation;

    public TaskMetricsService(Simulation simulation)
    {
        _simulation = simulation;
    }

    /// <summary>
    /// Рассчитать метрики для всех задач.
    /// </summary>
    public List<ApiTaskMetricsDto> CalculateAllTasksMetrics()
    {
        return _simulation.Board.Tasks
            .Select(t => CalculateTaskMetrics(t))
            .ToList();
    }

    /// <summary>
    /// Рассчитать метрики для конкретной задачи.
    /// </summary>
    public ApiTaskMetricsDto CalculateTaskMetrics(BoardTask boardTask)
    {
        var taskKey = boardTask.Task.Key;

        // Получить все активности задачи
        var allActivities = _simulation.History
            .SelectMany(d => d.Activities)
            .Where(a => GetTaskKeyFromActivity(a) == taskKey)
            .OrderBy(a => a.DayNumber)
            .ToList();

        // 1. Lead Time: от LeadTimeStarted/первого TaskMoved до Done/сейчас
        var leadTime = CalculateLeadTime(allActivities);

        // 2. Cycle Time: от первого WorkerTookTask до последнего WorkerCompletedTask
        var cycleTime = CalculateCycleTime(allActivities);

        // 3. Flow Efficiency: Active Time / (Active Time + Wait Time)
        var (activeTime, waitTime) = CalculateFlowEfficiencyTimes(allActivities);
        var totalTime = activeTime + waitTime;
        var efficiencyPercent = totalTime > 0 ? (activeTime / totalTime) * 100 : 0m;

        // 4. Количество стадий через которые прошла задача
        var stagesCount = allActivities
            .Where(a => a.Type == ActivityType.TaskMoved)
            .Select(a => a.StageName)
            .Distinct()
            .Count();

        // 5. Количество воркеров которые работали над задачей
        var workersCount = allActivities
            .Where(a => a.Type == ActivityType.WorkerTookTask || a.Type == ActivityType.WorkerCompletedTask)
            .Select(a => a.WorkerLogin)
            .Distinct()
            .Count();

        // 6. Статус задачи
        var status = GetTaskStatus(allActivities);

        return new ApiTaskMetricsDto
        {
            TaskKey = taskKey,
            LeadTimeDays = Math.Round(leadTime, 1),
            CycleTimeDays = Math.Round(cycleTime, 1),
            EfficiencyPercent = Math.Round(efficiencyPercent, 1),
            ActiveTimeDays = Math.Round(activeTime, 1),
            WaitTimeDays = Math.Round(waitTime, 1),
            StagesCount = stagesCount,
            WorkersCount = workersCount,
            Status = status,
            IsCompleted = status == "Done"
        };
    }

    /// <summary>
    /// Рассчитать Lead Time задачи (от LeadTimeStarted или первого TaskMoved до Done/сейчас).
    /// </summary>
    private decimal CalculateLeadTime(List<HistoryActivity> activities)
    {
        if (activities.Count == 0)
            return 0m;

        // Найти начало Lead Time
        var leadTimeStartEvent = activities
            .FirstOrDefault(a => a.Type == ActivityType.LeadTimeStarted);

        if (leadTimeStartEvent == null)
        {
            // Если нет явного события, берём первый TaskMoved
            leadTimeStartEvent = activities
                .FirstOrDefault(a => a.Type == ActivityType.TaskMoved);
        }

        if (leadTimeStartEvent == null)
            return 0m;

        var startDay = leadTimeStartEvent.DayNumber;

        // Найти конец (Done или текущий день)
        var doneActivity = activities
            .FirstOrDefault(a => a.Type == ActivityType.TaskMoved && a.StageName == "Done");

        var endDay = doneActivity?.DayNumber ?? _simulation.CurrentDay;

        return (endDay - startDay);
    }

    /// <summary>
    /// Рассчитать Cycle Time задачи (от первого WorkerTookTask до последнего WorkerCompletedTask).
    /// </summary>
    private decimal CalculateCycleTime(List<HistoryActivity> activities)
    {
        var tookTask = activities
            .FirstOrDefault(a => a.Type == ActivityType.WorkerTookTask);

        if (tookTask == null)
            return 0m;

        var completedTask = activities
            .LastOrDefault(a => a.Type == ActivityType.WorkerCompletedTask);

        var endDay = completedTask?.DayNumber ?? _simulation.CurrentDay;

        return (endDay - tookTask.DayNumber);
    }

    /// <summary>
    /// Рассчитать активное время и время ожидания для задачи.
    /// </summary>
    private (decimal ActiveTime, decimal WaitTime) CalculateFlowEfficiencyTimes(
        List<HistoryActivity> activities)
    {
        var activeTime = 0m;
        var waitTime = 0m;

        // Активное время: пары WorkerTookTask -> WorkerCompletedTask
        var tookTasks = activities
            .Where(a => a.Type == ActivityType.WorkerTookTask)
            .ToList();

        foreach (var tookTask in tookTasks)
        {
            var completedTask = activities
                .FirstOrDefault(a => 
                    a.Type == ActivityType.WorkerCompletedTask && 
                    a.CorrelationId == tookTask.CorrelationId);

            if (completedTask != null)
            {
                var duration = (completedTask.DayNumber - tookTask.DayNumber);
                activeTime += duration;
            }
            else
            {
                // Задача ещё не завершена на этой стадии
                var duration = (_simulation.CurrentDay - tookTask.DayNumber);
                activeTime += duration;
            }
        }

        // Время ожидания: TaskWaiting -> TaskResumed
        var waitingEvents = activities
            .Where(a => a.Type == ActivityType.TaskWaiting)
            .ToList();

        foreach (var waitingEvent in waitingEvents)
        {
            var resumedEvent = activities
                .FirstOrDefault(a =>
                    a.Type == ActivityType.TaskResumed &&
                    a.StageName == waitingEvent.StageName &&
                    a.DayNumber > waitingEvent.DayNumber);

            var endDay = resumedEvent?.DayNumber ?? _simulation.CurrentDay;
            var waitDuration = (endDay - waitingEvent.DayNumber);
            waitTime += waitDuration;
        }

        // Время ожидания между стадиями (между TaskMoved и следующим WorkerTookTask)
        var movements = activities
            .Where(a => a.Type == ActivityType.TaskMoved)
            .OrderBy(a => a.DayNumber)
            .ToList();

        for (var i = 0; i < movements.Count - 1; i++)
        {
            var currentMove = movements[i];
            var nextTookTask = activities
                .FirstOrDefault(a => a.Type == ActivityType.WorkerTookTask && a.DayNumber > currentMove.DayNumber);

            if (nextTookTask != null && nextTookTask.DayNumber > currentMove.DayNumber)
            {
                var waitDuration = (nextTookTask.DayNumber - currentMove.DayNumber);
                waitTime += waitDuration;
            }
        }

        return (activeTime, waitTime);
    }

    /// <summary>
    /// Определить статус задачи.
    /// </summary>
    private string GetTaskStatus(List<HistoryActivity> activities)
    {
        var lastMove = activities
            .LastOrDefault(a => a.Type == ActivityType.TaskMoved);

        if (lastMove == null)
            return "Todo";

        if (lastMove.StageName == "Done")
            return "Done";

        // Проверить есть ли активная работа
        var lastCompleted = activities
            .LastOrDefault(a => a.Type == ActivityType.WorkerCompletedTask);
        var lastTook = activities
            .LastOrDefault(a => a.Type == ActivityType.WorkerTookTask);

        if (lastTook != null && (lastCompleted == null || lastTook.DayNumber > lastCompleted.DayNumber))
            return "In Progress";

        return lastMove.StageName ?? "Todo";
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

/// <summary>
/// DTO для метрик задачи.
/// </summary>
public sealed record ApiTaskMetricsDto
{
    /// <summary>
    /// Ключ задачи.
    /// </summary>
    public string TaskKey { get; set; } = null!;

    /// <summary>
    /// Lead Time в днях (от начала до завершения/сейчас).
    /// </summary>
    public decimal LeadTimeDays { get; set; }

    /// <summary>
    /// Cycle Time в днях (от первого WorkerTookTask до последнего WorkerCompletedTask).
    /// </summary>
    public decimal CycleTimeDays { get; set; }

    /// <summary>
    /// Flow Efficiency — процент активного времени.
    /// </summary>
    public decimal EfficiencyPercent { get; set; }

    /// <summary>
    /// Активное время работы в днях.
    /// </summary>
    public decimal ActiveTimeDays { get; set; }

    /// <summary>
    /// Время ожидания в днях.
    /// </summary>
    public decimal WaitTimeDays { get; set; }

    /// <summary>
    /// Количество стадий через которые прошла задача.
    /// </summary>
    public int StagesCount { get; set; }

    /// <summary>
    /// Количество воркеров которые работали над задачей.
    /// </summary>
    public int WorkersCount { get; set; }

    /// <summary>
    /// Текущий статус задачи.
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// Завершена ли задача.
    /// </summary>
    public bool IsCompleted { get; set; }
}
