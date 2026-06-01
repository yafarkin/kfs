namespace KanbanFlowApi.Dtos.Task;

/// <summary>
/// DTO для агрегированных метрик стадий (P50, P85) по всем задачам.
/// </summary>
public sealed record StageMetricsAggregatedDto
{
    /// <summary>
    /// Название стадии.
    /// </summary>
    public string StageName { get; set; } = null!;

    /// <summary>
    /// Тип стадии (Work, Buffer).
    /// </summary>
    public string StageType { get; set; } = null!;

    /// <summary>
    /// P50 (медиана) времени в стадии в днях.
    /// </summary>
    public decimal P50Days { get; set; }

    /// <summary>
    /// P85 времени в стадии в днях.
    /// </summary>
    public decimal P85Days { get; set; }

    /// <summary>
    /// P95 времени в стадии в днях.
    /// </summary>
    public decimal P95Days { get; set; }

    /// <summary>
    /// Среднее время в стадии в днях.
    /// </summary>
    public decimal AvgDays { get; set; }

    /// <summary>
    /// Максимальное время в стадии в днях.
    /// </summary>
    public decimal MaxDays { get; set; }

    /// <summary>
    /// Количество задач, прошедших через стадию.
    /// </summary>
    public int TaskCount { get; set; }
}
