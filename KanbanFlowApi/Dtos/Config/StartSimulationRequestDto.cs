namespace KanbanFlowApi.Dtos.Config;

/// <summary>
/// DTO для запроса на запуск симуляции из комбинации пресетов.
/// </summary>
public sealed record StartSimulationRequestDto
{
    /// <summary>
    /// Имя пресета производственного процесса (обязательно).
    /// </summary>
    public string ProcessPresetName { get; set; } = null!;

    /// <summary>
    /// Имя пресета пула работников (обязательно).
    /// </summary>
    public string WorkerPoolPresetName { get; set; } = null!;

    /// <summary>
    /// Имя пресета задач (опционально). Если не указан, используются задачи из процесса.
    /// </summary>
    public string? TaskPresetName { get; set; }

    /// <summary>
    /// Seed для генератора случайных чисел (воспроизводимость симуляции).
    /// </summary>
    public long Seed { get; set; }

    /// <summary>
    /// Использовать ли вариативность при расчёте времени выполнения задач.
    /// </summary>
    public bool UseVariability { get; set; } = true;
}
