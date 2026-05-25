using KanbanFlowConsole.Enums;

namespace KanbanFlowApi.Dtos;

/// <summary>
/// DTO для стадии workflow (без циклических ссылок).
/// </summary>
public sealed record ApiStageDto
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

    /// <summary>
    /// Краткое представление для отладки.
    /// </summary>
    public override string ToString() => $"{Name} ({Type})";
}
