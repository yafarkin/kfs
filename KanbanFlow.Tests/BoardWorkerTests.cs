using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Board;
using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Enums;
using Task = KanbanFlowSerivce.Dtos.Config.Task;

namespace KanbanFlow.Tests;

public class BoardWorkerTests
{
    [Fact]
    public void IsAvailable_WithoutWipLimit_ReturnsTrue()
    {
        // Arrange
        var worker = new BoardWorker
        {
            Worker = new Worker
            {
                Login = "dev1",
                Skills = ["backend"],
                WipLimit = null,
                Performance = 100
            },
            Assignments = new List<BoardTaskAssignment>
            {
                new()
                {
                    Task = new BoardTask { Task = new Task { Key = "TASK-1" } },
                    Stage = new BoardStage { Stage = new Stage { Name = "Developing" } }
                }
            }
        };

        // Act & Assert
        Assert.True(worker.IsAvailable);
    }

    [Fact]
    public void IsAvailable_WithWipLimit_NotExceeded_ReturnsTrue()
    {
        // Arrange
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
                    Task = new BoardTask { Task = new Task { Key = "TASK-1" } },
                    Stage = new BoardStage { Stage = new Stage { Name = "Developing" } }
                }
            }
        };

        // Act & Assert
        Assert.True(worker.IsAvailable);
    }

    [Fact]
    public void IsAvailable_WithWipLimit_Exceeded_ReturnsFalse()
    {
        // Arrange
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
                    Task = new BoardTask { Task = new Task { Key = "TASK-1" } },
                    Stage = new BoardStage { Stage = new Stage { Name = "Developing" } }
                },
                new()
                {
                    Task = new BoardTask { Task = new Task { Key = "TASK-2" } },
                    Stage = new BoardStage { Stage = new Stage { Name = "Developing" } }
                }
            }
        };

        // Act & Assert
        Assert.False(worker.IsAvailable);
    }

    [Fact]
    public void IsAvailable_WithWipLimit_AtLimit_ReturnsFalse()
    {
        // Arrange
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
                    Task = new BoardTask { Task = new Task { Key = "TASK-1" } },
                    Stage = new BoardStage { Stage = new Stage { Name = "Developing" } }
                }
            }
        };

        // Act & Assert
        Assert.False(worker.IsAvailable);
    }

    [Fact]
    public void WipCount_ReturnsAssignmentsCount()
    {
        // Arrange
        var worker = new BoardWorker
        {
            Worker = new Worker { Login = "dev1", Skills = ["backend"], Performance = 100 },
            Assignments = new List<BoardTaskAssignment>
            {
                new()
                {
                    Task = new BoardTask { Task = new Task { Key = "TASK-1" } },
                    Stage = new BoardStage { Stage = new Stage { Name = "Developing" } }
                },
                new()
                {
                    Task = new BoardTask { Task = new Task { Key = "TASK-2" } },
                    Stage = new BoardStage { Stage = new Stage { Name = "Testing" } }
                },
                new()
                {
                    Task = new BoardTask { Task = new Task { Key = "TASK-3" } },
                    Stage = new BoardStage { Stage = new Stage { Name = "Developing" } }
                }
            }
        };

        // Act & Assert
        Assert.Equal(3, worker.WipCount);
    }

    [Fact]
    public void IsAvailable_EmptyAssignments_ReturnsTrue()
    {
        // Arrange
        var worker = new BoardWorker
        {
            Worker = new Worker
            {
                Login = "dev1",
                Skills = ["backend"],
                WipLimit = 2,
                Performance = 100
            },
            Assignments = new List<BoardTaskAssignment>()
        };

        // Act & Assert
        Assert.True(worker.IsAvailable);
    }

    [Fact]
    public void WipCount_DoesNotIncludeCompletedTasks()
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
    public void IsAvailable_True_WhenAllTasksCompleted_ButNotMovedYet()
    {
        // Arrange
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
    }

    [Fact]
    public void WipCount_OnlyWorkStages_CompletedTasksOnBufferStageNotCounted()
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

        // Assert - только задача на Work-стадии считается
        Assert.Equal(1, wipCount);
    }

    [Fact]
    public void Worker_CanTakeNewTask_WhenPreviousTaskCompleted_ButNotMovedYet()
    {
        // Arrange - воркер с WIP=1 завершил задачу, но она ещё не перемещена
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

    [Fact]
    public void Worker_NotAvailable_WhenHasActiveTasks()
    {
        // Arrange - воркер с WIP=1 имеет активную задачу (не завершена)
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
}
