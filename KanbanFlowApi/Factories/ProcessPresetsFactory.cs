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
    /// Простой процесс: Planning → Todo → Developing → Done (1 стадия работы).
    /// </summary>
    private static ProcessPresetDto CreateSimpleProcess()
    {
        var stages = new List<ApiStageDto>
        {
            new() { Name = "Planning", Type = StageType.Buffer },
            new() { Name = "Todo", Type = StageType.Buffer, IsLeadTimeStart = true, WipLimit = 5 },
            new() { Name = "Developing", Type = StageType.Work, StageProgressPercent = 100, CreatesValue = true, RequiredSkills = ["backend", "frontend"] },
            new() { Name = "Done", Type = StageType.Buffer }
        };

        // Устанавливаем переходы
        stages[0].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Todo", Probability = 1.0 });
        stages[1].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Developing", Probability = 1.0 });
        stages[2].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Done", Probability = 1.0 });

        return new ProcessPresetDto
        {
            Name = "simple-process",
            DisplayName = "Простой процесс",
            Description = "4 стадии: Planning → Todo → Developing → Done. Подходит для обучения.",
            IsDefault = false,
            Workflow = new ApiWorkflowDto { Stages = stages }
        };
    }

    /// <summary>
    /// Kanban Software Development: полный цикл разработки.
    /// </summary>
    private static ProcessPresetDto CreateKanbanSoftware()
    {
        var stages = new List<ApiStageDto>
        {
            new() { Name = "Planning", Type = StageType.Buffer },
            new() { Name = "Todo", Type = StageType.Buffer, IsLeadTimeStart = true, WipLimit = 5 },
            new() { Name = "Developing", Type = StageType.Work, StageProgressPercent = 100, CreatesValue = true, RequiredSkills = ["backend", "frontend"] },
            new() { Name = "Ready for Testing", Type = StageType.Buffer },
            new() { Name = "Testing", Type = StageType.Work, StageProgressPercent = 30, CreatesValue = true, RequiredSkills = ["qa"] },
            new() { Name = "Ready to Merge", Type = StageType.Buffer },
            new() { Name = "Release Preparation", Type = StageType.Work, StageProgressPercent = 10, CreatesValue = false, RequiredSkills = ["backend", "frontend"] },
            new() { Name = "Done", Type = StageType.Buffer }
        };

        // Устанавливаем переходы
        stages[0].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Todo", Probability = 1.0 });
        stages[1].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Developing", Probability = 1.0 });
        stages[2].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Ready for Testing", Probability = 1.0 });
        stages[3].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Testing", Probability = 1.0 });
        stages[4].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Ready to Merge", Probability = 1.0 });
        stages[5].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Release Preparation", Probability = 1.0 });
        stages[6].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Done", Probability = 1.0 });

        return new ProcessPresetDto
        {
            Name = "kanban-software",
            DisplayName = "Kanban Software Dev",
            Description = "8 стадий: Planning → Todo → Developing → Ready for Testing → Testing → Ready to Merge → Release Prep → Done",
            IsDefault = true,
            Workflow = new ApiWorkflowDto { Stages = stages }
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
        stages[9].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Code Review", Probability = 1.0 }); // Ready for Code Review -> Code Review
        stages[10].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Ready for Testing", Probability = 1.0 }); // Code Review -> Ready for Testing
        stages[11].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Testing", Probability = 1.0 });
        stages[12].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Design Review", Probability = 0.1 });
        stages[12].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Waiting for Automation", Probability = 0.3 });
        stages[12].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Ready to Merge", Probability = 0.6 });
        stages[13].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Waiting for Automation", Probability = 1.0 });
        stages[14].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Automation", Probability = 1.0 });
        stages[15].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Ready to Merge", Probability = 1.0 });
        stages[16].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Ready to Release", Probability = 1.0 });
        stages[17].Transitions.Add(new ApiStageTransitionDto { TargetStageName = "Done", Probability = 1.0 });

        return new ProcessPresetDto
        {
            Name = "twork-process",
            DisplayName = "TWork Process",
            Description = "19 стадий: Planning → To Do → полный цикл TWork с Code Review, автоматизацией тестов",
            IsDefault = false,
            Workflow = new ApiWorkflowDto { Stages = stages }
        };
    }
}
