using KanbanFlowApi.Dtos;
using KanbanFlowApi.Dtos.Board;
using KanbanFlowApi.Dtos.Config;
using KanbanFlowApi.Dtos.History;
using KanbanFlowApi.Mappers;
using KanbanFlowConsole.Dtos.Config;
using KanbanFlowConsole.Enums;
using Stage = KanbanFlowConsole.Dtos.Config.Stage;
using Task = KanbanFlowConsole.Dtos.Config.Task;

namespace KanbanFlow.Tests;

public class ApiMapperTests
{
    [Fact]
    public void ToApiDto_ConvertsDomainConfig_ToApiDto()
    {
        // Arrange
        var domainConfig = CreateDomainConfig();

        // Act
        var apiDto = ApiMapper.ToApiDto(domainConfig);

        // Assert
        Assert.NotNull(apiDto);
        Assert.Equal(42, apiDto.Seed);
        Assert.Equal(2, apiDto.Workers.Count);
        Assert.Equal(2, apiDto.Tasks.Count);
        Assert.Equal(3, apiDto.Workflow.Stages.Count);
    }

    [Fact]
    public void ToApiDto_Workers_MappedCorrectly()
    {
        // Arrange
        var domainConfig = CreateDomainConfig();

        // Act
        var apiDto = ApiMapper.ToApiDto(domainConfig);

        // Assert
        var devWorker = Assert.Single(apiDto.Workers, w => w.Login == "dev1");
        Assert.Contains("backend", devWorker.Skills);
        Assert.Equal(100, devWorker.Performance);
    }

    [Fact]
    public void ToApiDto_Stages_MappedCorrectly()
    {
        // Arrange
        var domainConfig = CreateDomainConfig();

        // Act
        var apiDto = ApiMapper.ToApiDto(domainConfig);

        // Assert
        var todoStage = Assert.Single(apiDto.Workflow.Stages, s => s.Name == "Todo");
        Assert.Equal(StageType.Buffer, todoStage.Type);
        Assert.True(todoStage.IsStart);
        var todoTransition = Assert.Single(todoStage.Transitions);
        Assert.Equal("Developing", todoTransition.TargetStageName);
        Assert.Equal(1.0, todoTransition.Probability);

        var developingStage = Assert.Single(apiDto.Workflow.Stages, s => s.Name == "Developing");
        Assert.Equal(StageType.Work, developingStage.Type);
        Assert.False(developingStage.IsStart);
        var devTransition = Assert.Single(developingStage.Transitions);
        Assert.Equal("Done", devTransition.TargetStageName);
        Assert.Equal(1.0, devTransition.Probability);
    }

    [Fact]
    public void ToApiDto_Tasks_MappedCorrectly()
    {
        // Arrange
        var domainConfig = CreateDomainConfig();

        // Act
        var apiDto = ApiMapper.ToApiDto(domainConfig);

        // Assert
        var task1 = Assert.Single(apiDto.Tasks, t => t.Key == "TASK-1");
        Assert.Equal("Implement feature", task1.Summary);
        Assert.Equal(TShirtType.L, task1.ShirtType);
    }

    [Fact]
    public void ToDomainConfig_ConvertsApiDto_ToDomainConfig()
    {
        // Arrange
        var apiDto = CreateApiDto();

        // Act
        var domainConfig = ApiMapper.ToDomainConfig(apiDto);

        // Assert
        Assert.NotNull(domainConfig);
        Assert.Equal(42, domainConfig.Seed);
        Assert.Equal(2, domainConfig.Workers.Count);
        Assert.Equal(2, domainConfig.Tasks.Count);
        Assert.Equal(3, domainConfig.Workflow.Stages.Count);
    }

    [Fact]
    public void ToDomainConfig_Workers_MappedCorrectly()
    {
        // Arrange
        var apiDto = CreateApiDto();

        // Act
        var domainConfig = ApiMapper.ToDomainConfig(apiDto);

        // Assert
        var devWorker = Assert.Single(domainConfig.Workers, w => w.Login == "dev1");
        Assert.Contains("backend", devWorker.Skills);
        Assert.Equal(100, devWorker.Performance);
    }

