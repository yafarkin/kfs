using KanbanFlowConsole.Enums;

namespace KanbanFlowApi.Dtos.Config;

/// <summary>
/// DTO для задачи (тип, описание).
/// </summary>
public sealed record ApiTaskDto
{
    /// <summary>
    /// Уникальный ключ задачи (например, TASK-1).
    /// </summary>
    public string Key { get; set; } = null!;

    /// <summary>
    /// Краткое описание задачи.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Размер задачи (TShirt: XS, S, M, L, XL).
    /// </summary>
    public TShirtType? ShirtType { get; set; }

    /// <summary>
    /// Роль, необходимая для выполнения задачи.
    /// Устаревшее поле, используется для обратной совместимости.
    /// </summary>
    [Obsolete("Используйте RequiredSkills вместо Role")]
    public string? Role { get; set; }

    /// <summary>
    /// Навыки, необходимые для выполнения задачи на стадии производства.
    /// Например: ["backend", "api"], ["frontend", "react"], ["qa-manual"].
    /// </summary>
    public List<string> RequiredSkills { get; set; } = new();

    /// <summary>
    /// Навыки, необходимые для выполнения задачи на конкретных стадиях.
    /// Ключ: имя стадии, Значение: список требуемых навыков.
    /// Например: { ["Testing"] = ["qa-manual"], ["Automation"] = ["qa-auto"] }.
    /// </summary>
    public Dictionary<string, List<string>> RequiredSkillsPerStage { get; set; } = new();

    /// <summary>
    /// Дочерние задачи (для иерархических структур).
    /// </summary>
    public List<ApiTaskDto>? Children { get; set; }

    /// <summary>
    /// Предпочтительные исполнители для стадий (ключ: имя стадии, значение: логин воркера).
    /// </summary>
    public Dictionary<string, string>? AcceptableWorkers { get; set; }

    /// <summary>
    /// Краткое представление для отладки.
    /// </summary>
    public override string ToString() => $"{Key}: {Summary ?? "no summary"}";
}
