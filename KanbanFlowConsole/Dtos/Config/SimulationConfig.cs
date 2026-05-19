namespace KanbanFlowConsole.Dtos.Config;

public sealed record SimulationConfig
{
    public long Seed { get; set; }

    public List<Worker> Workers { get; set; } = [];

    public Workflow Workflow { get; set; } = null!;

    public List<Task> Tasks { get; set; } = [];
}