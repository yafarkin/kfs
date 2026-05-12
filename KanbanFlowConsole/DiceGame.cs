namespace KanbanFlowConsole;

/// <summary>
/// Конфигурация игры.
/// </summary>
public class GameConfig
{
    public int Workers { get; set; }
    public int Rounds { get; set; }
}

/// <summary>
/// Симуляция игры в кости из книги "Цель" Голдратта.
/// Demonstrates dependent events and statistical fluctuations in a production line.
/// </summary>
public class DiceGame
{
    private readonly int _workers;
    private readonly int _rounds;
    private readonly bool _interactive;
    private readonly Random _random = new();

    public DiceGame(int workers, int rounds, bool interactive = false)
    {
        _workers = workers;
        _rounds = rounds;
        _interactive = interactive;
    }

    public void Play()
    {
        var workers = new Worker[_workers];
        for (int i = 0; i < _workers; i++)
        {
            workers[i] = new Worker(i + 1);
        }

        Console.WriteLine($"\nИгра в кости: {_workers} рабочих, {_rounds} раундов\n");
        Console.WriteLine($"{"",-12} {"Бросок",-8} {"Получено",-10} {"Передано",-10} {"Накоплено"}");
        Console.WriteLine(new string('-', 60));

        int totalProduced = 0;

        for (int round = 1; round <= _rounds; round++)
        {
            if (_interactive)
            {
                Console.WriteLine($"\n=== Раунд {round} ===");
                Console.WriteLine("Нажмите Enter для броска кубика...");
                Console.ReadLine();
            }
            else
            {
                Console.WriteLine($"\nРаунд {round}:");
            }

            // Первый рабочий бросает кубик и получает материалы из бесконечного запаса
            int diceRoll = RollDice();
            workers[0].Process(diceRoll, diceRoll, unlimitedSupply: true);

            // Последующие рабочие получают то, что передал предыдущий
            for (int i = 1; i < _workers; i++)
            {
                diceRoll = RollDice();
                int passedFromPrev = workers[i - 1].LastPassed;
                workers[i].Process(diceRoll, passedFromPrev);
            }

            // Вывод статистики по раунду
            for (int i = 0; i < _workers; i++)
            {
                var w = workers[i];
                if (_interactive && i == 0)
                {
                    Console.WriteLine($"{"Рабочий " + w.Id,-12} {w.LastRoll,-8} {"-1",-10} {w.LastPassed,-10} {0}");
                }
                else if (_interactive)
                {
                    int passedFromPrev = workers[i - 1].LastPassed;
                    Console.WriteLine($"{"Рабочий " + w.Id,-12} {w.LastRoll,-8} {passedFromPrev,-10} {w.LastPassed,-10} {w.Accumulated}");
                }
                else
                {
                    Console.WriteLine($"{"Рабочий " + w.Id,-12} {w.LastRoll,-8} {w.LastReceived,-10} {w.LastPassed,-10} {w.Accumulated}");
                }
            }

            // Последний рабочий выпускает продукцию
            int finished = workers[_workers - 1].LastPassed;
            totalProduced += finished;

            if (_interactive)
            {
                Console.WriteLine($"\nВыпущено за раунд: {finished}, Всего: {totalProduced}");
            }
        }

        Console.WriteLine($"\n=== Итоги ===");
        Console.WriteLine($"Итого выпущено: {totalProduced}");
        Console.WriteLine($"Среднее за раунд: {totalProduced / (double)_rounds:F2}");
        Console.WriteLine($"Ожидаемое среднее (3.5 * {_rounds}): {_rounds * 3.5:F2}");
        Console.WriteLine($"\nЭффективность: {totalProduced / (_rounds * 3.5) * 100:F1}%");
    }

    private int RollDice() => _random.Next(1, 7);
}

/// <summary>
/// Представляет одного рабочего в производственной линии.
/// </summary>
public class Worker
{
    public int Id { get; }
    public int Accumulated { get; private set; }
    public int LastRoll { get; private set; }
    public int LastPassed { get; private set; }
    public int LastReceived { get; private set; }

    public Worker(int id)
    {
        Id = id;
        Accumulated = 0;
    }

    /// <summary>
    /// Обрабатывает ход: получает материалы, бросает кубик, передаёт дальше.
    /// </summary>
    /// <param name="diceRoll">Результат броска кубика (максимум что может обработать)</param>
    /// <param name="available">Сколько доступно для обработки (от предыдущего рабочего или запаса)</param>
    /// <param name="unlimitedSupply">true если материалы из неограниченного запаса</param>
    public void Process(int diceRoll, int available, bool unlimitedSupply = false)
    {
        LastRoll = diceRoll;
        LastReceived = unlimitedSupply ? -1 : available; // -1 означает неограниченный запас
        
        // Рабочий может обработать минимум из того, что выпало на кубике и что доступно
        int canProcess = Math.Min(diceRoll, available);
        
        LastPassed = canProcess;
        
        // Накопление: если не последний рабочий, сохраняем разницу
        if (!unlimitedSupply)
        {
            Accumulated = available - canProcess;
        }
    }
}
