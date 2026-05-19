using KanbanFlowConsole.Dtos;
using KanbanFlowConsole.Enums;

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
                Role = "Backend Developer",
                WipLimit = null,
                Performance = 100
            },
            Assignments = new List<BoardTaskAssignment>
            {
                new()
                {
                    Task = new BoardTask { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-1" } },
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
                Role = "Backend Developer",
                WipLimit = 2,
                Performance = 100
            },
            Assignments = new List<BoardTaskAssignment>
            {
                new()
                {
                    Task = new BoardTask { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-1" } },
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
                Role = "Backend Developer",
                WipLimit = 2,
                Performance = 100
            },
            Assignments = new List<BoardTaskAssignment>
            {
                new()
                {
                    Task = new BoardTask { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-1" } },
                    Stage = new BoardStage { Stage = new Stage { Name = "Developing" } }
                },
                new()
                {
                    Task = new BoardTask { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-2" } },
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
                Role = "Backend Developer",
                WipLimit = 1,
                Performance = 100
            },
            Assignments = new List<BoardTaskAssignment>
            {
                new()
                {
                    Task = new BoardTask { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-1" } },
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
            Worker = new Worker { Login = "dev1", Role = "Backend Developer", Performance = 100 },
            Assignments = new List<BoardTaskAssignment>
            {
                new()
                {
                    Task = new BoardTask { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-1" } },
                    Stage = new BoardStage { Stage = new Stage { Name = "Developing" } }
                },
                new()
                {
                    Task = new BoardTask { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-2" } },
                    Stage = new BoardStage { Stage = new Stage { Name = "Testing" } }
                },
                new()
                {
                    Task = new BoardTask { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-3" } },
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
                Role = "Backend Developer",
                WipLimit = 2,
                Performance = 100
            },
            Assignments = new List<BoardTaskAssignment>()
        };

        // Act & Assert
        Assert.True(worker.IsAvailable);
    }
}
