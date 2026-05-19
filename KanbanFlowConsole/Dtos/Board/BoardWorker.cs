namespace KanbanFlowConsole.Dtos;

public sealed record BoardWorker
{
    public Worker Worker { get; set; } = null!;

    /// <summary>
    ///     Текущие задачи воркера с указанием стадий
    /// </summary>
    public List<BoardTaskAssignment> Assignments { get; set; } = new();

    /// <summary>
    ///     Текущее количество задач в работе (WIP)
    /// </summary>
    public int WipCount => Assignments.Count;

    /// <summary>
    ///     Доступен ли воркер для взятия новых задач (с учётом WIP лимита)
    /// </summary>
    public bool IsAvailable => !Worker.WipLimit.HasValue || WipCount < Worker.WipLimit.Value;
}

/// <summary>
///     Назначение задачи воркеру (связь задачи и стадии)
/// </summary>
public sealed record BoardTaskAssignment
{
    /// <summary>
    ///     Задача
    /// </summary>
    public BoardTask Task { get; set; } = null!;

    /// <summary>
    ///     Стадия, на которой воркер работает над задачей
    /// </summary>
    public BoardStage Stage { get; set; } = null!;
}