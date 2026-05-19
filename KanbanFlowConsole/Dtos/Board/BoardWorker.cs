using KanbanFlowConsole.Dtos.Config;

namespace KanbanFlowConsole.Dtos.Board;

public sealed record BoardWorker
{
    public Worker Worker { get; set; } = null!;

    /// <summary>
    ///     WIP лимит воркера (берётся из конфигурации)
    /// </summary>
    public int? WipLimit => Worker.WipLimit;

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
    public bool IsAvailable => !WipLimit.HasValue || WipCount < WipLimit.Value;
}