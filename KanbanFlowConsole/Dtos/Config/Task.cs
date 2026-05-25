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
    /// </summary>
    public string? Role { get; set; }

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