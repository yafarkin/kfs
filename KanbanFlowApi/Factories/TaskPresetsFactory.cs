using KanbanFlowApi.Dtos.Config;
using KanbanFlowSerivce.Enums;

namespace KanbanFlowApi.Factories;

/// <summary>
/// Фабрика пресетов задач — содержит готовые наборы задач для симуляции.
/// </summary>
public static class TaskPresetsFactory
{
    /// <summary>
    /// Получить список всех доступных пресетов задач.
    /// </summary>
    public static List<TaskPresetDto> GetAllPresets()
    {
        return new List<TaskPresetDto>
        {
            CreateQuickSprint(),
            CreateStandardSprint(),
            CreateLargeBacklog()
        };
    }

    /// <summary>
    /// Получить пресет по имени.
    /// </summary>
    public static TaskPresetDto? GetPresetByName(string name)
    {
        return GetAllPresets().FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Быстрый спринт: 4 задачи (2 S, 2 M) - только backend и frontend.
    /// </summary>
    private static TaskPresetDto CreateQuickSprint()
    {
        return new TaskPresetDto
        {
            Name = "quick-sprint",
            DisplayName = "Быстрый спринт",
            Description = "4 задачи: 2 размера S, 2 размера M. Для быстрой симуляции.",
            IsDefault = false,
            Tasks = new List<ApiTaskDto>
            {
                new() { Key = "TASK-1", Summary = "Задача S #1", ShirtType = TShirtType.S, RequiredSkills = ["backend"] },
                new() { Key = "TASK-2", Summary = "Задача M #1", ShirtType = TShirtType.M, RequiredSkills = ["backend"] },
                new() { Key = "TASK-3", Summary = "Задача S #2", ShirtType = TShirtType.S, RequiredSkills = ["frontend"] },
                new() { Key = "TASK-4", Summary = "Задача M #2", ShirtType = TShirtType.M, RequiredSkills = ["frontend"] }
            }
        };
    }

    /// <summary>
    /// Стандартный спринт: 8 задач разного размера - только backend и frontend.
    /// </summary>
    private static TaskPresetDto CreateStandardSprint()
    {
        return new TaskPresetDto
        {
            Name = "standard-sprint",
            DisplayName = "Стандартный спринт",
            Description = "8 задач: 2 XS, 3 S, 3 M. Для стандартной симуляции.",
            IsDefault = true,
            Tasks = new List<ApiTaskDto>
            {
                new() { Key = "TASK-1", Summary = "XS задача #1", ShirtType = TShirtType.XS, RequiredSkills = ["backend"] },
                new() { Key = "TASK-2", Summary = "S задача #1", ShirtType = TShirtType.S, RequiredSkills = ["backend"] },
                new() { Key = "TASK-3", Summary = "S задача #2", ShirtType = TShirtType.S, RequiredSkills = ["frontend"] },
                new() { Key = "TASK-4", Summary = "M задача #1", ShirtType = TShirtType.M, RequiredSkills = ["backend"] },
                new() { Key = "TASK-5", Summary = "XS задача #2", ShirtType = TShirtType.XS, RequiredSkills = ["frontend"] },
                new() { Key = "TASK-6", Summary = "S задача #3", ShirtType = TShirtType.S, RequiredSkills = ["frontend"] },
                new() { Key = "TASK-7", Summary = "M задача #2", ShirtType = TShirtType.M, RequiredSkills = ["backend"] },
                new() { Key = "TASK-8", Summary = "XS задача #3", ShirtType = TShirtType.XS, RequiredSkills = ["backend"] }
            }
        };
    }

    /// <summary>
    /// Большой бэклог: 14 задач для длительной симуляции - только backend и frontend.
    /// </summary>
    private static TaskPresetDto CreateLargeBacklog()
    {
        var tasks = new List<ApiTaskDto>();

        // Backend задачи (8 шт)
        for (int i = 1; i <= 8; i++)
        {
            tasks.Add(new()
            {
                Key = $"BE-{i}",
                Summary = $"[BE] Backend задача #{i}",
                ShirtType = i % 3 == 0 ? TShirtType.M : TShirtType.S,
                RequiredSkills = ["backend", "qa"]
            });
        }

        // Frontend задачи (6 шт)
        for (int i = 1; i <= 6; i++)
        {
            tasks.Add(new()
            {
                Key = $"FE-{i}",
                Summary = $"[FE] Frontend задача #{i}",
                ShirtType = i % 2 == 0 ? TShirtType.M : TShirtType.S,
                RequiredSkills = ["frontend", "qa"]
            });
        }

        return new TaskPresetDto
        {
            Name = "large-backlog",
            DisplayName = "Большой бэклог",
            Description = "14 задач: 8 BE, 6 FE. Для длительной симуляции.",
            IsDefault = false,
            Tasks = tasks
        };
    }
}
