using KanbanFlowSerivce.Dtos.Board;

namespace KanbanFlowSerivce.Dtos.History;

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
    ///     Задача достигла стадии начала Lead Time
    /// </summary>
    LeadTimeStarted,

    /// <summary>
    ///     Задача ожидает доступного воркера (неактивное время)
    /// </summary>
    TaskWaiting,

    /// <summary>
    ///     Задача возобновлена после ожидания (воркер назначен)
    /// </summary>
    TaskResumed,
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
    ///     Имя стадии (для сериализации/десериализации)
    /// </summary>
    public string? StageName { get; set; }

    /// <summary>
    ///     Логин воркера (для сериализации/десериализации)
    /// </summary>
    public string? WorkerLogin { get; set; }

    /// <summary>
    ///     Ключ задачи (для сериализации/десериализации)
    /// </summary>
    public string? TaskKey { get; set; }

    /// <summary>
    ///     Дополнительные данные (например, процент прогресса)
    /// </summary>
    public decimal? Progress { get; set; }

    /// <summary>
    ///     Обратная ссылка на день, в котором произошло событие
    /// </summary>
    public HistoryDay? Day { get; set; }

    /// <summary>
    ///     Номер дня, в котором произошло событие (для сериализации)
    /// </summary>
    public int DayNumber => Day?.DayNumber ?? 0;

    /// <summary>
    ///     Уникальный идентификатор для корреляции связанных событий
    ///     Например, WorkerTookTask и WorkerCompletedTask одной задачи на одной стадии имеют одинаковый CorrelationId
    /// </summary>
    public Guid CorrelationId { get; set; }
}
