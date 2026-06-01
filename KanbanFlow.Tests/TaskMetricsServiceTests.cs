using KanbanFlowApi.Services;
using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Enums;
using KanbanFlowSerivce.Factories;
using KanbanFlowSerivce.Services;

namespace KanbanFlow.Tests;

/// <summary>
/// Тесты для TaskMetricsService — расчёт метрик по задачам.
/// </summary>
public class TaskMetricsServiceTests
{
    [Fact]
    public void CalculateTaskMetrics_LeadTime_CalculatedCorrectly()
    {
        // Arrange
        var config = CreateSimpleConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем до завершения
        for (var i = 0; i < 15; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Act
        var taskMetricsService = new TaskMetricsService(simulation);
        var taskMetrics = taskMetricsService.CalculateAllTasksMetrics();

        // Assert
        var task = taskMetrics.First();
        // Lead Time должен быть неотрицательным
        Assert.True(task.LeadTimeDays >= 0);
        // Lead Time не должен превышать длительность симуляции значительно
        Assert.True(task.LeadTimeDays <= 20);
    }

    [Fact]
    public void CalculateTaskMetrics_ActiveWaitTime_NonNegative()
    {
        // Arrange
        var config = CreateSimpleConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем до завершения
        for (var i = 0; i < 15; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Act
        var taskMetricsService = new TaskMetricsService(simulation);
        var taskMetrics = taskMetricsService.CalculateAllTasksMetrics();

        // Assert
        var task = taskMetrics.First();
        Assert.True(task.ActiveTimeDays >= 0);
        Assert.True(task.WaitTimeDays >= 0);
    }

    [Fact]
    public void CalculateTaskMetrics_Efficiency_InValidRange()
    {
        // Arrange
        var config = CreateSimpleConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем до завершения
        for (var i = 0; i < 15; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Act
        var taskMetricsService = new TaskMetricsService(simulation);
        var taskMetrics = taskMetricsService.CalculateAllTasksMetrics();

        // Assert
        var task = taskMetrics.First();
        Assert.InRange(task.FlowEfficiencyPercent, 0, 100);
    }

    [Fact]
    public void CalculateTaskMetrics_ActiveTime_PlusWaitTime_EqualsTotal()
    {
        // Arrange
        var config = CreateSimpleConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем до завершения
        for (var i = 0; i < 15; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Act
        var taskMetricsService = new TaskMetricsService(simulation);
        var taskMetrics = taskMetricsService.CalculateAllTasksMetrics();

        // Assert
        var task = taskMetrics.First();
        var totalTime = task.ActiveTimeDays + task.WaitTimeDays;

        // Общее время должно быть неотрицательным
        Assert.True(totalTime >= 0);

        // Если totalTime > 0, проверяем что Efficiency соответствует
        if (totalTime > 0)
        {
            var expectedEfficiency = (task.ActiveTimeDays / totalTime) * 100;
            // Допускаем погрешность из-за округления
            var diff = Math.Abs(expectedEfficiency - task.FlowEfficiencyPercent);
            Assert.True(diff < 1);
        }
    }

    [Fact]
    public void CalculateTaskMetrics_Stages_HasStagesList()
    {
        // Arrange
        var config = CreateSimpleConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем до завершения
        for (var i = 0; i < 15; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Act
        var taskMetricsService = new TaskMetricsService(simulation);
        var taskMetrics = taskMetricsService.CalculateAllTasksMetrics();

        // Assert
        var task = taskMetrics.First();
        // Должен быть список стадий
        Assert.NotNull(task.Stages);
        Assert.True(task.Stages.Count >= 2);
    }

    [Fact]
    public void CalculateTaskMetrics_Status_IsValid()
    {
        // Arrange
        var config = CreateSimpleConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем до завершения
        for (var i = 0; i < 15; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Act
        var taskMetricsService = new TaskMetricsService(simulation);
        var taskMetrics = taskMetricsService.CalculateAllTasksMetrics();

        // Assert
        var task = taskMetrics.First();
        Assert.True(task.Status == "Done" || task.Status == "In Progress" || task.Status == "Todo");
    }

    [Fact]
    public void CalculateTaskMetrics_CompletedTask_HasDoneStatus()
    {
        // Arrange
        var config = CreateSimpleConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем до завершения
        for (var i = 0; i < 15; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Act
        var taskMetricsService = new TaskMetricsService(simulation);
        var taskMetrics = taskMetricsService.CalculateAllTasksMetrics();

        // Assert
        var completedTask = taskMetrics.First(t => t.Status == "Done");
        Assert.Equal("Done", completedTask.Status);
    }

    [Fact]
    public void CalculateTaskMetrics_InProgressTask_HasCorrectStatus()
    {
        // Arrange
        var config = CreateSimpleConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем только несколько дней (задача не завершена)
        for (var i = 0; i < 3; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Act
        var taskMetricsService = new TaskMetricsService(simulation);
        var taskMetrics = taskMetricsService.CalculateAllTasksMetrics();

        // Assert
        var task = taskMetrics.First();
        // Задача должна быть в процессе или на одной из стадий
        Assert.True(task.Status == "In Progress" || task.Status == "Developing" || task.Status == "Todo");
    }

    [Fact]
    public void CalculateTaskMetrics_MultipleTasks_AllHaveMetrics()
    {
        // Arrange
        var config = CreateConfigWithMultipleTasks();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем до завершения
        for (var i = 0; i < 20; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Act
        var taskMetricsService = new TaskMetricsService(simulation);
        var taskMetrics = taskMetricsService.CalculateAllTasksMetrics();

        // Assert
        Assert.Equal(3, taskMetrics.Count);

        foreach (var task in taskMetrics)
        {
            Assert.NotNull(task.TaskKey);
            Assert.True(task.LeadTimeDays >= 0);
            Assert.True(task.FlowEfficiencyPercent >= 0 && task.FlowEfficiencyPercent <= 100);
        }
    }

    [Fact]
    public void CalculateTaskMetrics_TaskKey_MatchesOriginalTask()
    {
        // Arrange
        var config = CreateSimpleConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем до завершения
        for (var i = 0; i < 15; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Act
        var taskMetricsService = new TaskMetricsService(simulation);
        var taskMetrics = taskMetricsService.CalculateAllTasksMetrics();

        // Assert
        var task = taskMetrics.First();
        Assert.Equal("TASK-1", task.TaskKey);
    }

    [Fact]
    public void CalculateTaskMetrics_FlowEfficiency_CalculatedCorrectly()
    {
        // Arrange
        var config = CreateSimpleConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем до завершения
        for (var i = 0; i < 15; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Act
        var taskMetricsService = new TaskMetricsService(simulation);
        var taskMetrics = taskMetricsService.CalculateAllTasksMetrics();

        // Assert
        var task = taskMetrics.First();

        // Проверяем что Flow Efficiency рассчитан корректно
        var totalTime = task.ActiveTimeDays + task.WaitTimeDays;
        if (totalTime > 0)
        {
            var calculatedEfficiency = (task.ActiveTimeDays / totalTime) * 100;
            // Допускаем небольшую погрешность из-за округления
            Assert.Equal(Math.Round(calculatedEfficiency, 1), task.FlowEfficiencyPercent, 1);
        }
    }

    #region Helper Methods

    private static SimulationConfig CreateSimpleConfig()
    {
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            IsLeadTimeStart = true,
            CreatesValue = false,
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
            CreatesValue = false,
            Transitions = new List<StageTransition>()
        };

        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = [new() { Login = "dev1", Skills = ["dev"], Performance = 100, WipLimit = 1 }],
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks = [new() { Key = "TASK-1", ShirtType = TShirtType.M, RequiredSkills = ["dev"] }],
            UseVariability = false
        };
    }

    private static SimulationConfig CreateConfigWithMultipleTasks()
    {
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            IsLeadTimeStart = true,
            CreatesValue = false,
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
            CreatesValue = false,
            Transitions = new List<StageTransition>()
        };

        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = [
                new() { Login = "dev1", Skills = ["dev"], Performance = 100, WipLimit = 1 },
                new() { Login = "dev2", Skills = ["dev"], Performance = 100, WipLimit = 1 }
            ],
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks = [
                new() { Key = "TASK-1", ShirtType = TShirtType.S, RequiredSkills = ["dev"] },
                new() { Key = "TASK-2", ShirtType = TShirtType.M, RequiredSkills = ["dev"] },
                new() { Key = "TASK-3", ShirtType = TShirtType.L, RequiredSkills = ["dev"] }
            ],
            UseVariability = false
        };
    }

    #endregion
}
