using KanbanFlowConsole.Dtos;
using KanbanFlowConsole.Enums;

namespace KanbanFlow.Tests;

public class BoardStageTests
{
    [Fact]
    public void WipCount_ReturnsTasksCount()
    {
        // Arrange
        var stage = new BoardStage
        {
            Stage = new Stage { Name = "Developing", Type = StageType.Work },
            Tasks = new List<BoardTask>
            {
                new() { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-1" } },
                new() { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-2" } },
                new() { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-3" } }
            }
        };

        // Act & Assert
        Assert.Equal(3, stage.WipCount);
    }

    [Fact]
    public void IsWipExceeded_WithoutWipLimit_ReturnsFalse()
    {
        // Arrange
        var stage = new BoardStage
        {
            Stage = new Stage { Name = "Developing", Type = StageType.Work, WipLimit = null },
            Tasks = new List<BoardTask>
            {
                new() { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-1" } },
                new() { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-2" } }
            }
        };

        // Act & Assert
        Assert.False(stage.IsWipExceeded);
    }

    [Fact]
    public void IsWipExceeded_WithWipLimit_NotExceeded_ReturnsFalse()
    {
        // Arrange
        var stage = new BoardStage
        {
            Stage = new Stage { Name = "Developing", Type = StageType.Work, WipLimit = 3 },
            Tasks = new List<BoardTask>
            {
                new() { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-1" } },
                new() { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-2" } }
            }
        };

        // Act & Assert
        Assert.False(stage.IsWipExceeded);
    }

    [Fact]
    public void IsWipExceeded_WithWipLimit_Exceeded_ReturnsTrue()
    {
        // Arrange
        var stage = new BoardStage
        {
            Stage = new Stage { Name = "Developing", Type = StageType.Work, WipLimit = 2 },
            Tasks = new List<BoardTask>
            {
                new() { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-1" } },
                new() { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-2" } },
                new() { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-3" } }
            }
        };

        // Act & Assert
        Assert.True(stage.IsWipExceeded);
    }

    [Fact]
    public void IsWipExceeded_WithWipLimit_AtLimit_ReturnsFalse()
    {
        // Arrange
        var stage = new BoardStage
        {
            Stage = new Stage { Name = "Developing", Type = StageType.Work, WipLimit = 2 },
            Tasks = new List<BoardTask>
            {
                new() { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-1" } },
                new() { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-2" } }
            }
        };

        // Act & Assert
        Assert.False(stage.IsWipExceeded);
    }

    [Fact]
    public void CanAcceptTasks_WithoutWipLimit_ReturnsTrue()
    {
        // Arrange
        var stage = new BoardStage
        {
            Stage = new Stage { Name = "Developing", Type = StageType.Work, WipLimit = null },
            Tasks = new List<BoardTask>
            {
                new() { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-1" } }
            }
        };

        // Act & Assert
        Assert.True(stage.CanAcceptTasks);
    }

    [Fact]
    public void CanAcceptTasks_WithWipLimit_NotExceeded_ReturnsTrue()
    {
        // Arrange
        var stage = new BoardStage
        {
            Stage = new Stage { Name = "Developing", Type = StageType.Work, WipLimit = 3 },
            Tasks = new List<BoardTask>
            {
                new() { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-1" } },
                new() { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-2" } }
            }
        };

        // Act & Assert
        Assert.True(stage.CanAcceptTasks);
    }

    [Fact]
    public void CanAcceptTasks_WithWipLimit_AtLimit_ReturnsFalse()
    {
        // Arrange
        var stage = new BoardStage
        {
            Stage = new Stage { Name = "Developing", Type = StageType.Work, WipLimit = 2 },
            Tasks = new List<BoardTask>
            {
                new() { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-1" } },
                new() { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-2" } }
            }
        };

        // Act & Assert
        Assert.False(stage.CanAcceptTasks);
    }

    [Fact]
    public void CanAcceptTasks_WithWipLimit_Exceeded_ReturnsFalse()
    {
        // Arrange
        var stage = new BoardStage
        {
            Stage = new Stage { Name = "Developing", Type = StageType.Work, WipLimit = 2 },
            Tasks = new List<BoardTask>
            {
                new() { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-1" } },
                new() { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-2" } },
                new() { Task = new KanbanFlowConsole.Dtos.Task { Key = "TASK-3" } }
            }
        };

        // Act & Assert
        Assert.False(stage.CanAcceptTasks);
    }

    [Fact]
    public void CanAcceptTasks_EmptyTasks_ReturnsTrue()
    {
        // Arrange
        var stage = new BoardStage
        {
            Stage = new Stage { Name = "Developing", Type = StageType.Work, WipLimit = 2 },
            Tasks = new List<BoardTask>()
        };

        // Act & Assert
        Assert.True(stage.CanAcceptTasks);
    }
}
