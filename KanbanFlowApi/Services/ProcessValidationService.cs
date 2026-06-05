using KanbanFlowApi.Dtos.Config;

namespace KanbanFlowApi.Services;

/// <summary>
/// Сервис валидации конфигурации процесса.
/// Проверяет корректность workflow (стадии и переходы).
/// </summary>
public class ProcessValidationService
{
    /// <summary>
    /// Результат валидации.
    /// </summary>
    public sealed record ValidationResult(bool IsValid, string? ErrorMessage)
    {
        public static ValidationResult Valid() => new(true, null);
        public static ValidationResult Invalid(string error) => new(false, error);
    }

    /// <summary>
    /// Валидирует конфигурацию процесса.
    /// </summary>
    public ValidationResult Validate(ProcessPresetDto preset)
    {
        if (preset.Workflow == null || preset.Workflow.Stages == null || preset.Workflow.Stages.Count == 0)
        {
            return ValidationResult.Invalid("Процесс должен содержать хотя бы одну стадию");
        }

        var stages = preset.Workflow.Stages;
        var stageNames = stages.Select(s => s.Name).ToHashSet();

        // Валидация каждой стадии
        foreach (var stage in stages)
        {
            // Проверка имени
            if (string.IsNullOrWhiteSpace(stage.Name))
            {
                return ValidationResult.Invalid("Название стадии не может быть пустым");
            }

            // Проверка WIP-лимита
            if (stage.WipLimit.HasValue && stage.WipLimit <= 0)
            {
                return ValidationResult.Invalid($"WIP-лимит стадии '{stage.Name}' должен быть больше 0");
            }

            // Проверка прогресса
            if (stage.StageProgressPercent < 0 || stage.StageProgressPercent > 100)
            {
                return ValidationResult.Invalid($"Прогресс стадии '{stage.Name}' должен быть от 0 до 100%");
            }

            // Валидация переходов
            var transitionValidation = ValidateTransitions(stage, stageNames);
            if (!transitionValidation.IsValid)
            {
                return transitionValidation;
            }
        }

        // Проверка наличия хотя бы одной стартовой стадии (без входящих переходов)
        var hasStartStage = HasStartStage(stages);
        if (!hasStartStage)
        {
            return ValidationResult.Invalid(
                "Процесс должен иметь хотя бы одну стартовую стадию (без входящих переходов). " +
                "Все стадии имеют входящие переходы — задачи не смогут начать движение.");
        }

        // Проверка на наличие циклов, блокирующих старт
        var cycleValidation = ValidateNoBlockingCycles(stages);
        if (!cycleValidation.IsValid)
        {
            return cycleValidation;
        }

        return ValidationResult.Valid();
    }

    /// <summary>
    /// Валидирует переходы стадии.
    /// </summary>
    private ValidationResult ValidateTransitions(
        ApiStageDto stage,
        HashSet<string> stageNames)
    {
        if (stage.Transitions == null || stage.Transitions.Count == 0)
        {
            // Финальная стадия может не иметь переходов
            return ValidationResult.Valid();
        }

        // Проверка существования целевых стадий
        foreach (var transition in stage.Transitions)
        {
            if (string.IsNullOrWhiteSpace(transition.TargetStageName))
            {
                return ValidationResult.Invalid(
                    $"Целевая стадия перехода не может быть пустой (стадия '{stage.Name}')");
            }

            if (!stageNames.Contains(transition.TargetStageName))
            {
                return ValidationResult.Invalid(
                    $"Целевая стадия '{transition.TargetStageName}' не существует (переход из стадии '{stage.Name}')");
            }

            if (transition.Probability <= 0 || transition.Probability > 1)
            {
                return ValidationResult.Invalid(
                    $"Вероятность перехода должна быть от 0 до 1 (стадия '{stage.Name}', переход в '{transition.TargetStageName}')");
            }
        }

        // Проверка суммы вероятностей
        var totalProbability = stage.Transitions.Sum(t => t.Probability);
        if (totalProbability > 1.01) // Небольшой допуск для floating point
        {
            return ValidationResult.Invalid(
                $"Сумма вероятностей переходов стадии '{stage.Name}' не может превышать 1.0 (текущая: {totalProbability:F2})");
        }

        return ValidationResult.Valid();
    }

