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

        // Проверка на наличие циклов (включая self-loop)
        var cycleValidation = ValidateNoCycles(stages);
        if (!cycleValidation.IsValid)
        {
            return cycleValidation;
        }

        // Проверка наличия хотя бы одной стартовой стадии (без входящих переходов)
        var hasStartStage = HasStartStage(stages);
        if (!hasStartStage)
        {
            return ValidationResult.Invalid(
                "Процесс должен иметь хотя бы одну стартовую стадию (без входящих переходов). " +
                "Все стадии имеют входящие переходы — задачи не смогут начать движение.");
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
    /// Проверяет отсутствие любых циклов в процессе (включая self-loop).
    /// Циклы недопустимы — процесс должен быть ациклическим графом (DAG).
    /// </summary>
    private ValidationResult ValidateNoCycles(List<ApiStageDto> stages)
    {
        // Проверка self-loop (переход стадии на себя)
        foreach (var stage in stages)
        {
            if (stage.Transitions != null)
            {
                var selfLoop = stage.Transitions.FirstOrDefault(t => t.TargetStageName == stage.Name);
                if (selfLoop != null)
                {
                    return ValidationResult.Invalid(
                        $"Цикл на себя (self-loop) недопустим: стадия '{stage.Name}' имеет переход на себя.");
                }
            }
        }

        // Проверка циклов через DFS с раскраской вершин
        // 0 = white (не посещена), 1 = gray (в стеке), 2 = black (завершена)
        var color = new Dictionary<string, int>();
        foreach (var stage in stages)
        {
            color[stage.Name] = 0;
        }

        foreach (var stage in stages)
        {
            if (color[stage.Name] == 0)
            {
                var cycleResult = DetectCycle(stage.Name, color, stages.ToDictionary(s => s.Name, s => s.Transitions?.Select(t => t.TargetStageName).ToList() ?? new List<string>()));
                if (cycleResult != null)
                {
                    return ValidationResult.Invalid(
                        $"Обнаружен цикл в процессе: {string.Join(" -> ", cycleResult)}. Циклы недопустимы — процесс должен быть ациклическим графом (DAG).");
                }
            }
        }

        return ValidationResult.Valid();
    }

    /// <summary>
    /// DFS для обнаружения цикла. Возвращает путь цикла или null.
    /// </summary>
    private List<string>? DetectCycle(
        string current,
        Dictionary<string, int> color,
        Dictionary<string, List<string>> adjacencyList)
    {
        color[current] = 1; // gray - в стеке

        if (adjacencyList.TryGetValue(current, out var neighbors))
        {
            foreach (var neighbor in neighbors)
            {
                if (color[neighbor] == 1)
                {
                    // Обнаружен цикл - возвращаем путь
                    return new List<string> { current, neighbor };
                }

                if (color[neighbor] == 0)
                {
                    var cycle = DetectCycle(neighbor, color, adjacencyList);
                    if (cycle != null)
                    {
                        cycle.Insert(0, current);
                        return cycle;
                    }
                }
            }
        }

        color[current] = 2; // black - завершена
        return null;
    }

    /// <summary>
    /// Проверяет наличие хотя бы одной стартовой стадии (без входящих переходов).
    /// </summary>
    private bool HasStartStage(List<ApiStageDto> stages)
    {
        // Находим все стадии, которые являются целевыми для переходов
        var targetStages = new HashSet<string>();
        foreach (var stage in stages)
        {
            if (stage.Transitions != null)
            {
                foreach (var transition in stage.Transitions)
                {
                    targetStages.Add(transition.TargetStageName);
                }
            }
        }

        // Стартовая стадия — та, которая не является целевой ни для одного перехода
        var hasStart = stages.Any(s => !targetStages.Contains(s.Name));
        return hasStart;
    }
}
