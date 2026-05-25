using KanbanFlowConsole.Dtos;
using KanbanFlowConsole.Dtos.Board;
using KanbanFlowConsole.Dtos.Config;
using KanbanFlowConsole.Dtos.History;
using KanbanFlowConsole.Enums;
using Task = KanbanFlowConsole.Dtos.Config.Task;

namespace KanbanFlow.Tests;

public class HistoryTests
{
    [Fact]
    public void HistoryDay_AddActivity_SetsReverseLink()
    {
        // Arrange
        var day = new HistoryDay { DayNumber = 1 };
        var activity = new HistoryActivity
        {
            Type = ActivityType.WorkerTookTask,
            Description = "worker dev1 took task TASK-1"
        };

        // Act
        day.AddActivity(activity);

        // Assert
        Assert.Same(day, activity.Day);
        Assert.Single(day.Activities);
        Assert.Contains(activity, day.Activities);
    }

    [Fact]
    public void HistoryDay_ActivityCount_ReturnsActivitiesCount()
    {
        // Arrange
        var day = new HistoryDay { DayNumber = 1 };
        day.AddActivity(new HistoryActivity { Type = ActivityType.WorkerTookTask, Description = "a1" });
        day.AddActivity(new HistoryActivity { Type = ActivityType.TaskMoved, Description = "a2" });
        day.AddActivity(new HistoryActivity { Type = ActivityType.TaskProgressUpdated, Description = "a3" });

        // Act & Assert
        Assert.Equal(3, day.ActivityCount);
    }

    [Fact]
    public void HistoryActivity_ContainsBoardReferences()
    {
        // Arrange
        var task = new BoardTask { Task = new Task { Key = "TASK-1" } };
        var worker = new BoardWorker { Worker = new Worker { Login = "dev1", Role = "Backend Developer", Skills = ["backend"], Performance = 100 } };
        var stage = new BoardStage { Stage = new Stage { Name = "Developing", Type = StageType.Work } };

        var activity = new HistoryActivity
        {
            Type = ActivityType.TaskMoved,
            Description = "task TASK-1 moved to Developing",
            Task = task,
            Worker = worker,
            Stage = stage,
            Progress = 50
        };

        // Act & Assert
        Assert.Same(task, activity.Task);
        Assert.Same(worker, activity.Worker);
        Assert.Same(stage, activity.Stage);
        Assert.Equal(50, activity.Progress);
    }

    [Fact]
    public void BoardTask_TransitionHistory_IsInitialized()
    {
        // Arrange
        var task = new BoardTask { Task = new Task { Key = "TASK-1" } };

        // Act & Assert
        Assert.NotNull(task.TransitionHistory);
        Assert.Empty(task.TransitionHistory);
    }

    [Fact]
    public void BoardTask_AddTransitionHistory()
    {
        // Arrange
        var task = new BoardTask { Task = new Task { Key = "TASK-1" } };
        var fromStage = new BoardStage { Stage = new Stage { Name = "Todo", Type = StageType.Buffer } };
        var toStage = new BoardStage { Stage = new Stage { Name = "Developing", Type = StageType.Work } };
        var day = new HistoryDay { DayNumber = 1 };
        var activity = new HistoryActivity
        {
            Type = ActivityType.TaskMoved,
            Description = "task TASK-1 moved to Developing",
            Day = day
        };

        var transition = new TaskTransitionHistory
        {
            Activity = activity,
            FromStage = fromStage,
            ToStage = toStage,
            Tick = 10
        };

        // Act
        task.TransitionHistory.Add(transition);

        // Assert
        Assert.Single(task.TransitionHistory);
        Assert.Same(activity, task.TransitionHistory[0].Activity);
        Assert.Same(fromStage, task.TransitionHistory[0].FromStage);
        Assert.Same(toStage, task.TransitionHistory[0].ToStage);
        Assert.Equal(10, task.TransitionHistory[0].Tick);
    }

    [Fact]
    public void BoardTask_CurrentStage_CanBeSet()
    {
        // Arrange
        var task = new BoardTask { Task = new Task { Key = "TASK-1" } };
        var stage = new BoardStage { Stage = new Stage { Name = "Developing", Type = StageType.Work } };

        // Act
        task.CurrentStage = stage;

        // Assert
        Assert.Same(stage, task.CurrentStage);
    }

    [Fact]
    public void TaskTransitionHistory_ContainsAllData()
    {
        // Arrange
        var activity = new HistoryActivity { Type = ActivityType.TaskMoved, Description = "moved" };
        var fromStage = new BoardStage { Stage = new Stage { Name = "Todo" } };
        var toStage = new BoardStage { Stage = new Stage { Name = "Developing" } };

        var transition = new TaskTransitionHistory
        {
            Activity = activity,
            FromStage = fromStage,
            ToStage = toStage,
            Tick = 25
        };

        // Act & Assert
        Assert.Same(activity, transition.Activity);
        Assert.Same(fromStage, transition.FromStage);
        Assert.Same(toStage, transition.ToStage);
        Assert.Equal(25, transition.Tick);
    }

    [Fact]
    public void HistoryDay_MultipleActivities_AllHaveReverseLinks()
    {
        // Arrange
        var day = new HistoryDay { DayNumber = 2 };
        var activities = new List<HistoryActivity>
        {
            new() { Type = ActivityType.WorkerTookTask, Description = "a1" },
            new() { Type = ActivityType.TaskMoved, Description = "a2" },
            new() { Type = ActivityType.TaskProgressUpdated, Description = "a3" }
        };

        // Act
        foreach (var activity in activities)
        {
            day.AddActivity(activity);
        }

        // Assert
        foreach (var activity in activities)
        {
            Assert.Same(day, activity.Day);
        }
    }
}
