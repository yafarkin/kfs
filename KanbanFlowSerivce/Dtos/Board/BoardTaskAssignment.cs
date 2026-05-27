namespace KanbanFlowSerivce.Dtos.Board;

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
