namespace KanbanFlowConsole.Dtos.Config;

/// <summary>
///     Переход к следующей стадии в DAG
/// </summary>
public sealed record StageTransition
{
    /// <summary>
    ///     Целевая стадия (прямая ссылка для DAG)
    /// </summary>
    public Stage Stage { get; set; } = null!;

    /// <summary>
    ///     Вероятность перехода (0.0 - 1.0)
    /// </summary>
    public double Probability { get; set; }
}
