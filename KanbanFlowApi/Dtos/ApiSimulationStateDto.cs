namespace KanbanFlowApi.Dtos;

/// <summary>
/// DTO для полного состояния симуляции (конфигурация + доска + история + текущий день/тик).
/// Используется для итеративной симуляции: POST /api/simulation/simulate-day принимает и возвращает это состояние.
/// </summary>
public sealed record ApiSimulationStateDto
{
    /// <summary>
    /// Конфигурация симуляции (воркфлоу, воркеры, задачи).
    /// </summary>
    public ApiSimulationConfigDto Config { get; set; } = null!;

    /// <summary>
    /// Текущее состояние доски (задачи, стадии, исполнители).
    /// </summary>
    public ApiBoardDto Board { get; set; } = null!;

    /// <summary>
    /// История симуляции по дням.
    /// </summary>
    public List<ApiHistoryDayDto> History { get; set; } = new();

    /// <summary>
    /// Текущий день симуляции (0 = ещё не началась).
    /// </summary>
    public int CurrentDay { get; set; }

    /// <summary>
    /// Текущий тик симуляции (внутри дня).
    /// </summary>
    public int CurrentTick { get; set; }

    /// <summary>
    /// Краткое представление для отладки.
    /// </summary>
    public override string ToString() => $"Simulation Day {CurrentDay}, Tick {CurrentTick}";
}
