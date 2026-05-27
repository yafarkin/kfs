namespace KanbanFlowSerivce.Dtos.History;

/// <summary>
///     История действий за один день симуляции
/// </summary>
public sealed record HistoryDay
{
    /// <summary>
    ///     Номер дня симуляции (начиная с 1)
    /// </summary>
    public int DayNumber { get; set; }

    /// <summary>
    ///     Список действий за этот день
    /// </summary>
    public List<HistoryActivity> Activities { get; set; } = new();

    /// <summary>
    ///     Добавить действие в историю дня
    /// </summary>
    public void AddActivity(HistoryActivity activity)
    {
        activity.Day = this;
        Activities.Add(activity);
    }

    /// <summary>
    ///     Количество действий за день
    /// </summary>
    public int ActivityCount => Activities.Count;
}
