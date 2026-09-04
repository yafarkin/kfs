namespace KanbanFlowSerivce.Enums;

/// <summary>
///     Методы расширения для TShirtType
/// </summary>
public static class TShirtTypeExtensions
{
    /// <summary>
    ///     Возвращает количество дней для выполнения задачи (по верхней границе)
    ///     Используется для расчёта времени выполнения задачи на стадии
    /// </summary>
    public static (int, int) GetDaysToComplete(this TShirtType shirtType)
    {
        return shirtType switch
        {
            TShirtType.XS => (1, 1),
            TShirtType.S => (2, 3),
            TShirtType.M => (4, 6),
            TShirtType.L => (7, 15),
            TShirtType.XL => (16, 30),
            _ => (1, 1)
        };
    }
}