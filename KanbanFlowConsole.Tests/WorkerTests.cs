using KanbanFlowConsole;

namespace KanbanFlowConsole.Tests;

public class WorkerTests
{
    [Fact]
    public void Process_WithUnlimitedSupply_PassesDiceRollAmount()
    {
        var worker = new Worker(1);
        
        worker.Process(5, 5, unlimitedSupply: true);
        
        Assert.Equal(5, worker.LastPassed);
        Assert.Equal(5, worker.LastRoll);
    }

    [Fact]
    public void Process_WithLimitedSupply_PassesMinimumOfDiceAndAvailable()
    {
        var worker = new Worker(2);
        
        // Кубик показал 5, но доступно только 3
        worker.Process(5, 3);
        
        Assert.Equal(3, worker.LastPassed);
        Assert.Equal(0, worker.Accumulated);
    }

    [Fact]
    public void Process_WithLimitedSupply_AccumulatesRemainder()
    {
        var worker = new Worker(3);
        
        // Кубик показал 2, доступно 5
        worker.Process(2, 5);
        
        Assert.Equal(2, worker.LastPassed);
        Assert.Equal(3, worker.Accumulated); // 5 - 2 = 3 осталось
    }

    [Fact]
    public void Process_WithDiceRollLessThanAvailable_PassesDiceRoll()
    {
        var worker = new Worker(4);
        
        worker.Process(1, 6);
        
        Assert.Equal(1, worker.LastPassed);
        Assert.Equal(5, worker.Accumulated);
    }

    [Fact]
    public void Worker_InitializesWithZeroAccumulated()
    {
        var worker = new Worker(1);
        
        Assert.Equal(0, worker.Accumulated);
        Assert.Equal(1, worker.Id);
    }
}
