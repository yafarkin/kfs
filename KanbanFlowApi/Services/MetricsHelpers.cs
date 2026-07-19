using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.History;

namespace KanbanFlowApi.Services;

/// <summary>
/// Общие хелперы для расчёта метрик.
/// </summary>
public static class MetricsHelpers
{
    /// <summary>
    /// Извлечь ключ задачи из активности.
    /// Возвращает null, если TaskKey не заполнен — regex-фоллбек не используется.
    /// </summary>
    public static string? GetTaskKeyFromActivity(HistoryActivity activity)
    {
        return activity.TaskKey;
    }

    /// <summary>
    /// Получить имена финальных стадий (стоков) — стадии без исходящих переходов.
    /// Задача считается завершённой при входе в любую из этих стадий.
    /// </summary>
    public static HashSet<string> GetFinalStageNames(Simulation simulation)
    {
        return simulation.Board.Stages
            .Where(s => s.NextStages.Count == 0)
            .Select(s => s.Stage.Name)
            .ToHashSet();
    }

    /// <summary>
    /// Рассчитать Lead Time задачи от события LeadTimeStarted.
    /// Если события нет — возвращает null (задача ещё не вошла в измеряемую зону).
    /// Не фоллбечится на первый TaskMoved.
    /// </summary>
    public static decimal? CalculateLeadTimeFromStartEvent(
        List<HistoryActivity> activities,
        HashSet<string> finalStageNames,
        int currentDay)
    {
        if (activities.Count == 0)
            return null;

        // Найти начало Lead Time — только событие LeadTimeStarted
        var leadTimeStartEvent = activities
            .FirstOrDefault(a => a.Type == ActivityType.LeadTimeStarted);

        if (leadTimeStartEvent == null)
        {
            // Задача ещё не вошла в измеряемую зону — не фоллбечимся на TaskMoved
            return null;
        }

        var startDay = leadTimeStartEvent.DayNumber;

        // Найти конец — вход в любую финальную стадию
        var endActivity = activities
            .FirstOrDefault(a => a.Type == ActivityType.TaskMoved && a.StageName != null && finalStageNames.Contains(a.StageName));

        var endDay = endActivity?.DayNumber ?? currentDay;

        return (endDay - startDay);
    }
}
