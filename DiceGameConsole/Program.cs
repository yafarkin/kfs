using System.Text.Json;
using DiceGameConsole;

await RunAsync();

async Task RunAsync()
{
    var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    var config = new GameConfig { Workers = 5, Rounds = 10 };

    if (File.Exists(configPath))
    {
        try
        {
            var json = await File.ReadAllTextAsync(configPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var deserialized = JsonSerializer.Deserialize<GameConfig>(json, options);
            if (deserialized != null && deserialized.Workers > 0 && deserialized.Rounds > 0)
            {
                config = deserialized;
            }
        }
        catch
        {
            // Игнорируем ошибки чтения конфига, используем значения по умолчанию
        }
    }

    Console.WriteLine("=== Игра в кости из книги \"Цель\" ===\n");
    Console.WriteLine($"Настройки: {config.Workers} рабочих, {config.Rounds} раундов");
    Console.WriteLine("\nВыберите режим:");
    Console.WriteLine("1. Быстрый (все раунды сразу)");
    Console.WriteLine("2. Интерактивный (по шагам)");
    Console.Write("\nВаш выбор (1/2): ");

    var choice = Console.ReadLine();
    bool interactive = choice == "2";

    var game = new DiceGame(config.Workers, config.Rounds, interactive);
    game.Play();
}
