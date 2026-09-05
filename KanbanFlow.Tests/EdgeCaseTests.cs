using KanbanFlowApi.Services;
using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Board;
using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Dtos.History;
using KanbanFlowSerivce.Enums;
using KanbanFlowSerivce.Services;
using Task = KanbanFlowSerivce.Dtos.Config.Task;

namespace KanbanFlow.Tests;

/// <summary>
/// Тесты для граничных условий и крайних случаев.
/// </summary>
public class EdgeCaseTests
{
    [Fact]
    public void Simulation_WithNoTasks_CompletesWithoutErrors()
    {
        // Arrange
        var config = CreateConfigWithNoTasks();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act & Assert - симуляция не должна падать с ошибками
        for (var i = 0; i < 5; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        Assert.Empty(simulation.Board.Tasks);
    }

    [Fact]
    public void Simulation_WithNoWorkers_TasksDoNotProgress()
    {
        // Arrange
        var config = CreateConfigWithNoWorkers();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act - 3 дня без единого воркера
        for (var i = 0; i < 3; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Assert - задача не может зайти на Work-стадию без воркера и не двигается вовсе
        var todoStage = simulation.Board.Stages.First(s => s.Stage.Name == "Todo");
        var developingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");
        var task = simulation.Board.Tasks.Single();

        Assert.Empty(developingStage.Tasks);
        Assert.Single(todoStage.Tasks);
        Assert.Equal("Todo", task.CurrentStage?.Stage.Name);
        Assert.Null(task.Worker);
        Assert.Equal(0, task.Progress);
    }

    [Fact]
    public void Metrics_WithNoCompletedTasks_ReturnsZeroValues()
    {
        // Arrange
        var config = CreateSimpleConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act - только один день, задачи не завершены
        simulation.StartNewDay();
        movementService.ProcessMovements();
        progressService.SimulateWorkDay();

        var metricsService = new WorkerMetricsService(simulation);
        var workerMetrics = metricsService.CalculateAllWorkersMetrics();

        // Assert - ни одна задача не дошла до Done → нулевой throughput у всех
        Assert.NotEmpty(workerMetrics);
        Assert.All(workerMetrics, metrics => Assert.Equal(0m, metrics.Throughput));
    }

    [Fact]
    public void Metrics_WithEmptyHistory_ReturnsDefaultValues()
    {
        // Arrange
        var config = CreateSimpleConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        // Не запускаем симуляцию - история пуста

        // Act
        var metricsService = new WorkerMetricsService(simulation);
        var workerMetrics = metricsService.CalculateAllWorkersMetrics();

        // Assert
        Assert.NotEmpty(workerMetrics);
        foreach (var metrics in workerMetrics)
        {
            Assert.Equal(0, metrics.Throughput);
            Assert.Equal(0, metrics.LeadTime);
            Assert.Equal(0, metrics.EfficiencyPercent);
        }
    }

    [Fact]
    public void TaskMetrics_WithInProgressTask_HasCorrectStatus()
    {
        // Arrange
        var config = CreateSimpleConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act - только один день
        simulation.StartNewDay();
        movementService.ProcessMovements();
        progressService.SimulateWorkDay();

        var taskMetricsService = new TaskMetricsService(simulation);
        var taskMetrics = taskMetricsService.CalculateAllTasksMetrics();

        // Assert - после одного дня задача должна быть в работе на стадии Developing
        var task = taskMetrics.First();
        // Задача перемещается из Todo в Developing и берётся воркером в первый день
        Assert.Equal("In Progress", task.Status);
    }

    [Fact]
    public void FlowEfficiency_OneQuickTaskOverFiveDays_ReflectsIdleTime()
    {
        // Arrange - одна XS-задача (1 день работы), симуляция крутится 5 дней
        var config = CreateConfigWithInstantCompletion();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act
        for (var i = 0; i < 5; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        var metrics = new WorkerMetricsService(simulation).CalculateAllWorkersMetrics().Single();

        // Assert - активен ровно 1 день из 5 → эффективность 20%, throughput 1/5
        Assert.Equal(20m, metrics.EfficiencyPercent);
        Assert.Equal(1m, metrics.WorkTimeDays);
        Assert.Equal(0.2m, metrics.Throughput);
        Assert.Equal(1, metrics.ValuableTasksCount);
    }

    [Fact]
    public void TaskMovement_WithZeroWipLimit_BlocksMovementIntoStage()
    {
        // Arrange
        var config = CreateConfigWithZeroWipLimit();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);

        // Act
        simulation.StartNewDay();
        movementService.ProcessMovements();

        // Assert - задача НЕ должна переместиться в Developing с WIP=0
        var todoStage = simulation.Board.Stages.First(s => s.Stage.Name == "Todo");
        var developingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");

        // Задача остаётся в Todo т.к. Developing имеет WIP=0 и не может принять задачу
        Assert.Single(todoStage.Tasks);
        Assert.Empty(developingStage.Tasks);
    }

    [Fact]
    public void WorkerWithZeroPerformance_IsTreatedAs100Percent()
    {
        // Arrange
        var config = CreateConfigWithZeroPerformanceWorker();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act - 5 дней (день 1: перемещение+назначение, дни 1..5: работа)
        for (var i = 0; i < 5; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Assert - performance=0 трактуется как 100% (защита от деления на ноль).
        // M: (4+6)/2 = 5 дней · множитель 1.0 → DaysRequired=5 → за 5 рабочих дней ровно 100%.
        var task = simulation.Board.Tasks.Single();
        Assert.Equal(100, task.Progress);
    }

    [Fact]
    public void HistoryActivity_CorrelationId_NotEmptyForAllWorkerEvents()
    {
        // Arrange
        var config = CreateSimpleConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act
        for (var i = 0; i < 10; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Assert
        var allActivities = simulation.History.SelectMany(d => d.Activities).ToList();
        var workerEvents = allActivities.Where(a =>
            a.Type == ActivityType.WorkerTookTask ||
            a.Type == ActivityType.WorkerCompletedTask
        ).ToList();

        Assert.NotEmpty(workerEvents);
        Assert.All(workerEvents, e => Assert.NotEqual(Guid.Empty, e.CorrelationId));

        // took и completed для одной задачи связаны одним CorrelationId
        var took = workerEvents.Single(a => a.Type == ActivityType.WorkerTookTask);
        var completed = workerEvents.Single(a => a.Type == ActivityType.WorkerCompletedTask);
        Assert.Equal(took.CorrelationId, completed.CorrelationId);
    }

    [Fact]
    public void TaskWithNoRequiredSkills_MovesToAnyStage()
    {
        // Arrange
        var config = CreateConfigWithNoSkillRequirements();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);

        // Act
        simulation.StartNewDay();
        movementService.ProcessMovements();

        // Assert - задача без навыков заходит на стадию без требований и берётся воркером
        var todoStage = simulation.Board.Stages.First(s => s.Stage.Name == "Todo");
        var developingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");

        Assert.Empty(todoStage.Tasks);
        var task = Assert.Single(developingStage.Tasks);
        Assert.Equal("dev1", task.Worker?.Worker.Login);
    }

    #region Helper Methods

    private static SimulationConfig CreateConfigWithNoTasks()
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
            Workers = [new() { Login = "dev1", Skills = ["dev"], Performance = 100 }],
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks = [],
            UseVariability = false
        };
    }

    private static SimulationConfig CreateConfigWithNoWorkers()
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
            RequiredSkills = ["dev"],
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
            Workers = [],
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks = [new() { Key = "TASK-1", RequiredSkills = ["dev"] }],
            UseVariability = false
        };
    }

    private static SimulationConfig CreateSimpleConfig()
    {
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            IsLeadTimeStart = true,
            Transitions = new List<StageTransition>()
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            CreatesValue = true,
            StageProgressPercent = 100,
            RequiredSkills = ["dev"],
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
            Workers = [new() { Login = "dev1", Skills = ["dev"], Performance = 100 }],
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks = [new() { Key = "TASK-1", ShirtType = TShirtType.M, RequiredSkills = ["dev"] }],
            UseVariability = false
        };
    }

    private static SimulationConfig CreateConfigWithInstantCompletion()
    {
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            IsLeadTimeStart = true,
            Transitions = new List<StageTransition>()
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            CreatesValue = true,
            StageProgressPercent = 100,
            RequiredSkills = ["dev"],
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
            Workers = [new() { Login = "dev1", Skills = ["dev"], Performance = 100, WipLimit = 10 }],
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks = [new() { Key = "TASK-1", ShirtType = TShirtType.XS, RequiredSkills = ["dev"] }],
            UseVariability = false
        };
    }

    private static SimulationConfig CreateConfigWithZeroWipLimit()
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
            WipLimit = 0, // Нулевой лимит
            RequiredSkills = ["dev"],
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
            Workers = [new() { Login = "dev1", Skills = ["dev"], Performance = 100 }],
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks = [new() { Key = "TASK-1", RequiredSkills = ["dev"] }],
            UseVariability = false
        };
    }

    private static SimulationConfig CreateConfigWithZeroPerformanceWorker()
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
            RequiredSkills = ["dev"],
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
            Workers = [new() { Login = "dev1", Skills = ["dev"], Performance = 0 }],
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks = [new() { Key = "TASK-1", ShirtType = TShirtType.M, RequiredSkills = ["dev"] }],
            UseVariability = false
        };
    }

    private static SimulationConfig CreateConfigWithNoSkillRequirements()
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
            RequiredSkills = [], // Нет требований к навыкам
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
            Workers = [new() { Login = "dev1", Skills = ["dev"], Performance = 100 }],
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks = [new() { Key = "TASK-1", RequiredSkills = [] }], // Задача без навыков
            UseVariability = false
        };
    }

    #endregion
}
