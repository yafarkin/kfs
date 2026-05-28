namespace KanbanFlowApi.Dtos.Metrics;

/// <summary>
/// DTO для метрик Lead Time.
/// </summary>
public sealed record ApiLeadTimeMetricsDto
{
    /// <summary>
    /// 50-й перцентиль Lead Time (медиана) в часах.
    /// </summary>
    public decimal P50 { get; set; }

    /// <summary>
    /// 85-й перцентиль Lead Time в часах.
    /// </summary>
    public decimal P85 { get; set; }

    /// <summary>
    /// Общее количество задач, использованных для расчёта.
    /// </summary>
    public int TaskCount { get; set; }
}
