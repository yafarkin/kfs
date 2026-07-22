using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Enums;

namespace KanbanFlowSerivce.Dtos.Board;

/// <summary>
/// Воркер (исполнитель) на доске симуляции — представляет участника workflow с персональным WIP-лимитом.
/// Отслеживает назначенные задачи и доступность для новой работы.
/// </summary>
public sealed record BoardWorker
{
    /// <summary>
    /// Конфигурация воркера (логин, роль, производительность).
    /// </summary>
    public Worker Worker { get; set; } = null!;

    /// <summary>
    ///     WIP лимит воркера (берётся из конфигурации)
    /// </summary>
    public int? WipLimit => Worker.WipLimit;

    /// <summary>
    ///     Текущие задачи воркера с указанием стадий
    /// </summary>
    public List<BoardTaskAssignment> Assignments { get; set; } = new();

    /// <summary>
    ///     Текущее количество задач в работе (WIP).
    ///     Только в рабочих статусах - буфферные не считаются что по ним ведётся активная работа.
    ///     Завершённые задачи (Progress = 100%) не считаются — воркер считается свободным для новой работы.
    /// </summary>
    public int WipCount => Assignments.Count(x => 
        x.Stage.Stage.Type == StageType.Work && 
        !x.Task.IsCompleted
    );

    /// <summary>
    ///     Доступен ли воркер для взятия новых задач (с учётом WIP лимита)
    /// </summary>
    public bool IsAvailable => !WipLimit.HasValue || WipCount < WipLimit.Value;

    public void RemoveTaskAssignment(BoardTask task)
    {
        Assignments.RemoveAll(a => a.Task == task);
    }
}