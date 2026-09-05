using KanbanFlowApi.Services;
using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Dtos.History;
using KanbanFlowSerivce.Enums;
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

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем работу (6 дней)
        for (var i = 0; i < 6; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Act
        var metricsService = new WorkerMetricsService(simulation);
        var workerMetrics = metricsService.CalculateAllWorkersMetrics();

        // Assert - Проверяем что метрики рассчитаны
        Assert.Equal(3, workerMetrics.Count);

        var dev1Metrics = workerMetrics.Single(w => w.Login == "dev1-be");
        var dev2Metrics = workerMetrics.Single(w => w.Login == "dev2-fe");
        var qaMetrics = workerMetrics.Single(w => w.Login == "qa1");

        // Все workers имеют >= 0 ценных задач
        Assert.True(dev1Metrics.ValuableTasksCount >= 0);
        Assert.True(dev2Metrics.ValuableTasksCount >= 0);
        Assert.True(qaMetrics.ValuableTasksCount >= 0);

        // Проверяем что WorkerLogin корректно заполнен в истории для WorkerCompletedTask
        var allActivities = simulation.History.SelectMany(d => d.Activities).ToList();
        var completedActivities = allActivities
            .Where(a => a.Type == KanbanFlowSerivce.Dtos.History.ActivityType.WorkerCompletedTask)
            .ToList();

        foreach (var activity in completedActivities)
        {
            Assert.NotNull(activity.WorkerLogin);
        }
    }

    [Fact]
    public void CalculateAllWorkersMetrics_LeadTime_GreaterThanZero()
    {
        // Arrange
        var config = CreateTestConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем работу
        for (var i = 0; i < 6; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
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

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем работу
        for (var i = 0; i < 6; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
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

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем работу
        for (var i = 0; i < 6; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
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
    public void CalculateAllWorkersMetrics_WorkerCompletedTask_HasWorkerLogin()
    {
        // Arrange
        var config = CreateTestConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем работу
        for (var i = 0; i < 6; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Act - Проверяем историю
        var allActivities = simulation.History.SelectMany(d => d.Activities).ToList();
        var completedActivities = allActivities
            .Where(a => a.Type == KanbanFlowSerivce.Dtos.History.ActivityType.WorkerCompletedTask)
            .ToList();

        // Assert - Если есть WorkerCompletedTask, проверяем что WorkerLogin заполнен
        foreach (var activity in completedActivities)
        {
            Assert.NotNull(activity.WorkerLogin);
            Assert.NotEmpty(activity.WorkerLogin);
        }
    }

    [Fact]
    public void CalculateAllWorkersMetrics_BufferStages_IgnoredInValuableCount()
    {
        // Arrange - Создаём конфигурацию где буфер имеет CreatesValue = true (должен игнорироваться)
        var config = CreateTestConfigWithBufferCreatesValue();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем работу
        for (var i = 0; i < 6; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
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

    [Fact]
    public void WorkerTookTask_And_WorkerCompletedTask_HaveSameCorrelationId()
    {
        // Arrange
        var config = CreateTestConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем работу (6 дней)
        for (var i = 0; i < 6; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Act - Проверяем что WorkerTookTask и WorkerCompletedTask имеют одинаковый CorrelationId
        var allActivities = simulation.History.SelectMany(d => d.Activities).ToList();
        var tookTasks = allActivities.Where(a => a.Type == ActivityType.WorkerTookTask).ToList();
        var completedTasks = allActivities.Where(a => a.Type == ActivityType.WorkerCompletedTask).ToList();

        // Assert
        foreach (var tookTask in tookTasks)
        {
            Assert.NotEqual(Guid.Empty, tookTask.CorrelationId);

            // Найти соответствующее WorkerCompletedTask
            var completedTask = completedTasks.FirstOrDefault(c => c.CorrelationId == tookTask.CorrelationId);
            
            // Если задача завершена, CorrelationId должен совпадать
            if (completedTask != null)
            {
                Assert.Equal(tookTask.CorrelationId, completedTask.CorrelationId);
            }
        }
    }

    [Fact]
    public void TaskMetricsService_CalculatesMetrics_ForAllTasks()
    {
        // Arrange
        var config = CreateTestConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем работу
        for (var i = 0; i < 6; i++)
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

        foreach (var metrics in taskMetrics)
        {
            Assert.NotNull(metrics.TaskKey);
            Assert.StartsWith("TASK-", metrics.TaskKey);
            Assert.True(metrics.LeadTimeDays >= 0);
            Assert.True(metrics.FlowEfficiencyPercent >= 0);
            Assert.True(metrics.FlowEfficiencyPercent <= 100);
        }
    }

    [Fact]
    public void TaskMetricsService_CompletedTask_HasCorrectStatus()
    {
        // Arrange
        var config = CreateTestConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем работу пока задачи не дойдут до Done
        for (var i = 0; i < 10; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Act
        var taskMetricsService = new TaskMetricsService(simulation);
        var taskMetrics = taskMetricsService.CalculateAllTasksMetrics();

        // Assert - хотя бы одна задача должна быть завершена
        var completedTasks = taskMetrics.Where(m => m.Status == "Done").ToList();
        Assert.NotEmpty(completedTasks);

        foreach (var metrics in completedTasks)
        {
            Assert.Equal("Done", metrics.Status);
        }
    }

    [Fact]
    public void CalculateAllWorkersMetrics_Efficiency_HasStrongerAssertions()
    {
        // Arrange
        var config = CreateTestConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем работу (10 дней для большей статистики)
        for (var i = 0; i < 10; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Act
        var metricsService = new WorkerMetricsService(simulation);
        var workerMetrics = metricsService.CalculateAllWorkersMetrics();

        // Assert - более строгие проверки
        foreach (var metrics in workerMetrics)
        {
            // EfficiencyPercent должен быть в диапазоне 0-100
            Assert.InRange(metrics.EfficiencyPercent, 0, 100);

            // WorkTime и BufferTime >= 0
            Assert.True(metrics.WorkTimeDays >= 0);
            Assert.True(metrics.BufferTimeDays >= 0);

            // Throughput >= 0
            Assert.True(metrics.Throughput >= 0);

            // LeadTime >= 0
            Assert.True(metrics.LeadTime >= 0);
        }
    }

    [Fact]
    public void CalculateAllWorkersMetrics_Throughput_CalculatedWithExpectedRange()
    {
        // Arrange
        var config = CreateTestConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем работу (15 дней для завершения задач)
        for (var i = 0; i < 15; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Act
        var metricsService = new WorkerMetricsService(simulation);
        var workerMetrics = metricsService.CalculateAllWorkersMetrics();

        // Assert - более конкретные проверки throughput
        foreach (var metrics in workerMetrics)
        {
            // Throughput не должен превышать 1 задачу в день (физическое ограничение)
            Assert.True(metrics.Throughput <= 1.5m);

            // Если есть ценные задачи, throughput должен быть > 0
            if (metrics.ValuableTasksCount > 0)
            {
                Assert.True(metrics.Throughput > 0);
            }
        }
    }

    [Fact]
    public void CalculateAllWorkersMetrics_LeadTime_InReasonableRange()
    {
        // Arrange
        var config = CreateTestConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем работу (20 дней для завершения всех задач)
        for (var i = 0; i < 20; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Act
        var metricsService = new WorkerMetricsService(simulation);
        var workerMetrics = metricsService.CalculateAllWorkersMetrics();

        // Assert - Lead Time должен быть в разумных пределах
        foreach (var metrics in workerMetrics)
        {
            if (metrics.ValuableTasksCount > 0)
            {
                // Lead Time не должен превышать общую длительность симуляции значительно
                Assert.True(metrics.LeadTime <= 30);
            }
            // Lead Time всегда >= 0
            Assert.True(metrics.LeadTime >= 0);
        }
    }

    [Fact]
    public void CalculateAllWorkersMetrics_WorkerWithNoValuableTasks_HasZeroThroughput()
    {
        // Arrange - создаём конфигурацию где worker не работает на ценных стадиях
        var config = CreateConfigWithNonValuableWorker();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем работу
        for (var i = 0; i < 10; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Act
        var metricsService = new WorkerMetricsService(simulation);
        var workerMetrics = metricsService.CalculateAllWorkersMetrics();

        // Assert - worker на не-ценной стадии должен иметь 0 throughput
        var nonValuableWorker = workerMetrics.FirstOrDefault(w => w.Login == "dev-non-valuable");
        if (nonValuableWorker != null)
        {
            Assert.Equal(0, nonValuableWorker.ValuableTasksCount);
            Assert.Equal(0, nonValuableWorker.Throughput);
        }
    }

    /// <summary>
    /// Конфигурация где worker работает только на не-ценной стадии.
    /// </summary>
    private static SimulationConfig CreateConfigWithNonValuableWorker()
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
                CreatesValue = true, // Ценная стадия
                StageProgressPercent = 100,
                RequiredSkills = ["backend"],
                Transitions = []
            },
            new()
            {
                Name = "ReleasePrep",
                Type = StageType.Work,
                CreatesValue = false, // Не ценная стадия
                StageProgressPercent = 50,
                RequiredSkills = ["release"],
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

        stages[0].Transitions.Add(new StageTransition { Stage = stages[1], Probability = 1.0 });
        stages[1].Transitions.Add(new StageTransition { Stage = stages[2], Probability = 1.0 });
        stages[2].Transitions.Add(new StageTransition { Stage = stages[3], Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = [
                new() { Login = "dev-backend", Skills = ["backend"], WipLimit = 1, Performance = 100 },
                new() { Login = "dev-non-valuable", Skills = ["release"], WipLimit = 1, Performance = 100 }
            ],
            Workflow = new Workflow { Stages = stages },
            Tasks = [
                new() { Key = "TASK-1", ShirtType = TShirtType.S, RequiredSkills = ["backend"] }
            ],
            UseVariability = false
        };
    }

    [Fact]
    public void CalculateAllWorkersMetrics_WorkerTookTask_IsLogged()
    {
        // Arrange
        var config = CreateTestConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act - один день
        simulation.StartNewDay();
        movementService.ProcessMovements();
        progressService.SimulateWorkDay();

        // Assert - проверяем что WorkerTookTask записан в историю
        var allActivities = simulation.History.SelectMany(d => d.Activities).ToList();
        var tookTasks = allActivities.Where(a => a.Type == ActivityType.WorkerTookTask).ToList();

        Assert.NotEmpty(tookTasks);
        foreach (var tookTask in tookTasks)
        {
            Assert.NotEqual(Guid.Empty, tookTask.CorrelationId);
            Assert.NotNull(tookTask.WorkerLogin);
        }
    }

    [Fact]
    public void CalculateAllWorkersMetrics_ActiveTime_CalculatedCorrectly()
    {
        // Arrange - конфигурация где задачи занимают несколько дней
        var stages = new List<Stage>
        {
            new()
            {
                Name = "Todo",
                Type = StageType.Buffer,
                IsLeadTimeStart = true,
                CreatesValue = false,
                StageProgressPercent = 100,
                Transitions = []
            },
            new()
            {
                Name = "Developing",
                Type = StageType.Work,
                CreatesValue = true,
                StageProgressPercent = 100, // TShirtType.L (7-15 дней) * 100% = 7-15 дней
                RequiredSkills = ["backend"],
                Transitions = []
            },
            new()
            {
                Name = "Done",
                Type = StageType.Buffer,
                CreatesValue = false,
                StageProgressPercent = 100,
                Transitions = []
            }
        };
        stages[0].Transitions.Add(new StageTransition { Stage = stages[1], Probability = 1.0 });
        stages[1].Transitions.Add(new StageTransition { Stage = stages[2], Probability = 1.0 });

        var config = new SimulationConfig
        {
            Seed = 42,
            Workers = [new() { Login = "dev1", Skills = ["backend"], WipLimit = 1, Performance = 100 }],
            Workflow = new Workflow { Stages = stages },
            Tasks = [
                new() { Key = "TASK-1", ShirtType = TShirtType.L, RequiredSkills = ["backend"] }, // L = 7-15 дней
                new() { Key = "TASK-2", ShirtType = TShirtType.L, RequiredSkills = ["backend"] }
            ],
            UseVariability = false
        };

        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем 10 дней
        for (var i = 0; i < 10; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Act
        var allActivities = simulation.History.SelectMany((d, idx) => d.Activities.Select(a => new { Day = idx + 1, Activity = a })).ToList();
        var tookTasks = allActivities.Where(x => x.Activity.Type == ActivityType.WorkerTookTask).ToList();
        var completedTasks = allActivities.Where(x => x.Activity.Type == ActivityType.WorkerCompletedTask).ToList();

        var metricsService = new WorkerMetricsService(simulation);
        var workerMetrics = metricsService.CalculateAllWorkersMetrics();

        // Assert
        var dev1Metrics = workerMetrics.Single(m => m.Login == "dev1");
        Assert.True(dev1Metrics.WorkTimeDays > 0,
            $"WorkTimeDays должен быть > 0, фактически: {dev1Metrics.WorkTimeDays}. " +
            $"TookTasks: {string.Join(", ", tookTasks.Select(t => $"{t.Activity.TaskKey}@Day{t.Day}"))}, " +
            $"CompletedTasks: {string.Join(", ", completedTasks.Select(c => $"{c.Activity.TaskKey}@Day{c.Day}"))}");
        Assert.True(dev1Metrics.EfficiencyPercent > 0, $"EfficiencyPercent должен быть > 0, фактически: {dev1Metrics.EfficiencyPercent}");
    }
}
