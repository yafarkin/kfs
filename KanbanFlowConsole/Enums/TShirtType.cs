namespace KanbanFlowConsole.Enums;

/// <summary>
///     Размеры задач (майки) для оценки сложности
/// </summary>
public enum TShirtType
{
    /// <summary>
    ///     Очень маленький размер (1 день)
    /// </summary>
    XS,

    /// <summary>
    ///     Маленький размер (2-3 дня)
    /// </summary>
    S,

    /// <summary>
    ///     Средний размер (4-6 дней)
    /// </summary>
    M,

    /// <summary>
    ///     Большой размер (7-15 дней)
    /// </summary>
    L
}

/// <summary>
///     Методы расширения для TShirtType
/// </summary>
public static class TShirtTypeExtensions
{
    /// <summary>
    ///     Возвращает количество дней для выполнения задачи (по верхней границе)
    ///     Используется для расчёта времени выполнения задачи на стадии
    /// </summary>
    public static int GetDaysToComplete(this TShirtType shirtType)
    {
        return shirtType switch
        {
            TShirtType.XS => 1,
            TShirtType.S => 3,
            TShirtType.M => 6,
            TShirtType.L => 15,
            _ => 1
        };
    }
}