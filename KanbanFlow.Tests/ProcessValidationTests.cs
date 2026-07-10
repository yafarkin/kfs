using KanbanFlowApi.Dtos.Config;
using KanbanFlowApi.Services;
using KanbanFlowSerivce.Enums;
using Xunit;

namespace KanbanFlow.Tests;

/// <summary>
/// Тесты для ProcessValidationService.
/// </summary>
public class ProcessValidationTests
{
    private readonly ProcessValidationService _validationService;

    public ProcessValidationTests()
    {
        _validationService = new ProcessValidationService();
    }

    #region Valid Processes

    [Fact]
    public void Validate_SimpleProcess_ReturnsValid()
    {
        // Arrange
        var preset = CreateSimpleProcess();

        // Act
        var result = _validationService.Validate(preset);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Validate_ProcessWithMultipleTransitions_ReturnsValid()
    {
        // Arrange
        var preset = new ProcessPresetDto
        {
            Name = "test-process",
            DisplayName = "Test Process",
            Description = "Test",
            Workflow = new ApiWorkflowDto
            {
                Stages = new List<ApiStageDto>
                {
                    new() { Name = "Todo", Type = StageType.Buffer, Transitions = new List<ApiStageTransitionDto>
                        {
                            new() { TargetStageName = "Developing", Probability = 0.8 },
                            new() { TargetStageName = "Done", Probability = 0.2 }
                        }
                    },
                    new() { Name = "Developing", Type = StageType.Work, StageProgressPercent = 100, Transitions = new List<ApiStageTransitionDto>
                        {
                            new() { TargetStageName = "Done", Probability = 1.0 }
                        }
                    },
                    new() { Name = "Done", Type = StageType.Buffer, Transitions = new List<ApiStageTransitionDto>() }
                }
            },
            Tasks = new List<ApiTaskDto>
            {
                new() { Key = "TASK-1", Summary = "Test", ShirtType = TShirtType.S, RequiredSkills = new List<string> { "backend" } }
            }
        };

        // Act
        var result = _validationService.Validate(preset);

        // Assert
        Assert.True(result.IsValid);
    }

    #endregion

    #region Invalid Processes - Cycles

    [Fact]
    public void Validate_SelfLoop_ReturnsInvalid()
    {
        // Arrange - процесс с циклом на себя
        var preset = new ProcessPresetDto
        {
            Name = "test-process",
            DisplayName = "Test Process",
            Description = "Test",
            Workflow = new ApiWorkflowDto
            {
                Stages = new List<ApiStageDto>
                {
                    new() { Name = "Todo", Type = StageType.Buffer, Transitions = new List<ApiStageTransitionDto>
                        {
                            new() { TargetStageName = "Todo", Probability = 0.1 }, // Цикл на себя
                            new() { TargetStageName = "Developing", Probability = 0.9 }
                        }
                    },
                    new() { Name = "Developing", Type = StageType.Work, StageProgressPercent = 100, Transitions = new List<ApiStageTransitionDto>
                        {
                            new() { TargetStageName = "Done", Probability = 1.0 }
                        }
                    },
                    new() { Name = "Done", Type = StageType.Buffer, Transitions = new List<ApiStageTransitionDto>() }
                }
            },
            Tasks = new List<ApiTaskDto>
            {
                new() { Key = "TASK-1", Summary = "Test", ShirtType = TShirtType.S, RequiredSkills = new List<string> { "backend" } }
            }
        };

        // Act
        var result = _validationService.Validate(preset);

        // Assert - self-loop недопустим
        Assert.False(result.IsValid);
        Assert.Contains("Цикл на себя", result.ErrorMessage);
        Assert.Contains("self-loop", result.ErrorMessage);
    }

    [Fact]
    public void Validate_Cycle_ReturnsInvalid()
    {
        // Arrange - цикл между стадиями
        var preset = new ProcessPresetDto
        {
            Name = "test-process",
            DisplayName = "Test Process",
            Description = "Test",
            Workflow = new ApiWorkflowDto
            {
                Stages = new List<ApiStageDto>
                {
                    new() { Name = "A", Type = StageType.Buffer, Transitions = new List<ApiStageTransitionDto>
                        {
                            new() { TargetStageName = "B", Probability = 1.0 }
                        }
                    },
                    new() { Name = "B", Type = StageType.Buffer, Transitions = new List<ApiStageTransitionDto>
                        {
                            new() { TargetStageName = "C", Probability = 1.0 }
                        }
                    },
                    new() { Name = "C", Type = StageType.Buffer, Transitions = new List<ApiStageTransitionDto>
                        {
                            new() { TargetStageName = "A", Probability = 1.0 } // Цикл A -> B -> C -> A
                        }
                    }
                }
            },
            Tasks = new List<ApiTaskDto>
            {
                new() { Key = "TASK-1", Summary = "Test", ShirtType = TShirtType.S, RequiredSkills = new List<string> { "backend" } }
            }
        };

        // Act
        var result = _validationService.Validate(preset);

        // Assert - цикл недопустим
        Assert.False(result.IsValid);
        Assert.Contains("цикл", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_TransitionToNonExistentStage_ReturnsInvalid()
    {
        // Arrange
        var preset = new ProcessPresetDto
        {
            Name = "test-process",
            DisplayName = "Test Process",
            Description = "Test",
            Workflow = new ApiWorkflowDto
            {
                Stages = new List<ApiStageDto>
                {
                    new() { Name = "Todo", Type = StageType.Buffer, Transitions = new List<ApiStageTransitionDto>
                        {
                            new() { TargetStageName = "NonExistent", Probability = 1.0 }
                        }
                    }
                }
            },
            Tasks = new List<ApiTaskDto>
            {
                new() { Key = "TASK-1", Summary = "Test", ShirtType = TShirtType.S, RequiredSkills = new List<string> { "backend" } }
            }
        };

        // Act
        var result = _validationService.Validate(preset);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("NonExistent", result.ErrorMessage);
        Assert.Contains("не существует", result.ErrorMessage);
    }

    #endregion

    #region Invalid Processes - No Start Stage

    [Fact]
    public void Validate_AllStagesHaveIncomingTransitions_ReturnsInvalid()
    {
        // Arrange - все стадии имеют входящие переходы, нет стартовой
        // A -> B -> A это цикл, который должен быть обнаружен
        var preset = new ProcessPresetDto
        {
            Name = "test-process",
            DisplayName = "Test Process",
            Description = "Test",
            Workflow = new ApiWorkflowDto
            {
                Stages = new List<ApiStageDto>
                {
                    new() { Name = "A", Type = StageType.Buffer, Transitions = new List<ApiStageTransitionDto>
                        {
                            new() { TargetStageName = "B", Probability = 1.0 }
                        }
                    },
                    new() { Name = "B", Type = StageType.Buffer, Transitions = new List<ApiStageTransitionDto>
                        {
                            new() { TargetStageName = "A", Probability = 1.0 }
                        }
                    }
                }
            },
            Tasks = new List<ApiTaskDto>
            {
                new() { Key = "TASK-1", Summary = "Test", ShirtType = TShirtType.S, RequiredSkills = new List<string> { "backend" } }
            }
        };

        // Act
        var result = _validationService.Validate(preset);

        // Assert - цикл должен быть обнаружен
        Assert.False(result.IsValid);
        Assert.Contains("цикл", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Invalid Processes - Probability Sum

    [Fact]
    public void Validate_ProbabilitySumExceeds1_ReturnsInvalid()
    {
        // Arrange
        var preset = new ProcessPresetDto
        {
            Name = "test-process",
            DisplayName = "Test Process",
            Description = "Test",
            Workflow = new ApiWorkflowDto
            {
                Stages = new List<ApiStageDto>
                {
                    new() { Name = "Todo", Type = StageType.Buffer, Transitions = new List<ApiStageTransitionDto>
                        {
                            new() { TargetStageName = "Developing", Probability = 0.6 },
                            new() { TargetStageName = "Done", Probability = 0.6 } // Сумма = 1.2
                        }
                    },
                    new() { Name = "Developing", Type = StageType.Work, StageProgressPercent = 100, Transitions = new List<ApiStageTransitionDto>
                        {
                            new() { TargetStageName = "Done", Probability = 1.0 }
                        }
                    },
                    new() { Name = "Done", Type = StageType.Buffer, Transitions = new List<ApiStageTransitionDto>() }
                }
            },
            Tasks = new List<ApiTaskDto>
            {
                new() { Key = "TASK-1", Summary = "Test", ShirtType = TShirtType.S, RequiredSkills = new List<string> { "backend" } }
            }
        };

        // Act
        var result = _validationService.Validate(preset);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Сумма вероятностей", result.ErrorMessage);
        Assert.Contains("1.0", result.ErrorMessage);
    }

    #endregion

    #region Invalid Processes - Invalid Probability

    [Fact]
    public void Validate_NegativeProbability_ReturnsInvalid()
    {
        // Arrange
        var preset = new ProcessPresetDto
        {
            Name = "test-process",
            DisplayName = "Test Process",
            Description = "Test",
            Workflow = new ApiWorkflowDto
            {
                Stages = new List<ApiStageDto>
                {
                    new() { Name = "Todo", Type = StageType.Buffer, Transitions = new List<ApiStageTransitionDto>
                        {
                            new() { TargetStageName = "Done", Probability = -0.1 }
                        }
                    },
                    new() { Name = "Done", Type = StageType.Buffer, Transitions = new List<ApiStageTransitionDto>() }
                }
            },
            Tasks = new List<ApiTaskDto>
            {
                new() { Key = "TASK-1", Summary = "Test", ShirtType = TShirtType.S, RequiredSkills = new List<string> { "backend" } }
            }
        };

        // Act
        var result = _validationService.Validate(preset);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Вероятность перехода", result.ErrorMessage);
    }

    [Fact]
    public void Validate_ProbabilityGreaterThan1_ReturnsInvalid()
    {
        // Arrange
        var preset = new ProcessPresetDto
        {
            Name = "test-process",
            DisplayName = "Test Process",
            Description = "Test",
            Workflow = new ApiWorkflowDto
            {
                Stages = new List<ApiStageDto>
                {
                    new() { Name = "Todo", Type = StageType.Buffer, Transitions = new List<ApiStageTransitionDto>
                        {
                            new() { TargetStageName = "Done", Probability = 1.5 }
                        }
                    },
                    new() { Name = "Done", Type = StageType.Buffer, Transitions = new List<ApiStageTransitionDto>() }
                }
            },
            Tasks = new List<ApiTaskDto>
            {
                new() { Key = "TASK-1", Summary = "Test", ShirtType = TShirtType.S, RequiredSkills = new List<string> { "backend" } }
            }
        };

        // Act
        var result = _validationService.Validate(preset);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Вероятность перехода", result.ErrorMessage);
    }

    #endregion

    #region Helper Methods

    private static ProcessPresetDto CreateSimpleProcess()
    {
        return new ProcessPresetDto
        {
            Name = "simple-process",
            DisplayName = "Simple Process",
            Description = "Test",
            Workflow = new ApiWorkflowDto
            {
                Stages = new List<ApiStageDto>
                {
                    new() { Name = "Todo", Type = StageType.Buffer, IsLeadTimeStart = true, Transitions = new List<ApiStageTransitionDto>
                        {
                            new() { TargetStageName = "Developing", Probability = 1.0 }
                        }
                    },
                    new() { Name = "Developing", Type = StageType.Work, StageProgressPercent = 100, RequiredSkills = new List<string> { "backend" }, Transitions = new List<ApiStageTransitionDto>
                        {
                            new() { TargetStageName = "Done", Probability = 1.0 }
                        }
                    },
                    new() { Name = "Done", Type = StageType.Buffer, Transitions = new List<ApiStageTransitionDto>() }
                }
            },
            Tasks = new List<ApiTaskDto>
            {
                new() { Key = "TASK-1", Summary = "Test Task", ShirtType = TShirtType.S, RequiredSkills = new List<string> { "backend" } }
            },
            IsDefault = false
        };
    }

    #endregion
}
