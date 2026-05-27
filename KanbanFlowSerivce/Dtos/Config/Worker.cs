namespace KanbanFlowSerivce.Dtos.Config;

/// <summary>
/// Воркер (исполнитель) в конфигурации симуляции — представляет участника workflow с ролью и производительностью.
/// </summary>
public sealed record Worker
{
    /// <summary>
    /// Уникальный логин воркера.
    /// </summary>
    public string Login { get; set; } = null!;

    /// <summary>
    /// Навыки воркера. Например: ["backend", "api"], ["frontend", "react"], ["qa-manual", "qa-auto"].
    /// Воркер может брать задачи, требующие эти навыки.
    /// </summary>
    public List<string> Skills { get; set; } = new();

    /// <summary>
    /// Персональный WIP-лимит (максимум задач одновременно).
    /// Null = без лимита.
    /// </summary>
    public int? WipLimit { get; set; }

    /// <summary>
    ///     Производительность ресурса в процентах (100 = стандартная скорость).
    ///     Значения > 100 означают повышенную производительность.
    /// </summary>
    public double Performance { get; set; }
}