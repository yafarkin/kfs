using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Dtos.History;
using KanbanFlowSerivce.Mappers;

namespace KanbanFlowSerivce.Dtos;

public sealed record Simulation
{
    public SimulationConfig Config { get; private set; } = null!;
    public Board.Board Board { get; set; } = null!;

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

    /// <summary>
    ///     Генератор случайных чисел для воспроизводимости (используется seed из конфига)
    /// </summary>
    public Random Random { get; private set; } = null!;

    public void InitFromConfig(SimulationConfig config)
    {
        Config = config;
        Board = BoardMapper.MapToBoard(config);
        History = [];
        CurrentDay = 0;
        CurrentTick = 0;
        Random = new Random((int)config.Seed);
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

    /// <summary>
    ///     Восстановить состояние симуляции (день и тик) из сериализованных данных.
    ///     Используется при загрузке состояния для продолжения симуляции.
    /// </summary>
    public void RestoreState(int currentDay, int currentTick)
    {
        CurrentDay = currentDay;
        CurrentTick = currentTick;
    }
}