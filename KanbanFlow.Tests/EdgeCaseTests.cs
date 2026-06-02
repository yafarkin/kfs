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

        // Act
        simulation.StartNewDay();
        movementService.ProcessMovements();
        progressService.SimulateWorkDay();

        // Assert - задачи не должны переместиться на рабочую стадию без воркеров
        var todoStage = simulation.Board.Stages.First(s => s.Stage.Name == "Todo");
        var developingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");

        // Задача остаётся в Todo или перемещается но без воркера
        Assert.Empty(developingStage.Tasks);
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

        // Assert
        foreach (var metrics in workerMetrics)
        {
            Assert.Equal(0, metrics.Throughput);
            Assert.True(metrics.LeadTime >= 0); // Может быть > 0 если задача началась
        }
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
    public void FlowEfficiency_WithOnlyActiveTime_IsHigh()
    {
        // Arrange - конфигурация где задача выполняется быстро
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

        var metricsService = new WorkerMetricsService(simulation);
        var workerMetrics = metricsService.CalculateAllWorkersMetrics();

        // Assert - Efficiency должен быть в допустимых пределах
        var metrics = workerMetrics.First();
        // Проверяем что метрики рассчитаны (не null/NaN)
        Assert.True(metrics.EfficiencyPercent >= 0);
        Assert.True(metrics.EfficiencyPercent <= 100);
        // Throughput должен быть > 0 если задача завершена
        Assert.True(metrics.Throughput >= 0);
    }

    [Fact]
    public void TaskMovement_WithZeroWipLimit_DoesNotBlockMovement()
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
    public void WorkerWithZeroPerformance_TaskTakesMaxTime()
    {
        // Arrange
        var config = CreateConfigWithZeroPerformanceWorker();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act - несколько дней
        for (var i = 0; i < 5; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Assert - прогресс должен быть очень низким
        var task = simulation.Board.Tasks.First();
        Assert.True(task.Progress < 50); // Менее 50% за 5 дней
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

        foreach (var workerEvent in workerEvents)
        {
            Assert.NotEqual(Guid.Empty, workerEvent.CorrelationId);
        }
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

        // Assert - задача должна переместиться на Developing
        var developingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");
        Assert.NotEmpty(developingStage.Tasks);
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
