using KanbanFlowConsole.Enums;

namespace KanbanFlowApi.Dtos;

/// <summary>
/// Переход из стадии в другую стадию.
/// </summary>
public sealed class ApiStageTransitionDto
{
    /// <summary>
    /// Имя целевой стадии.
    /// </summary>
    public string TargetStageName { get; set; } = null!;

    /// <summary>
    /// Вероятность перехода (0.0 - 1.0).
    /// </summary>
    public double Probability { get; set; }
}

/// <summary>
/// DTO для стадии workflow (без циклических ссылок).
/// </summary>
public sealed class ApiStageDto
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
    /// Роли, которым разрешено работать на стадии (пусто = всем разрешено).
    /// </summary>
    public List<string> AllowedRoles { get; set; } = new();

    /// <summary>
    /// Требует ли стадия отдельного ресурса (например, Code Review).
    /// </summary>
    public bool RequiresDifferentResource { get; set; }

    /// <summary>
    /// Имя стадии, от которой требуется отдельный ресурс.
    /// </summary>
    public string? RequiresDifferentResourceFromStage { get; set; }

    /// <summary>
    /// Процент прогресса, который даёт стадия (для Work-стадий).
    /// </summary>
    public int StageProgressPercent { get; set; }

    /// <summary>
    /// Список переходов в другие стадии с вероятностями.
    /// </summary>
    public List<ApiStageTransitionDto> Transitions { get; set; } = new();
}

/// <summary>
/// DTO для workflow (набор стадий и переходов).
/// </summary>
public sealed class ApiWorkflowDto
{
    /// <summary>
    /// Список стадий workflow.
    /// </summary>
    public List<ApiStageDto> Stages { get; set; } = new();
}

/// <summary>
/// DTO для задачи (тип, описание).
/// </summary>
public sealed class ApiTaskDto
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
    /// Дочерние задачи (для иерархических структур).
    /// </summary>
    public List<ApiTaskDto>? Children { get; set; }

    /// <summary>
    /// Предпочтительные исполнители для стадий (ключ: имя стадии, значение: логин воркера).
    /// </summary>
    public Dictionary<string, string>? AcceptableWorkers { get; set; }
}

/// <summary>
/// DTO для исполнителя (воркера).
/// </summary>
public sealed class ApiWorkerDto
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
    /// Производительность (100 = базовая, 150 = на 50% быстрее).
    /// </summary>
    public double Performance { get; set; }
}

/// <summary>
/// DTO для конфигурации симуляции (без циклических ссылок).
/// Используется для описания параметров симуляции: воркфлоу, воркеры, задачи.
/// </summary>
public sealed class ApiSimulationConfigDto
{
    /// <summary>
    /// Seed для генератора случайных чисел (воспроизводимость симуляции).
    /// </summary>
    public long Seed { get; set; }

    /// <summary>
    /// Список исполнителей (воркеров).
    /// </summary>
    public List<ApiWorkerDto> Workers { get; set; } = new();

    /// <summary>
    /// Воркфлоу (стадии и переходы).
    /// </summary>
    public ApiWorkflowDto Workflow { get; set; } = null!;

    /// <summary>
    /// Список задач для симуляции.
    /// </summary>
    public List<ApiTaskDto> Tasks { get; set; } = new();
}
