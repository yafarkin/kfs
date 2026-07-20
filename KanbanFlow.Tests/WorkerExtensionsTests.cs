using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Enums;

namespace KanbanFlow.Tests;

/// <summary>
///     Тесты для WorkerExtensions.GetDaysForTask
/// </summary>
public class WorkerExtensionsTests
{
    [Theory]
    [InlineData(100, 11)]  // Performance 100% → среднее (7+15)/2 = 11
    [InlineData(50, 22)]   // Performance 50% → в 2 раза медленнее: 11 * 2 = 22
    [InlineData(200, 6)]   // Performance 200% → в 2 раза быстрее: 11 / 2 = 5.5 → 6
    public void GetDaysForTask_NoVariability_PerformanceAsMultiplier(int performance, int expectedDays)
    {
        // Arrange
        var worker = new Worker
        {
            Login = "dev1",
            Skills = ["backend"],
            Performance = performance,
            DeviationDownPercent = 0,
            DeviationUpPercent = 0
        };

        var stage = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            StageProgressPercent = 100
        };

        // Act - L размер: 7-15 дней, без вариативности → среднее 11 дней
        // Performance применяется как множитель: 100% = 1x, 50% = 2x, 200% = 0.5x
        var days = worker.GetDaysForTask(stage, TShirtType.L, useVariability: false);

        // Assert
        Assert.Equal(expectedDays, days);
    }

    [Fact]
    public void GetDaysForTask_WithVariability_RandomInRange()
    {
        // Arrange
        var worker = new Worker
        {
            Login = "dev1",
            Skills = ["backend"],
            Performance = 100, // без влияния
            DeviationDownPercent = 0,
            DeviationUpPercent = 0
        };

        var stage = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            StageProgressPercent = 100
        };

        var random = new CountingRandom(42);

        // Act - L размер: 7-15 дней
        // Случайное значение в диапазоне [7, 15]
        var days = worker.GetDaysForTask(stage, TShirtType.L, useVariability: true, random);

        // Assert - значение в диапазоне [7, 15]
        Assert.InRange(days, 7, 15);
    }

    [Fact]
    public void GetDaysForTask_WithDeviation_VariabilityAppliesAfterPerformance()
    {
        // Arrange
        var worker = new Worker
        {
            Login = "dev1",
            Skills = ["backend"],
            Performance = 100, // baseEstimate = среднее = 11
            DeviationDownPercent = 20, // -20%
            DeviationUpPercent = 50    // +50%
        };

        var stage = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            StageProgressPercent = 100
        };

        var random = new CountingRandom(42);

        // Act - L размер: 7-15 дней
        // baseEstimate = 11 (среднее)
        // estimateDown = 11 * 0.8 = 8.8
        // estimateUp = 11 * 1.5 = 16.5
        // random выбирает между 8.8 и 16.5
        var days = worker.GetDaysForTask(stage, TShirtType.L, useVariability: true, random);

        // Assert - значение в разумном диапазоне
        Assert.InRange(days, 9, 17);
    }

    [Theory]
    [InlineData(100)]   // 100% performance
    [InlineData(50)]    // 50% performance
    public void GetDaysForTask_StageProgressPercent_AppliedCorrectly(int performance)
    {
        // Arrange
        var worker = new Worker
        {
            Login = "dev1",
            Skills = ["backend"],
            Performance = performance,
            DeviationDownPercent = 0,
            DeviationUpPercent = 0
        };

        var stage = new Stage
        {
            Name = "Code Review",
            Type = StageType.Work,
            StageProgressPercent = 25 // 25% от оценки
        };

        // Act - L размер: 7-15 дней, 25% стадии
        // StageProgressPercent: min=2 (7*0.25=1.75→2), max=4 (15*0.25=3.75→4)
        // Без variability → среднее = 3
        // Performance применяется к среднему
        var days = worker.GetDaysForTask(stage, TShirtType.L, useVariability: false);

        // Assert - значение должно быть в разумном диапазоне
        // При 100%: 3 дня, при 50%: 6 дней
        Assert.InRange(days, 2, 7);
    }

    [Fact]
    public void GetDaysForTask_StageProgressPercent_ZeroPerformance_TreatedAs100()
    {
        // Arrange
        var worker = new Worker
        {
            Login = "dev1",
            Skills = ["backend"],
            Performance = 0, // 0% трактуется как 100% (защита от деления на ноль)
            DeviationDownPercent = 0,
            DeviationUpPercent = 0
        };

        var stage = new Stage
        {
            Name = "Code Review",
            Type = StageType.Work,
            StageProgressPercent = 25
        };

        // Act
        var days = worker.GetDaysForTask(stage, TShirtType.L, useVariability: false);

        // Assert - как при 100% performance
        Assert.InRange(days, 2, 4);
    }

    [Fact]
    public void GetDaysForTask_FullFormula_IntegrationTest()
    {
        // Arrange
        var worker = new Worker
        {
            Login = "qa1",
            Skills = ["qa"],
            Performance = 80,
            DeviationDownPercent = 30,
            DeviationUpPercent = 40
        };

        var stage = new Stage
        {
            Name = "Testing",
            Type = StageType.Work,
            StageProgressPercent = 30
        };

        // Act - M размер: 4-6 дней
        // StageProgressPercent (30%): min=2 (4*0.3=1.2→2), max=2 (6*0.3=1.8→2)
        // Без variability → среднее = 2
        // Performance 80%: 2 * (100/80) = 2.5 → 3
        var days = worker.GetDaysForTask(stage, TShirtType.M, useVariability: false);

        // Assert
        Assert.Equal(3, days);
    }

    [Fact]
    public void GetDaysForTask_NoShirtSize_ReturnsOneDay()
    {
        // Arrange
        var worker = new Worker
        {
            Login = "dev1",
            Skills = ["backend"],
            Performance = 100
        };

        var stage = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            StageProgressPercent = 100
        };

        // Act
        var days = worker.GetDaysForTask(stage, null, useVariability: false);

        // Assert
        Assert.Equal(1, days);
    }

    [Fact]
    public void GetDaysForTask_HighPerformance_ReducesDuration()
    {
        // Arrange
        var worker100 = new Worker { Login = "w1", Performance = 100, Skills = [], DeviationDownPercent = 0, DeviationUpPercent = 0 };
        var worker200 = new Worker { Login = "w2", Performance = 200, Skills = [], DeviationDownPercent = 0, DeviationUpPercent = 0 };
        var worker400 = new Worker { Login = "w3", Performance = 400, Skills = [], DeviationDownPercent = 0, DeviationUpPercent = 0 };

        var stage = new Stage { Name = "Dev", Type = StageType.Work, StageProgressPercent = 100 };

        // Act
        var days100 = worker100.GetDaysForTask(stage, TShirtType.L, useVariability: false);
        var days200 = worker200.GetDaysForTask(stage, TShirtType.L, useVariability: false);
        var days400 = worker400.GetDaysForTask(stage, TShirtType.L, useVariability: false);

        // Assert
        // 100%: 11 дней (среднее 7-15)
        // 200%: 6 дней (11 / 2)
        // 400%: 3 дня (11 / 4)
        Assert.Equal(11, days100);
        Assert.Equal(6, days200);
        Assert.Equal(3, days400);
        Assert.True(days200 < days100, "200% должен быть быстрее 100%");
        Assert.True(days400 < days200, "400% должен быть быстрее 200%");
    }
}
