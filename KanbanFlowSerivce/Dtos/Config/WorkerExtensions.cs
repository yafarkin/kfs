using KanbanFlowSerivce.Enums;

namespace KanbanFlowSerivce.Dtos.Config;

/// <summary>
///     Методы расширения для Worker
/// </summary>
public static class WorkerExtensions
{
    /// <summary>
    ///     Рассчитывает количество дней для выполнения задачи worker'ом на данной стадии.
    ///     Учитывает размер задачи (ShirtType) и процент стадии.
    ///     В дальнейшем будет учитывать Performance worker'а.
    /// </summary>
    public static int GetDaysForTask(this Worker worker, Stage stage, TShirtType? shirtType)
    {
        if (!shirtType.HasValue)
        {
            return 1; // Задачи без размера выполняются за 1 день
        }

        // Базовое количество дней для стадии с учётом размера задачи
        var baseDays = stage.GetDaysForTask(shirtType.Value);

        // В дальнейшем здесь будет расчёт с учётом Performance worker'а
        // Например: baseDays * (100.0 / worker.Performance)
        // Сейчас возвращаем базовое значение (как будто Performance = 100)
        return baseDays;
    }
}