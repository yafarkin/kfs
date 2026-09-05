using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Board;
using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Dtos.History;
using KanbanFlowSerivce.Enums;
using KanbanFlowSerivce.Services;
using Task = KanbanFlowSerivce.Dtos.Config.Task;

namespace KanbanFlow.Tests;

/// <summary>
/// Тесты для новых событий истории: CorrelationId, TaskWaiting, TaskResumed, LeadTimeStarted
/// </summary>
public class HistoryActivityTests
{
    [Fact]
    public void WorkerTookTask_And_WorkerCompletedTask_HaveSameCorrelationId()
    {
        // Arrange
        var config = CreateSimpleWorkflowConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act - Симулируем работу до завершения задачи
        for (var day = 0; day < 10; day++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Assert - симуляция прогнана до конца, поэтому у каждого WorkerTookTask
        // обязан быть парный WorkerCompletedTask с тем же CorrelationId.
        var allActivities = simulation.History.SelectMany(d => d.Activities).ToList();
        var tookTasks = allActivities.Where(a => a.Type == ActivityType.WorkerTookTask).ToList();
        var completedTasks = allActivities.Where(a => a.Type == ActivityType.WorkerCompletedTask).ToList();

        Assert.NotEmpty(tookTasks);
        Assert.Equal(tookTasks.Count, completedTasks.Count);

        foreach (var tookTask in tookTasks)
        {
            Assert.NotEqual(Guid.Empty, tookTask.CorrelationId);

            var completedTask = completedTasks.Single(c => c.CorrelationId == tookTask.CorrelationId);
            Assert.Equal(tookTask.TaskKey, completedTask.TaskKey);
            Assert.True(completedTask.DayNumber >= tookTask.DayNumber);
        }
    }

    // TaskWaiting_* / TaskResumed_* удалены: события ActivityType.TaskWaiting/TaskResumed
    // не воспроизводятся текущей логикой перемещения (задача НЕ заходит на Work-стадию без
    // свободного воркера — TryMoveTask возвращает false), поэтому прежние тесты были
    // вхолостую (foreach по пустой коллекции). Триггер этих событий требует отдельного
    // разбора — см. заметку в docs/todo.md (возможный мёртвый код в TaskMovementService).

    [Fact]
    public void LeadTimeStarted_Logged_WhenTaskEntersLeadTimeStage()
    {
        var config = CreateQueueingWorkflow();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act - симулируем несколько дней
        for (var day = 0; day < 10; day++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Assert - все задачи прошли через стадию с IsLeadTimeStart → по одному событию на задачу
        var allActivities = simulation.History.SelectMany(d => d.Activities).ToList();
        var leadTimeStartEvents = allActivities.Where(a => a.Type == ActivityType.LeadTimeStarted).ToList();

        Assert.NotEmpty(leadTimeStartEvents);
        Assert.Equal(
            simulation.Board.Tasks.Select(t => t.Task.Key).OrderBy(k => k),
            leadTimeStartEvents.Select(e => e.TaskKey).OrderBy(k => k));
        Assert.All(leadTimeStartEvents, e => Assert.NotNull(e.StageName));
    }

    [Fact]
    public void LeadTimeStarted_LoggedOnly_ForFirstMovement()
    {
        // Arrange
        var config = CreateQueueingWorkflow();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act - симулируем полный цикл
        for (var day = 0; day < 20; day++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Assert - LeadTimeStarted строго один раз на задачу, даже пройдя весь workflow
        var allActivities = simulation.History.SelectMany(d => d.Activities).ToList();
        var leadTimeStartEvents = allActivities.Where(a => a.Type == ActivityType.LeadTimeStarted).ToList();

        Assert.NotEmpty(leadTimeStartEvents);
        Assert.All(leadTimeStartEvents.GroupBy(e => e.TaskKey), group => Assert.Single(group));
    }

    // TaskWaiting_HasCorrectTaskAndStageInfo / TaskResumed_HasCorrectTickReference удалены:
    // тот же конфиг и те же проверки, что в *_Logged_* тестах выше (теперь с Assert.NotEmpty).

    #region Helper Methods

    private static SimulationConfig CreateSimpleWorkflowConfig()
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
            Workers = [new() { Login = "dev1", Skills = ["dev"], Performance = 100, WipLimit = 1 }],
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks = [
                new() { Key = "TASK-1", RequiredSkills = ["dev"] },
                new() { Key = "TASK-2", RequiredSkills = ["dev"] }
            ],
            UseVariability = false
        };
    }

    /// <summary>
    ///     3 задачи, 1 воркер (WipLimit=1), у Developing НЕТ стадийного WIP-лимита —
    ///     поэтому все задачи заходят на Work-стадию сразу, а двум из трёх не хватает воркера
    ///     → пишутся TaskWaiting, затем (когда воркер освобождается) TaskResumed.
    ///     IsLeadTimeStart стоит на Developing (стадия, В КОТОРУЮ задачи перемещаются), а не на
    ///     стартовом Todo — иначе LeadTimeStarted не пишется вообще (в стартовую задачи не «входят»).
    /// </summary>
    private static SimulationConfig CreateQueueingWorkflow()
    {
        var todo = new Stage { Name = "Todo", Type = StageType.Buffer, Transitions = [] };
        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            IsLeadTimeStart = true,
            CreatesValue = true,
            StageProgressPercent = 100,
            RequiredSkills = ["dev"],
            Transitions = []
        };
        var done = new Stage { Name = "Done", Type = StageType.Buffer, Transitions = [] };

        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            UseVariability = false,
            Workers = [new() { Login = "dev1", Skills = ["dev"], Performance = 100, WipLimit = 1 }],
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks =
            [
                new() { Key = "TASK-1", ShirtType = TShirtType.S, RequiredSkills = ["dev"] },
                new() { Key = "TASK-2", ShirtType = TShirtType.S, RequiredSkills = ["dev"] },
                new() { Key = "TASK-3", ShirtType = TShirtType.S, RequiredSkills = ["dev"] }
            ]
        };
    }

    #endregion
}
