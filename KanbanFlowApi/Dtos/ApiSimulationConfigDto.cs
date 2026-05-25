namespace KanbanFlowApi.Dtos;

/// <summary>
/// DTO для конфигурации симуляции (без циклических ссылок).
/// Используется для описания параметров симуляции: воркфлоу, воркеры, задачи.
/// </summary>
public sealed record ApiSimulationConfigDto
{
    /// <summary>
    /// Seed для генератора случайных чисел (воспроизводимость симуляции).
    /// </summary>
    public long Seed { get; set; }

    /// <summary>
    /// Список исполнителей (воркеров).
    /// </summary>
    public List<ApiWorkerDto> Workers { get; set; } = new();

    /// <summary>
    /// Воркфлоу (стадии и переходы).
    /// </summary>
    public ApiWorkflowDto Workflow { get; set; } = null!;

    /// <summary>
    /// Список задач для симуляции.
    /// </summary>
    public List<ApiTaskDto> Tasks { get; set; } = new();

    /// <summary>
    /// Краткое представление для отладки.
    /// </summary>
    public override string ToString() => $"Config (Seed={Seed}, {Workers.Count} workers, {Tasks.Count} tasks)";
}
