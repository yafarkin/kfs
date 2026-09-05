using KanbanFlowApi.Services;
using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Dtos.History;
using KanbanFlowSerivce.Enums;
using KanbanFlowSerivce.Services;
using Xunit;

namespace KanbanFlow.Tests;

/// <summary>
/// Тесты для Flow Efficiency в MetricsService
/// </summary>
public class MetricsServiceFlowEfficiencyTests
{
    [Fact]
    public void CalculateFlowEfficiency_DoneStage_NotCountedAsWaitTime()
    {
        // Arrange - конфигурация где задачи быстро переходят в Done
        var stages = new List<Stage>
        {
            new()
            {
                Name = "Todo",
                Type = StageType.Buffer,
                IsLeadTimeStart = false,
                CreatesValue = false,
                Transitions = []
            },
            new()
            {
                Name = "Developing",
                Type = StageType.Work,
                IsLeadTimeStart = true,
                CreatesValue = true,
                StageProgressPercent = 100,
                RequiredSkills = ["backend"],
                Transitions = []
            },
            new()
            {
                Name = "Done",
                Type = StageType.Buffer,
                IsLeadTimeStart = false,
                CreatesValue = false,
                Transitions = []
            }
        };

        stages[0].Transitions.Add(new StageTransition { Stage = stages[1], Probability = 1.0 });
        stages[1].Transitions.Add(new StageTransition { Stage = stages[2], Probability = 1.0 });

        var config = new SimulationConfig
        {
            Seed = 42,
            Workers = [
                new() { Login = "dev1", Skills = ["backend"], WipLimit = 1, Performance = 100 }
            ],
            Workflow = new Workflow { Stages = stages },
            Tasks = [
                new() { Key = "TASK-1", ShirtType = TShirtType.S, RequiredSkills = ["backend"] },
                new() { Key = "TASK-2", ShirtType = TShirtType.M, RequiredSkills = ["backend"] }
            ],
            UseVariability = true
        };

        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем 6 дней (обе задачи должны завершиться)
        for (var i = 0; i < 6; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Act
        var metricsService = new MetricsService(simulation);
        var flowEfficiency = metricsService.CalculateFlowEfficiency();

        // Assert - время в Done не должно считаться как Wait Time
        // TASK-1: 1 день в Developing (Active)
        // TASK-2: 3 дня в Developing (Active)
        // Итого Active = 4 дня, Wait = 0 (время в Done не считается)
        Assert.True(flowEfficiency.ActiveTime > 0, "ActiveTime должен быть > 0");
        Assert.Equal(0, flowEfficiency.WaitTime);
        Assert.Equal(100, flowEfficiency.EfficiencyPercent);
    }

    [Fact]
    public void CalculateFlowEfficiency_InProgressTask_CountsTimeUntilCurrentDay()
    {
        // Arrange - задача ещё не завершена
        var stages = new List<Stage>
        {
            new()
            {
                Name = "Todo",
                Type = StageType.Buffer,
                IsLeadTimeStart = false,
                CreatesValue = false,
                Transitions = []
            },
            new()
            {
                Name = "Developing",
                Type = StageType.Work,
                IsLeadTimeStart = true,
                CreatesValue = true,
                StageProgressPercent = 100,
                RequiredSkills = ["backend"],
                Transitions = []
            },
            new()
            {
                Name = "Done",
                Type = StageType.Buffer,
                IsLeadTimeStart = false,
                CreatesValue = false,
                Transitions = []
            }
        };

        stages[0].Transitions.Add(new StageTransition { Stage = stages[1], Probability = 1.0 });
        stages[1].Transitions.Add(new StageTransition { Stage = stages[2], Probability = 1.0 });

        var config = new SimulationConfig
        {
            Seed = 42,
            Workers = [
                new() { Login = "dev1", Skills = ["backend"], WipLimit = 1, Performance = 100 }
            ],
            Workflow = new Workflow { Stages = stages },
            Tasks = [
                new() { Key = "TASK-1", ShirtType = TShirtType.L, RequiredSkills = ["backend"] } // L = 7-15 дней
            ],
            UseVariability = false
        };

        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем 3 дня (задача ещё не завершена)
        for (var i = 0; i < 3; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Act
        var metricsService = new MetricsService(simulation);
        var flowEfficiency = metricsService.CalculateFlowEfficiency();

        // Assert - для незавершённой задачи время считается до текущего дня
        Assert.True(flowEfficiency.ActiveTime > 0, "ActiveTime должен быть > 0 для задачи в работе");
        // WaitTime может быть > 0 если задача ждала в Todo
    }

    [Fact]
    public void CalculateFlowEfficiency_BufferStageBeforeWork_CountedAsWaitTime()
    {
        // Arrange - есть буферная стадия перед рабочей
        var stages = new List<Stage>
        {
            new()
            {
                Name = "Todo",
                Type = StageType.Buffer,
                IsLeadTimeStart = true,
                CreatesValue = false,
                Transitions = []
            },
            new()
            {
                Name = "Ready",
                Type = StageType.Buffer,
                IsLeadTimeStart = false,
                CreatesValue = false,
                Transitions = []
            },
            new()
            {
                Name = "Developing",
                Type = StageType.Work,
                IsLeadTimeStart = false,
                CreatesValue = true,
                StageProgressPercent = 100,
                RequiredSkills = ["backend"],
                Transitions = []
            },
            new()
            {
                Name = "Done",
                Type = StageType.Buffer,
                IsLeadTimeStart = false,
                CreatesValue = false,
                Transitions = []
            }
        };

        stages[0].Transitions.Add(new StageTransition { Stage = stages[1], Probability = 1.0 });
        stages[1].Transitions.Add(new StageTransition { Stage = stages[2], Probability = 1.0 });
        stages[2].Transitions.Add(new StageTransition { Stage = stages[3], Probability = 1.0 });

        var config = new SimulationConfig
        {
            Seed = 42,
            Workers = [
                new() { Login = "dev1", Skills = ["backend"], WipLimit = 1, Performance = 100 }
            ],
            Workflow = new Workflow { Stages = stages },
            // Две задачи, один воркер (WIP=1): пока он делает TASK-1 (~3 дня на Developing),
            // TASK-2 вынужденно простаивает в буфере Ready → это и есть Wait Time.
            Tasks = [
                new() { Key = "TASK-1", ShirtType = TShirtType.S, RequiredSkills = ["backend"] },
                new() { Key = "TASK-2", ShirtType = TShirtType.S, RequiredSkills = ["backend"] }
            ],
            UseVariability = false
        };

        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем пока обе задачи не завершатся
        for (var i = 0; i < 15; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Act
        var flowEfficiency = new MetricsService(simulation).CalculateFlowEfficiency();

        // Assert - у TASK-2 реально было ожидание в буфере → агрегатный WaitTime > 0,
        // и эффективность потока строго меньше 100%.
        Assert.True(flowEfficiency.ActiveTime > 0);
        Assert.True(flowEfficiency.WaitTime > 0, "ожидание TASK-2 в буфере Ready должно попасть в WaitTime");
        Assert.True(flowEfficiency.EfficiencyPercent < 100m);
    }
}
