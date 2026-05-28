namespace KanbanFlowApi.Dtos.Metrics;

/// <summary>
/// DTO для всех рассчитанных метрик симуляции.
/// </summary>
public sealed record ApiMetricsDto
{
    /// <summary>
    /// Метрики Lead Time (p50, p85).
    /// </summary>
    public ApiLeadTimeMetricsDto LeadTime { get; set; } = new();

    /// <summary>
    /// Метрики Throughput (пропускная способность).
    /// </summary>
    public ApiThroughputMetricsDto Throughput { get; set; } = new();

    /// <summary>
    /// Метрики Flow Efficiency.
    /// </summary>
    public ApiFlowEfficiencyMetricsDto FlowEfficiency { get; set; } = new();

    /// <summary>
    /// Частотная метрика (распределение задач по времени).
    /// </summary>
    public ApiFrequencyMetricsDto Frequency { get; set; } = new();
}
