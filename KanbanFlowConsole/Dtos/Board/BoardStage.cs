namespace KanbanFlowConsole.Dtos;

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
    ///     Текущее количество задач на стадии (WIP)
    /// </summary>
    public int WipCount => Tasks.Count;

    /// <summary>
    ///     Превышает ли стадия свой WIP лимит
    /// </summary>
    public bool IsWipExceeded => Stage.WipLimit.HasValue && WipCount > Stage.WipLimit.Value;

    /// <summary>
    ///     Доступна ли стадия для приёма новых задач (с учётом WIP лимита)
    /// </summary>
    public bool CanAcceptTasks => !Stage.WipLimit.HasValue || WipCount < Stage.WipLimit.Value;
}