    [Fact]
    public void ToDomainConfig_Stages_MappedCorrectly()
    {
        // Arrange
        var apiDto = CreateApiDto();

        // Act
        var domainConfig = ApiMapper.ToDomainConfig(apiDto);

        // Assert
        var todoStage = Assert.Single(domainConfig.Workflow.Stages, s => s.Name == "Todo");
        Assert.Equal(StageType.Buffer, todoStage.Type);
        Assert.True(todoStage.IsStart);
        Assert.Single(todoStage.Transitions, t => t.Stage.Name == "Developing");

        var developingStage = Assert.Single(domainConfig.Workflow.Stages, s => s.Name == "Developing");
        Assert.Equal(StageType.Work, developingStage.Type);
        Assert.False(developingStage.IsStart);
        Assert.Single(developingStage.Transitions, t => t.Stage.Name == "Done");
    }

    [Fact]
    public void ToDomainConfig_Transitions_ReferenceCorrectStages()
    {
        // Arrange
        var apiDto = CreateApiDto();

        // Act
        var domainConfig = ApiMapper.ToDomainConfig(apiDto);

        // Assert - проверяем что переходы ссылаются на правильные объекты стадий
        var todoStage = domainConfig.Workflow.Stages.First(s => s.Name == "Todo");
        var developingStage = domainConfig.Workflow.Stages.First(s => s.Name == "Developing");
        var doneStage = domainConfig.Workflow.Stages.First(s => s.Name == "Done");

        // Todo -> Developing
        var todoTransition = Assert.Single(todoStage.Transitions);
        Assert.Same(developingStage, todoTransition.Stage);

        // Developing -> Done
        var developingTransition = Assert.Single(developingStage.Transitions);
        Assert.Same(doneStage, developingTransition.Stage);

        // Done - нет переходов
        Assert.Empty(doneStage.Transitions);
    }

    [Fact]
    public void ToDomainConfig_Tasks_MappedCorrectly()
    {
        // Arrange
        var apiDto = CreateApiDto();

        // Act
        var domainConfig = ApiMapper.ToDomainConfig(apiDto);

        // Assert
        var task1 = Assert.Single(domainConfig.Tasks, t => t.Key == "TASK-1");
        Assert.Equal("Implement feature", task1.Summary);
        Assert.Equal(TShirtType.L, task1.ShirtType);
    }

    [Fact]
    public void RoundTrip_DomainToApiToDomain_PreservesData()
    {
        // Arrange
        var originalDomain = CreateDomainConfig();

        // Act - Domain -> API -> Domain
        var apiDto = ApiMapper.ToApiDto(originalDomain);
        var roundTripDomain = ApiMapper.ToDomainConfig(apiDto);

        // Assert
        Assert.Equal(originalDomain.Seed, roundTripDomain.Seed);
        Assert.Equal(originalDomain.Workers.Count, roundTripDomain.Workers.Count);
        Assert.Equal(originalDomain.Tasks.Count, roundTripDomain.Tasks.Count);
        Assert.Equal(originalDomain.Workflow.Stages.Count, roundTripDomain.Workflow.Stages.Count);

        // Проверяем воркеров
        foreach (var originalWorker in originalDomain.Workers)
        {
            var roundTripWorker = Assert.Single(
                roundTripDomain.Workers,
                w => w.Login == originalWorker.Login);
            Assert.Equal(originalWorker.Skills, roundTripWorker.Skills);
            Assert.Equal(originalWorker.Performance, roundTripWorker.Performance);
        }

        // Проверяем задачи
        foreach (var originalTask in originalDomain.Tasks)
        {
            var roundTripTask = Assert.Single(
                roundTripDomain.Tasks,
                t => t.Key == originalTask.Key);
            Assert.Equal(originalTask.Summary, roundTripTask.Summary);
            Assert.Equal(originalTask.ShirtType, roundTripTask.ShirtType);
        }

        // Проверяем стадии и переходы
        foreach (var originalStage in originalDomain.Workflow.Stages)
        {
            var roundTripStage = Assert.Single(
                roundTripDomain.Workflow.Stages,
                s => s.Name == originalStage.Name);
            Assert.Equal(originalStage.Type, roundTripStage.Type);
            Assert.Equal(originalStage.Transitions.Count, roundTripStage.Transitions.Count);

            // Проверяем имена целевых стадий переходов
            var originalTargetNames = originalStage.Transitions.Select(t => t.Stage.Name).OrderBy(n => n);
            var roundTripTargetNames = roundTripStage.Transitions.Select(t => t.Stage.Name).OrderBy(n => n);
            Assert.Equal(originalTargetNames, roundTripTargetNames);
        }
    }

