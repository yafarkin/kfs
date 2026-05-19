using KanbanFlowConsole.Dtos.Config;

namespace KanbanFlowConsole.Dtos.Board;

public sealed record BoardStage
{
    public Stage Stage { get; set; } = null!;

    public List<BoardStage> PrevStages { get; set; } = new();
    public List<BoardStage> NextStages { get; set; } = new();

    /// <summary>
    ///     Задачи, находящиеся на этой стадии (для WIP лимитов)
    /// </summary>
    public List<BoardTask> Tasks { get; set; } = new();

    /// <summary>
    ///     WIP лимит стадии (берётся из конфигурации)
    /// </summary>
    public int? WipLimit => Stage.WipLimit;

    /// <summary>
    ///     Текущее количество задач на стадии (WIP)
    /// </summary>
    public int WipCount => Tasks.Count;

    /// <summary>
    ///     Превышает ли стадия свой WIP лимит
    /// </summary>
    public bool IsWipExceeded => WipLimit.HasValue && WipCount > WipLimit.Value;

    /// <summary>
    ///     Доступна ли стадия для приёма новых задач (с учётом WIP лимита)
    /// </summary>
    public bool CanAcceptTasks => !WipLimit.HasValue || WipCount < WipLimit.Value;
}