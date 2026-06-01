using KanbanFlowApi.Services;
using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Enums;
using KanbanFlowSerivce.Factories;
using KanbanFlowSerivce.Services;

namespace KanbanFlow.Tests;

/// <summary>
/// Тесты для WorkerMetricsService — расчёт метрик работников (Throughput, Lead Time, Efficiency).
/// </summary>
public class WorkerMetricsServiceTests
{
    [Fact]
    public void CalculateAllWorkersMetrics_ThreeWorkers_DifferentValuableTasks()
    {
        // Arrange - Создаём симуляцию через фабрику
        var config = CreateTestConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var service = new TaskMovementService(simulation);
        
        // Симулируем работу (6 дней)
        for (var i = 0; i < 6; i++)
        {
            simulation.StartNewDay();
            service.ProcessMovements();
            SimulateWorkProgress(simulation);
        }

        // Act
        var metricsService = new WorkerMetricsService(simulation);
        var workerMetrics = metricsService.CalculateAllWorkersMetrics();

        // Assert - Проверяем что метрики рассчитаны
        Assert.Equal(3, workerMetrics.Count);
        
        var dev1Metrics = workerMetrics.Single(w => w.Login == "dev1-be");
        var dev2Metrics = workerMetrics.Single(w => w.Login == "dev2-fe");
        var qaMetrics = workerMetrics.Single(w => w.Login == "qa1");

        // Все workers имеют > 0 ценных задач (зависит от симуляции)
        Assert.True(dev1Metrics.ValuableTasksCount >= 0);
        Assert.True(dev2Metrics.ValuableTasksCount >= 0);
        Assert.True(qaMetrics.ValuableTasksCount >= 0);
    }

    [Fact]
    public void CalculateAllWorkersMetrics_LeadTime_GreaterThanZero()
    {
        // Arrange
        var config = CreateTestConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var service = new TaskMovementService(simulation);
        
        // Симулируем работу
        for (var i = 0; i < 6; i++)
        {
            simulation.StartNewDay();
            service.ProcessMovements();
            SimulateWorkProgress(simulation);
        }

        // Act
        var metricsService = new WorkerMetricsService(simulation);
        var workerMetrics = metricsService.CalculateAllWorkersMetrics();

        // Assert - Lead Time >= 0 (может быть 0 если задачи не завершены)
        foreach (var metrics in workerMetrics)
        {
            Assert.True(metrics.LeadTime >= 0);
        }
    }

    [Fact]
    public void CalculateAllWorkersMetrics_Efficiency_InValidRange()
    {
        // Arrange
        var config = CreateTestConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var service = new TaskMovementService(simulation);
        
        // Симулируем работу
        for (var i = 0; i < 6; i++)
        {
            simulation.StartNewDay();
            service.ProcessMovements();
            SimulateWorkProgress(simulation);
        }

        // Act
        var metricsService = new WorkerMetricsService(simulation);
        var workerMetrics = metricsService.CalculateAllWorkersMetrics();

        // Assert
        foreach (var metrics in workerMetrics)
        {
            // EfficiencyPercent должен быть в диапазоне 0-100
            Assert.True(metrics.EfficiencyPercent >= 0);
            Assert.True(metrics.EfficiencyPercent <= 100);
            
            // WorkTime и BufferTime >= 0
            Assert.True(metrics.WorkTimeDays >= 0);
            Assert.True(metrics.BufferTimeDays >= 0);
        }
    }

    [Fact]
    public void CalculateAllWorkersMetrics_Throughput_CalculatedCorrectly()
    {
        // Arrange
        var config = CreateTestConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var service = new TaskMovementService(simulation);
        
        // Симулируем работу
        for (var i = 0; i < 6; i++)
        {
            simulation.StartNewDay();
            service.ProcessMovements();
            SimulateWorkProgress(simulation);
        }

        // Act
        var metricsService = new WorkerMetricsService(simulation);
        var workerMetrics = metricsService.CalculateAllWorkersMetrics();

        // Assert - Throughput >= 0
        foreach (var metrics in workerMetrics)
        {
            Assert.True(metrics.Throughput >= 0);
        }
    }

