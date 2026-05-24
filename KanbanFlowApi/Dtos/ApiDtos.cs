using KanbanFlowConsole.Enums;

namespace KanbanFlowApi.Dtos;

/// <summary>
/// DTO для стадии workflow (без циклических ссылок)
/// </summary>
public sealed class ApiStageDto
{
    public string Name { get; set; } = null!;
    public StageType Type { get; set; }
    public bool IsStart { get; set; }
    public bool IsLeadTimeStart { get; set; }
    public int? WipLimit { get; set; }
    public List<string> AllowedRoles { get; set; } = new();
    public bool RequiresDifferentResource { get; set; }
    public string? RequiresDifferentResourceFromStage { get; set; }
    public int StageProgressPercent { get; set; }
    public List<string> TransitionStageNames { get; set; } = new();
}

/// <summary>
/// DTO для workflow
/// </summary>
public sealed class ApiWorkflowDto
{
    public List<ApiStageDto> Stages { get; set; } = new();
}

/// <summary>
/// DTO для задачи
/// </summary>
public sealed class ApiTaskDto
{
    public string Key { get; set; } = null!;
    public string? Summary { get; set; }
    public TShirtType? ShirtType { get; set; }
    public string? Role { get; set; }
}

/// <summary>
/// DTO для воркера
/// </summary>
public sealed class ApiWorkerDto
{
    public string Login { get; set; } = null!;
    public string? Role { get; set; }
    public int? WipLimit { get; set; }
    public double Performance { get; set; }
}

/// <summary>
/// DTO для конфигурации симуляции (без циклических ссылок)
/// </summary>
public sealed class ApiSimulationConfigDto
{
    public long Seed { get; set; }
    public List<ApiWorkerDto> Workers { get; set; } = new();
    public ApiWorkflowDto Workflow { get; set; } = null!;
    public List<ApiTaskDto> Tasks { get; set; } = new();
}
