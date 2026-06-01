using KanbanFlowSerivce.Enums;

namespace KanbanFlowSerivce.Dtos.Config;

public sealed record Stage
{
    public string Name { get; set; } = null!;
    public StageType Type { get; set; }
    public bool IsLeadTimeStart { get; set; }
    public int? WipLimit { get; set; }
    
    /// <summary>
    /// Навыки, требуемые для работы на стадии.
    /// Например: ["backend"], ["qa-manual"], ["qa-auto"].
    /// Воркер должен иметь хотя бы один навык для работы на стадии.
    /// </summary>
    public List<string> RequiredSkills { get; set; } = new();

    /// <summary>
    ///     Требуется ли, чтобы воркер отличался от того, что работал в предыдущей стадии
    /// </summary>
    public bool RequiresDifferentResource { get; set; }

    /// <summary>
    ///     Имя стадии, откуда нельзя брать того же воркера (если RequiresDifferentResource = true)
    ///     Если null, проверяется последняя стадия, где воркер выполнял задачу
    /// </summary>
    public string? RequiresDifferentResourceFromStage { get; set; }

    /// <summary>
    ///     Процент выполнения работы на этой стадии (от общего размера задачи)
    ///     Например: Developing = 100%, QA = 30%, Code Review = 20%
    ///     Используется для расчёта времени выполнения задачи worker'ом
    /// </summary>
    public int StageProgressPercent { get; set; } = 100;

    /// <summary>
    ///     Создаёт ли стадия ценность для бизнеса.
    ///     Например: Developing = true, Testing = true, Code Review = false.
    ///     Используется для расчёта метрик worker'ов (Throughput, Lead Time).
    /// </summary>
    public bool CreatesValue { get; set; } = true;

    /// <summary>
    ///     Переходы к следующим стадиям (DAG)
    /// </summary>
    public List<StageTransition> Transitions { get; set; } = new();
}