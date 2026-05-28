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
    ///     Указывает к какой оценке в майке склонятся: 0% - верхняя граница, 100% - нижняя.
    /// </summary>
    public double Performance { get; set; }

    /// <summary>
    ///     Отклонение вниз от оценки в процентах (0-100).
    ///     Например, 30 означает что задача может быть выполнена на 30% быстрее базовой оценки.
    /// </summary>
    public double DeviationDownPercent { get; set; }

    /// <summary>
    ///     Отклонение вверх от оценки в процентах (0-100).
    ///     Например, 50 означает что задача может выполняться на 50% дольше базовой оценки.
    /// </summary>
    public double DeviationUpPercent { get; set; }
}