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

    [Fact]
    public void Validate_ProcessWithSelfCycle_ReturnsValid()
    {
        // Arrange - процесс с циклом на себя, но есть путь к финальной стадии
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

        // Assert - валидно, т.к. есть путь к финальной стадии
        Assert.True(result.IsValid);
    }

    #endregion

    #region Invalid Processes - Missing Target Stage

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

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("стартовую стадию", result.ErrorMessage);
    }

    #endregion

    #region Invalid Processes - Blocking Cycles

    [Fact]
    public void Validate_CycleWithoutExit_ReturnsInvalid()
    {
        // Arrange - цикл без выхода к финальной стадии
        var preset = new ProcessPresetDto
        {
            Name = "test-process",
            DisplayName = "Test Process",
            Description = "Test",
            Workflow = new ApiWorkflowDto
            {
                Stages = new List<ApiStageDto>
                {
                    new() { Name = "Start", Type = StageType.Buffer, Transitions = new List<ApiStageTransitionDto>
                        {
                            new() { TargetStageName = "Loop1", Probability = 1.0 }
                        }
                    },
                    new() { Name = "Loop1", Type = StageType.Buffer, Transitions = new List<ApiStageTransitionDto>
                        {
                            new() { TargetStageName = "Loop2", Probability = 1.0 }
                        }
                    },
                    new() { Name = "Loop2", Type = StageType.Buffer, Transitions = new List<ApiStageTransitionDto>
                        {
                            new() { TargetStageName = "Loop1", Probability = 1.0 } // Цикл без выхода
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
        Assert.Contains("невозможно достичь финальной стадии", result.ErrorMessage);
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
