using System.Text.Json;
using System.Text.Json.Serialization;
using KanbanFlowSerivce.Dtos.Board;
using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Enums;
using BoardTask = KanbanFlowSerivce.Dtos.Config.Task;
using Task = KanbanFlowSerivce.Dtos.Config.Task;

namespace KanbanFlowSerivce.Factories;

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
            IsLeadTimeStart = true,
            RequiredSkills = [],
            Transitions = new List<StageTransition>()
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            RequiredSkills = ["backend", "frontend"],
            StageProgressPercent = 100,
            Transitions = new List<StageTransition>()
        };

        var readyForTesting = new Stage
        {
            Name = "Ready for Testing",
            Type = StageType.Buffer,
            IsLeadTimeStart = false,
            RequiredSkills = [],
            Transitions = new List<StageTransition>()
        };

        var testing = new Stage
        {
            Name = "Testing",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            RequiredSkills = ["qa"],
            StageProgressPercent = 30,
            Transitions = []
        };

        var readyToMerge = new Stage
        {
            Name = "Ready to Merge",
            Type = StageType.Buffer,
            Transitions = []
        };

        var releasePreparation = new Stage
        {
            Name = "Release Preparation",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            RequiredSkills = ["backend", "frontend"],
            StageProgressPercent = 10,
            Transitions = []
        };

        var done = new Stage
        {
            Name = "Done",
            Type = StageType.Buffer,
            IsLeadTimeStart = false,
            RequiredSkills = [],
            Transitions = []
        };

        // Устанавливаем DAG переходы (прямые ссылки)
        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = readyForTesting, Probability = 1.0 });
        readyForTesting.Transitions.Add(new StageTransition { Stage = testing, Probability = 1.0 });
        testing.Transitions.Add(new StageTransition { Stage = readyToMerge, Probability = 1.0 });
        readyToMerge.Transitions.Add(new StageTransition { Stage = releasePreparation, Probability = 1.0 });
        releasePreparation.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers =
            [
                new()
                {
                    Login = "dev1-be",
                    Skills = ["backend"],
                    WipLimit = 1,
                    Performance = 100
                },

                new()
                {
                    Login = "dev2-fe",
                    Skills = ["frontend"],
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
            ],
            Workflow = new Workflow
            {
                Stages = [todo, developing, readyForTesting, testing, readyToMerge, releasePreparation, done]
            },
            Tasks =
            [
                new()
                {
                    Key = "TASK-1",
                    Summary = "[BE] Реализовать API для пользователей",
                    ShirtType = TShirtType.S,
                    RequiredSkills = ["backend", "qa"]
                },

                new()
                {
                    Key = "TASK-2",
                    Summary = "[BE] Написать тесты для сервиса",
                    ShirtType = TShirtType.M,
                    RequiredSkills = ["backend", "qa"]
                },

                new()
                {
                    Key = "TASK-3",
                    Summary = "[FE] Создать UI компонент формы",
                    ShirtType = TShirtType.S,
                    RequiredSkills = ["frontend"]
                }
            ]
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
