using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Board;
using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Enums;
using Task = KanbanFlowSerivce.Dtos.Config.Task;

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
                new() { Task = new Task { Key = "TASK-1" } },
                new() { Task = new Task { Key = "TASK-2" } },
                new() { Task = new Task { Key = "TASK-3" } }
            }
        };

        // Act & Assert
        Assert.Equal(3, stage.WipCount);
    }

    [Theory]
    [InlineData(null, 2, false)]           // No WIP limit, 2 tasks → not exceeded
    [InlineData(3, 2, false)]              // WIP 3, 2 tasks → not exceeded
    [InlineData(2, 2, false)]              // WIP 2, 2 tasks → at limit, not exceeded
    [InlineData(2, 3, true)]               // WIP 2, 3 tasks → exceeded
    public void IsWipExceeded_VariousScenarios_ReturnsCorrectResult(int? wipLimit, int taskCount, bool expectedExceeded)
    {
        // Arrange
        var stage = new BoardStage
        {
            Stage = new Stage { Name = "Developing", Type = StageType.Work, WipLimit = wipLimit },
            Tasks = new List<BoardTask>()
        };

        for (var i = 0; i < taskCount; i++)
        {
            stage.Tasks.Add(new BoardTask { Task = new Task { Key = $"TASK-{i + 1}" } });
        }

        // Act & Assert
        Assert.Equal(expectedExceeded, stage.IsWipExceeded);
    }

    [Theory]
    [InlineData(null, 1, true)]            // No WIP limit → can accept
    [InlineData(3, 2, true)]               // WIP 3, 2 tasks → can accept
    [InlineData(2, 2, false)]              // WIP 2, 2 tasks → at limit, cannot accept
    [InlineData(2, 3, false)]              // WIP 2, 3 tasks → exceeded, cannot accept
    [InlineData(2, 0, true)]               // WIP 2, 0 tasks → can accept
    public void CanAcceptTasks_VariousScenarios_ReturnsCorrectResult(int? wipLimit, int taskCount, bool expectedCanAccept)
    {
        // Arrange
        var stage = new BoardStage
        {
            Stage = new Stage { Name = "Developing", Type = StageType.Work, WipLimit = wipLimit },
            Tasks = new List<BoardTask>()
        };

        for (var i = 0; i < taskCount; i++)
        {
            stage.Tasks.Add(new BoardTask { Task = new Task { Key = $"TASK-{i + 1}" } });
        }

        // Act & Assert
        Assert.Equal(expectedCanAccept, stage.CanAcceptTasks);
    }
}
