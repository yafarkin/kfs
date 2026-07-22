using KanbanFlowSerivce.Dtos.Board;
using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Enums;
using Task = KanbanFlowSerivce.Dtos.Config.Task;

namespace KanbanFlow.Tests;

/// <summary>
/// Тесты для проверки доступности воркера после завершения задачи (Progress = 100%),
/// но до физического перемещения задачи (из-за WIP-лимита следующей стадии).
/// </summary>
public class WorkerAvailabilityAfterCompletionTests
{
    [Fact]
    public void WipCount_ExcludesCompletedTasks()
    {
        // Arrange - воркер с завершёнными и активными задачами
        var worker = new BoardWorker
        {
            Worker = new Worker
            {
                Login = "dev1",
                Skills = ["backend"],
                WipLimit = 2,
                Performance = 100
            },
            Assignments = new List<BoardTaskAssignment>
            {
                new()
                {
                    Task = new BoardTask { Task = new Task { Key = "TASK-1" }, Progress = 100 },
                    Stage = new BoardStage { Stage = new Stage { Name = "Developing", Type = StageType.Work } }
                },
                new()
                {
                    Task = new BoardTask { Task = new Task { Key = "TASK-2" }, Progress = 50 },
                    Stage = new BoardStage { Stage = new Stage { Name = "Developing", Type = StageType.Work } }
                },
                new()
                {
                    Task = new BoardTask { Task = new Task { Key = "TASK-3" }, Progress = 100 },
                    Stage = new BoardStage { Stage = new Stage { Name = "Testing", Type = StageType.Work } }
                }
            }
        };

        // Act
        var wipCount = worker.WipCount;

        // Assert - только незавершённая задача считается
        Assert.Equal(1, wipCount);
    }

    [Fact]
    public void IsAvailable_True_WhenAllTasksCompleted_ButWipLimitReached()
    {
        // Arrange - воркер с WIP=1 и завершённой задачей
        var worker = new BoardWorker
        {
            Worker = new Worker
            {
                Login = "dev1",
                Skills = ["backend"],
                WipLimit = 1,
                Performance = 100
            },
            Assignments = new List<BoardTaskAssignment>
            {
                new()
                {
                    Task = new BoardTask { Task = new Task { Key = "TASK-1" }, Progress = 100 },
                    Stage = new BoardStage { Stage = new Stage { Name = "Developing", Type = StageType.Work } }
                }
            }
        };

        // Act & Assert
        // Воркер доступен, несмотря на задачу в assignments — она завершена
        Assert.True(worker.IsAvailable);
        Assert.Equal(0, worker.WipCount);
    }

    [Fact]
    public void IsAvailable_False_WhenHasActiveTask()
    {
        // Arrange - воркер с WIP=1 и активной задачей
        var worker = new BoardWorker
        {
            Worker = new Worker
            {
                Login = "dev1",
                Skills = ["backend"],
                WipLimit = 1,
                Performance = 100
            },
            Assignments = new List<BoardTaskAssignment>
            {
                new()
                {
                    Task = new BoardTask { Task = new Task { Key = "TASK-1" }, Progress = 50 },
                    Stage = new BoardStage { Stage = new Stage { Name = "Developing", Type = StageType.Work } }
                }
            }
        };

        // Act & Assert
        // Воркер НЕ доступен - задача активна
        Assert.False(worker.IsAvailable);
        Assert.Equal(1, worker.WipCount);
    }

    [Fact]
    public void WipCount_DoesNotIncludeCompletedTasks_OnBufferStages()
    {
        // Arrange
        var worker = new BoardWorker
        {
            Worker = new Worker { Login = "dev1", Skills = ["backend"], Performance = 100 },
            Assignments = new List<BoardTaskAssignment>
            {
                new()
                {
                    Task = new BoardTask { Task = new Task { Key = "TASK-1" }, Progress = 100 },
                    Stage = new BoardStage { Stage = new Stage { Name = "Ready for Testing", Type = StageType.Buffer } }
                },
                new()
                {
                    Task = new BoardTask { Task = new Task { Key = "TASK-2" }, Progress = 50 },
                    Stage = new BoardStage { Stage = new Stage { Name = "Developing", Type = StageType.Work } }
                }
            }
        };

        // Act
        var wipCount = worker.WipCount;

        // Assert - только задача на Work-стадии считается (и она не завершена)
        Assert.Equal(1, wipCount);
    }

    [Fact]
    public void Worker_CanTakeNewTask_WhenPreviousCompleted_UnitTest()
    {
        // Arrange - воркер с WIP=1 завершил задачу
        var worker = new BoardWorker
        {
            Worker = new Worker
            {
                Login = "dev1",
                Skills = ["backend", "qa"],
                WipLimit = 1,
                Performance = 100
            },
            Assignments = new List<BoardTaskAssignment>
            {
                new()
                {
                    Task = new BoardTask { Task = new Task { Key = "TASK-1" }, Progress = 100 },
                    Stage = new BoardStage { Stage = new Stage { Name = "Developing", Type = StageType.Work } }
                }
            }
        };

        // Act & Assert
        // Воркер доступен для новой задачи, несмотря на TASK-1 в assignments
        Assert.True(worker.IsAvailable);
        Assert.Equal(0, worker.WipCount);
    }
}
