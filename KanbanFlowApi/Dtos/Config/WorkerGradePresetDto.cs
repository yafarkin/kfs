namespace KanbanFlowApi.Dtos.Config;

/// <summary>
/// DTO для пресета «грейда» воркера — готовый набор Performance/Deviation/CostPerDay
/// для конкретной роли (backend/frontend/qa) и уровня (стажёр..лид).
/// Используется фронтом только для одноразового заполнения полей воркера (quick-fill) —
/// после применения значения становятся обычными редактируемыми числами.
/// </summary>
public sealed record WorkerGradePresetDto : PresetDto
{
    /// <summary>
    /// Роль, для которой подобраны параметры. Например: "backend", "frontend", "qa".
    /// </summary>
    public string Role { get; set; } = null!;

    /// <summary>
    /// Уровень грейда. Например: "intern", "junior", "middle", "senior", "lead".
    /// </summary>
    public string Grade { get; set; } = null!;

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
    public int CostPerDay { get; set; }
}
