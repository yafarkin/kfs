namespace KanbanFlowApi.Dtos.Metrics;

/// <summary>
/// DTO для метрик работника.
/// </summary>
public sealed record ApiWorkerMetricsDto
{
    /// <summary>
    /// Логин работника.
    /// </summary>
    public string Login { get; set; } = null!;

    /// <summary>
    /// Throughput — количество задач с ценными стадиями, завершённых за период / количество дней.
    /// </summary>
    public decimal Throughput { get; set; }

    /// <summary>
    /// Lead Time — среднее время задач (от isLeadTimeStart до Done/сейчас), где работник участвовал в ценной стадии.
    /// </summary>
    public decimal LeadTime { get; set; }

    /// <summary>
    /// Количество задач с ценными стадиями, где работник участвовал.
    /// </summary>
    public int ValuableTasksCount { get; set; }

    /// <summary>
    /// Flow Efficiency — процент активного времени (Work стадии) от общего.
    /// </summary>
    public decimal EfficiencyPercent { get; set; }

    /// <summary>
    /// Активное время (на Work стадиях) в днях.
    /// </summary>
    public decimal WorkTimeDays { get; set; }

    /// <summary>
    /// Время ожидания (на Buffer стадиях) в днях.
    /// </summary>
    public decimal BufferTimeDays { get; set; }
}
