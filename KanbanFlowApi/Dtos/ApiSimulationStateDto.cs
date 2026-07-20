using KanbanFlowApi.Dtos.Board;
using KanbanFlowApi.Dtos.Config;
using KanbanFlowApi.Dtos.History;

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
    /// Количество вызовов Random.NextDouble для детерминированной перемотки.
    /// </summary>
    public int RandomCallCount { get; set; }

    /// <summary>
    /// Краткое представление для отладки.
    /// </summary>
    public override string ToString() => $"Simulation Day {CurrentDay}";
}
