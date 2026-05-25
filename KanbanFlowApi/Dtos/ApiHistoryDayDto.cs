namespace KanbanFlowApi.Dtos;

/// <summary>
/// DTO для дня истории симуляции (содержит все события за день).
/// </summary>
public sealed record ApiHistoryDayDto
{
    /// <summary>
    /// Номер дня симуляции.
    /// </summary>
    public int DayNumber { get; set; }

    /// <summary>
    /// Список событий, произошедших в этот день.
    /// </summary>
    public List<ApiHistoryActivityDto> Activities { get; set; } = new();

    /// <summary>
    /// Краткое представление для отладки.
    /// </summary>
    public override string ToString() => $"Day {DayNumber} ({Activities.Count} activities)";
}