    [Fact]
    public void RoundTrip_ApiToDomainToApi_PreservesData()
    {
        // Arrange
        var originalApi = CreateApiDto();

        // Act - API -> Domain -> API
        var domain = ApiMapper.ToDomainConfig(originalApi);
        var roundTripApi = ApiMapper.ToApiDto(domain);

        // Assert
        Assert.Equal(originalApi.Seed, roundTripApi.Seed);
        Assert.Equal(originalApi.Workers.Count, roundTripApi.Workers.Count);
        Assert.Equal(originalApi.Tasks.Count, roundTripApi.Tasks.Count);
        Assert.Equal(originalApi.Workflow.Stages.Count, roundTripApi.Workflow.Stages.Count);

        // Проверяем стадии и Transitions
        foreach (var originalStage in originalApi.Workflow.Stages)
        {
            var roundTripStage = Assert.Single(
                roundTripApi.Workflow.Stages,
                s => s.Name == originalStage.Name);
            Assert.Equal(originalStage.Type, roundTripStage.Type);
            Assert.Equal(originalStage.Transitions.Count, roundTripStage.Transitions.Count);

            var originalNames = originalStage.Transitions.Select(t => t.TargetStageName).OrderBy(n => n);
            var roundTripNames = roundTripStage.Transitions.Select(t => t.TargetStageName).OrderBy(n => n);
            Assert.Equal(originalNames, roundTripNames);
        }
    }

    [Fact]
    public void ToDomainConfig_EmptyTransitions_CreatesEmptyTransitions()
    {
        // Arrange
        var apiDto = new ApiSimulationConfigDto
        {
            Seed = 1,
            Workers = new List<ApiWorkerDto>(),
            Workflow = new ApiWorkflowDto
            {
                Stages = new List<ApiStageDto>
                {
                    new()
                    {
                        Name = "Done",
                        Type = StageType.Buffer,
                        IsStart = false,
                        Transitions = new List<ApiStageTransitionDto>() // Пустой список
                    }
                }
            },
            Tasks = new List<ApiTaskDto>()
        };

        // Act
        var domainConfig = ApiMapper.ToDomainConfig(apiDto);

        // Assert
        var doneStage = Assert.Single(domainConfig.Workflow.Stages);
        Assert.Empty(doneStage.Transitions);
    }

    [Fact]
    public void ToApiDto_NullAllowedRoles_MappedToEmptyList()
    {
        // Arrange
        var domainConfig = CreateDomainConfig();

        // Act
        var apiDto = ApiMapper.ToApiDto(domainConfig);

        // Assert
        foreach (var stage in apiDto.Workflow.Stages)
        {
            Assert.NotNull(stage.AllowedRoles);
        }
    }

    private static SimulationConfig CreateDomainConfig()
    {
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            IsStart = true,
            IsLeadTimeStart = true,
            
            Transitions = new List<StageTransition>()
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            IsStart = false,
            IsLeadTimeStart = false,
            StageProgressPercent = 100,
            Transitions = new List<StageTransition>()
        };

