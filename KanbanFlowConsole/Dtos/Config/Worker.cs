using KanbanFlowConsole.Enums;

namespace KanbanFlowConsole.Dtos.Config;

/// <summary>
/// Воркер (исполнитель) в конфигурации симуляции — представляет участника workflow с ролью и производительностью.
/// </summary>
public sealed record Worker
{
    /// <summary>
    /// Уникальный логин воркера.
    /// </summary>
    public string Login { get; set; } = null!;

    /// <summary>
    /// Навыки воркера. Например: ["backend", "api"], ["frontend", "react"], ["qa-manual", "qa-auto"].
    /// Воркер может брать задачи, требующие эти навыки.
    /// </summary>
    public List<string> Skills { get; set; } = new();

    /// <summary>
    /// Персональный WIP-лимит (максимум задач одновременно).
    /// Null = без лимита.
    /// </summary>
    public int? WipLimit { get; set; }

    /// <summary>
    ///     Производительность ресурса в процентах (100 = стандартная скорость).
    ///     Значения > 100 означают повышенную производительность.
    /// </summary>
    public double Performance { get; set; }
}

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