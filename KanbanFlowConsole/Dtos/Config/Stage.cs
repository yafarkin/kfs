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
    public bool RequiresDifferentResource { get; set; }

    /// <summary>
    /// Переходы к следующим стадиям (DAG)
    /// </summary>
    public List<StageTransition> Transitions { get; set; } = new();
}