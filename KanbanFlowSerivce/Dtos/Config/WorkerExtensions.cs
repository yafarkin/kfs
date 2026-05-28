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

        // 1. Получаем min/max из стадии и размера задачи
        var baseDays = stage.GetDaysForTask(shirtType.Value);
        var minDays = baseDays.Item1;
        var maxDays = baseDays.Item2;

        // 2. Применяем performance: 100% = min, 0% = max, 50% = середина
        var performancePosition = 1.0 - (worker.Performance / 100.0);
        var baseEstimate = minDays + (maxDays - minDays) * performancePosition;

        // 3-4. Если включена вариативность — применяем отклонения и выбираем случайное значение
        if (useVariability && random != null)
        {
            // Отклонения от базовой оценки
            var estimateWithDeviationDown = baseEstimate * (1.0 - worker.DeviationDownPercent / 100.0);
            var estimateWithDeviationUp = baseEstimate * (1.0 + worker.DeviationUpPercent / 100.0);

            // Случайное значение в диапазоне [-deviation, +deviation]
            baseEstimate = random.NextDouble() * (estimateWithDeviationUp - estimateWithDeviationDown) + estimateWithDeviationDown;
        }

        // Округляем до целого (минимум 1 день)
        return Math.Max(1, (int)Math.Ceiling(baseEstimate));
    }
}