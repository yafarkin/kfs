using KanbanFlowConsole.Enums;

namespace KanbanFlowConsole.Dtos.Config;

/// <summary>
/// Задача в конфигурации симуляции — описывает рабочую единицу с размером, ролью и опциональными настройками.
/// Поддерживает иерархическую структуру через дочерние задачи (Children).
/// </summary>
public sealed record Task
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
    /// Размер задачи по T-Shirt шкале (XS, S, M, L, XL).
    /// Используется для расчёта времени выполнения.
    /// </summary>
    public TShirtType? ShirtType { get; set; }

    /// <summary>
    /// Роль, необходимая для выполнения задачи (например, "Backend Developer").
    /// Устаревшее поле, используется для обратной совместимости.
    /// Рекомендуется использовать RequiredSkills.
    /// </summary>
    [Obsolete("Используйте RequiredSkills вместо Role")]
    public string? Role { get; set; }

    /// <summary>
    /// Навыки, необходимые для выполнения задачи на стадии производства.
    /// Например: ["backend", "api"], ["frontend", "react"], ["qa-manual"].
    /// Задача может быть взята воркером, у которого есть все требуемые навыки.
    /// </summary>
    public List<string> RequiredSkills { get; set; } = new();

    /// <summary>
    /// Навыки, необходимые для выполнения задачи на конкретных стадиях.
    /// Ключ: имя стадии, Значение: список требуемых навыков.
    /// Например: { ["Testing"] = ["qa-manual"], ["Automation"] = ["qa-auto"] }.
    /// Если навык для стадии не указан, используется RequiredSkills.
    /// </summary>
    public Dictionary<string, List<string>> RequiredSkillsPerStage { get; set; } = new();

    /// <summary>
    /// Дочерние задачи для иерархической структуры.
    /// Позволяет группировать подзадачи внутри родительской.
    /// </summary>
    public List<Task>? Children { get; set; }

    /// <summary>
    /// Опционально — предпочтительные исполнители для конкретных стадий.
    /// Ключ: имя стадии, Значение: логин воркера.
    /// Если задано, задача будет назначаться только указанному воркеру на этой стадии.
    /// </summary>
    public Dictionary<string, string>? AcceptableWorkers { get; set; } = new();
}