namespace KanbanFlowApi.Dtos;

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
    /// Роль исполнителя (например, Backend Developer).
    /// </summary>
    public string? Role { get; set; }

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
    /// Ключи задач, назначенных исполнителю.
    /// </summary>
    public List<string> AssignedTaskKeys { get; set; } = new();

    /// <summary>
    /// Краткое представление для отладки.
    /// </summary>
    public override string ToString() => $"{Login} ({WipCount}/{(WipLimit.HasValue ? WipLimit.Value.ToString() : "∞")} tasks)";
}
