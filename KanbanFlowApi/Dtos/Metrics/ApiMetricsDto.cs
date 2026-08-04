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

    /// <summary>
    /// Общая стоимость проекта (сумма по всем воркерам).
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// Стоимость полезной работы (сумма по всем воркерам).
    /// </summary>
    public decimal WorkCost { get; set; }

    /// <summary>
    /// Стоимость простоя (сумма по всем воркерам).
    /// </summary>
    public decimal BufferCost { get; set; }
}
