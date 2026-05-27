namespace KanbanFlowSerivce.Dtos.Config;

/// <summary>
/// Конфигурация симуляции — содержит все параметры для запуска: воркфлоу, воркеров, задачи и seed.
/// </summary>
public sealed record SimulationConfig
{
    /// <summary>
    /// Seed для генератора случайных чисел (обеспечивает воспроизводимость симуляции).
    /// </summary>
    public long Seed { get; set; }

    /// <summary>
    /// Список воркеров (исполнителей), участвующих в симуляции.
    /// </summary>
    public List<Worker> Workers { get; set; } = [];

    /// <summary>
    /// Воркфлоу — набор стадий и переходов между ними.
    /// </summary>
    public Workflow Workflow { get; set; } = null!;

    /// <summary>
    /// Список задач для симуляции.
    /// </summary>
    public List<Task> Tasks { get; set; } = [];
}