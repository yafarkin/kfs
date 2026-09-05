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
///     WorkerMetricsService на детерминированных прогонах (Seed=42, UseVariability=false).
///     Проверяются точные значения и инварианты, а не "число >= 0 / процент в [0,100]".
/// </summary>
public class WorkerMetricsServiceTests
{
    // --- happy path: один воркер, одна XS-задача, линейный Todo→Dev→Done ---

    [Fact]
    public void SingleWorker_SingleTask_ExactMetrics()
    {
        var sim = RunToCompletion(LinearConfig(costPerDay: 10, taskCount: 1));
        var m = Assert.Single(new WorkerMetricsService(sim).CalculateAllWorkersMetrics());

        var totalDays = sim.History.Count;

        Assert.Equal("w1", m.Login);
        Assert.Equal(1, m.ValuableTasksCount);
        Assert.Equal(Math.Round(1m / totalDays, 2), m.Throughput);
        Assert.Equal(10, m.CostPerDay);

        // Инвариант стоимости: workCost + bufferCost == totalCost == totalDays * costPerDay
        Assert.Equal(m.WorkCost + m.BufferCost, m.TotalCost);
        Assert.Equal(totalDays * 10, m.TotalCost);

        // Воркер отработал ровно один день (XS = 1 день) из totalDays
        Assert.Equal(10, m.WorkCost);
        Assert.Equal((totalDays - 1) * 10, m.BufferCost);
        Assert.Equal(1m, m.WorkTimeDays);
    }

    // --- воркер, которому не досталось работы: строго нули ---

    [Fact]
    public void WorkerWithoutAssignments_AllMetricsZero_ExceptIdleCost()
    {
        // Вторая роль (qa) есть в команде, но в workflow нет qa-стадии → воркер простаивает.
        var config = LinearConfig(costPerDay: 7, taskCount: 1);
        config.Workers.Add(new Worker { Login = "idle", Skills = ["qa"], Performance = 100, WipLimit = 1, CostPerDay = 7 });

        var sim = RunToCompletion(config);
        var idle = new WorkerMetricsService(sim).CalculateAllWorkersMetrics().Single(w => w.Login == "idle");
        var totalDays = sim.History.Count;

        Assert.Equal(0, idle.ValuableTasksCount);
        Assert.Equal(0m, idle.Throughput);
        Assert.Equal(0m, idle.LeadTime);
        Assert.Equal(0m, idle.EfficiencyPercent);
        Assert.Equal(0m, idle.WorkTimeDays);
        Assert.Equal(0, idle.WorkCost);
        Assert.Equal(totalDays * 7, idle.BufferCost);
        Assert.Equal(totalDays * 7, idle.TotalCost);
    }

    // --- сумма стоимости по воркерам сходится с общей стоимостью проекта ---

    [Fact]
    public void SumOfWorkerCosts_EqualsProjectTotalCost()
    {
        var sim = RunToCompletion(LinearConfig(costPerDay: 10, taskCount: 4));

        var workerMetrics = new WorkerMetricsService(sim).CalculateAllWorkersMetrics();
        var projectTotal = new MetricsService(sim).CalculateAllMetrics().TotalCost;

        Assert.Equal(projectTotal, workerMetrics.Sum(w => w.TotalCost));
        Assert.All(workerMetrics, w => Assert.Equal(w.WorkCost + w.BufferCost, w.TotalCost));
    }

    // --- буферные стадии не увеличивают ValuableTasksCount ---

    [Fact]
    public void BufferStageWork_DoesNotCountAsValuable()
    {
        // Dev создаёт ценность, Review — буфер (ценности не создаёт). Задача проходит обе.
        var todo = new Stage { Name = "Todo", Type = StageType.Buffer, IsLeadTimeStart = true, Transitions = [] };
        var dev = new Stage
        {
            Name = "Dev", Type = StageType.Work, CreatesValue = true, StageProgressPercent = 100,
            RequiredSkills = ["d"], Transitions = []
        };
        var review = new Stage
        {
            Name = "Review", Type = StageType.Work, CreatesValue = false, StageProgressPercent = 100,
            RequiredSkills = ["d"], Transitions = []
        };
        var done = new Stage { Name = "Done", Type = StageType.Buffer, Transitions = [] };
        todo.Transitions.Add(new StageTransition { Stage = dev, Probability = 1.0 });
        dev.Transitions.Add(new StageTransition { Stage = review, Probability = 1.0 });
        review.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        var config = new SimulationConfig
        {
            Seed = 42,
            UseVariability = false,
            Workers = [new() { Login = "w1", Skills = ["d"], Performance = 100, WipLimit = 1, CostPerDay = 10 }],
            Workflow = new Workflow { Stages = [todo, dev, review, done] },
            Tasks = [new() { Key = "TASK-1", ShirtType = TShirtType.XS, RequiredSkills = ["d"] }]
        };

        var sim = RunToCompletion(config);
        var m = Assert.Single(new WorkerMetricsService(sim).CalculateAllWorkersMetrics());

        // Задача завершена на ценной стадии Dev ровно один раз — не учитываем прохождение Review.
        Assert.Equal(1, m.ValuableTasksCount);
    }

