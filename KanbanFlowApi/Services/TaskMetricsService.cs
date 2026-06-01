using KanbanFlowApi.Dtos.Task;
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
    public List<TaskMetricsDto> CalculateAllTasksMetrics()
    {
        return _simulation.Board.Tasks
            .Select(t => CalculateTaskMetrics(t))
            .ToList();
    }

    /// <summary>
    /// Рассчитать метрики для конкретной задачи.
    /// </summary>
    public TaskMetricsDto CalculateTaskMetrics(BoardTask boardTask)
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

        // 2. Flow Efficiency: Active Time / (Active Time + Wait Time)
        var (activeTime, waitTime) = CalculateFlowEfficiencyTimes(allActivities);
        var totalTime = activeTime + waitTime;
        var efficiencyPercent = totalTime > 0 ? (activeTime / totalTime) * 100 : 0m;

        // 3. Статус задачи
        var status = GetTaskStatus(allActivities);

        // 4. Детальная информация по стадиям
        var stages = CalculateStageMetrics(allActivities);

        return new TaskMetricsDto
        {
            TaskKey = taskKey,
            Summary = boardTask.Task.Summary ?? taskKey,
            ShirtType = boardTask.Task.ShirtType.ToString(),
            LeadTimeDays = Math.Round(leadTime, 1),
            FlowEfficiencyPercent = Math.Round(efficiencyPercent, 1),
            ActiveTimeDays = Math.Round(activeTime, 1),
            WaitTimeDays = Math.Round(waitTime, 1),
            Status = status,
            Stages = stages
        };
    }

    /// <summary>
    /// Рассчитать метрики по стадиям.
    /// </summary>
    private List<StageMetricsDto> CalculateStageMetrics(List<HistoryActivity> activities)
    {
        var stages = new List<StageMetricsDto>();

        // Получаем все переходы по стадиям в хронологическом порядке
        var movements = activities
            .Where(a => a.Type == ActivityType.TaskMoved && a.StageName != null)
            .OrderBy(a => a.DayNumber)
            .ToList();

        if (movements.Count == 0)
            return stages;

        // Проходим по всем переходам и считаем время в каждой стадии
        for (var i = 0; i < movements.Count; i++)
        {
            var currentMove = movements[i];
            var stageName = currentMove.StageName;
            var enterDay = currentMove.DayNumber;

            // Выход из стадии - следующий переход или текущий день
            var exitDay = (i < movements.Count - 1)
                ? movements[i + 1].DayNumber
                : _simulation.CurrentDay;

            var timeInStage = exitDay - enterDay;

            // Определить тип стадии
            var stageType = GetStageTypeByName(stageName);

            // Найти воркеров на этой стадии
            var workers = activities
                .Where(a => a.StageName == stageName &&
                           (a.Type == ActivityType.WorkerTookTask || a.Type == ActivityType.WorkerCompletedTask) &&
                           !string.IsNullOrEmpty(a.WorkerLogin))
                .Select(a => a.WorkerLogin!)
                .Distinct()
                .ToList();

            stages.Add(new StageMetricsDto
            {
                StageName = stageName,
                StageType = stageType.ToString(),
                TimeInStageDays = timeInStage,
                Workers = workers
            });
        }

        return stages;
    }

    /// <summary>
    /// Определить тип стадии по имени.
    /// </summary>
    private StageType GetStageTypeByName(string stageName)
    {
        var stage = _simulation.Board.Stages
            .FirstOrDefault(s => s.Stage.Name == stageName);

        return stage?.Stage.Type ?? StageType.Buffer;
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
    /// Рассчитать активное время и время ожидания для задачи.
    /// Active = время в Work стадиях (от входа до следующего перехода)
    /// Wait = время в Buffer стадиях (от входа до следующего перехода)
    /// Стадия Done исключается из расчёта — задача завершена, время не считается.
    /// </summary>
    private (decimal ActiveTime, decimal WaitTime) CalculateFlowEfficiencyTimes(
        List<HistoryActivity> activities)
    {
        var activeTime = 0m;
        var waitTime = 0m;

        // Получаем все переходы по стадиям в хронологическом порядке
        var movements = activities
            .Where(a => a.Type == ActivityType.TaskMoved && a.StageName != null)
            .OrderBy(a => a.DayNumber)
            .ToList();

        if (movements.Count == 0)
            return (0, 0);

        // Проверяем, достигла ли задача Done
        var doneMove = movements.FirstOrDefault(m => m.StageName == "Done");
        var isCompleted = doneMove != null;

        // Если задача завершена, считаем только до перехода в Done (не включая Done)
        var movementsToProcess = isCompleted
            ? movements.TakeWhile(m => m.StageName != "Done").ToList()
            : movements;

        if (movementsToProcess.Count == 0)
            return (0, 0);

        // Проходим по всем переходам и считаем время в каждой стадии
        for (var i = 0; i < movementsToProcess.Count; i++)
        {
            var currentMove = movementsToProcess[i];
            var stageName = currentMove.StageName;
            var enterDay = currentMove.DayNumber;

            // Выход из стадии - следующий переход или текущий день
            var exitDay = (i < movementsToProcess.Count - 1)
                ? movementsToProcess[i + 1].DayNumber
                : _simulation.CurrentDay;

            var timeInStage = exitDay - enterDay;

            // Определяем тип стадии
            var stageType = GetStageTypeByName(stageName);

            if (stageType == StageType.Work)
            {
                activeTime += timeInStage;
            }
            else if (stageType == StageType.Buffer)
            {
                waitTime += timeInStage;
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
