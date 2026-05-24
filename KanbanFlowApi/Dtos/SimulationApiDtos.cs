namespace KanbanFlowApi.Dtos;

/// <summary>
/// Запрос на расчёт дня симуляции
/// </summary>
public sealed record SimulateDayRequest
{
    /// <summary>
    /// Конфигурация симуляции (без циклических ссылок)
    /// </summary>
    public ApiSimulationConfigDto Config { get; set; } = null!;

    /// <summary>
    /// Текущий день симуляции
    /// </summary>
    public int CurrentDay { get; set; }

    /// <summary>
    /// Текущий тик симуляции
    /// </summary>
    public int CurrentTick { get; set; }
}

/// <summary>
/// Результат расчёта дня симуляции
/// </summary>
public sealed record SimulateDayResponse
{
    /// <summary>
    /// Обновлённая конфигурация (без циклических ссылок)
    /// </summary>
    public ApiSimulationConfigDto Config { get; set; } = null!;

    /// <summary>
    /// Текущий день симуляции после расчёта
    /// </summary>
    public int CurrentDay { get; set; }

    /// <summary>
    /// Текущий тик симуляции после расчёта
    /// </summary>
    public int CurrentTick { get; set; }
}
