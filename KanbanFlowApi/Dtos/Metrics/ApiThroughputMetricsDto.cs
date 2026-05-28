namespace KanbanFlowApi.Dtos.Metrics;

/// <summary>
/// DTO для метрик Throughput (пропускная способность).
/// </summary>
public sealed record ApiThroughputMetricsDto
{
    /// <summary>
    /// Общая пропускная способность (задач в день).
    /// </summary>
    public decimal Overall { get; set; }

    /// <summary>
    /// История пропускной способности по дням.
    /// </summary>
    public List<ApiThroughputDayDto> DailyHistory { get; set; } = new();
}

/// <summary>
/// DTO для пропускной способности за один день.
/// </summary>
public sealed record ApiThroughputDayDto
{
    /// <summary>
    /// Номер дня.
    /// </summary>
    public int DayNumber { get; set; }

    /// <summary>
    /// Количество завершённых задач в этот день.
    /// </summary>
    public int CompletedTasksCount { get; set; }
}
