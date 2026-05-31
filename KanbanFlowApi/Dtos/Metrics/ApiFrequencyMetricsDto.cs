namespace KanbanFlowApi.Dtos.Metrics;

/// <summary>
/// DTO для частотной метрики (распределение задач по времени выполнения).
/// </summary>
public sealed record ApiFrequencyMetricsDto
{
    /// <summary>
    /// Распределение задач по диапазонам времени (в днях).
    /// Ключ - диапазон времени (например, "0-7", "7-14"), значение - количество задач.
    /// </summary>
    public Dictionary<string, int> Distribution { get; set; } = new();

    /// <summary>
    /// Общее количество задач.
    /// </summary>
    public int TaskCount { get; set; }
}
