namespace KanbanFlowApi.Dtos.Config;

/// <summary>
/// DTO для запроса на запуск симуляции с полной конфигурацией.
/// Backend stateless — конфигурация передаётся полностью с клиента.
/// </summary>
public sealed record StartSimulationRequestDto
{
    /// <summary>
    /// Seed для генератора случайных чисел (воспроизводимость симуляции).
    /// </summary>
    public long Seed { get; set; }

    /// <summary>
    /// Использовать ли вариативность при расчёте времени выполнения задач.
    /// </summary>
    public bool UseVariability { get; set; } = true;

    /// <summary>
    /// Воркфлоу (стадии и переходы).
    /// </summary>
    public ApiWorkflowDto Workflow { get; set; } = null!;

    /// <summary>
    /// Список исполнителей (воркеров).
    /// </summary>
    public List<ApiWorkerDto> Workers { get; set; } = new();

    /// <summary>
    /// Список задач для симуляции.
    /// </summary>
    public List<ApiTaskDto> Tasks { get; set; } = new();

    /// <summary>
    /// Количество дней для симуляции (опционально).
    /// Если null - симуляция выполняется на 1 день.
    /// Если 0 - симуляция выполняется до завершения всех задач.
    /// Если > 0 - симуляция выполняется на указанное количество дней.
    /// </summary>
    public int? DaysToSimulate { get; set; }
}
