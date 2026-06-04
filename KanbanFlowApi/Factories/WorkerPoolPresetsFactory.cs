using KanbanFlowApi.Dtos.Config;

namespace KanbanFlowApi.Factories;

/// <summary>
/// Фабрика пресетов пулов работников — содержит готовые наборы исполнителей.
/// </summary>
public static class WorkerPoolPresetsFactory
{
    /// <summary>
    /// Получить список всех доступных пресетов работников.
    /// </summary>
    public static List<WorkerPoolPresetDto> GetAllPresets()
    {
        return new List<WorkerPoolPresetDto>
        {
            CreateSoloDeveloper(),
            CreateSmallTeam(),
            CreateTWorkTeam()
        };
    }

    /// <summary>
    /// Получить пресет по имени.
    /// </summary>
    public static WorkerPoolPresetDto? GetPresetByName(string name)
    {
        return GetAllPresets().FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Соло-разработчик: 1 универсал.
    /// </summary>
    private static WorkerPoolPresetDto CreateSoloDeveloper()
    {
        return new WorkerPoolPresetDto
        {
            Name = "solo-developer",
            DisplayName = "Соло-разработчик",
            Description = "1 разработчик (backend + frontend). WIP=1. Для простых задач.",
            IsDefault = false,
            Workers = new List<ApiWorkerDto>
            {
                new()
                {
                    Login = "dev1",
                    Skills = ["backend", "frontend"],
                    WipLimit = 1,
                    Performance = 100,
                    DeviationDownPercent = 20,
                    DeviationUpPercent = 50
                }
            }
        };
    }

    /// <summary>
    /// Маленькая команда: 2 разработчика + 1 QA.
    /// </summary>
    private static WorkerPoolPresetDto CreateSmallTeam()
    {
        return new WorkerPoolPresetDto
        {
            Name = "small-team",
            DisplayName = "Маленькая команда",
            Description = "3 человека: 2 разработчика (BE + FE) + 1 QA. WIP=1 у каждого.",
            IsDefault = true,
            Workers = new List<ApiWorkerDto>
            {
                new()
                {
                    Login = "dev1-be",
                    Skills = ["backend"],
                    WipLimit = 1,
                    Performance = 100,
                    DeviationDownPercent = 20,
                    DeviationUpPercent = 50
                },
                new()
                {
                    Login = "dev2-fe",
                    Skills = ["frontend"],
                    WipLimit = 1,
                    Performance = 100,
                    DeviationDownPercent = 20,
                    DeviationUpPercent = 50
                },
                new()
                {
                    Login = "qa1",
                    Skills = ["qa"],
                    WipLimit = 1,
                    Performance = 100,
                    DeviationDownPercent = 30,
                    DeviationUpPercent = 40
                }
            }
        };
    }

    /// <summary>
    /// TWork команда: 4 backend + 1 frontend + 2 QA.
    /// </summary>
    private static WorkerPoolPresetDto CreateTWorkTeam()
    {
        return new WorkerPoolPresetDto
        {
            Name = "twork-team",
            DisplayName = "TWork Team",
            Description = "7 человек: 4 backend, 1 frontend, 2 QA. WIP=1 у каждого.",
            IsDefault = false,
            Workers = new List<ApiWorkerDto>
            {
                new() { Login = "be-dev-1", Skills = ["backend"], WipLimit = 1, Performance = 100, DeviationDownPercent = 20, DeviationUpPercent = 50 },
                new() { Login = "be-dev-2", Skills = ["backend"], WipLimit = 1, Performance = 100, DeviationDownPercent = 20, DeviationUpPercent = 50 },
                new() { Login = "be-dev-3", Skills = ["backend"], WipLimit = 1, Performance = 100, DeviationDownPercent = 20, DeviationUpPercent = 50 },
                new() { Login = "be-dev-4", Skills = ["backend"], WipLimit = 1, Performance = 100, DeviationDownPercent = 20, DeviationUpPercent = 50 },
                new() { Login = "fe-dev-1", Skills = ["frontend"], WipLimit = 1, Performance = 100, DeviationDownPercent = 20, DeviationUpPercent = 50 },
                new() { Login = "qa-eng-1", Skills = ["qa"], WipLimit = 1, Performance = 100, DeviationDownPercent = 30, DeviationUpPercent = 40 },
                new() { Login = "qa-eng-2", Skills = ["qa"], WipLimit = 1, Performance = 100, DeviationDownPercent = 30, DeviationUpPercent = 40 }
            }
        };
    }
}
