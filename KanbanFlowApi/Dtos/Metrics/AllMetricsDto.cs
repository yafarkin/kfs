using KanbanFlowApi.Dtos.Task;

namespace KanbanFlowApi.Dtos.Metrics;

/// <summary>
/// Единый DTO для всех метрик симуляции.
/// Включает общие метрики, метрики работников, задач и стадий.
/// </summary>
public sealed record AllMetricsDto
{
    /// <summary>
    /// Общие метрики симуляции (Lead Time, Throughput, Flow Efficiency, Frequency).
    /// </summary>
    public ApiMetricsDto SimulationMetrics { get; set; } = new();

    /// <summary>
    /// Метрики работников (Throughput, Lead Time, Efficiency).
    /// </summary>
    public List<ApiWorkerMetricsDto> WorkerMetrics { get; set; } = [];

    /// <summary>
    /// Метрики задач (Lead Time, Flow Efficiency, время по стадиям).
    /// </summary>
    public List<TaskMetricsDto> TaskMetrics { get; set; } = [];

    /// <summary>
    /// Агрегированные метрики стадий (P50, P85, P95, Avg, Max).
    /// </summary>
    public List<StageMetricsAggregatedDto> StageMetrics { get; set; } = [];
}
