using KanbanFlowConsole.Enums;

namespace KanbanFlowApi.Dtos;

/// <summary>
/// DTO для задачи на доске (состояние задачи в симуляции).
/// </summary>
public sealed record ApiBoardTaskDto
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
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// Прогресс выполнения (0-100).
    /// </summary>
    public decimal Progress { get; set; }

    /// <summary>
    /// Логин исполнителя, работающего над задачей.
    /// </summary>
    public string? WorkerLogin { get; set; }

    /// <summary>
    /// Имя текущей стадии, где находится задача.
    /// </summary>
    public string? CurrentStageName { get; set; }

    /// <summary>
    /// Краткое представление для отладки.
    /// </summary>
    public override string ToString() => $"{Key}: {CurrentStageName ?? "unassigned"} ({Progress}%)";
}
