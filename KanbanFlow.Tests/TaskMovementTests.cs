using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Dtos.History;
using KanbanFlowSerivce.Enums;
using KanbanFlowSerivce.Services;
using Task = KanbanFlowSerivce.Dtos.Config.Task;

namespace KanbanFlow.Tests;

/// <summary>
///     Сценарии перемещения задач, не покрытые <see cref="TaskMovementServiceTests"/>:
///     переход с вероятностью 0 и правило "другого ресурса" (RequiresDifferentResource).
///     Прогон детерминированный (Seed=42, UseVariability=false), поэтому проверяем точные
///     стадии/воркеров, а не "хоть что-то произошло".
/// </summary>
public class TaskMovementTests
{
    [Fact]
    public void ProcessMovements_ZeroProbabilityTransition_TaskNeverLeavesStartStage()
    {
        var config = LinearConfig(todoToDevProbability: 0.0);
        var simulation = new Simulation();
        simulation.InitFromConfig(config);
        var movement = new TaskMovementService(simulation);
        var work = new WorkProgressService(simulation);

        for (var day = 0; day < 5; day++)
        {
            simulation.StartNewDay();
            movement.ProcessMovements();
            work.SimulateWorkDay();
        }

        var todo = simulation.Board.Stages.First(s => s.Stage.Name == "Todo");
        var developing = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");
        var task = simulation.Board.Tasks.Single();

        Assert.Equal(new[] { "TASK-1" }, todo.Tasks.Select(t => t.Task.Key).ToArray());
        Assert.Empty(developing.Tasks);
        Assert.Equal("Todo", task.CurrentStage?.Stage.Name);
        Assert.Null(task.Worker);
        Assert.Equal(0, task.Progress);
        Assert.DoesNotContain(
            simulation.History.SelectMany(d => d.Activities),
            a => a.Type == ActivityType.TaskMoved);
    }

    [Fact]
    public void ProcessMovements_RequiresDifferentResource_CodeReviewTakenByAnotherWorker()
    {
        var config = RequiresDifferentResourceConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);
        var movement = new TaskMovementService(simulation);
        var work = new WorkProgressService(simulation);

        for (var day = 0; day < 15; day++)
        {
            simulation.StartNewDay();
            movement.ProcessMovements();
            var completed = work.SimulateWorkDay();
            if (completed.Count > 0)
            {
                movement.ProcessMovements(completed);
            }
        }

        var done = simulation.Board.Stages.First(s => s.Stage.Name == "Done");
        var finalTask = simulation.Board.Tasks.Single();

        string TookBy(string stage) => simulation.History
            .SelectMany(d => d.Activities)
            .Single(a => a.Type == ActivityType.WorkerTookTask && a.StageName == stage)
            .WorkerLogin!;

        Assert.Equal("dev1", TookBy("Developing"));    // первый подходящий воркер по порядку
        Assert.Equal("dev2", TookBy("Code Review"));   // RequiresDifferentResource → не dev1
        Assert.Equal("Done", finalTask.CurrentStage?.Stage.Name);
        Assert.Single(done.Tasks);
    }

    // --- фикстуры ---

    private static SimulationConfig LinearConfig(double todoToDevProbability)
    {
        var todo = new Stage { Name = "Todo", Type = StageType.Buffer, Transitions = [] };
        var developing = new Stage
        {
            Name = "Developing", Type = StageType.Work, StageProgressPercent = 100,
            RequiredSkills = [], Transitions = []
        };
        var done = new Stage { Name = "Done", Type = StageType.Buffer, Transitions = [] };

        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = todoToDevProbability });
        developing.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            UseVariability = false,
            Workers = [new() { Login = "dev1", Skills = [], Performance = 100, WipLimit = 1 }],
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks = [new() { Key = "TASK-1", ShirtType = TShirtType.XS, RequiredSkills = [] }]
        };
    }

    private static SimulationConfig RequiresDifferentResourceConfig()
    {
        var todo = new Stage { Name = "Todo", Type = StageType.Buffer, Transitions = [] };
        var developing = new Stage
        {
            Name = "Developing", Type = StageType.Work, StageProgressPercent = 100,
            RequiredSkills = ["dev"], Transitions = []
        };
        var codeReview = new Stage
        {
            Name = "Code Review", Type = StageType.Work, StageProgressPercent = 100,
            RequiredSkills = ["dev"],
            RequiresDifferentResource = true,
            RequiresDifferentResourceFromStage = "Developing",
            Transitions = []
        };
        var done = new Stage { Name = "Done", Type = StageType.Buffer, Transitions = [] };

        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = codeReview, Probability = 1.0 });
        codeReview.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            UseVariability = false,
            Workers =
            [
                new() { Login = "dev1", Skills = ["dev"], Performance = 100, WipLimit = 1 },
                new() { Login = "dev2", Skills = ["dev"], Performance = 100, WipLimit = 1 }
            ],
            Workflow = new Workflow { Stages = [todo, developing, codeReview, done] },
            Tasks = [new() { Key = "TASK-1", ShirtType = TShirtType.XS, RequiredSkills = ["dev"] }]
        };
    }
}
