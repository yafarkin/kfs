namespace KanbanFlowSerivce.Enums;

/// <summary>
///     Тип участка
/// </summary>
public enum StageType
{
    /// <summary>
    ///     Рабочий участок (требует ресурс)
    /// </summary>
    Work,

    /// <summary>
    ///     Буферный участок (ожидание)
    /// </summary>
    Buffer
}