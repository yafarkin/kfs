using KanbanFlowSerivce.Dtos.Board;

namespace KanbanFlowSerivce.Dtos.History;

/// <summary>
///     Запись о переходе задачи в истории
/// </summary>
public sealed record TaskTransitionHistory
{
    /// <summary>
    ///     Событие истории, связанное с переходом
    /// </summary>
    public HistoryActivity Activity { get; set; } = null!;

    /// <summary>
    ///     Исходная стадия (может быть null для первой стадии)
    /// </summary>
    public BoardStage? FromStage { get; set; }

    /// <summary>
    ///     Целевая стадия
    /// </summary>
    public BoardStage ToStage { get; set; } = null!;

    /// <summary>
    ///     День симуляции, когда произошёл переход
    /// </summary>
    public int Day { get; set; }
}
