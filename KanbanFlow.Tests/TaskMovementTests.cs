using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Board;
using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Dtos.History;
using KanbanFlowSerivce.Enums;
using KanbanFlowSerivce.Services;
using Task = KanbanFlowSerivce.Dtos.Config.Task;

namespace KanbanFlow.Tests;

/// <summary>
/// Тесты для TaskMovementService - перемещение задач по доске
/// </summary>
public class TaskMovementTests
{
    [Fact]
    public void ProcessMovements_TryAssignWorkersToWaitingTasks_AssignsWorkerToTaskWithoutWorker()
    {
        // Arrange - задача в рабочей стадии с воркером
        var config = CreateConfigWithTaskInWorkStageWithoutWorker();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        // Симулируем день чтобы задача переместилась в Developing
        simulation.StartNewDay();
        var movementService = new TaskMovementService(simulation);
        movementService.ProcessMovements();

        // Act - ProcessMovements должен назначить воркера на задачу
        movementService.ProcessMovements();

        // Assert - воркер должен быть назначен на задачу
        var developingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");
        var task = developingStage.Tasks.First();
        
        // Задача должна быть назначена на воркера
        Assert.NotNull(task.Worker);
        
        // Проверяем что событие WorkerTookTask было записано
        var tookTaskEvent = simulation.History
            .SelectMany(d => d.Activities)
            .FirstOrDefault(a => a.Type == ActivityType.WorkerTookTask);
        Assert.NotNull(tookTaskEvent);
        Assert.Equal("TASK-1", tookTaskEvent.TaskKey);
    }

    [Fact]
    public void ProcessMovements_RequiresDifferentResource_DoesNotAssignSameWorker()
    {
        // Arrange - стадия Code Review требует другого воркера (не того кто был в Developing)
        var config = CreateConfigWithRequiresDifferentResource();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act - симулируем работу
        for (var day = 0; day < 5; day++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Assert - задача должна перейти в Code Review к другому воркеру (qa1)
        var codeReviewStage = simulation.Board.Stages.First(s => s.Stage.Name == "Code Review");
        
        if (codeReviewStage.Tasks.Any())
        {
            var task = codeReviewStage.Tasks.First();
            // Если задача в Code Review, воркер должен быть qa1 (не dev1)
            if (task.Worker != null)
            {
                Assert.Equal("qa1", task.Worker.Worker.Login);
            }
        }
        
        // Проверяем что события перемещения в Code Review были
        var allActivities = simulation.History.SelectMany(d => d.Activities).ToList();
        var codeReviewMoves = allActivities
            .Where(a => a.Type == ActivityType.TaskMoved && a.StageName == "Code Review")
            .ToList();
        
        // Задача должна хотя бы раз перейти в Code Review
        Assert.NotEmpty(codeReviewMoves);
    }

    [Fact]
    public void ProcessMovements_AcceptableWorkers_AssignsSpecificWorker()
    {
        // Arrange - задача требует конкретного воркера для стадии
        var config = CreateConfigWithAcceptableWorkers();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act - симулируем работу
        for (var day = 0; day < 10; day++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Assert - задача TASK-1 должна быть выполнена только dev1 (senior)
        var allActivities = simulation.History.SelectMany(d => d.Activities).ToList();
        var completedEvents = allActivities
            .Where(a => a.Type == ActivityType.WorkerCompletedTask && a.TaskKey == "TASK-1")
            .ToList();

        foreach (var completed in completedEvents)
        {
            // TASK-1 должна завершаться только dev1
            Assert.Equal("dev1", completed.WorkerLogin);
        }
    }

    [Fact]
    public void ProcessMovements_TopologicalOrder_MovesTasksCascade()
    {
        // Arrange - задача может пройти несколько стадий за один день
        var config = CreateConfigWithHighProbability();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);

        // Act - один день симуляции
        simulation.StartNewDay();
        movementService.ProcessMovements();

        // Assert - задача должна переместиться из Todo в Developing (и возможно дальше)
        var todoStage = simulation.Board.Stages.First(s => s.Stage.Name == "Todo");
        var developingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");

        // Задача не должна остаться в Todo при вероятности 1.0
        Assert.Empty(todoStage.Tasks);
        Assert.NotEmpty(developingStage.Tasks);
    }

    [Fact]
    public void ProcessMovements_Probability_Zero_DoesNotMove()
    {
        // Arrange - переход с вероятностью 0
        var config = CreateConfigWithZeroProbability();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);

        // Act
        simulation.StartNewDay();
        movementService.ProcessMovements();

        // Assert - задача не должна перейти из Todo в Developing
        var todoStage = simulation.Board.Stages.First(s => s.Stage.Name == "Todo");
        var developingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");

        Assert.Single(todoStage.Tasks);
        Assert.Empty(developingStage.Tasks);
    }

    [Fact]
    public void ProcessMovements_SkillsMismatch_DoesNotAssignWorker()
    {
        // Arrange - задача требует навыка которого нет у воркера
        var config = CreateConfigWithSkillsMismatch();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);

