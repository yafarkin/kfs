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
    /// Отклонение вниз в процентах (на сколько % может быть быстрее базовой оценки).
    /// </summary>
    public double DeviationDownPercent { get; set; }

    /// <summary>
    /// Отклонение вверх в процентах (на сколько % может быть медленнее базовой оценки).
    /// </summary>
    public double DeviationUpPercent { get; set; }

    /// <summary>
    /// Стоимость дня работы исполнителя (в условных единицах).
    /// </summary>
    public int CostPerDay { get; set; } = 100;

    /// <summary>
    /// Краткое представление для отладки.
    /// </summary>
    public override string ToString() => $"{Login} ({string.Join(", ", Skills)})";
}
