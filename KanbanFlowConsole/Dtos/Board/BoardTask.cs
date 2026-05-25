using KanbanFlowConsole.Dtos.History;
using Task = KanbanFlowConsole.Dtos.Config.Task;

namespace KanbanFlowConsole.Dtos.Board;

/// <summary>
/// Задача на доске симуляции — представляет рабочую единицу с прогрессом, назначенным воркером и историей переходов.
/// Отслеживает текущую стадию и прогресс выполнения (0-100%).
/// </summary>
public sealed record BoardTask
{
    /// <summary>
    /// Конфигурация задачи (ключ, описание, размер, роль).
    /// </summary>
    public Task Task { get; set; } = null!;

    /// <summary>
    /// Текущий прогресс выполнения задачи (0-100%).
    /// </summary>
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