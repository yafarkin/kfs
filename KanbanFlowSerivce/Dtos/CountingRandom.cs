namespace KanbanFlowSerivce.Dtos;

/// <summary>
/// Обёртка над Random со счётчиком вызовов NextDouble для детерминированной перемотки состояния.
/// </summary>
public sealed class CountingRandom
{
    private readonly Random _random;

    /// <summary>
    /// Количество вызовов NextDouble с момента создания или последней сбросы.
    /// </summary>
    public int CallCount { get; private set; }

    public CountingRandom(int seed)
    {
        _random = new Random(seed);
        CallCount = 0;
    }

    /// <summary>
    /// Возвращает случайное число в диапазоне [0.0, 1.0).
    /// </summary>
    public double NextDouble()
    {
        CallCount++;
        return _random.NextDouble();
    }

    /// <summary>
    /// Перемотать случайность до указанного количества вызовов.
    /// </summary>
    public void RewindTo(int targetCallCount)
    {
        while (CallCount < targetCallCount)
        {
            NextDouble();
        }
    }
}
