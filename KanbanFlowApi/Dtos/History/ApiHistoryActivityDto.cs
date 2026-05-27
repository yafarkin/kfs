using KanbanFlowSerivce.Dtos.History;

namespace KanbanFlowApi.Dtos.History;

/// <summary>
/// DTO для события истории симуляции.
/// </summary>
public sealed record ApiHistoryActivityDto
{
    /// <summary>
    /// Тип события (TaskPulled, TaskMoved, WorkPerformed и т.д.).
    /// </summary>
    public ActivityType Type { get; set; }

    /// <summary>
    /// Тик симуляции, когда произошло событие.
    /// </summary>
    public int Tick { get; set; }

    /// <summary>
    /// Текстовое описание события.
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// Ключ задачи, связанной с событием.
    /// </summary>
    public string? TaskKey { get; set; }

    /// <summary>
    /// Логин исполнителя, связанного с событием.
    /// </summary>
    public string? WorkerLogin { get; set; }

    /// <summary>
    /// Имя стадии, связанной с событием.
    /// </summary>
    public string? StageName { get; set; }

    /// <summary>
    /// Прогресс задачи после события (если применимо).
    /// </summary>
    public decimal? Progress { get; set; }

    /// <summary>
    /// Краткое представление для отладки.
    /// </summary>
    public override string ToString() => $"{Type} @ tick {Tick}: {Description}";
}