    /// <summary>
    /// Проверяет наличие хотя бы одной стартовой стадии (без входящих переходов от других стадий).
    /// Self-loop (переход на себя) не считается входящим переходом.
    /// </summary>
    private bool HasStartStage(List<ApiStageDto> stages)
    {
        // Находим все стадии, которые являются целевыми для переходов ОТ ДРУГИХ стадий
        var targetStages = new HashSet<string>();
        foreach (var stage in stages)
        {
            if (stage.Transitions != null)
            {
                foreach (var transition in stage.Transitions)
                {
                    // Self-loop не считается входящим переходом
                    if (transition.TargetStageName != stage.Name)
                    {
                        targetStages.Add(transition.TargetStageName);
                    }
                }
            }
        }

        // Стартовая стадия — та, которая не является целевой ни для одного перехода от другой стадии
        var hasStart = stages.Any(s => !targetStages.Contains(s.Name));
        return hasStart;
    }

    /// <summary>
    /// Проверяет отсутствие циклов, блокирующих возможность старта.
    /// Цикл блокирует старт, если все стадии в цикле имеют только входящие переходы из этого же цикла.
    /// </summary>
    private ValidationResult ValidateNoBlockingCycles(List<ApiStageDto> stages)
    {
        // Строим граф переходов
        var adjacencyList = new Dictionary<string, List<string>>();
        foreach (var stage in stages)
        {
            adjacencyList[stage.Name] = stage.Transitions?.Select(t => t.TargetStageName).ToList() ?? new List<string>();
        }

        // Находим стартовые стадии (без входящих от других стадий)
        var targetStages = new HashSet<string>();
        foreach (var stage in stages)
        {
            if (stage.Transitions != null)
            {
                foreach (var transition in stage.Transitions)
                {
                    // Self-loop не считается входящим переходом
                    if (transition.TargetStageName != stage.Name)
                    {
                        targetStages.Add(transition.TargetStageName);
                    }
                }
            }
        }

        var startStages = stages.Where(s => !targetStages.Contains(s.Name)).Select(s => s.Name).ToList();

        if (startStages.Count == 0)
        {
            // Уже проверено в HasStartStage
            return ValidationResult.Valid();
        }

        // Проверяем, что из каждой стартовой стадии можно достичь хотя бы одной финальной
        // Финальная стадия — та, у которой нет исходящих переходов
        var finalStages = stages.Where(s => s.Transitions == null || s.Transitions.Count == 0)
            .Select(s => s.Name)
            .ToHashSet();

        foreach (var startStage in startStages)
        {
            var reachableFinals = FindReachableFinals(startStage, adjacencyList, finalStages);
            if (reachableFinals.Count == 0 && finalStages.Count > 0)
            {
                return ValidationResult.Invalid(
                    $"Из стартовой стадии '{startStage}' невозможно достичь финальной стадии. " +
                    "Проверьте наличие циклов или тупиковых переходов.");
            }
        }

        return ValidationResult.Valid();
    }

    /// <summary>
    /// Находит все финальные стадии, достижимые из заданной.
    /// </summary>
    private HashSet<string> FindReachableFinals(
        string startStage,
        Dictionary<string, List<string>> adjacencyList,
        HashSet<string> finalStages)
    {
        var visited = new HashSet<string>();
        var reachable = new HashSet<string>();
        var stack = new Stack<string>();

        stack.Push(startStage);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            if (visited.Contains(current))
            {
                continue;
            }

            visited.Add(current);

            if (finalStages.Contains(current))
            {
                reachable.Add(current);
            }

            if (adjacencyList.TryGetValue(current, out var neighbors))
            {
                foreach (var neighbor in neighbors)
                {
                    if (!visited.Contains(neighbor))
                    {
                        stack.Push(neighbor);
                    }
                }
            }
        }

        return reachable;
    }
}
