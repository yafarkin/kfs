namespace KanbanFlowSerivce.Dtos.Board;

/// <summary>
/// Доска симуляции — содержит все стадии, воркеров и задачи в текущий момент.
/// Используется для отслеживания состояния симуляции и применения правил WIP-лимитов.
/// </summary>
public sealed record Board
{
    /// <summary>
    /// Список стадий на доске.
    /// </summary>
    public List<BoardStage> Stages { get; set; } = new();

    /// <summary>
    /// Список воркеров (исполнителей) на доске.
    /// </summary>
    public List<BoardWorker> Workers { get; set; } = new();

    /// <summary>
    /// Список задач на доске.
    /// </summary>
    public List<BoardTask> Tasks { get; set; } = new();
}