        // Act
        simulation.StartNewDay();
        movementService.ProcessMovements();

        // Assert - задача не должна быть назначена на воркера без нужного навыка
        var developingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");
        
        if (developingStage.Tasks.Any())
        {
            var task = developingStage.Tasks.First();
            // Задача может быть в стадии но без воркера
            Assert.Null(task.Worker);
        }
    }

    #region Helper Methods

    private static SimulationConfig CreateConfigWithTaskInWorkStageWithoutWorker()
    {
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            Transitions = new List<StageTransition>()
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            StageProgressPercent = 100,
            RequiredSkills = ["backend"],
            Transitions = new List<StageTransition>()
        };

        var done = new Stage
        {
            Name = "Done",
            Type = StageType.Buffer,
            Transitions = new List<StageTransition>()
        };

        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = [new() { Login = "dev1", Skills = ["backend"], Performance = 100, WipLimit = 1 }],
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks = [new() { Key = "TASK-1", RequiredSkills = ["backend"] }],
            UseVariability = false
        };
    }

    private static SimulationConfig CreateConfigWithRequiresDifferentResource()
    {
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            Transitions = new List<StageTransition>()
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            StageProgressPercent = 100,
            RequiredSkills = ["backend"],
            Transitions = new List<StageTransition>()
        };

        var codeReview = new Stage
        {
            Name = "Code Review",
            Type = StageType.Work,
            StageProgressPercent = 100,
            RequiredSkills = ["review"],
            RequiresDifferentResource = true,
            RequiresDifferentResourceFromStage = "Developing",
            Transitions = new List<StageTransition>()
        };

        var done = new Stage
        {
            Name = "Done",
            Type = StageType.Buffer,
            Transitions = new List<StageTransition>()
        };

        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = codeReview, Probability = 1.0 });
        codeReview.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = [
                new() { Login = "dev1", Skills = ["backend"], Performance = 100 },
                new() { Login = "qa1", Skills = ["review"], Performance = 100 }
            ],
            Workflow = new Workflow { Stages = [todo, developing, codeReview, done] },
            Tasks = [new() { Key = "TASK-1", RequiredSkills = ["backend", "review"] }],
            UseVariability = false
        };
    }

    private static SimulationConfig CreateConfigWithAcceptableWorkers()
    {
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            Transitions = new List<StageTransition>()
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            StageProgressPercent = 100,
            RequiredSkills = ["backend"],
            Transitions = new List<StageTransition>()
        };

        var done = new Stage
        {
            Name = "Done",
            Type = StageType.Buffer,
            Transitions = new List<StageTransition>()
        };

        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = [
                new() { Login = "dev1", Skills = ["backend"], Performance = 100 },
                new() { Login = "dev2", Skills = ["backend"], Performance = 100 }
            ],
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks = [
                new() 
                { 
                    Key = "TASK-1", 
                    RequiredSkills = ["backend"],
                    AcceptableWorkers = new Dictionary<string, string> { ["Developing"] = "dev1" }
                }
            ],
            UseVariability = false
        };
    }

    private static SimulationConfig CreateConfigWithHighProbability()
    {
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            Transitions = new List<StageTransition>()
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            StageProgressPercent = 100,
            RequiredSkills = [],
            Transitions = new List<StageTransition>()
        };

        var done = new Stage
        {
            Name = "Done",
            Type = StageType.Buffer,
            Transitions = new List<StageTransition>()
        };

        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = [new() { Login = "dev1", Skills = [], Performance = 100, WipLimit = 10 }],
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks = [new() { Key = "TASK-1", RequiredSkills = [] }],
            UseVariability = false
        };
    }

    private static SimulationConfig CreateConfigWithZeroProbability()
    {
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            Transitions = new List<StageTransition>()
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            RequiredSkills = [],
            Transitions = new List<StageTransition>()
        };

        var done = new Stage
        {
            Name = "Done",
            Type = StageType.Buffer,
            Transitions = new List<StageTransition>()
        };

        // Вероятность 0 - переход никогда не произойдёт
        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 0.0 });
        developing.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = [new() { Login = "dev1", Skills = [], Performance = 100 }],
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks = [new() { Key = "TASK-1", RequiredSkills = [] }],
            UseVariability = false
        };
    }

    private static SimulationConfig CreateConfigWithSkillsMismatch()
    {
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            Transitions = new List<StageTransition>()
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            StageProgressPercent = 100,
            RequiredSkills = ["backend"], // Требует backend
            Transitions = new List<StageTransition>()
        };

        var done = new Stage
        {
            Name = "Done",
            Type = StageType.Buffer,
            Transitions = new List<StageTransition>()
        };

        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = [new() { Login = "dev1", Skills = ["frontend"], Performance = 100 }], // Нет backend
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks = [new() { Key = "TASK-1", RequiredSkills = ["backend"] }], // Задача требует backend
            UseVariability = false
        };
    }

    #endregion
}