    // --- активное время воркера = ОБЪЕДИНЕНИЕ интервалов [took, completed], а не сумма ---

    [Fact]
    public void OverlappingAssignments_ActiveTimeIsUnionNotSum()
    {
        // Строим историю руками: воркер держит две задачи с перекрытием по дням.
        // TASK-A: взял день 1, завершил день 3.  TASK-B: взял день 2, завершил день 4.
        // Объединение [1..4] = 4 активных дня (а сумма длин была бы 3+3 = 6).
        var config = LinearConfig(costPerDay: 5, taskCount: 1);
        var sim = new Simulation();
        sim.InitFromConfig(config);

        var dev = sim.Board.Stages.First(s => s.Stage.Name == "Dev");
        sim.StartNewDay(); sim.StartNewDay(); sim.StartNewDay(); sim.StartNewDay(); // 4 дня

        AddWorkerInterval(sim, day: 1, endDay: 3, worker: "w1", stage: dev, taskKey: "TASK-A");
        AddWorkerInterval(sim, day: 2, endDay: 4, worker: "w1", stage: dev, taskKey: "TASK-B");

        var m = Assert.Single(new WorkerMetricsService(sim).CalculateAllWorkersMetrics());

        Assert.Equal(4m, m.WorkTimeDays);                 // union, не 6
        Assert.Equal(Math.Round(4m / 4 * 100, 1), m.EfficiencyPercent); // 100.0
        Assert.Equal(4 * 5, m.WorkCost);
        Assert.Equal(0, m.BufferCost);
    }

    // --- helpers ---

    private static SimulationConfig LinearConfig(int costPerDay, int taskCount)
    {
        var todo = new Stage { Name = "Todo", Type = StageType.Buffer, IsLeadTimeStart = true, Transitions = [] };
        var dev = new Stage
        {
            Name = "Dev", Type = StageType.Work, CreatesValue = true, StageProgressPercent = 100,
            RequiredSkills = ["d"], Transitions = []
        };
        var done = new Stage { Name = "Done", Type = StageType.Buffer, Transitions = [] };
        todo.Transitions.Add(new StageTransition { Stage = dev, Probability = 1.0 });
        dev.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            UseVariability = false,
            Workers = [new() { Login = "w1", Skills = ["d"], Performance = 100, WipLimit = 1, CostPerDay = costPerDay }],
            Workflow = new Workflow { Stages = [todo, dev, done] },
            Tasks = Enumerable.Range(1, taskCount)
                .Select(i => new Task { Key = $"TASK-{i}", ShirtType = TShirtType.XS, RequiredSkills = ["d"] })
                .ToList()
        };
    }

    private static Simulation RunToCompletion(SimulationConfig config)
    {
        var sim = new Simulation();
        sim.InitFromConfig(config);
        var movement = new TaskMovementService(sim);
        var work = new WorkProgressService(sim);

        for (var day = 0; day < 100; day++)
        {
            var done = sim.Board.Stages.First(s => s.Stage.Name == "Done");
            if (done.Tasks.Count == sim.Board.Tasks.Count && sim.CurrentDay > 0)
            {
                break;
            }

            sim.StartNewDay();
            movement.ProcessMovements();
            var completed = work.SimulateWorkDay();
            if (completed.Count > 0)
            {
                movement.ProcessMovements(completed);
            }
        }

        return sim;
    }

    private static void AddWorkerInterval(
        Simulation sim, int day, int endDay, string worker, BoardStage stage, string taskKey)
    {
        var correlationId = Guid.NewGuid();
        sim.History[day - 1].AddActivity(new HistoryActivity
        {
            Type = ActivityType.WorkerTookTask,
            WorkerLogin = worker,
            TaskKey = taskKey,
            StageName = stage.Stage.Name,
            Stage = stage,
            CorrelationId = correlationId
        });
        sim.History[endDay - 1].AddActivity(new HistoryActivity
        {
            Type = ActivityType.WorkerCompletedTask,
            WorkerLogin = worker,
            TaskKey = taskKey,
            StageName = stage.Stage.Name,
            Stage = stage,
            CorrelationId = correlationId
        });
    }
}
