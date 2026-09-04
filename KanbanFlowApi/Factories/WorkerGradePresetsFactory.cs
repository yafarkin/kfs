using KanbanFlowApi.Dtos.Config;

namespace KanbanFlowApi.Factories;

/// <summary>
/// Фабрика пресетов «грейдов» воркера — готовые наборы Performance/Deviation/CostPerDay
/// по роли (backend/frontend/qa) и уровню (стажёр..лид) для быстрого заполнения полей
/// воркера в редакторе. Значения — стартовая точка, после применения поля остаются
/// редактируемыми вручную.
///
/// Стажёр/джун намеренно сделаны не только медленнее, но и дороже за единицу
/// производительности (CostPerDay / (Performance/100)), чем миддл — чтобы наём джунов
/// не был выигрышной стратегией «быстро и дёшево», плюс у них высокий разброс вверх
/// (DeviationUpPercent) и почти нулевой разброс вниз — они почти никогда не укладываются
/// раньше оценки и часто сильно её превышают.
/// </summary>
public static class WorkerGradePresetsFactory
{
    /// <summary>
    /// Получить список всех доступных пресетов грейдов.
    /// </summary>
    public static List<WorkerGradePresetDto> GetAllPresets()
    {
        return new List<WorkerGradePresetDto>
        {
            Create("backend", "intern", "Backend, стажёр", 35, 5, 120, 65),
            Create("backend", "junior", "Backend, джун", 55, 10, 90, 85),
            Create("backend", "middle", "Backend, миддл", 100, 20, 50, 100),
            Create("backend", "senior", "Backend, сеньор", 130, 25, 30, 150),
            Create("backend", "lead", "Backend, лид", 150, 30, 20, 200),

            Create("frontend", "intern", "Frontend, стажёр", 40, 5, 110, 60),
            Create("frontend", "junior", "Frontend, джун", 60, 10, 85, 80),
            Create("frontend", "middle", "Frontend, миддл", 100, 20, 50, 100),
            Create("frontend", "senior", "Frontend, сеньор", 125, 25, 30, 140),
            Create("frontend", "lead", "Frontend, лид", 145, 30, 20, 180),

            Create("qa", "intern", "QA, стажёр", 30, 5, 130, 55),
            Create("qa", "junior", "QA, джун", 50, 15, 100, 75),
            Create("qa", "middle", "QA, миддл", 100, 30, 40, 100),
            Create("qa", "senior", "QA, сеньор", 120, 35, 25, 140),
            Create("qa", "lead", "QA, лид", 140, 40, 15, 180)
        };
    }

    /// <summary>
    /// Получить пресет по имени.
    /// </summary>
    public static WorkerGradePresetDto? GetPresetByName(string name)
    {
        return GetAllPresets().FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static WorkerGradePresetDto Create(
        string role,
        string grade,
        string displayName,
        double performance,
        double deviationDownPercent,
        double deviationUpPercent,
        int costPerDay)
    {
        return new WorkerGradePresetDto
        {
            Name = $"{role}-{grade}",
            DisplayName = displayName,
            Description = $"Готовый набор Performance/Deviation/CostPerDay для роли «{role}», уровень «{grade}».",
            IsDefault = false,
            Role = role,
            Grade = grade,
            Performance = performance,
            DeviationDownPercent = deviationDownPercent,
            DeviationUpPercent = deviationUpPercent,
            CostPerDay = costPerDay
        };
    }
}
