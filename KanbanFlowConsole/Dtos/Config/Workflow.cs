namespace KanbanFlowConsole.Dtos;

public sealed record Workflow
{
    public List<Stage> Stages { get; set; } = new();
}