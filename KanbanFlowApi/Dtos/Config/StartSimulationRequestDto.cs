namespace KanbanFlowApi.Dtos.Config;

/// <summary>
/// DTO для запроса на запуск симуляции с полной конфигурацией.
/// Backend stateless — конфигурация передаётся полностью с клиента.
/// Набор полей конфигурации (Seed/UseVariability/Workflow/Workers/Tasks) наследуется от
/// <see cref="ApiSimulationConfigDto"/> — это та же конфигурация, что живёт внутри состояния
/// запущенной симуляции, плюс одноразовая инструкция DaysToSimulate. JSON-формат при этом не
/// меняется: System.Text.Json сериализует унаследованные свойства в тот же плоский объект.
/// </summary>
public sealed record StartSimulationRequestDto : ApiSimulationConfigDto
{
    /// <summary>
    /// Количество дней для симуляции (опционально).
    /// Если null - симуляция выполняется на 1 день.
    /// Если 0 - симуляция выполняется до завершения всех задач.
    /// Если > 0 - симуляция выполняется на указанное количество дней.
    /// </summary>
    public int? DaysToSimulate { get; set; }
}
