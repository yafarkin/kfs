namespace KanbanFlowApi.Dtos.Board;

/// <summary>
/// DTO для воркера на доске (состояние исполнителя в симуляции).
/// </summary>
public sealed record ApiBoardWorkerDto
{
    /// <summary>
    /// Логин исполнителя.
    /// </summary>
    public string Login { get; set; } = null!;

    /// <summary>
    /// Навыки исполнителя.
    /// </summary>
    public List<string> Skills { get; set; } = new();

    /// <summary>
    /// Персональный WIP-лимит (максимум задач одновременно). Null = без лимита.
    /// </summary>
    public int? WipLimit { get; set; }

    /// <summary>
    /// Текущее количество задач у исполнителя.
    /// </summary>
    public int WipCount { get; set; }

    /// <summary>
    /// Доступен ли исполнитель для взятия новых задач.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Детали назначений задач (с DaysRequired/DaysWorked) для сериализации состояния.
    /// </summary>
    public List<ApiTaskAssignmentDto> AssignedAssignments { get; set; } = new();

    /// <summary>
    /// Краткое представление для отладки.
    /// </summary>
    public override string ToString() => $"{Login} ({WipCount}/{(WipLimit.HasValue ? WipLimit.Value.ToString() : "∞")} tasks)";
}

/// <summary>
/// DTO для назначения задачи воркеру.
/// </summary>
public sealed record ApiTaskAssignmentDto
{
    /// <summary>
    /// Ключ задачи.
    /// </summary>
    public string TaskKey { get; set; } = null!;

    /// <summary>
    /// Имя стадии, на которой воркер работает над задачей.
    /// </summary>
    public string StageName { get; set; } = null!;

    /// <summary>
    /// Сколько дней требуется для выполнения задачи.
    /// </summary>
    public decimal DaysRequired { get; set; }

    /// <summary>
    /// Сколько дней уже отработано.
    /// </summary>
    public decimal DaysWorked { get; set; }
}
