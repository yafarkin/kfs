namespace KanbanFlowApi.Dtos;

/// <summary>
/// DTO для доски (полное состояние всех задач, стадий и исполнителей).
/// </summary>
public sealed record ApiBoardDto
{
    /// <summary>
    /// Список стадий доски.
    /// </summary>
    public List<ApiBoardStageDto> Stages { get; set; } = new();

    /// <summary>
    /// Список исполнителей (воркеров).
    /// </summary>
    public List<ApiBoardWorkerDto> Workers { get; set; } = new();

    /// <summary>
    /// Список задач на доске.
    /// </summary>
    public List<ApiBoardTaskDto> Tasks { get; set; } = new();

    /// <summary>
    /// Краткое представление для отладки.
    /// </summary>
    public override string ToString() => $"Board ({Stages.Count} stages, {Workers.Count} workers, {Tasks.Count} tasks)";
}
