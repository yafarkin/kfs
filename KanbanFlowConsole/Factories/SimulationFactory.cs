using System.Text.Json;
using System.Text.Json.Serialization;
using KanbanFlowConsole.Dtos;
using KanbanFlowConsole.Dtos.Board;
using KanbanFlowConsole.Dtos.Config;
using KanbanFlowConsole.Enums;
using BoardTask = KanbanFlowConsole.Dtos.Config.Task;

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
    ///     Создаёт конфигурацию симуляции по умолчанию (как в smoke тестах)
    /// </summary>
    public static SimulationConfig CreateDefaultConfig()
    {
        // Создаём стадии
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            IsStart = true,
            IsLeadTimeStart = true,
            RequiredSkills = [],
            Transitions = new List<StageTransition>()
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            IsStart = false,
            IsLeadTimeStart = false,
            RequiredSkills = ["backend"],
            StageProgressPercent = 100,
            Transitions = new List<StageTransition>()
        };

        var readyForTesting = new Stage
        {
            Name = "Ready for Testing",
            Type = StageType.Buffer,
            IsStart = false,
            IsLeadTimeStart = false,
            RequiredSkills = [],
            Transitions = new List<StageTransition>()
        };

        var testing = new Stage
        {
            Name = "Testing",
            Type = StageType.Work,
            IsStart = false,
            IsLeadTimeStart = false,
            RequiredSkills = ["qa"],
            StageProgressPercent = 30,
            Transitions = new List<StageTransition>()
        };

        var releasePreparation = new Stage
        {
            Name = "Release Preparation",
            Type = StageType.Work,
            IsStart = false,
            IsLeadTimeStart = false,
            RequiredSkills = ["backend"],
            StageProgressPercent = 20,
            Transitions = new List<StageTransition>()
        };

        var done = new Stage
        {
            Name = "Done",
            Type = StageType.Buffer,
            IsStart = false,
            IsLeadTimeStart = false,
            RequiredSkills = [],
            Transitions = new List<StageTransition>()
        };

        // Устанавливаем DAG переходы (прямые ссылки)
        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = readyForTesting, Probability = 1.0 });
        readyForTesting.Transitions.Add(new StageTransition { Stage = testing, Probability = 1.0 });
        testing.Transitions.Add(new StageTransition { Stage = releasePreparation, Probability = 1.0 });
        releasePreparation.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = new List<Worker>
            {
                new()
                {
                    Login = "dev1",
                    Skills = ["backend"],
                    WipLimit = 1,
                    Performance = 100
                },
                new()
                {
                    Login = "qa1",
                    Skills = ["qa"],
                    WipLimit = 1,
                    Performance = 100
                }
            },
            Workflow = new Workflow
            {
                Stages = new List<Stage> { todo, developing, readyForTesting, testing, releasePreparation, done }
            },
            Tasks = new List<BoardTask>
            {
                new()
                {
                    Key = "TASK-1",
                    Summary = "Реализовать API для пользователей",
                    ShirtType = TShirtType.S,
                    RequiredSkills = ["backend"]
                },
                new()
                {
                    Key = "TASK-2",
                    Summary = "Написать тесты для сервиса",
                    ShirtType = TShirtType.M,
                    RequiredSkills = ["backend"]
                }
            }
        };
    }

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
                    RequiredSkills = stage.RequiredSkills,
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
