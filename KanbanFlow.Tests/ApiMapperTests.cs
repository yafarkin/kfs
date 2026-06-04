using KanbanFlowApi.Dtos;
using KanbanFlowApi.Dtos.Board;
using KanbanFlowApi.Dtos.Config;
using KanbanFlowApi.Dtos.History;
using KanbanFlowApi.Mappers;
using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Enums;
using Stage = KanbanFlowSerivce.Dtos.Config.Stage;
using Task = KanbanFlowSerivce.Dtos.Config.Task;

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

        // Assert - основная структура
        Assert.NotNull(apiDto);
        Assert.Equal(42, apiDto.Seed);
        Assert.Equal(2, apiDto.Workers.Count);
        Assert.Equal(2, apiDto.Tasks.Count);
        Assert.Equal(3, apiDto.Workflow.Stages.Count);

        // Assert - Workers
        var devWorker = Assert.Single(apiDto.Workers, w => w.Login == "dev1");
        Assert.Contains("backend", devWorker.Skills);
        Assert.Equal(100, devWorker.Performance);

        // Assert - Stages и переходы
        var todoStage = Assert.Single(apiDto.Workflow.Stages, s => s.Name == "Todo");
        Assert.Equal(StageType.Buffer, todoStage.Type);
        var todoTransition = Assert.Single(todoStage.Transitions);
        Assert.Equal("Developing", todoTransition.TargetStageName);
        Assert.Equal(1.0, todoTransition.Probability);

        var developingStage = Assert.Single(apiDto.Workflow.Stages, s => s.Name == "Developing");
        Assert.Equal(StageType.Work, developingStage.Type);
        var devTransition = Assert.Single(developingStage.Transitions);
        Assert.Equal("Done", devTransition.TargetStageName);
        Assert.Equal(1.0, devTransition.Probability);

        // Assert - Tasks
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

        // Assert - основная структура
        Assert.NotNull(domainConfig);
        Assert.Equal(42, domainConfig.Seed);
        Assert.Equal(2, domainConfig.Workers.Count);
        Assert.Equal(2, domainConfig.Tasks.Count);
        Assert.Equal(3, domainConfig.Workflow.Stages.Count);

        // Assert - Workers
        var devWorker = Assert.Single(domainConfig.Workers, w => w.Login == "dev1");
        Assert.Contains("backend", devWorker.Skills);
        Assert.Equal(100, devWorker.Performance);

        // Assert - Stages и переходы (ссылки на правильные объекты)
        var todoStage = domainConfig.Workflow.Stages.First(s => s.Name == "Todo");
        var developingStage = domainConfig.Workflow.Stages.First(s => s.Name == "Developing");
        var doneStage = domainConfig.Workflow.Stages.First(s => s.Name == "Done");

        var todoTransition = Assert.Single(todoStage.Transitions);
        Assert.Same(developingStage, todoTransition.Stage);

        var developingTransition = Assert.Single(developingStage.Transitions);
        Assert.Same(doneStage, developingTransition.Stage);

        Assert.Empty(doneStage.Transitions);

        // Assert - Tasks
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
    public void ToApiDto_EmptyTransitions_CreatesEmptyTransitions()
    {
        // Arrange
        var apiDto = new ApiSimulationConfigDto
        {
            Seed = 1,
            Workers = new List<ApiWorkerDto>(),
            Workflow = new ApiWorkflowDto
            {
                Stages =
                [
                    new()
                    {
                        Name = "Done",
                        Type = StageType.Buffer,
                        Transitions = new List<ApiStageTransitionDto>() // Пустой список
                    }
                ]
            },
            Tasks = new List<ApiTaskDto>()
        };

        // Act
        var domainConfig = ApiMapper.ToDomainConfig(apiDto);

        // Assert
        var doneStage = Assert.Single(domainConfig.Workflow.Stages);
        Assert.Empty(doneStage.Transitions);
    }

    #region Negative Tests

    [Fact]
    public void ToDomainConfig_TransitionToNonExistentStage_IgnoresTransition()
    {
        // Arrange - переход ссылается на несуществующую стадию
        var apiDto = new ApiSimulationConfigDto
        {
            Seed = 1,
            Workers = new List<ApiWorkerDto>(),
            Workflow = new ApiWorkflowDto
            {
                Stages =
                [
                    new()
                    {
                        Name = "Todo",
                        Type = StageType.Buffer,
                        Transitions = [new() { TargetStageName = "NonExistent", Probability = 1.0 }]
                    }
                ]
            },
            Tasks = new List<ApiTaskDto>()
        };

        // Act
        var domainConfig = ApiMapper.ToDomainConfig(apiDto);

        // Assert - переход должен быть проигнорирован
        var todoStage = Assert.Single(domainConfig.Workflow.Stages);
        Assert.Empty(todoStage.Transitions);
    }

    [Fact]
    public void ToDomainConfig_MissingWorkerInBoard_ThrowsException()
    {
        // Arrange - Board ссылается на воркера которого нет в конфигурации
        var config = CreateDomainConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);
        
        var apiState = ApiMapper.ToApiDto(simulation);
        // Удаляем воркера из конфигурации но оставляем в Board
        apiState.Config.Workers.Clear();

        // Act & Assert - восстановление должно завершиться ошибкой
        Assert.Throws<InvalidOperationException>(() => 
            ApiMapper.ToDomainSimulation(apiState));
    }

    [Fact]
    public void ToDomainConfig_MissingTaskInBoard_ThrowsException()
    {
        // Arrange - Board ссылается на задачу которой нет в конфигурации
        var config = CreateDomainConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);
        
        var apiState = ApiMapper.ToApiDto(simulation);
        // Удаляем задачу из конфигурации но оставляем в Board
        apiState.Config.Tasks.Clear();

        // Act & Assert - восстановление должно завершиться ошибкой
        Assert.Throws<InvalidOperationException>(() => 
            ApiMapper.ToDomainSimulation(apiState));
    }

    [Fact]
    public void ToDomainConfig_MissingStageInBoard_ThrowsException()
    {
        // Arrange - Board ссылается на стадию которой нет в конфигурации
        var config = CreateDomainConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);
        
        var apiState = ApiMapper.ToApiDto(simulation);
        // Удаляем стадию из конфигурации
        apiState.Config.Workflow.Stages.Clear();

        // Act & Assert - восстановление должно завершиться ошибкой
        Assert.Throws<InvalidOperationException>(() => 
            ApiMapper.ToDomainSimulation(apiState));
    }

    [Fact]
    public void ToDomainConfig_NullCollections_HandledGracefully()
    {
        // Arrange - DTO с null коллекциями (если тип позволяет)
        var apiDto = new ApiSimulationConfigDto
        {
            Seed = 1,
            Workers = null!,
            Workflow = new ApiWorkflowDto
            {
                Stages = null!
            },
            Tasks = null!
        };

        // Act & Assert - должно выбросить ArgumentNullException или обработать
        Assert.ThrowsAny<Exception>(() => ApiMapper.ToDomainConfig(apiDto));
    }

    #endregion

    private static SimulationConfig CreateDomainConfig()
    {
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            IsLeadTimeStart = true,
            Transitions = []
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            StageProgressPercent = 100,
            Transitions = []
        };

        var done = new Stage
        {
            Name = "Done",
            Type = StageType.Buffer,
            IsLeadTimeStart = false,
            Transitions = []
        };

        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers =
            [
                new() {Login = "dev1", Skills = ["backend"], Performance = 100},
                new() {Login = "qa1", Skills = ["qa"], Performance = 100}
            ],
            Workflow = new Workflow
            {
                Stages = [todo, developing, done]
            },
            Tasks =
            [
                new() {Key = "TASK-1", Summary = "Implement feature", ShirtType = TShirtType.L, RequiredSkills = ["backend"]},
                new() {Key = "TASK-2", Summary = "Write tests", ShirtType = TShirtType.M, RequiredSkills = ["backend"]}
            ]
        };
    }

    private static ApiSimulationConfigDto CreateApiDto()
    {
        return new ApiSimulationConfigDto
        {
            Seed = 42,
            Workers =
            [
                new() {Login = "dev1", Skills = ["backend"], Performance = 100},
                new() {Login = "qa1", Skills = ["qa"], Performance = 100}
            ],
            Workflow = new ApiWorkflowDto
            {
                Stages =
                [
                    new()
                    {
                        Name = "Todo",
                        Type = StageType.Buffer,
                        IsLeadTimeStart = true,
                        RequiredSkills = [],
                        Transitions = [new() {TargetStageName = "Developing", Probability = 1.0}]
                    },
                    new()
                    {
                        Name = "Developing",
                        Type = StageType.Work,
                        IsLeadTimeStart = false,
                        RequiredSkills = ["backend"],
                        StageProgressPercent = 100,
                        Transitions = [new() {TargetStageName = "Done", Probability = 1.0}]
                    },
                    new()
                    {
                        Name = "Done",
                        Type = StageType.Buffer,
                        IsLeadTimeStart = false,
                        RequiredSkills = [],
                        Transitions = []
                    }
                ]
            },
            Tasks =
            [
                new()
                {
                    Key = "TASK-1", Summary = "Implement feature", ShirtType = TShirtType.L, RequiredSkills = ["backend"]
                },
                new() {Key = "TASK-2", Summary = "Write tests", ShirtType = TShirtType.M, RequiredSkills = ["backend"]}
            ]
        };
    }

    #region Simulation State Tests

    [Fact]
    public void ToApiDto_Simulation_ReturnsFullState()
    {
        // Arrange
        var config = CreateDomainConfig();
        var simulation = new KanbanFlowSerivce.Dtos.Simulation();
        simulation.InitFromConfig(config);
        simulation.StartNewDay();

        // Act
        var apiState = ApiMapper.ToApiDto(simulation);

        // Assert
        Assert.NotNull(apiState);
        Assert.NotNull(apiState.Config);
        Assert.NotNull(apiState.Board);
        Assert.NotNull(apiState.History);
        Assert.Equal(1, apiState.CurrentDay);
        Assert.Single(apiState.History);
    }

    [Fact]
    public void ToApiDto_Board_ContainsStagesWorkersTasks()
    {
        // Arrange
        var config = CreateDomainConfig();
        var simulation = new KanbanFlowSerivce.Dtos.Simulation();
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
        var simulation = new KanbanFlowSerivce.Dtos.Simulation();
        simulation.InitFromConfig(config);

        // Act
        var apiState = ApiMapper.ToApiDto(simulation);
        var todoStage = Assert.Single(apiState.Board.Stages, s => s.Name == "Todo");

        // Assert
        Assert.Equal(KanbanFlowSerivce.Enums.StageType.Buffer, todoStage.Type);
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
        var simulation = new KanbanFlowSerivce.Dtos.Simulation();
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
        var simulation = new KanbanFlowSerivce.Dtos.Simulation();
        simulation.InitFromConfig(config);

        // Act
        var apiState = ApiMapper.ToApiDto(simulation);
        var task1 = Assert.Single(apiState.Board.Tasks, t => t.Key == "TASK-1");

        // Assert
        Assert.Equal("Implement feature", task1.Summary);
        Assert.Equal(KanbanFlowSerivce.Enums.TShirtType.L, task1.ShirtType);
        Assert.Null(task1.WorkerLogin); // Задача ещё не назначена
        Assert.Equal("Todo", task1.CurrentStageName); // Задача инициализируется в Todo
    }

    [Fact]
    public void ToDomainSimulation_RestoresBoardFromDto()
    {
        // Arrange
        var config = CreateDomainConfig();
        var simulation = new KanbanFlowSerivce.Dtos.Simulation();
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
        var simulation = new KanbanFlowSerivce.Dtos.Simulation();
        simulation.InitFromConfig(config);
        simulation.StartNewDay();
        simulation.LogActivity(new KanbanFlowSerivce.Dtos.History.HistoryActivity
        {
            Type = KanbanFlowSerivce.Dtos.History.ActivityType.WorkerTookTask,
            Description = "Test activity"
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
        var simulation = new KanbanFlowSerivce.Dtos.Simulation();
        simulation.InitFromConfig(config);
        simulation.StartNewDay();
        simulation.LogActivity(new KanbanFlowSerivce.Dtos.History.HistoryActivity
        {
            Type = KanbanFlowSerivce.Dtos.History.ActivityType.TaskMoved,
            Description = "Task moved",
            Progress = 50
        });

        // Act
        var apiState = ApiMapper.ToApiDto(simulation);

        // Assert
        var historyDay = Assert.Single(apiState.History);
        Assert.Equal(1, historyDay.DayNumber);
        var activity = Assert.Single(historyDay.Activities);
        Assert.Equal(KanbanFlowSerivce.Dtos.History.ActivityType.TaskMoved, activity.Type);
        Assert.Equal("Task moved", activity.Description);
        Assert.Equal(50, activity.Progress);
    }

    #endregion
}
