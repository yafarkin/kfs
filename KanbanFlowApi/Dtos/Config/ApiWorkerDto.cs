namespace KanbanFlowApi.Dtos.Config;

/// <summary>
/// DTO для исполнителя (воркера).
/// </summary>
public sealed record ApiWorkerDto
{
    /// <summary>
    /// Логин исполнителя.
    /// </summary>
    public string Login { get; set; } = null!;

    /// <summary>
    /// Роль исполнителя (например, Backend Developer).
    /// Устаревшее поле, используется для обратной совместимости.
    /// </summary>
    [Obsolete("Используйте Skills вместо Role")]
    public string? Role { get; set; }

    /// <summary>
    /// Навыки исполнителя. Например: ["backend", "api"], ["frontend", "react"], ["qa-manual", "qa-auto"].
    /// </summary>
    public List<string> Skills { get; set; } = new();

    /// <summary>
    /// Персональный WIP-лимит (максимум задач одновременно). Null = без лимита.
    /// </summary>
    public int? WipLimit { get; set; }

    /// <summary>
    /// Производительность (100 = базовая, 150 = на 50% быстрее).
    /// </summary>
    public double Performance { get; set; }

    /// <summary>
    /// Краткое представление для отладки.
    /// </summary>
    public override string ToString() => $"{Login} ({string.Join(", ", Skills.Any() ? Skills : [Role ?? "no skills"])})";
}
