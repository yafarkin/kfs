namespace KanbanFlowSerivce.Dtos.Config;

/// <summary>
/// Воркфлоу — определяет структуру процесса: стадии и переходы между ними.
/// </summary>
public sealed record Workflow
{
    /// <summary>
    /// Список стадий воркфлоу.
    /// </summary>
    public List<Stage> Stages { get; set; } = new();
}