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
    ///     Генератор случайных чисел для воспроизводимости (используется seed из конфига).
    ///     Обёртка со счётчиком вызовов для детерминированной перемотки состояния.
    /// </summary>
    public CountingRandom Random { get; private set; } = null!;

    /// <summary>
    ///     Количество вызовов Random.NextDouble для сериализации состояния.
    ///     Нужно для детерминированной перемотки Random при восстановлении.
    /// </summary>
    public int RandomCallCount => Random?.CallCount ?? 0;

    public void InitFromConfig(SimulationConfig config)
    {
        Config = config;
        Board = BoardMapper.MapToBoard(config);
        History = [];
        CurrentDay = 0;
        Random = new CountingRandom((int)config.Seed);
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

        activity.Day = History.Last();
        History.Last().AddActivity(activity);
    }

    /// <summary>
    ///     Восстановить состояние симуляции (день) из сериализованных данных.
    ///     Используется при загрузке состояния для продолжения симуляции.
    /// </summary>
    public void RestoreState(int currentDay, int randomCallCount = 0)
    {
        CurrentDay = currentDay;
        Random?.RewindTo(randomCallCount);
    }
}