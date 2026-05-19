using KanbanFlowConsole.Dtos.Board;

namespace KanbanFlowConsole.Dtos.History;

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
    ///     Тик симуляции, когда произошёл переход
    /// </summary>
    public int Tick { get; set; }
}
