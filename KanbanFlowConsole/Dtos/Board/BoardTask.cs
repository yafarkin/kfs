using KanbanFlowConsole.Dtos.History;
using Task = KanbanFlowConsole.Dtos.Config.Task;

namespace KanbanFlowConsole.Dtos.Board;

public sealed record BoardTask
{
    public Task Task { get; set; } = null!;

    public decimal Progress { get; set; }

    /// <summary>
    ///     Воркер, назначенный на задачу
    /// </summary>
    public BoardWorker? Worker { get; set; }

    /// <summary>
    ///     История всех переходов задачи (для расчёта метрик)
    /// </summary>
    public List<TaskTransitionHistory> TransitionHistory { get; set; } = new();

    /// <summary>
    ///     Текущая стадия задачи
    /// </summary>
    public BoardStage? CurrentStage { get; set; }
}