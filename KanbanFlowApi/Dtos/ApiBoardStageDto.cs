using KanbanFlowConsole.Enums;

namespace KanbanFlowApi.Dtos;

/// <summary>
/// DTO для стадии на доске (состояние стадии в симуляции).
/// </summary>
public sealed record ApiBoardStageDto
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

    /// <summary>
    /// Краткое представление для отладки.
    /// </summary>
    public override string ToString() => $"{Name} ({WipCount}/{(WipLimit.HasValue ? WipLimit.Value.ToString() : "∞")})";
}
