using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Enums;

namespace KanbanFlow.Tests;

public class SimulationInitTests
{
    [Fact]
    public void InitFromConfig_CreatesBoard_WithCorrectStages()
    {
        // Arrange
        var config = TestConfigFactory.CreateDefaultConfig();
        var simulation = new Simulation();

        // Act
        simulation.InitFromConfig(config);

        // Assert
        Assert.NotNull(simulation.Board);
        Assert.Equal(6, simulation.Board.Stages.Count);

        var stageNames = simulation.Board.Stages.Select(s => s.Stage.Name).ToList();
        Assert.Equal(new[] { "Todo", "Developing", "Ready for Testing", "Testing", "Release Preparation", "Done" }, stageNames);
    }

    [Fact]
    public void InitFromConfig_CreatesBoard_WithCorrectWorkers()
    {
        // Arrange
        var config = TestConfigFactory.CreateDefaultConfig();
        var simulation = new Simulation();

        // Act
        simulation.InitFromConfig(config);

        // Assert
        Assert.Equal(2, simulation.Board.Workers.Count);
        
        var workerLogins = simulation.Board.Workers.Select(w => w.Worker.Login).ToList();
        Assert.Contains("dev1", workerLogins);
        Assert.Contains("qa1", workerLogins);
    }

    [Fact]
    public void InitFromConfig_CreatesBoard_WithCorrectTasks()
    {
        // Arrange
        var config = TestConfigFactory.CreateDefaultConfig();
        var simulation = new Simulation();

        // Act
        simulation.InitFromConfig(config);

        // Assert
        Assert.Equal(2, simulation.Board.Tasks.Count);
        Assert.All(simulation.Board.Tasks, t => Assert.Equal(0, t.Progress));
    }

    [Fact]
    public void InitFromConfig_DistributesTasks_ToStartStage()
    {
        // Arrange
        var config = TestConfigFactory.CreateDefaultConfig();
        var simulation = new Simulation();

        // Act
        simulation.InitFromConfig(config);

        // Assert
        var todoStage = simulation.Board.Stages.First(s => s.Stage.Name == "Todo");
        Assert.Equal(2, todoStage.Tasks.Count);
        
        var otherStages = simulation.Board.Stages.Where(s => s.Stage.Name != "Todo");
        Assert.All(otherStages, s => Assert.Empty(s.Tasks));
    }

    [Fact]
    public void InitFromConfig_LinksStages_Correctly()
    {
        // Arrange
        var config = TestConfigFactory.CreateDefaultConfig();
        var simulation = new Simulation();

        // Act
        simulation.InitFromConfig(config);

        // Assert
        var todoStage = simulation.Board.Stages.First(s => s.Stage.Name == "Todo");
        var developingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");
        var doneStage = simulation.Board.Stages.First(s => s.Stage.Name == "Done");

        // Todo имеет 1 следующую стадию и 0 предыдущих
        Assert.Single(todoStage.NextStages);    
        Assert.Empty(todoStage.PrevStages);
        Assert.Equal("Developing", todoStage.NextStages[0].Stage.Name);

        // Developing имеет 1 предыдущую и 1 следующую
        Assert.Single(developingStage.PrevStages);
        Assert.Single(developingStage.NextStages);
        Assert.Equal("Todo", developingStage.PrevStages[0].Stage.Name);
        Assert.Equal("Ready for Testing", developingStage.NextStages[0].Stage.Name);

        // Done имеет 1 предыдущую и 0 следующих
        Assert.Single(doneStage.PrevStages);
        Assert.Empty(doneStage.NextStages);
        Assert.Equal("Release Preparation", doneStage.PrevStages[0].Stage.Name);
    }

    [Fact]
    public void InitFromConfig_SetsConfig_Property()
    {
        // Arrange
        var config = TestConfigFactory.CreateDefaultConfig();
        var simulation = new Simulation();

        // Act
        simulation.InitFromConfig(config);

        // Assert
        Assert.Same(config, simulation.Config);
    }
}
