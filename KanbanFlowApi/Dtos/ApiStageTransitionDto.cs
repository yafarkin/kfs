namespace KanbanFlowApi.Dtos;

/// <summary>
/// Переход из стадии в другую стадию.
/// </summary>
public sealed record ApiStageTransitionDto
{
    /// <summary>
    /// Имя целевой стадии.
    /// </summary>
    public string TargetStageName { get; set; } = null!;

    /// <summary>
    /// Вероятность перехода (0.0 - 1.0).
    /// </summary>
    public double Probability { get; set; }

    /// <summary>
    /// Краткое представление для отладки.
    /// </summary>
    public override string ToString() => $"{TargetStageName} ({Probability:P0})";
}
