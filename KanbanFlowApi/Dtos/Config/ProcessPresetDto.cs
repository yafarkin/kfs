namespace KanbanFlowApi.Dtos.Config;

/// <summary>
/// DTO для пресета производственного процесса — содержит workflow и задачи по умолчанию.
/// </summary>
public sealed record ProcessPresetDto : PresetDto
{
    /// <summary>
    /// Воркфлоу (стадии и переходы).
    /// </summary>
    public ApiWorkflowDto Workflow { get; set; } = null!;

    /// <summary>
    /// Задачи по умолчанию для этого процесса.
    /// </summary>
    public List<ApiTaskDto> Tasks { get; set; } = new();
}
