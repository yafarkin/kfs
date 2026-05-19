using KanbanFlowConsole.Dtos.History;
using KanbanFlowConsole.Mappers;

namespace KanbanFlowConsole.Dtos;

public sealed record Simulation
{
    public SimulationConfig Config { get; private set; } = null!;
    public Board Board { get; set; } = null!;

    /// <summary>
    ///     История действий симуляции по дням
    /// </summary>
    public List<HistoryDay> History { get; set; } = new();

    /// <summary>
    ///     Текущий день симуляции
    /// </summary>
    public int CurrentDay { get; private set; }

    /// <summary>
    ///     Текущий тик симуляции
    /// </summary>
    public int CurrentTick { get; private set; }

    public void InitFromConfig(SimulationConfig config)
    {
        Config = config;
        Board = BoardMapper.MapToBoard(config);
        History = new List<HistoryDay>();
        CurrentDay = 0;
        CurrentTick = 0;
    }

    /// <summary>
    ///     Начать новый день симуляции
    /// </summary>
    public void StartNewDay()
    {
        CurrentDay++;
        History.Add(new HistoryDay { DayNumber = CurrentDay });
    }

    /// <summary>
    ///     Добавить событие в историю текущего дня
    /// </summary>
    public void LogActivity(HistoryActivity activity)
    {
        if (History.Count == 0)
        {
            StartNewDay();
        }

        activity.Tick = CurrentTick;
        History.Last().AddActivity(activity);
    }

    /// <summary>
    ///     Обновить текущий тик симуляции
    /// </summary>
    public void AdvanceTick(int ticks = 1)
    {
        CurrentTick += ticks;
    }
}