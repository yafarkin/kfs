namespace KanbanFlowApi.Dtos.Config;

/// <summary>
/// DTO для описания доступной конфигурации симуляции (пресет).
/// </summary>
public sealed record ConfigPresetDto
{
    /// <summary>
    /// Уникальное имя конфигурации (ключ для загрузки).
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Отображаемое название конфигурации.
    /// </summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// Описание конфигурации (количество воркеров, стадии, задачи).
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// Является ли эта конфигурация конфигурацией по умолчанию.
    /// </summary>
    public bool IsDefault { get; set; }
}
