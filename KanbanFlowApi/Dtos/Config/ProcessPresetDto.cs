namespace KanbanFlowApi.Dtos.Config;

/// <summary>
/// DTO для пресета производственного процесса — содержит workflow и задачи по умолчанию.
/// </summary>
public sealed record ProcessPresetDto
{
    /// <summary>
    /// Уникальное имя пресета (ключ для загрузки).
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Отображаемое название пресета.
    /// </summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// Описание пресета (количество стадий, тип процесса).
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// Воркфлоу (стадии и переходы).
    /// </summary>
    public ApiWorkflowDto Workflow { get; set; } = null!;

    /// <summary>
    /// Задачи по умолчанию для этого процесса.
    /// </summary>
    public List<ApiTaskDto> Tasks { get; set; } = new();

    /// <summary>
    /// Является ли этот пресет пресетом по умолчанию.
    /// </summary>
    public bool IsDefault { get; set; }
}
