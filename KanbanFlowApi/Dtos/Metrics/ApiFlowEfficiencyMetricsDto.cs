namespace KanbanFlowApi.Dtos.Metrics;

/// <summary>
/// DTO для метрик Flow Efficiency.
/// </summary>
public sealed record ApiFlowEfficiencyMetricsDto
{
    /// <summary>
    /// Общее время в рабочих статусах (в днях).
    /// </summary>
    public decimal ActiveTime { get; set; }

    /// <summary>
    /// Общее время в нерабочих статусах (ожидание, буферы) в днях.
    /// </summary>
    public decimal WaitTime { get; set; }

    /// <summary>
    /// Процент активного времени (ActiveTime / (ActiveTime + WaitTime) * 100).
    /// </summary>
    public decimal EfficiencyPercent { get; set; }
}
