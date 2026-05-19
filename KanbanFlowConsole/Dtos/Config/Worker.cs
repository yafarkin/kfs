namespace KanbanFlowConsole.Dtos.Config;

public sealed record Worker
{
    public string Login { get; set; } = null!;
    public string Role { get; set; } = null!;
    public int? WipLimit { get; set; }

    /// <summary>
    ///     Производительность ресурса в процентах (100 = стандартная скорость)
    /// </summary>
    public double Performance { get; set; }
}