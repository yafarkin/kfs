using KanbanFlowSerivce.Dtos.Config;

namespace KanbanFlowSerivce.Dtos.Board;

/// <summary>
/// Стадия на доске симуляции — представляет конкретный этап workflow с задачами, WIP-лимитом и переходами.
/// Содержит ссылки на предыдущие и следующие стадии для навигации по workflow.
/// </summary>
public sealed record BoardStage
{
    /// <summary>
    /// Конфигурация стадии (имя, тип, WIP-лимит и т.д.).
    /// </summary>
    public Stage Stage { get; set; } = null!;

    /// <summary>
    /// Предыдущие стадии в workflow (откуда задачи могут приходить).
    /// </summary>
    public List<BoardStage> PrevStages { get; set; } = new();

    /// <summary>
    /// Следующие стадии в workflow (куда задачи могут переходить).
    /// </summary>
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

    /// <summary>
    ///     Требуется ли воркер, отличный от того, что работал в указанной стадии
    /// </summary>
    public bool RequiresDifferentResource => Stage.RequiresDifferentResource;

    /// <summary>
    ///     Стадия, откуда нельзя брать того же воркера (если задана)
    /// </summary>
    public BoardStage? ExcludedStage { get; set; }
}