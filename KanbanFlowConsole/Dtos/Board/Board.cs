namespace KanbanFlowConsole.Dtos;

public sealed record Board
{
    public List<BoardStage> Stages { get; set; } = new();
    public List<BoardWorker> Workers { get; set; } = new();
    public List<BoardTask> Tasks { get; set; } = new();
}