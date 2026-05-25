namespace KanbanFlowApi.Dtos.Config;

/// <summary>
/// DTO для workflow (набор стадий и переходов).
/// </summary>
public sealed record ApiWorkflowDto
{
    /// <summary>
    /// Список стадий workflow.
    /// </summary>
    public List<ApiStageDto> Stages { get; set; } = new();

    /// <summary>
    /// Краткое представление для отладки.
    /// </summary>
    public override string ToString() => $"Workflow ({Stages.Count} stages)";
}