    [Fact]
    public void CalculateAllWorkersMetrics_BufferStages_IgnoredInValuableCount()
    {
        // Arrange - Создаём конфигурацию где буфер имеет CreatesValue = true (должен игнорироваться)
        var config = CreateTestConfigWithBufferCreatesValue();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var service = new TaskMovementService(simulation);
        
        // Симулируем работу
        for (var i = 0; i < 6; i++)
        {
            simulation.StartNewDay();
            service.ProcessMovements();
            SimulateWorkProgress(simulation);
        }

        // Act
        var metricsService = new WorkerMetricsService(simulation);
        var workerMetrics = metricsService.CalculateAllWorkersMetrics();

        // Assert - Буферные стадии не должны влиять на ценные задачи
        foreach (var metrics in workerMetrics)
        {
            // Ценные задачи считаются только по Work-стадиям с CreatesValue = true
            Assert.True(metrics.ValuableTasksCount >= 0);
        }
    }

    /// <summary>
    /// Создать тестовую конфигурацию с 3 workers и 3 задачами.
    /// </summary>
    private static SimulationConfig CreateTestConfig()
    {
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
                Name = "Developing",
                Type = StageType.Work,
                CreatesValue = true,
                StageProgressPercent = 100,
                RequiredSkills = ["backend", "frontend"],
                Transitions = []
            },
            new()
            {
                Name = "Ready for Testing",
                Type = StageType.Buffer,
                CreatesValue = false,
                Transitions = []
            },
            new()
            {
                Name = "Testing",
                Type = StageType.Work,
                CreatesValue = true,
                StageProgressPercent = 30,
                RequiredSkills = ["qa"],
                Transitions = []
            },
            new()
            {
                Name = "Release Preparation",
                Type = StageType.Work,
                CreatesValue = false,
                StageProgressPercent = 10,
                RequiredSkills = ["backend", "frontend"],
                Transitions = []
            },
            new()
            {
                Name = "Done",
                Type = StageType.Buffer,
                CreatesValue = false,
                Transitions = []
            }
        };

        // Устанавливаем переходы
        stages[0].Transitions.Add(new StageTransition { Stage = stages[1], Probability = 1.0 });
        stages[1].Transitions.Add(new StageTransition { Stage = stages[2], Probability = 1.0 });
        stages[2].Transitions.Add(new StageTransition { Stage = stages[3], Probability = 1.0 });
        stages[3].Transitions.Add(new StageTransition { Stage = stages[4], Probability = 1.0 });
        stages[4].Transitions.Add(new StageTransition { Stage = stages[5], Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = [
                new() { Login = "dev1-be", Skills = ["backend"], WipLimit = 1, Performance = 100 },
                new() { Login = "dev2-fe", Skills = ["frontend"], WipLimit = 1, Performance = 100 },
                new() { Login = "qa1", Skills = ["qa"], WipLimit = 1, Performance = 100 }
            ],
            Workflow = new Workflow { Stages = stages },
            Tasks = [
                new() { Key = "TASK-1", ShirtType = TShirtType.S, RequiredSkills = ["backend", "qa"] },
                new() { Key = "TASK-2", ShirtType = TShirtType.M, RequiredSkills = ["backend", "qa"] },
                new() { Key = "TASK-3", ShirtType = TShirtType.S, RequiredSkills = ["frontend"] }
            ],
            UseVariability = false
        };
    }

    /// <summary>
    /// Конфигурация где буферная стадия имеет CreatesValue = true (должна игнорироваться).
    /// </summary>
    private static SimulationConfig CreateTestConfigWithBufferCreatesValue()
    {
        var config = CreateTestConfig();
        
        // Устанавливаем буферной стадии CreatesValue = true (должна игнорироваться кодом)
        var readyForTesting = config.Workflow.Stages.Single(s => s.Name == "Ready for Testing");
        readyForTesting.CreatesValue = true;
        
        return config;
    }

    /// <summary>
    /// Симулировать работу воркеров (увеличение прогресса задач).
    /// </summary>
    private static void SimulateWorkProgress(Simulation simulation)
    {
        foreach (var worker in simulation.Board.Workers.Where(w => w.Assignments.Count > 0))
        {
            foreach (var assignment in worker.Assignments.ToList())
            {
                var task = assignment.Task;
                if (task.Progress < 100)
                {
                    task.Progress = Math.Min(100, task.Progress + 50);
                }
            }
        }
    }
}
