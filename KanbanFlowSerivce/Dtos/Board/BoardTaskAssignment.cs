namespace KanbanFlowSerivce.Dtos.Board;

/// <summary>
///     Назначение задачи воркеру (связь задачи и стадии)
/// </summary>
public sealed record BoardTaskAssignment
{
    /// <summary>
    ///     Задача
    /// </summary>
    public BoardTask Task { get; set; } = null!;

    /// <summary>
    ///     Стадия, на которой воркер работает над задачей
    /// </summary>
    public BoardStage Stage { get; set; } = null!;

    /// <summary>
    ///     Сколько дней требуется для выполнения задачи на этой стадии (бросается один раз при взятии задачи)
    /// </summary>
    public decimal DaysRequired { get; set; }

    /// <summary>
    ///     Сколько дней уже отработано над задачей (может быть дробным при многозадачности)
    /// </summary>
    public decimal DaysWorked { get; set; }
}
