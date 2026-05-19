namespace KanbanFlowConsole.Dtos.History;

/// <summary>
///     Тип события в истории симуляции
/// </summary>
public enum ActivityType
{
    /// <summary>
    ///     Воркер взял задачу в работу
    /// </summary>
    WorkerTookTask,

    /// <summary>
    ///     Воркер завершил задачу
    /// </summary>
    WorkerCompletedTask,

    /// <summary>
    ///     Задача перемещена в другую стадию
    /// </summary>
    TaskMoved,

    /// <summary>
    ///     Задача выполняется (прогресс обновлён)
    /// </summary>
    TaskProgressUpdated,

    /// <summary>
    ///     Задача заблокирована
    /// </summary>
    TaskBlocked,

    /// <summary>
    ///     Задача разблокирована
    /// </summary>
    TaskUnblocked,

    /// <summary>
    ///     Воркер стал доступен
    /// </summary>
    WorkerAvailable,

    /// <summary>
    ///     Воркер освобождён
    /// </summary>
    WorkerReleased
}

/// <summary>
///     Отдельное действие в истории симуляции
/// </summary>
public sealed record HistoryActivity
{
    /// <summary>
    ///     Тип события
    /// </summary>
    public ActivityType Type { get; set; }

    /// <summary>
    ///     Время события (в тиках симуляции)
    /// </summary>
    public int Tick { get; set; }

    /// <summary>
    ///     Описание события (например, "worker X взял задачу task 2")
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    ///     Задача, если событие связано с задачей
    /// </summary>
    public BoardTask? Task { get; set; }

    /// <summary>
    ///     Воркер, если событие связано с воркером
    /// </summary>
    public BoardWorker? Worker { get; set; }

    /// <summary>
    ///     Стадия, если событие связано с перемещением
    /// </summary>
    public BoardStage? Stage { get; set; }

    /// <summary>
    ///     Дополнительные данные (например, процент прогресса)
    /// </summary>
    public decimal? Progress { get; set; }

    /// <summary>
    ///     Обратная ссылка на день, в котором произошло событие
    /// </summary>
    public HistoryDay? Day { get; set; }
}
