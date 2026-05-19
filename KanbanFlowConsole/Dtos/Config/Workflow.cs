namespace KanbanFlowConsole.Dtos.Config;

public sealed record Workflow
{
    public List<Stage> Stages { get; set; } = new();
}