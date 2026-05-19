using KanbanFlowConsole.Enums;

namespace KanbanFlowConsole.Dtos;

public sealed record Task
{
    public string Key { get; set; } = null!;
    public string? Summary { get; set; }
    public TShirtType? ShirtType { get; set; }
    public string? Role { get; set; }
    public string? Developer { get; set; }
    public List<Task>? Children { get; set; }

    /// <summary>
    /// Опционально - задаём для каждой стадии (key) конкретного исполнителя (value).
    /// </summary>
    public Dictionary<string, string>? AcceptableWorkers = new();
}