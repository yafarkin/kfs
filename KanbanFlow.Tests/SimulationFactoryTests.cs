using System.Text.Json;
using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Enums;
using KanbanFlowSerivce.Factories;
using Task = KanbanFlowSerivce.Dtos.Config.Task;

namespace KanbanFlow.Tests;

public class SimulationFactoryTests
{
    [Fact]
    public void CreateFromConfig_CreatesSimulation()
    {
        // Arrange
        var config = CreateSampleConfig();

        // Act
        var simulation = SimulationFactory.CreateFromConfig(config);

        // Assert
        Assert.NotNull(simulation);
        Assert.NotNull(simulation.Board);
        Assert.Equal(3, simulation.Board.Tasks.Count);
        Assert.Equal(2, simulation.Board.Workers.Count);
    }

    [Fact]
    public void SerializeToJson_SerializesConfig()
    {
        // Arrange
        var config = CreateSampleConfig();
        var simulation = SimulationFactory.CreateFromConfig(config);

        // Act
        var json = SimulationFactory.SerializeToJson(simulation);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("TASK-1", json);
        Assert.Contains("Developing", json);
        Assert.Contains("dev1", json);
    }

    [Fact]
    public void CreateFromJson_DeserializesAndCreatesSimulation()
    {
        // Arrange
        var config = CreateSampleConfig();
        var simulation = SimulationFactory.CreateFromConfig(config);
        var json = SimulationFactory.SerializeToJson(simulation);

        // Act
        var simulation2 = SimulationFactory.CreateFromJson(json);

        // Assert
        Assert.NotNull(simulation2);
        Assert.Equal(simulation.Board.Tasks.Count, simulation2.Board.Tasks.Count);
        Assert.Equal(simulation.Board.Workers.Count, simulation2.Board.Workers.Count);
        Assert.Equal(simulation.Board.Stages.Count, simulation2.Board.Stages.Count);
    }

    [Fact]
    public void SaveToFile_And_LoadFromFile_RoundTrip()
    {
        // Arrange
        var config = CreateSampleConfig();
        var simulation = SimulationFactory.CreateFromConfig(config);
        var filePath = Path.Combine(Path.GetTempPath(), $"simulation_test_{Guid.NewGuid()}.json");

        try
        {
            // Act - Сохраняем
            SimulationFactory.SaveToFile(simulation, filePath);

            // Assert - Файл создан
            Assert.True(File.Exists(filePath));

            // Act - Загружаем
            var simulation2 = SimulationFactory.LoadFromFile(filePath);

            // Assert
            Assert.NotNull(simulation2);
            Assert.Equal(simulation.Board.Tasks.Count, simulation2.Board.Tasks.Count);
            Assert.Equal(simulation.Board.Workers.Count, simulation2.Board.Workers.Count);
        }
        finally
        {
            // Cleanup
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void CreateFromJson_InvalidJson_ThrowsException()
    {
        // Arrange
        var invalidJson = "{ invalid json }";

        // Act & Assert
        Assert.ThrowsAny<JsonException>(() =>
            SimulationFactory.CreateFromJson(invalidJson));
    }

    private static SimulationConfig CreateSampleConfig()
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
            IsLeadTimeStart = false,
            StageProgressPercent = 100,
            Transitions = new List<StageTransition>()
        };

        var done = new Stage
        {
            Name = "Done",
            Type = StageType.Buffer,
            IsLeadTimeStart = false,
            
            Transitions = new List<StageTransition>()
        };

        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers =
            [
                new() {Login = "dev1", Skills = ["dev"], Performance = 100},
                new() {Login = "dev2", Skills = ["dev"], Performance = 100}
            ],
            Workflow = new Workflow
            {
                Stages = [todo, developing, done]
            },
            Tasks =
            [
                new() {Key = "TASK-1", Summary = "Task 1", RequiredSkills = ["dev"]},
                new() {Key = "TASK-2", Summary = "Task 2", RequiredSkills = ["dev"]},
                new() {Key = "TASK-3", Summary = "Task 3", RequiredSkills = ["dev"]}
            ]
        };
    }
}