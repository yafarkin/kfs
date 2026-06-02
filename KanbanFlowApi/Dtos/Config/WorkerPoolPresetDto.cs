namespace KanbanFlowApi.Dtos.Config;

/// <summary>
/// DTO для пресета пула работников — содержит набор исполнителей.
/// </summary>
public sealed record WorkerPoolPresetDto
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
    /// Описание пресета (количество работников, навыки).
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// Список работников в пуле.
    /// </summary>
    public List<ApiWorkerDto> Workers { get; set; } = new();

    /// <summary>
    /// Является ли этот пресет пресетом по умолчанию.
    /// </summary>
    public bool IsDefault { get; set; }
}
