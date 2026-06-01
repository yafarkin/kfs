namespace KanbanFlowApi.Dtos.Task;

/// <summary>
/// DTO для метрик отдельной задачи.
/// </summary>
public sealed record TaskMetricsDto
{
    /// <summary>
    /// Ключ задачи (например, TASK-1).
    /// </summary>
    public string TaskKey { get; set; } = null!;

    /// <summary>
    /// Краткое описание задачи.
    /// </summary>
    public string Summary { get; set; } = null!;

    /// <summary>
    /// Размер задачи (S, M, L, XL).
    /// </summary>
    public string? ShirtType { get; set; }

    /// <summary>
    /// Lead Time задачи в днях (от первой стадии до Done).
    /// </summary>
    public decimal LeadTimeDays { get; set; }

    /// <summary>
    /// Flow Efficiency задачи в процентах.
    /// </summary>
    public decimal FlowEfficiencyPercent { get; set; }

    /// <summary>
    /// Активное время (работа) в днях.
    /// </summary>
    public decimal ActiveTimeDays { get; set; }

    /// <summary>
    /// Время ожидания (буфер) в днях.
    /// </summary>
    public decimal WaitTimeDays { get; set; }

    /// <summary>
    /// Статус задачи (In Progress, Done).
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// Детальная информация по стадиям.
    /// </summary>
    public List<StageMetricsDto> Stages { get; set; } = [];
}

/// <summary>
/// DTO для метрик стадии в рамках задачи.
/// </summary>
public sealed record StageMetricsDto
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
    /// Время проведённое в стадии в днях.
    /// </summary>
    public decimal TimeInStageDays { get; set; }

    /// <summary>
    /// Воркер(и), которые работали над задачей на этой стадии.
    /// </summary>
    public List<string> Workers { get; set; } = [];
}
