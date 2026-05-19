using KanbanFlowConsole.Enums;

namespace KanbanFlowConsole.Dtos.Config;

public sealed record Stage
{
    public string Name { get; set; } = null!;
    public StageType Type { get; set; }
    public bool IsStart { get; set; }
    public bool IsLeadTimeStart { get; set; }
    public int? WipLimit { get; set; }
    public string[] AllowedRoles { get; set; } = [];

    /// <summary>
    ///     Требуется ли, чтобы воркер отличался от того, что работал в предыдущей стадии
    /// </summary>
    public bool RequiresDifferentResource { get; set; }

    /// <summary>
    ///     Имя стадии, откуда нельзя брать того же воркера (если RequiresDifferentResource = true)
    ///     Если null, проверяется последняя стадия, где воркер выполнял задачу
    /// </summary>
    public string? RequiresDifferentResourceFromStage { get; set; }

    /// <summary>
    ///     Переходы к следующим стадиям (DAG)
    /// </summary>
    public List<StageTransition> Transitions { get; set; } = new();
}