namespace KanbanFlowSerivce.Dtos.Config;

/// <summary>
/// Переход из стадии в другую стадию в DAG (Directed Acyclic Graph).
/// Определяет, куда может перейти задача после завершения текущей стадии.
/// </summary>
public sealed record StageTransition
{
    /// <summary>
    ///     Целевая стадия перехода (прямая ссылка для DAG).
    /// </summary>
    public Stage Stage { get; set; } = null!;

    /// <summary>
    ///     Вероятность перехода (0.0 - 1.0).
    ///     Для детерминированных переходов используется 1.0.
    /// </summary>
    public double Probability { get; set; }
}
