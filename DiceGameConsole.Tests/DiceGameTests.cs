using DiceGameConsole;

namespace KanbanFlowConsole.Tests;

public class DiceGameTests
{
    [Fact]
    public void DiceGame_CreatesWithCorrectParameters()
    {
        var game = new DiceGame(5, 10);
        
        // Проверяем, что игра создаётся без ошибок
        Assert.NotNull(game);
    }

    [Fact]
    public void DiceGame_SupportsInteractiveMode()
    {
        var game = new DiceGame(3, 5, interactive: true);
        
        Assert.NotNull(game);
    }

    [Fact]
    public void RollDice_ReturnsValueBetween1And6()
    {
        // Используем рефлексию для проверки приватного метода
        var game = new DiceGame(1, 1);
        var method = typeof(DiceGame).GetMethod("RollDice", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(method);
        
        // Проверяем несколько бросков
        for (int i = 0; i < 100; i++)
        {
            var result = method!.Invoke(game, null);
            var diceValue = Assert.IsType<int>(result);
            Assert.InRange(diceValue, 1, 6);
        }
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 5)]
    [InlineData(10, 20)]
    public void DiceGame_AcceptsVariousConfigurations(int workers, int rounds)
    {
        var game = new DiceGame(workers, rounds);
        Assert.NotNull(game);
    }

    [Fact]
    public void GameConfig_HasDefaultValues()
    {
        var config = new GameConfig();
        
        Assert.Equal(0, config.Workers);
        Assert.Equal(0, config.Rounds);
    }

    [Fact]
    public void GameConfig_CanSetValues()
    {
        var config = new GameConfig
        {
            Workers = 5,
            Rounds = 10
        };
        
        Assert.Equal(5, config.Workers);
        Assert.Equal(10, config.Rounds);
    }
}
