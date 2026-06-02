using KanbanFlowApi.Dtos.Config;
using KanbanFlowSerivce.Enums;

namespace KanbanFlowApi.Factories;

/// <summary>
/// Фабрика пресетов производственных процессов — содержит готовые workflow с задачами.
/// </summary>
public static class ProcessPresetsFactory
{
    /// <summary>
    /// Получить список всех доступных пресетов процессов.
    /// </summary>
    public static List<ProcessPresetDto> GetAllPresets()
    {
        return new List<ProcessPresetDto>
        {
            CreateSimpleProcess(),
            CreateKanbanSoftware(),
            CreateTWorkProcess()
        };
    }

    /// <summary>
    /// Получить пресет по имени.
    /// </summary>
    public static ProcessPresetDto? GetPresetByName(string name)
    {
        return GetAllPresets().FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Простой процесс: Todo → Developing → Done (1 стадия работы).
    /// </summary>
    private static ProcessPresetDto CreateSimpleProcess()
    {
        var stages = new List<ApiStageDto>
        {
            new() { Name = "Todo", Type = StageType.Buffer, IsLeadTimeStart = true },
            new() { Name = "Developing", Type = StageType.Work, StageProgressPercent = 100, CreatesValue = true, RequiredSkills = ["backend", "frontend", "qa"] },
            new() { Name = "Done", Type = StageType.Buffer }
        };

        // Устанавливаем переходы
        stages[0].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Developing", Probability = 1.0 });
        stages[1].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Done", Probability = 1.0 });

        return new ProcessPresetDto
        {
            Name = "simple-process",
            DisplayName = "Простой процесс",
            Description = "3 стадии: Todo → Developing → Done. Подходит для обучения.",
            IsDefault = false,
            Workflow = new ApiWorkflowDto { Stages = stages },
            Tasks = new List<ApiTaskDto>
            {
                new() { Key = "TASK-1", Summary = "Задача S", ShirtType = TShirtType.S, RequiredSkills = ["backend"] },
                new() { Key = "TASK-2", Summary = "Задача M", ShirtType = TShirtType.M, RequiredSkills = ["backend"] }
            }
        };
    }

    /// <summary>
    /// Kanban Software Development: полный цикл разработки.
    /// </summary>
    private static ProcessPresetDto CreateKanbanSoftware()
    {
        var stages = new List<ApiStageDto>
        {
            new() { Name = "Todo", Type = StageType.Buffer, IsLeadTimeStart = true },
            new() { Name = "Developing", Type = StageType.Work, StageProgressPercent = 100, CreatesValue = true, RequiredSkills = ["backend", "frontend"] },
            new() { Name = "Ready for Testing", Type = StageType.Buffer },
            new() { Name = "Testing", Type = StageType.Work, StageProgressPercent = 30, CreatesValue = true, RequiredSkills = ["qa"] },
            new() { Name = "Ready to Merge", Type = StageType.Buffer },
            new() { Name = "Release Preparation", Type = StageType.Work, StageProgressPercent = 10, CreatesValue = false, RequiredSkills = ["backend", "frontend"] },
            new() { Name = "Done", Type = StageType.Buffer }
        };

        // Устанавливаем переходы
        stages[0].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Developing", Probability = 1.0 });
        stages[1].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Ready for Testing", Probability = 1.0 });
        stages[2].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Testing", Probability = 1.0 });
        stages[3].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Ready to Merge", Probability = 1.0 });
        stages[4].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Release Preparation", Probability = 1.0 });
        stages[5].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Done", Probability = 1.0 });

        return new ProcessPresetDto
        {
            Name = "kanban-software",
            DisplayName = "Kanban Software Dev",
            Description = "7 стадий: Todo → Developing → Ready for Testing → Testing → Ready to Merge → Release Prep → Done",
            IsDefault = true,
            Workflow = new ApiWorkflowDto { Stages = stages },
            Tasks = new List<ApiTaskDto>
            {
                new() { Key = "TASK-1", Summary = "[BE] API пользователей", ShirtType = TShirtType.S, RequiredSkills = ["backend", "qa"] },
                new() { Key = "TASK-2", Summary = "[BE] Тесты для сервиса", ShirtType = TShirtType.M, RequiredSkills = ["backend", "qa"] },
                new() { Key = "TASK-3", Summary = "[FE] UI компонент", ShirtType = TShirtType.S, RequiredSkills = ["frontend"] },
                new() { Key = "TASK-4", Summary = "[QA] Автотесты API", ShirtType = TShirtType.M, RequiredSkills = ["qa"] }
            }
        };
    }

    /// <summary>
    /// TWork процесс: расширенный workflow с Code Review и автоматизацией.
    /// </summary>
    private static ProcessPresetDto CreateTWorkProcess()
    {
        var stages = new List<ApiStageDto>
        {
            new() { Name = "Planning", Type = StageType.Buffer },
            new() { Name = "To Do", Type = StageType.Buffer, IsLeadTimeStart = true, WipLimit = 5 },
            new() { Name = "Technical Specification", Type = StageType.Work, StageProgressPercent = 25, RequiredSkills = ["backend"] },
            new() { Name = "Waiting for Approval", Type = StageType.Buffer },
            new() { Name = "Technical Review", Type = StageType.Work, StageProgressPercent = 15, WipLimit = 2, RequiredSkills = ["backend"] },
            new() { Name = "Waiting for Test Specification", Type = StageType.Buffer },
            new() { Name = "Test Specification", Type = StageType.Work, StageProgressPercent = 20, WipLimit = 2, RequiredSkills = ["qa"] },
            new() { Name = "Ready to Develop", Type = StageType.Buffer },
            new() { Name = "Developing", Type = StageType.Work, StageProgressPercent = 100, WipLimit = 4, CreatesValue = true, RequiredSkills = ["backend", "frontend"] },
            new() { Name = "Ready for Code Review", Type = StageType.Buffer },
            new() { Name = "Code Review", Type = StageType.Work, StageProgressPercent = 50, WipLimit = 4, RequiredSkills = ["backend"] },
            new() { Name = "Ready for Testing", Type = StageType.Buffer, WipLimit = 8 },
            new() { Name = "Testing", Type = StageType.Work, StageProgressPercent = 30, WipLimit = 2, CreatesValue = true, RequiredSkills = ["qa"] },
            new() { Name = "Design Review", Type = StageType.Work, StageProgressPercent = 10, WipLimit = 2, RequiredSkills = ["frontend"] },
            new() { Name = "Waiting for Automation", Type = StageType.Buffer },
            new() { Name = "Automation", Type = StageType.Work, StageProgressPercent = 40, WipLimit = 2, CreatesValue = true, RequiredSkills = ["qa"] },
            new() { Name = "Ready to Merge", Type = StageType.Buffer },
            new() { Name = "Ready to Release", Type = StageType.Work, StageProgressPercent = 5, WipLimit = 5, RequiredSkills = ["backend", "frontend"] },
            new() { Name = "Done", Type = StageType.Buffer }
        };

        // Устанавливаем переходы (линейный поток с ветвлением)
        stages[0].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "To Do", Probability = 1.0 }); // Planning -> To Do
        stages[1].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Technical Specification", Probability = 1.0 }); // To Do -> Tech Spec
        stages[2].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Waiting for Approval", Probability = 1.0 });
        stages[3].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Technical Review", Probability = 1.0 });
        stages[4].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Waiting for Test Specification", Probability = 1.0 });
        stages[5].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Test Specification", Probability = 1.0 });
        stages[6].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Ready to Develop", Probability = 1.0 });
        stages[7].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Developing", Probability = 1.0 });
        stages[8].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Ready for Code Review", Probability = 1.0 });
        stages[9].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Ready for Testing", Probability = 1.0 });
        stages[10].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Ready for Testing", Probability = 1.0 });
        stages[11].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Testing", Probability = 1.0 });
        stages[12].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Design Review", Probability = 0.1 });
        stages[12].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Waiting for Automation", Probability = 0.3 });
        stages[12].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Ready to Merge", Probability = 0.6 });
        stages[13].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Waiting for Automation", Probability = 1.0 });
        stages[14].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Automation", Probability = 1.0 });
        stages[15].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Ready to Merge", Probability = 1.0 });
        stages[16].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Ready to Release", Probability = 1.0 });
        stages[17].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Done", Probability = 1.0 });

