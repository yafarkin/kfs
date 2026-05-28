using KanbanFlowSerivce.Enums;

namespace KanbanFlowSerivce.Dtos.Config;

/// <summary>
///     Методы расширения для Worker
/// </summary>
public static class WorkerExtensions
{
    /// <summary>
    ///     Рассчитывает количество дней для выполнения задачи worker'ом на данной стадии.
    ///     Учитывает размер задачи (ShirtType), отклонения, performance воркера и random.
    /// </summary>
    /// <param name="worker">Воркер</param>
    /// <param name="stage">Стадия</param>
    /// <param name="shirtType">Размер задачи</param>
    /// <param name="useVariability">Использовать ли вариативность (random в диапазоне)</param>
    /// <param name="random">Генератор случайных чисел (для воспроизводимости)</param>
    public static int GetDaysForTask(this Worker worker, Stage stage, TShirtType? shirtType, bool useVariability = true, Random? random = null)
    {
        if (!shirtType.HasValue)
        {
            return 1; // Задачи без размера выполняются за 1 день
        }

        // Базовое количество дней для стадии (без учёта performance и отклонений)
        var baseDays = stage.GetDaysForTask(shirtType.Value);

        // Применяем отклонения к базовому значению
        var minDays = baseDays * (1.0 - worker.DeviationDownPercent / 100.0);
        var maxDays = baseDays * (1.0 + worker.DeviationUpPercent / 100.0);

        // Выбираем случайное значение в диапазоне [minDays, maxDays]
        var estimatedDays = useVariability && random != null
            ? random.NextDouble() * (maxDays - minDays) + minDays
            : (minDays + maxDays) / 2.0;

        // Применяем performance: 100% = min, 0% = max, 50% = середина
        // Performance влияет на итоговую оценку: чем выше performance, тем меньше дней
        var performanceFactor = 1.0 - (worker.Performance / 100.0);
        var finalDays = minDays + (estimatedDays - minDays) * performanceFactor;

        // Округляем до целого (минимум 1 день)
        return Math.Max(1, (int)Math.Ceiling(finalDays));
    }
}