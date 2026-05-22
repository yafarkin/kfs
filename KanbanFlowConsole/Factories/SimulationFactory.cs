using System.Text.Json;
using System.Text.Json.Serialization;
using KanbanFlowConsole.Dtos;
using KanbanFlowConsole.Dtos.Board;
using KanbanFlowConsole.Dtos.Config;

namespace KanbanFlowConsole.Factories;

/// <summary>
///     Фабрика для создания и сериализации симуляций
/// </summary>
public static class SimulationFactory
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    /// <summary>
    ///     Создаёт объект Simulation из конфигурации
    /// </summary>
    public static Simulation CreateFromConfig(SimulationConfig config)
    {
        var simulation = new Simulation();
        simulation.InitFromConfig(config);
        return simulation;
    }

    /// <summary>
    ///     Создаёт объект Simulation из JSON строки
    /// </summary>
    /// <param name="json">JSON строка с конфигурацией симуляции</param>
    /// <returns>Объект Simulation</returns>
    public static Simulation CreateFromJson(string json)
    {
        var config = JsonSerializer.Deserialize<SimulationConfig>(json, JsonSerializerOptions)
                     ?? throw new InvalidOperationException("Не удалось десериализовать конфигурацию симуляции");

        return CreateFromConfig(config);
    }

    /// <summary>
    ///     Сериализует конфигурацию симуляции в JSON строку
    /// </summary>
    /// <param name="simulation">Объект симуляции</param>
    /// <returns>JSON строка с конфигурацией</returns>
    public static string SerializeToJson(Simulation simulation)
    {
        var config = ExtractConfig(simulation);
        return JsonSerializer.Serialize(config, JsonSerializerOptions);
    }

    /// <summary>
    ///     Сохраняет конфигурацию симуляции в файл
    /// </summary>
    /// <param name="simulation">Объект симуляции</param>
    /// <param name="filePath">Путь к файлу</param>
    public static void SaveToFile(Simulation simulation, string filePath)
    {
        var json = SerializeToJson(simulation);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    ///     Загружает симуляцию из файла
    /// </summary>
    /// <param name="filePath">Путь к файлу</param>
    /// <returns>Объект Simulation</returns>
    public static Simulation LoadFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return CreateFromJson(json);
    }

    /// <summary>
    ///     Извлекает конфигурацию из существующей симуляции
    /// </summary>
    private static SimulationConfig ExtractConfig(Simulation simulation)
    {
        // Извлекаем воркеров
        var workers = simulation.Board.Workers
            .Select(w => w.Worker)
            .ToList();

        // Извлекаем задачи
        var tasks = simulation.Board.Tasks
            .Select(t => t.Task)
            .ToList();

        // Извлекаем стадии с переходами (по именам)
        var stages = simulation.Board.Stages
            .Select(s =>
            {
                var stage = s.Stage;
                // Создаём копию стадии с новыми переходами (чтобы избежать циклических ссылок)
                var stageCopy = new Stage
                {
                    Name = stage.Name,
                    Type = stage.Type,
                    IsStart = stage.IsStart,
                    IsLeadTimeStart = stage.IsLeadTimeStart,
                    WipLimit = stage.WipLimit,
                    AllowedRoles = stage.AllowedRoles,
                    RequiresDifferentResource = stage.RequiresDifferentResource,
                    RequiresDifferentResourceFromStage = stage.RequiresDifferentResourceFromStage,
                    StageProgressPercent = stage.StageProgressPercent,
                    Transitions = s.Stage.Transitions
                        .Select(t => new StageTransition
                        {
                            Stage = t.Stage, // Сохраняем ссылку на стадию
                            Probability = t.Probability
                        })
                        .ToList()
                };
                return stageCopy;
            })
            .ToList();

        return new SimulationConfig
        {
            Seed = 42, // Seed не сохраняется в симуляции, используем значение по умолчанию
            Workers = workers,
            Workflow = new Workflow
            {
                Stages = stages
            },
            Tasks = tasks
        };
    }
}
