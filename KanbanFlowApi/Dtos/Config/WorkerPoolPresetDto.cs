namespace KanbanFlowApi.Dtos.Config;

/// <summary>
/// DTO для пресета пула работников — содержит набор исполнителей.
/// </summary>
public sealed record WorkerPoolPresetDto : PresetDto
{
    /// <summary>
    /// Список работников в пуле.
    /// </summary>
    public List<ApiWorkerDto> Workers { get; set; } = new();
}
