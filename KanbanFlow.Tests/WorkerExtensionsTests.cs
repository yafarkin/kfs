using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Enums;

namespace KanbanFlow.Tests;

/// <summary>
///     Тесты для WorkerExtensions.GetDaysForTask
/// </summary>
public class WorkerExtensionsTests
{
    [Theory]
    [InlineData(100, 7)]   // Performance 100% → min days
    [InlineData(50, 11)]   // Performance 50% → average days
    [InlineData(0, 15)]    // Performance 0% → max days
    public void GetDaysForTask_NoVariability_PerformanceAffectsDays(int performance, int expectedDays)
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

        // Act - L размер: 7-15 дней
        var days = worker.GetDaysForTask(stage, TShirtType.L, useVariability: false);

        // Assert
        Assert.Equal(expectedDays, days);
    }

    [Fact]
    public void GetDaysForTask_WithDeviation_VariabilityAppliesToBaseEstimate()
    {
        // Arrange
        var worker = new Worker
        {
            Login = "dev1",
            Skills = ["backend"],
            Performance = 100, // baseEstimate = min = 7
            DeviationDownPercent = 20, // -20%
            DeviationUpPercent = 50    // +50%
        };

        var stage = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            StageProgressPercent = 100
        };

        var random = new Random(42);

        // Act - L размер: 7-15 дней
        // baseEstimate = 7 (performance 100%)
        // estimateDown = 7 * 0.8 = 5.6
        // estimateUp = 7 * 1.5 = 10.5
        // random выбирает между 5.6 и 10.5
        var days = worker.GetDaysForTask(stage, TShirtType.L, useVariability: true, random);

        // Assert - значение в диапазоне [6, 11] (округление)
        Assert.InRange(days, 6, 11);
    }

    [Theory]
    [InlineData(100)]   // 100% performance
    [InlineData(50)]    // 50% performance  
    [InlineData(0)]     // 0% performance
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
        // min = 7 * 0.25 = 1.75 → 2
        // max = 15 * 0.25 = 3.75 → 4
        var days = worker.GetDaysForTask(stage, TShirtType.L, useVariability: false);

        // Assert - значение должно быть в разумном диапазоне
        Assert.InRange(days, 2, 4);
    }

    [Fact]
    public void GetDaysForTask_Variability_RandomInRange()
    {
        // Arrange
        var worker = new Worker
        {
            Login = "dev1",
            Skills = ["backend"],
            Performance = 50, // Середина
            DeviationDownPercent = 0,
            DeviationUpPercent = 0
        };

        var stage = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            StageProgressPercent = 100
        };

        var random = new Random(42); // Fixed seed для воспроизводимости

        // Act - L размер: 7-15 дней, performance 50% = 11 дней
        var days = worker.GetDaysForTask(stage, TShirtType.L, useVariability: true, random);

        // Assert - значение в диапазоне 7-15
        Assert.InRange(days, 7, 15);
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
        // Performance 80%: 2 + (2-2)*0.2 = 2
        // Без variability = 2
        var days = worker.GetDaysForTask(stage, TShirtType.M, useVariability: false);

        // Assert
        Assert.Equal(2, days);
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
}
