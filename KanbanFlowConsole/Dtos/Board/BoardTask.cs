using KanbanFlowConsole.Dtos.History;

namespace KanbanFlowConsole.Dtos;

public sealed record BoardTask
{
    public Task Task { get; set; } = null!;

    public decimal Progress { get; set; }

    public BoardWorker? Assignee { get; set; }

    /// <summary>
    ///     История всех переходов задачи (для расчёта метрик)
    /// </summary>
    public List<TaskTransitionHistory> TransitionHistory { get; set; } = new();

    /// <summary>
    ///     Текущая стадия задачи
    /// </summary>
    public BoardStage? CurrentStage { get; set; }
}

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