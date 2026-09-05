using KanbanFlowApi.Services;
using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Enums;
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

        // Assert - задача завершилась, значит Lead Time строго положителен и в пределах прогона
        var task = Assert.Single(taskMetrics);
        Assert.Equal("Done", task.Status);
        Assert.InRange(task.LeadTimeDays, 1m, simulation.CurrentDay);
    }

    // Тавтологии "ActiveTimeDays >= 0" / "FlowEfficiencyPercent в [0,100]" удалены:
    // диапазон гарантирован типом/формулой. Точные значения — в
    // CalculateTaskMetrics_ActiveTime_PlusWaitTime_EqualsTotal и
    // CalculateTaskMetrics_FlowEfficiency_CalculatedCorrectly.

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

        // Assert - задача завершена → есть ненулевое суммарное время,
        // и FlowEfficiencyPercent == ActiveTime / (Active + Wait) * 100 (с учётом округления).
        var task = Assert.Single(taskMetrics);
        var totalTime = task.ActiveTimeDays + task.WaitTimeDays;
        Assert.True(totalTime > 0, "у завершённой задачи Active+Wait должно быть > 0");

        var expectedEfficiency = task.ActiveTimeDays / totalTime * 100;
        Assert.True(Math.Abs(expectedEfficiency - task.FlowEfficiencyPercent) < 1,
            $"efficiency: ожидалось ~{expectedEfficiency}, получено {task.FlowEfficiencyPercent}");
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

        // Assert - метрики есть у всех трёх задач, все завершены с положительным Lead Time
        Assert.Equal(
            new[] { "TASK-1", "TASK-2", "TASK-3" },
            taskMetrics.Select(t => t.TaskKey).OrderBy(k => k).ToArray());
        Assert.All(taskMetrics, task =>
        {
            Assert.Equal("Done", task.Status);
            Assert.True(task.LeadTimeDays > 0, $"{task.TaskKey}: нулевой Lead Time");
        });
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

    // Backlog (старт) → Todo (IsLeadTimeStart) → Developing → Done.
    // IsLeadTimeStart стоит на стадии, В КОТОРУЮ задача перемещается (как в реальных пресетах,
    // где перед Todo есть Backlog) — иначе событие LeadTimeStarted не пишется и LeadTimeDays == 0.
    private static SimulationConfig CreateSimpleConfig()
    {
        var backlog = new Stage { Name = "Backlog", Type = StageType.Buffer, CreatesValue = false, Transitions = [] };
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            IsLeadTimeStart = true,
            CreatesValue = false,
            Transitions = []
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            CreatesValue = true,
            StageProgressPercent = 100,
            RequiredSkills = ["dev"],
            Transitions = []
        };

        var done = new Stage { Name = "Done", Type = StageType.Buffer, CreatesValue = false, Transitions = [] };

        backlog.Transitions.Add(new StageTransition { Stage = todo, Probability = 1.0 });
        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = [new() { Login = "dev1", Skills = ["dev"], Performance = 100, WipLimit = 1 }],
            Workflow = new Workflow { Stages = [backlog, todo, developing, done] },
            Tasks = [new() { Key = "TASK-1", ShirtType = TShirtType.M, RequiredSkills = ["dev"] }],
            UseVariability = false
        };
    }

    private static SimulationConfig CreateConfigWithMultipleTasks()
    {
        var backlog = new Stage { Name = "Backlog", Type = StageType.Buffer, CreatesValue = false, Transitions = [] };
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            IsLeadTimeStart = true,
            CreatesValue = false,
            Transitions = []
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            CreatesValue = true,
            StageProgressPercent = 100,
            RequiredSkills = ["dev"],
            Transitions = []
        };

        var done = new Stage { Name = "Done", Type = StageType.Buffer, CreatesValue = false, Transitions = [] };

        backlog.Transitions.Add(new StageTransition { Stage = todo, Probability = 1.0 });
        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = [
                new() { Login = "dev1", Skills = ["dev"], Performance = 100, WipLimit = 1 },
                new() { Login = "dev2", Skills = ["dev"], Performance = 100, WipLimit = 1 }
            ],
            Workflow = new Workflow { Stages = [backlog, todo, developing, done] },
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
