namespace KanbanFlowApi.Dtos.Config;

/// <summary>
/// Общая часть для всех DTO системных пресетов (процесс, пул воркеров) —
/// имя/отображаемое название/описание/признак пресета по умолчанию.
/// Конкретный payload (Workflow+Tasks, Workers, ...) добавляет наследник.
/// </summary>
public abstract record PresetDto
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
    /// Описание пресета.
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// Является ли этот пресет пресетом по умолчанию.
    /// </summary>
    public bool IsDefault { get; set; }
}