        var tasks = new List<ApiTaskDto>
        {
            new() { Key = "BE-1", Summary = "[BE] Модель пользователя", ShirtType = TShirtType.S, RequiredSkills = ["backend", "qa"] },
            new() { Key = "BE-2", Summary = "[BE] API списка пользователей", ShirtType = TShirtType.S, RequiredSkills = ["backend", "qa"] },
            new() { Key = "BE-3", Summary = "[BE] API создания пользователя", ShirtType = TShirtType.S, RequiredSkills = ["backend", "qa"] },
            new() { Key = "FE-1", Summary = "[FE] Форма регистрации", ShirtType = TShirtType.S, RequiredSkills = ["frontend", "qa"] },
            new() { Key = "FE-2", Summary = "[FE] Форма входа", ShirtType = TShirtType.S, RequiredSkills = ["frontend", "qa"] },
            new() { Key = "FE-3", Summary = "[FE] Страница профиля", ShirtType = TShirtType.M, RequiredSkills = ["frontend", "qa"] }
        };

        return new ProcessPresetDto
        {
            Name = "twork-process",
            DisplayName = "TWork Process",
            Description = "19 стадий: полный цикл TWork с Code Review, автоматизацией тестов",
            IsDefault = false,
            Workflow = new ApiWorkflowDto { Stages = stages },
            Tasks = tasks
        };
    }
}
