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
    /// Навыки, необходимые для выполнения задачи.
    /// Например: ["backend"], ["frontend", "react"], ["qa-manual"].
    /// Задача подходит стадии, если есть хотя бы один общий навык.
    /// </summary>
    public List<string> RequiredSkills { get; set; } = new();

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
