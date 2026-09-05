using KanbanFlowApi.Mappers;
using KanbanFlowApi.Services;
using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Dtos.History;
using KanbanFlowSerivce.Enums;
using KanbanFlowSerivce.Services;
using Xunit;

namespace KanbanFlow.Tests;

/// <summary>
/// Тесты для проверки (де)сериализации CorrelationId и DayNumber в мапперах
/// </summary>
public class ApiMapperCorrelationTests
{
    [Fact]
    public void ToDomainSimulation_History_RestoresCorrelationIdAndDayNumber()
    {
        // Arrange - создаём симуляцию и проводим несколько дней
        var config = CreateSingleWorkerConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем 3 дня
        for (var i = 0; i < 3; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Запоминаем оригинальные CorrelationId и DayNumber
        var originalActivities = simulation.History.SelectMany(d => d.Activities).ToList();
        var originalTookTasks = originalActivities
            .Where(a => a.Type == ActivityType.WorkerTookTask)
            .ToList();

        Assert.NotEmpty(originalTookTasks);
        foreach (var took in originalTookTasks)
        {
            Assert.NotEqual(Guid.Empty, took.CorrelationId);
            Assert.True(took.DayNumber > 0);
        }

        // Act - сериализуем в DTO и обратно
        var dto = ApiMapper.ToApiDto(simulation);
        var restoredSimulation = ApiMapper.ToDomainSimulation(dto);

        // Assert - проверяем что CorrelationId и DayNumber восстановились
        var restoredActivities = restoredSimulation.History.SelectMany(d => d.Activities).ToList();
        var restoredTookTasks = restoredActivities
            .Where(a => a.Type == ActivityType.WorkerTookTask)
            .ToList();

        Assert.Equal(originalTookTasks.Count, restoredTookTasks.Count);

        for (var i = 0; i < originalTookTasks.Count; i++)
        {
            var original = originalTookTasks[i];
            var restored = restoredTookTasks[i];

            Assert.Equal(original.CorrelationId, restored.CorrelationId);
            Assert.Equal(original.DayNumber, restored.DayNumber);
            Assert.Equal(original.Type, restored.Type);
            Assert.Equal(original.TaskKey, restored.TaskKey);
            Assert.Equal(original.WorkerLogin, restored.WorkerLogin);
        }
    }

    [Fact]
    public void ToDomainSimulation_Metrics_CalculatedCorrectlyAfterRestore()
    {
        // Arrange - создаём симуляцию и проводим несколько дней
        var config = CreateSingleWorkerConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем 6 дней (TASK-1 должна завершиться)
        for (var i = 0; i < 6; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Act - сериализуем в DTO и обратно
        var dto = ApiMapper.ToApiDto(simulation);
        var restoredSimulation = ApiMapper.ToDomainSimulation(dto);

        // Assert - проверяем что метрики рассчитываются корректно после восстановления
        var originalMetricsService = new WorkerMetricsService(simulation);
        var restoredMetricsService = new WorkerMetricsService(restoredSimulation);

        var originalMetrics = originalMetricsService.CalculateAllWorkersMetrics().Single();
        var restoredMetrics = restoredMetricsService.CalculateAllWorkersMetrics().Single();

        // Метрики должны совпадать
        Assert.Equal(originalMetrics.Login, restoredMetrics.Login);
        Assert.Equal(originalMetrics.Throughput, restoredMetrics.Throughput);
        Assert.Equal(originalMetrics.LeadTime, restoredMetrics.LeadTime);
        Assert.Equal(originalMetrics.ValuableTasksCount, restoredMetrics.ValuableTasksCount);
        Assert.Equal(originalMetrics.EfficiencyPercent, restoredMetrics.EfficiencyPercent);
        Assert.Equal(originalMetrics.WorkTimeDays, restoredMetrics.WorkTimeDays);
        Assert.Equal(originalMetrics.BufferTimeDays, restoredMetrics.BufferTimeDays);
    }

    [Fact]
    public void ToDomainSimulation_WorkerTookTask_And_WorkerCompletedTask_HaveSameCorrelationId()
    {
        // Arrange
        var config = CreateSingleWorkerConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Симулируем 6 дней
        for (var i = 0; i < 6; i++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Act - сериализуем и восстанавливаем
        var dto = ApiMapper.ToApiDto(simulation);
        var restoredSimulation = ApiMapper.ToDomainSimulation(dto);

        // Assert - проверяем что CorrelationId совпадают после восстановления
        var restoredActivities = restoredSimulation.History.SelectMany(d => d.Activities).ToList();
        var tookTasks = restoredActivities.Where(a => a.Type == ActivityType.WorkerTookTask).ToList();
        var completedTasks = restoredActivities.Where(a => a.Type == ActivityType.WorkerCompletedTask).ToList();

        foreach (var took in tookTasks)
        {
            Assert.NotEqual(Guid.Empty, took.CorrelationId);

            var completed = completedTasks.FirstOrDefault(c => c.CorrelationId == took.CorrelationId);
            if (completed != null)
            {
                Assert.Equal(took.CorrelationId, completed.CorrelationId);
                Assert.Equal(took.TaskKey, completed.TaskKey);
            }
        }
    }

    private static SimulationConfig CreateSingleWorkerConfig()
    {
        var stages = new List<Stage>
        {
            new()
            {
                Name = "Todo",
                Type = StageType.Buffer,
                IsLeadTimeStart = false,
                CreatesValue = true,
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
                CreatesValue = true,
                Transitions = []
            }
        };

        stages[0].Transitions.Add(new StageTransition { Stage = stages[1], Probability = 1.0 });
        stages[1].Transitions.Add(new StageTransition { Stage = stages[2], Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = [
                new() { Login = "dev1", Skills = ["backend"], WipLimit = 1, Performance = 100 }
            ],
            Workflow = new Workflow { Stages = stages },
            Tasks = [
                new() { Key = "TASK-1", Summary = "Задача размера S", ShirtType = TShirtType.S, RequiredSkills = ["backend"] },
                new() { Key = "TASK-2", Summary = "Задача размера M", ShirtType = TShirtType.M, RequiredSkills = ["backend"] }
            ],
            UseVariability = true
        };
    }
}
