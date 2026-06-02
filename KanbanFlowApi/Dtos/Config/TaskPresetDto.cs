namespace KanbanFlowApi.Dtos.Config;

/// <summary>
/// DTO для пресета задач — содержит набор задач для симуляции.
/// </summary>
public sealed record TaskPresetDto
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
    /// Описание пресета (количество задач, типы).
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// Список задач в пресете.
    /// </summary>
    public List<ApiTaskDto> Tasks { get; set; } = new();

    /// <summary>
    /// Является ли этот пресет пресетом по умолчанию.
    /// </summary>
    public bool IsDefault { get; set; }
}
