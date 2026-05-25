using KanbanFlowConsole.Dtos.Board;
using KanbanFlowConsole.Dtos.History;
using KanbanFlowConsole.Enums;

namespace KanbanFlowApi.Dtos;

/// <summary>
/// DTO для задачи на доске (состояние задачи в симуляции).
/// </summary>
public sealed class ApiBoardTaskDto
{
    /// <summary>
    /// Уникальный ключ задачи (например, TASK-1).
    /// </summary>
    public string Key { get; set; } = null!;

    /// <summary>
    /// Краткое описание задачи.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Размер задачи (TShirt: XS, S, M, L, XL).
    /// </summary>
    public TShirtType? ShirtType { get; set; }

    /// <summary>
    /// Роль, необходимая для выполнения задачи.
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// Прогресс выполнения (0-100).
    /// </summary>
    public decimal Progress { get; set; }

    /// <summary>
    /// Логин исполнителя, работающего над задачей.
    /// </summary>
    public string? WorkerLogin { get; set; }

    /// <summary>
    /// Имя текущей стадии, где находится задача.
    /// </summary>
    public string? CurrentStageName { get; set; }
}

/// <summary>
/// DTO для стадии на доске (состояние стадии в симуляции).
/// </summary>
public sealed class ApiBoardStageDto
{
    /// <summary>
    /// Имя стадии.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Тип стадии: Work (требует исполнителя) или Buffer (буфер/очередь).
    /// </summary>
    public StageType Type { get; set; }

    /// <summary>
    /// Является ли стадия стартовой (в неё могут попадать новые задачи).
    /// </summary>
    public bool IsStart { get; set; }

    /// <summary>
    /// Является ли стадия началом для измерения Lead Time.
    /// </summary>
    public bool IsLeadTimeStart { get; set; }

    /// <summary>
    /// WIP-лимит (максимум задач одновременно). Null = без лимита.
    /// </summary>
    public int? WipLimit { get; set; }

    /// <summary>
    /// Текущее количество задач на стадии.
    /// </summary>
    public int WipCount { get; set; }

    /// <summary>
    /// Может ли стадия принять ещё задачи (с учётом WIP-лимита).
    /// </summary>
    public bool CanAcceptTasks { get; set; }

    /// <summary>
    /// Ключи задач, находящихся на этой стадии.
    /// </summary>
    public List<string> TaskKeys { get; set; } = new();

    /// <summary>
    /// Имена следующих стадий, куда можно перейти из текущей.
    /// </summary>
    public List<string> NextStageNames { get; set; } = new();
}

/// <summary>
/// DTO для воркера на доске (состояние исполнителя в симуляции).
/// </summary>
public sealed class ApiBoardWorkerDto
{
    /// <summary>
    /// Логин исполнителя.
    /// </summary>
    public string Login { get; set; } = null!;

    /// <summary>
    /// Роль исполнителя (например, Backend Developer).
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// Персональный WIP-лимит (максимум задач одновременно). Null = без лимита.
    /// </summary>
    public int? WipLimit { get; set; }

    /// <summary>
    /// Текущее количество задач у исполнителя.
    /// </summary>
    public int WipCount { get; set; }

    /// <summary>
    /// Доступен ли исполнитель для взятия новых задач.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Ключи задач, назначенных исполнителю.
    /// </summary>
    public List<string> AssignedTaskKeys { get; set; } = new();
}

/// <summary>
/// DTO для доски (полное состояние всех задач, стадий и исполнителей).
/// </summary>
public sealed class ApiBoardDto
{
    /// <summary>
    /// Список стадий доски.
    /// </summary>
    public List<ApiBoardStageDto> Stages { get; set; } = new();

    /// <summary>
    /// Список исполнителей (воркеров).
    /// </summary>
    public List<ApiBoardWorkerDto> Workers { get; set; } = new();

    /// <summary>
    /// Список задач на доске.
    /// </summary>
    public List<ApiBoardTaskDto> Tasks { get; set; } = new();
}

/// <summary>
/// DTO для события истории симуляции.
/// </summary>
public sealed class ApiHistoryActivityDto
{
    /// <summary>
    /// Тип события (TaskPulled, TaskMoved, WorkPerformed и т.д.).
    /// </summary>
    public ActivityType Type { get; set; }

    /// <summary>
    /// Тик симуляции, когда произошло событие.
    /// </summary>
    public int Tick { get; set; }

    /// <summary>
    /// Текстовое описание события.
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// Ключ задачи, связанной с событием.
    /// </summary>
    public string? TaskKey { get; set; }

    /// <summary>
    /// Логин исполнителя, связанного с событием.
    /// </summary>
    public string? WorkerLogin { get; set; }

    /// <summary>
    /// Имя стадии, связанной с событием.
    /// </summary>
    public string? StageName { get; set; }

    /// <summary>
    /// Прогресс задачи после события (если применимо).
    /// </summary>
    public decimal? Progress { get; set; }
}

/// <summary>
/// DTO для дня истории симуляции (содержит все события за день).
/// </summary>
public sealed class ApiHistoryDayDto
{
    /// <summary>
    /// Номер дня симуляции.
    /// </summary>
    public int DayNumber { get; set; }

    /// <summary>
    /// Список событий, произошедших в этот день.
    /// </summary>
    public List<ApiHistoryActivityDto> Activities { get; set; } = new();
}

/// <summary>
/// DTO для полного состояния симуляции (конфигурация + доска + история + текущий день/тик).
/// Используется для итеративной симуляции: POST /api/simulation/simulate-day принимает и возвращает это состояние.
/// </summary>
public sealed class ApiSimulationStateDto
{
    /// <summary>
    /// Конфигурация симуляции (воркфлоу, воркеры, задачи).
    /// </summary>
    public ApiSimulationConfigDto Config { get; set; } = null!;

    /// <summary>
    /// Текущее состояние доски (задачи, стадии, исполнители).
    /// </summary>
    public ApiBoardDto Board { get; set; } = null!;

    /// <summary>
    /// История симуляции по дням.
    /// </summary>
    public List<ApiHistoryDayDto> History { get; set; } = new();

    /// <summary>
    /// Текущий день симуляции (0 = ещё не началась).
    /// </summary>
    public int CurrentDay { get; set; }

    /// <summary>
    /// Текущий тик симуляции (внутри дня).
    /// </summary>
    public int CurrentTick { get; set; }
}
