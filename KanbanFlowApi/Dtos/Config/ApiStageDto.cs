using KanbanFlowSerivce.Enums;

namespace KanbanFlowApi.Dtos.Config;

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
    /// Является ли стадия началом для измерения Lead Time.
    /// </summary>
    public bool IsLeadTimeStart { get; set; }

    /// <summary>
    /// WIP-лимит (максимум задач одновременно). Null = без лимита.
    /// </summary>
    public int? WipLimit { get; set; }

    /// <summary>
    /// Навыки, требуемые для работы на стадии.
    /// Например: ["backend"], ["qa-manual"], ["qa-auto"].
    /// </summary>
    public List<string> RequiredSkills { get; set; } = new();

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
    /// Создаёт ли стадия ценность для бизнеса.
    /// Например: Developing = true, Testing = true, Code Review = false.
    /// </summary>
    public bool CreatesValue { get; set; } = true;

    /// <summary>
    /// Список переходов в другие стадии с вероятностями.
    /// </summary>
    public List<ApiStageTransitionDto> Transitions { get; set; } = new();

    /// <summary>
    /// Краткое представление для отладки.
    /// </summary>
    public override string ToString() => $"{Name} ({Type})";
}
