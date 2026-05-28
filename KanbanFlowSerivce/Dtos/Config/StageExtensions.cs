using KanbanFlowSerivce.Enums;

namespace KanbanFlowSerivce.Dtos.Config;

/// <summary>
///     Методы расширения для Stage
/// </summary>
public static class StageExtensions
{
    /// <summary>
    ///     Рассчитывает количество дней для выполнения задачи на данной стадии
    ///     Формула: (размер задачи в днях * процент стадии) / 100
    ///     Результат округляется вверх до целых дней
    /// </summary>  
    public static (int, int) GetDaysForTask(this Stage stage, TShirtType shirtType)
    {
        var baseDays = shirtType.GetDaysToComplete();
        var minDaysForStage = (int)Math.Ceiling((baseDays.Item1 * stage.StageProgressPercent) / 100.0);
        var maxDaysForStage = (int)Math.Ceiling((baseDays.Item2 * stage.StageProgressPercent) / 100.0);
        return (minDaysForStage, maxDaysForStage);
    }
}