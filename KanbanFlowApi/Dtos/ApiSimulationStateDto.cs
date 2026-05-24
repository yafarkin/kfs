using KanbanFlowConsole.Dtos.Board;
using KanbanFlowConsole.Dtos.History;
using KanbanFlowConsole.Enums;

namespace KanbanFlowApi.Dtos;

/// <summary>
/// DTO для задачи на доске (состояние задачи)
/// </summary>
public sealed class ApiBoardTaskDto
{
    public string Key { get; set; } = null!;
    public string? Summary { get; set; }
    public TShirtType? ShirtType { get; set; }
    public string? Role { get; set; }
    public decimal Progress { get; set; }
    public string? WorkerLogin { get; set; }
    public string? CurrentStageName { get; set; }
}

/// <summary>
/// DTO для стадии на доске (состояние стадии)
/// </summary>
public sealed class ApiBoardStageDto
{
    public string Name { get; set; } = null!;
    public StageType Type { get; set; }
    public bool IsStart { get; set; }
    public bool IsLeadTimeStart { get; set; }
    public int? WipLimit { get; set; }
    public int WipCount { get; set; }
    public bool CanAcceptTasks { get; set; }
    public List<string> TaskKeys { get; set; } = new();
    public List<string> NextStageNames { get; set; } = new();
}

/// <summary>
/// DTO для воркера на доске (состояние воркера)
/// </summary>
public sealed class ApiBoardWorkerDto
{
    public string Login { get; set; } = null!;
    public string? Role { get; set; }
    public int? WipLimit { get; set; }
    public int WipCount { get; set; }
    public bool IsAvailable { get; set; }
    public List<string> AssignedTaskKeys { get; set; } = new();
}

/// <summary>
/// DTO для доски (состояние всех задач, стадий, воркеров)
/// </summary>
public sealed class ApiBoardDto
{
    public List<ApiBoardStageDto> Stages { get; set; } = new();
    public List<ApiBoardWorkerDto> Workers { get; set; } = new();
    public List<ApiBoardTaskDto> Tasks { get; set; } = new();
}

/// <summary>
/// DTO для события истории
/// </summary>
public sealed class ApiHistoryActivityDto
{
    public ActivityType Type { get; set; }
    public int Tick { get; set; }
    public string Description { get; set; } = null!;
    public string? TaskKey { get; set; }
    public string? WorkerLogin { get; set; }
    public string? StageName { get; set; }
    public decimal? Progress { get; set; }
}

/// <summary>
/// DTO для дня истории
/// </summary>
public sealed class ApiHistoryDayDto
{
    public int DayNumber { get; set; }
    public List<ApiHistoryActivityDto> Activities { get; set; } = new();
}

/// <summary>
/// DTO для полного состояния симуляции
/// </summary>
public sealed class ApiSimulationStateDto
{
    /// <summary>
    /// Конфигурация симуляции
    /// </summary>
    public ApiSimulationConfigDto Config { get; set; } = null!;

    /// <summary>
    /// Состояние доски
    /// </summary>
    public ApiBoardDto Board { get; set; } = null!;

    /// <summary>
    /// История симуляции
    /// </summary>
    public List<ApiHistoryDayDto> History { get; set; } = new();

    /// <summary>
    /// Текущий день симуляции
    /// </summary>
    public int CurrentDay { get; set; }

    /// <summary>
    /// Текущий тик симуляции
    /// </summary>
    public int CurrentTick { get; set; }
}