        var done = new Stage
        {
            Name = "Done",
            Type = StageType.Buffer,
            IsStart = false,
            IsLeadTimeStart = false,
            
            Transitions = new List<StageTransition>()
        };

        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = new List<Worker>
            {
                new() { Login = "dev1", Skills = ["backend"], Performance = 100 },
                new() { Login = "qa1", Skills = ["qa"], Performance = 100 }
            },
            Workflow = new Workflow
            {
                Stages = new List<Stage> { todo, developing, done }
            },
            Tasks = new List<Task>
            {
                new() { Key = "TASK-1", Summary = "Implement feature", ShirtType = TShirtType.L, RequiredSkills = ["backend"] },
                new() { Key = "TASK-2", Summary = "Write tests", ShirtType = TShirtType.M, RequiredSkills = ["backend"] }
            }
        };
    }

    private static ApiSimulationConfigDto CreateApiDto()
    {
        return new ApiSimulationConfigDto
        {
            Seed = 42,
            Workers = new List<ApiWorkerDto>
            {
                new() { Login = "dev1", Skills = ["backend"], Performance = 100 },
                new() { Login = "qa1", Skills = ["qa"], Performance = 100 }
            },
            Workflow = new ApiWorkflowDto
            {
                Stages = new List<ApiStageDto>
                {
                    new()
                    {
                        Name = "Todo",
                        Type = StageType.Buffer,
                        IsStart = true,
                        IsLeadTimeStart = true,
                        RequiredSkills = new List<string>(),
                        Transitions = new List<ApiStageTransitionDto>
                        {
                            new() { TargetStageName = "Developing", Probability = 1.0 }
                        }
                    },
                    new()
                    {
                        Name = "Developing",
                        Type = StageType.Work,
                        IsStart = false,
                        IsLeadTimeStart = false,
                        RequiredSkills = new List<string> { "backend" },
                        StageProgressPercent = 100,
                        Transitions = new List<ApiStageTransitionDto>
                        {
                            new() { TargetStageName = "Done", Probability = 1.0 }
                        }
                    },
                    new()
                    {
                        Name = "Done",
                        Type = StageType.Buffer,
                        IsStart = false,
                        IsLeadTimeStart = false,
                        RequiredSkills = new List<string>(),
                        Transitions = new List<ApiStageTransitionDto>()
                    }
                }
            },
            Tasks = new List<ApiTaskDto>
            {
                new() { Key = "TASK-1", Summary = "Implement feature", ShirtType = TShirtType.L, RequiredSkills = new List<string> { "backend" } },
                new() { Key = "TASK-2", Summary = "Write tests", ShirtType = TShirtType.M, RequiredSkills = new List<string> { "backend" } }
            }
        };
    }

    #region Simulation State Tests

    [Fact]
    public void ToApiDto_Simulation_ReturnsFullState()
    {
        // Arrange
        var config = CreateDomainConfig();
        var simulation = new KanbanFlowConsole.Dtos.Simulation();
        simulation.InitFromConfig(config);
        simulation.StartNewDay();
        simulation.AdvanceTick(24);

        // Act
        var apiState = ApiMapper.ToApiDto(simulation);

        // Assert
        Assert.NotNull(apiState);
        Assert.NotNull(apiState.Config);
        Assert.NotNull(apiState.Board);
        Assert.NotNull(apiState.History);
        Assert.Equal(1, apiState.CurrentDay);
        Assert.Equal(24, apiState.CurrentTick);
        Assert.Single(apiState.History);
    }

    [Fact]
    public void ToApiDto_Board_ContainsStagesWorkersTasks()
    {
        // Arrange
        var config = CreateDomainConfig();
        var simulation = new KanbanFlowConsole.Dtos.Simulation();
        simulation.InitFromConfig(config);

        // Act
        var apiState = ApiMapper.ToApiDto(simulation);
        var board = apiState.Board;

        // Assert
        Assert.NotNull(board);
        Assert.Equal(3, board.Stages.Count);
        Assert.Equal(2, board.Workers.Count);
        Assert.Equal(2, board.Tasks.Count);
    }

    [Fact]
    public void ToApiDto_BoardStage_ContainsCorrectInfo()
    {
        // Arrange
        var config = CreateDomainConfig();
        var simulation = new KanbanFlowConsole.Dtos.Simulation();
        simulation.InitFromConfig(config);

        // Act
        var apiState = ApiMapper.ToApiDto(simulation);
        var todoStage = Assert.Single(apiState.Board.Stages, s => s.Name == "Todo");

        // Assert
        Assert.Equal(KanbanFlowConsole.Enums.StageType.Buffer, todoStage.Type);
        Assert.True(todoStage.IsStart);
        Assert.Single(todoStage.NextStageNames, n => n == "Developing");
        // Задачи находятся на стадии Todo после инициализации
        Assert.Equal(2, todoStage.TaskKeys.Count);
        Assert.Contains("TASK-1", todoStage.TaskKeys);
        Assert.Contains("TASK-2", todoStage.TaskKeys);
    }

    [Fact]
    public void ToApiDto_BoardWorker_ContainsCorrectInfo()
    {
        // Arrange
        var config = CreateDomainConfig();
        var simulation = new KanbanFlowConsole.Dtos.Simulation();
        simulation.InitFromConfig(config);

        // Act
        var apiState = ApiMapper.ToApiDto(simulation);
        var devWorker = Assert.Single(apiState.Board.Workers, w => w.Login == "dev1");

        // Assert
        Assert.Contains("backend", devWorker.Skills);
        Assert.True(devWorker.IsAvailable);
        Assert.Empty(devWorker.AssignedTaskKeys);
    }

    [Fact]
    public void ToApiDto_BoardTask_ContainsCorrectInfo()
    {
        // Arrange
        var config = CreateDomainConfig();
        var simulation = new KanbanFlowConsole.Dtos.Simulation();
        simulation.InitFromConfig(config);

        // Act
        var apiState = ApiMapper.ToApiDto(simulation);
        var task1 = Assert.Single(apiState.Board.Tasks, t => t.Key == "TASK-1");

        // Assert
        Assert.Equal("Implement feature", task1.Summary);
        Assert.Equal(KanbanFlowConsole.Enums.TShirtType.L, task1.ShirtType);
        Assert.Null(task1.WorkerLogin); // Задача ещё не назначена
        Assert.Null(task1.CurrentStageName);
    }

    [Fact]
    public void ToDomainSimulation_RestoresBoardFromDto()
    {
        // Arrange
        var config = CreateDomainConfig();
        var simulation = new KanbanFlowConsole.Dtos.Simulation();
        simulation.InitFromConfig(config);
        var apiState = ApiMapper.ToApiDto(simulation);

        // Act
        var restoredSimulation = ApiMapper.ToDomainSimulation(apiState);

        // Assert
        Assert.NotNull(restoredSimulation.Board);
        Assert.Equal(3, restoredSimulation.Board.Stages.Count);
        Assert.Equal(2, restoredSimulation.Board.Workers.Count);
        Assert.Equal(2, restoredSimulation.Board.Tasks.Count);
    }

    [Fact]
    public void ToDomainSimulation_RestoresHistoryFromDto()
    {
        // Arrange
        var config = CreateDomainConfig();
        var simulation = new KanbanFlowConsole.Dtos.Simulation();
        simulation.InitFromConfig(config);
        simulation.StartNewDay();
        simulation.LogActivity(new KanbanFlowConsole.Dtos.History.HistoryActivity
        {
            Type = KanbanFlowConsole.Dtos.History.ActivityType.WorkerTookTask,
            Description = "Test activity",
            Tick = 5
        });
        var apiState = ApiMapper.ToApiDto(simulation);

        // Act
        var restoredSimulation = ApiMapper.ToDomainSimulation(apiState);

        // Assert
        Assert.Single(restoredSimulation.History);
        Assert.Single(restoredSimulation.History[0].Activities);
    }

    [Fact]
    public void ToApiDto_HistoryDay_ContainsActivities()
    {
        // Arrange
        var config = CreateDomainConfig();
        var simulation = new KanbanFlowConsole.Dtos.Simulation();
        simulation.InitFromConfig(config);
        simulation.StartNewDay();
        simulation.AdvanceTick(10); // Устанавливаем тик перед логированием
        simulation.LogActivity(new KanbanFlowConsole.Dtos.History.HistoryActivity
        {
            Type = KanbanFlowConsole.Dtos.History.ActivityType.TaskMoved,
            Description = "Task moved",
            Progress = 50
        });

        // Act
        var apiState = ApiMapper.ToApiDto(simulation);

        // Assert
        var historyDay = Assert.Single(apiState.History);
        Assert.Equal(1, historyDay.DayNumber);
        var activity = Assert.Single(historyDay.Activities);
        Assert.Equal(KanbanFlowConsole.Dtos.History.ActivityType.TaskMoved, activity.Type);
        Assert.Equal(10, activity.Tick); // Tick устанавливается из CurrentTick симуляции
        Assert.Equal("Task moved", activity.Description);
        Assert.Equal(50, activity.Progress);
    }

    #endregion
}
