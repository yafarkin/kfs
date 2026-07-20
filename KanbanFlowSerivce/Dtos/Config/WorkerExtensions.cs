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
    public static int GetDaysForTask(this Worker worker, Stage stage, TShirtType? shirtType, bool useVariability = true, CountingRandom? random = null)
    {
        if (!shirtType.HasValue)
        {
            return 1; // Задачи без размера выполняются за 1 день
        }

        // 1. Получаем min/max из стадии и размера задачи
        var baseDays = stage.GetDaysForTask(shirtType.Value);
        var minDays = baseDays.Item1;
        var maxDays = baseDays.Item2;

        // 2. Бросаем кубик для определения базовой оценки в диапазоне [min, max]
        double baseEstimate;
        if (useVariability && random != null)
        {
            // Случайное значение в диапазоне [minDays, maxDays]
            baseEstimate = random.NextDouble() * (maxDays - minDays) + minDays;
        }
        else
        {
            // Без вариативности — используем среднее значение
            baseEstimate = (minDays + maxDays) / 2.0;
        }

        // 3. Применяем performance как множитель скорости
        // performance = 100% → множитель 1.0 (без изменений)
        // performance = 200% → множитель 0.5 (в 2 раза быстрее)
        // performance = 50% → множитель 2.0 (в 2 раза медленнее)
        // performance = 0% → множитель 1.0 (защита от деления на ноль, трактуем как 100%)
        var performanceMultiplier = worker.Performance > 0 ? 100.0 / worker.Performance : 1.0;
        baseEstimate *= performanceMultiplier;

        // 4. Применяем отклонения (deviation) к итоговой оценке
        if (useVariability && random != null)
        {
            var estimateWithDeviationDown = baseEstimate * (1.0 - worker.DeviationDownPercent / 100.0);
            var estimateWithDeviationUp = baseEstimate * (1.0 + worker.DeviationUpPercent / 100.0);

            // Случайное значение в диапазоне [-deviation, +deviation]
            // Если deviation = 0, диапазон схлопывается в точку
            baseEstimate = random.NextDouble() * (estimateWithDeviationUp - estimateWithDeviationDown) + estimateWithDeviationDown;
        }

        // Округляем до целого (минимум 1 день)
        return Math.Max(1, (int)Math.Ceiling(baseEstimate));
    }
}