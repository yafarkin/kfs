using System.Text.Json;
using System.Text.Json.Serialization;
using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Enums;

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
            CreatesValue = false,
            Transitions = new List<StageTransition>()
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            RequiredSkills = ["backend", "frontend"],
            StageProgressPercent = 100,
            CreatesValue = true,
            Transitions = new List<StageTransition>()
        };

        var readyForTesting = new Stage
        {
            Name = "Ready for Testing",
            Type = StageType.Buffer,
            IsLeadTimeStart = false,
            RequiredSkills = [],
            CreatesValue = false,
            Transitions = new List<StageTransition>()
        };

        var testing = new Stage
        {
            Name = "Testing",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            RequiredSkills = ["qa"],
            StageProgressPercent = 30,
            CreatesValue = true,
            Transitions = []
        };

        var readyToMerge = new Stage
        {
            Name = "Ready to Merge",
            Type = StageType.Buffer,
            CreatesValue = false,
            Transitions = []
        };

        var releasePreparation = new Stage
        {
            Name = "Release Preparation",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            RequiredSkills = ["backend", "frontend"],
            StageProgressPercent = 10,
            CreatesValue = false,
            Transitions = []
        };

        var done = new Stage
        {
            Name = "Done",
            Type = StageType.Buffer,
            IsLeadTimeStart = false,
            RequiredSkills = [],
            CreatesValue = false,
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
                    Performance = 100,
                    DeviationDownPercent = 20,
                    DeviationUpPercent = 50
                },

                new()
                {
                    Login = "dev2-fe",
                    Skills = ["frontend"],
                    WipLimit = 1,
                    Performance = 100,
                    DeviationDownPercent = 20,
                    DeviationUpPercent = 50
                },

                new()
                {
                    Login = "qa1",
                    Skills = ["qa"],
                    WipLimit = 1,
                    Performance = 100,
                    DeviationDownPercent = 30,
                    DeviationUpPercent = 40
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
    ///     Создаёт конфигурацию TWork с полным workflow (Kanban + Shift-Left Testing)
    ///     Воркеры: 1 FE, 2 QA, 4 BE
    /// </summary>
    public static SimulationConfig CreateTWorkConfig()
    {
        // === PLANNING (upstream, не учитывается в lead time) ===
        var planning = new Stage
        {
            Name = "Planning",
            Type = StageType.Buffer,
            IsLeadTimeStart = false,
            RequiredSkills = [],
            Transitions = []
        };

        // === TO DO (точка принятия обязательств, начало lead time) ===
        var toDo = new Stage
        {
            Name = "To Do",
            Type = StageType.Buffer,
            IsLeadTimeStart = true,
            WipLimit = 5,
            RequiredSkills = [],
            Transitions = []
        };

        // === PREPARATION (подготовка к производству) ===
        var technicalSpec = new Stage
        {
            Name = "Technical Specification",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            RequiredSkills = ["backend"],
            StageProgressPercent = 25,
            WipLimit = 4,
            CreatesValue = false,
            Transitions = []
        };

        var waitingForApproval = new Stage
        {
            Name = "Waiting for Approval",
            Type = StageType.Buffer,
            IsLeadTimeStart = false,
            RequiredSkills = [],
            Transitions = []
        };

        var technicalReview = new Stage
        {
            Name = "Technical Review",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            RequiredSkills = ["backend"],
            StageProgressPercent = 15,
            WipLimit = 2,
            CreatesValue = false,
            Transitions = []
        };

        var waitingForTestSpec = new Stage
        {
            Name = "Waiting for Test Specification",
            Type = StageType.Buffer,
            IsLeadTimeStart = false,
            RequiredSkills = [],
            Transitions = []
        };

        var testSpec = new Stage
        {
            Name = "Test Specification",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            RequiredSkills = ["qa"],
            StageProgressPercent = 20,
            WipLimit = 2,
            CreatesValue = false,
            Transitions = []
        };

        // === DEVELOPING (стадия разработки) ===
        var readyToDevelop = new Stage
        {
            Name = "Ready to Develop",
            Type = StageType.Buffer,
            IsLeadTimeStart = false,
            RequiredSkills = [],
            Transitions = []
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            RequiredSkills = ["backend", "frontend"],
            StageProgressPercent = 100,
            WipLimit = 4,
            CreatesValue = true,
            Transitions = []
        };

        var readyForCodeReview = new Stage
        {
            Name = "Ready for Code Review",
            Type = StageType.Buffer,
            IsLeadTimeStart = false,
            RequiredSkills = [],
            Transitions = []
        };

        var codeReview = new Stage
        {
            Name = "Code Review",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            RequiredSkills = ["backend"],
            StageProgressPercent = 50,
            WipLimit = 4,
            CreatesValue = false,
            Transitions = []
        };

        // === TESTING (стадия тестирования) ===
        var readyForTesting = new Stage
        {
            Name = "Ready for Testing",
            Type = StageType.Buffer,
            IsLeadTimeStart = false,
            RequiredSkills = [],
            WipLimit = 8,
            Transitions = []
        };

        var testing = new Stage
        {
            Name = "Testing",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            RequiredSkills = ["qa"],
            StageProgressPercent = 30,
            WipLimit = 2,
            CreatesValue = true,
            Transitions = []
        };

        var designReview = new Stage
        {
            Name = "Design Review",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            RequiredSkills = ["frontend"],
            StageProgressPercent = 10,
            WipLimit = 2,
            CreatesValue = false,
            Transitions = []
        };

        var waitingForAutomation = new Stage
        {
            Name = "Waiting for Automation",
            Type = StageType.Buffer,
            IsLeadTimeStart = false,
            RequiredSkills = [],
            Transitions = []
        };

        var automation = new Stage
        {
            Name = "Automation",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            RequiredSkills = ["qa"],
            StageProgressPercent = 40,
            WipLimit = 2,
            CreatesValue = true,
            Transitions = []
        };

        // === RELEASING (подготовка к релизу) ===
        var readyToMerge = new Stage
        {
            Name = "Ready to Merge",
            Type = StageType.Buffer,
            IsLeadTimeStart = false,
            RequiredSkills = [],
            Transitions = []
        };

        var readyToRelease = new Stage
        {
            Name = "Ready to Release",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            RequiredSkills = ["backend", "frontend"],
            StageProgressPercent = 5,
            WipLimit = 5,
            CreatesValue = false,
            Transitions = []
        };

        // === DONE ===
        var done = new Stage
        {
            Name = "Done",
            Type = StageType.Buffer,
            IsLeadTimeStart = false,
            RequiredSkills = [],
            Transitions = []
        };

        // === Устанавливаем переходы (DAG) ===
        // Planning -> To Do
        planning.Transitions.Add(new StageTransition { Stage = toDo, Probability = 1.0 });

        // To Do -> Technical Specification
        toDo.Transitions.Add(new StageTransition { Stage = technicalSpec, Probability = 1.0 });

        // Technical Specification -> Waiting for Approval -> Technical Review
        technicalSpec.Transitions.Add(new StageTransition { Stage = waitingForApproval, Probability = 1.0 });
        waitingForApproval.Transitions.Add(new StageTransition { Stage = technicalReview, Probability = 1.0 });

        // Technical Review -> Waiting for Test Spec -> Test Specification
        technicalReview.Transitions.Add(new StageTransition { Stage = waitingForTestSpec, Probability = 1.0 });
        waitingForTestSpec.Transitions.Add(new StageTransition { Stage = testSpec, Probability = 1.0 });

        // Test Specification -> Ready to Develop
        testSpec.Transitions.Add(new StageTransition { Stage = readyToDevelop, Probability = 1.0 });

        // Ready to Develop -> Developing
        readyToDevelop.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });

        // Developing -> Ready for Code Review -> Code Review
        developing.Transitions.Add(new StageTransition { Stage = readyForCodeReview, Probability = 1.0 });
        readyForCodeReview.Transitions.Add(new StageTransition { Stage = codeReview, Probability = 1.0 });

        // Code Review -> Ready for Testing
        codeReview.Transitions.Add(new StageTransition { Stage = readyForTesting, Probability = 1.0 });

        // Ready for Testing -> Testing
        readyForTesting.Transitions.Add(new StageTransition { Stage = testing, Probability = 1.0 });

        // Testing -> Design Review (опционально) -> Waiting for Automation
        testing.Transitions.Add(new StageTransition { Stage = designReview, Probability = 0.1 });
        testing.Transitions.Add(new StageTransition { Stage = waitingForAutomation, Probability = 0.3 });
        testing.Transitions.Add(new StageTransition { Stage = readyToMerge, Probability = 0.6 });

        // Design Review -> Waiting for Automation
        designReview.Transitions.Add(new StageTransition { Stage = waitingForAutomation, Probability = 1.0 });

        // Waiting for Automation -> Automation
        waitingForAutomation.Transitions.Add(new StageTransition { Stage = automation, Probability = 1.0 });

        // Automation -> Ready to Merge
        automation.Transitions.Add(new StageTransition { Stage = readyToMerge, Probability = 1.0 });

        // Ready to Merge -> Ready to Release
        readyToMerge.Transitions.Add(new StageTransition { Stage = readyToRelease, Probability = 1.0 });

        // Ready to Release -> Done
        readyToRelease.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers =
            [
                // 4 Backend разработчика
                new() { Login = "be-dev-1", Skills = ["backend"], WipLimit = 1, Performance = 100, DeviationDownPercent = 20, DeviationUpPercent = 50 },
                new() { Login = "be-dev-2", Skills = ["backend"], WipLimit = 1, Performance = 100, DeviationDownPercent = 20, DeviationUpPercent = 50 },
                new() { Login = "be-dev-3", Skills = ["backend"], WipLimit = 1, Performance = 100, DeviationDownPercent = 20, DeviationUpPercent = 50 },
                new() { Login = "be-dev-4", Skills = ["backend"], WipLimit = 1, Performance = 100, DeviationDownPercent = 20, DeviationUpPercent = 50 },

                // 1 Frontend разработчик
                new() { Login = "fe-dev-1", Skills = ["frontend"], WipLimit = 1, Performance = 100, DeviationDownPercent = 20, DeviationUpPercent = 50 },

                // 2 QA инженера
                new() { Login = "qa-eng-1", Skills = ["qa"], WipLimit = 1, Performance = 100, DeviationDownPercent = 30, DeviationUpPercent = 40 },
                new() { Login = "qa-eng-2", Skills = ["qa"], WipLimit = 1, Performance = 100, DeviationDownPercent = 30, DeviationUpPercent = 40 }
            ],
            Workflow = new Workflow
            {
                Stages = [
                    planning, toDo,
                    technicalSpec, waitingForApproval, technicalReview, waitingForTestSpec, testSpec,
                    readyToDevelop, developing, readyForCodeReview, codeReview,
                    readyForTesting, testing, designReview, waitingForAutomation, automation,
                    readyToMerge, readyToRelease,
                    done
                ]
            },
            Tasks =
            [
                // Backend задачи (12 задач)
                new() { Key = "BE-1", Summary = "[BE] Создать модель пользователя", ShirtType = TShirtType.S, RequiredSkills = ["backend", "qa"] },
                new() { Key = "BE-2", Summary = "[BE] API получения списка пользователей", ShirtType = TShirtType.S, RequiredSkills = ["backend", "qa"] },
                new() { Key = "BE-3", Summary = "[BE] API создания пользователя", ShirtType = TShirtType.S, RequiredSkills = ["backend", "qa"] },
                new() { Key = "BE-4", Summary = "[BE] API обновления пользователя", ShirtType = TShirtType.S, RequiredSkills = ["backend", "qa"] },
                new() { Key = "BE-5", Summary = "[BE] API удаления пользователя", ShirtType = TShirtType.XS, RequiredSkills = ["backend", "qa"] },
                new() { Key = "BE-6", Summary = "[BE] Валидация email при регистрации", ShirtType = TShirtType.XS, RequiredSkills = ["backend", "qa"] },
                new() { Key = "BE-7", Summary = "[BE] Хэширование паролей", ShirtType = TShirtType.S, RequiredSkills = ["backend", "qa"] },
                new() { Key = "BE-8", Summary = "[BE] JWT токены для аутентификации", ShirtType = TShirtType.M, RequiredSkills = ["backend", "qa"] },
                new() { Key = "BE-9", Summary = "[BE] Refresh токены", ShirtType = TShirtType.S, RequiredSkills = ["backend", "qa"] },
                new() { Key = "BE-10", Summary = "[BE] Логирование запросов", ShirtType = TShirtType.XS, RequiredSkills = ["backend", "qa"] },
                new() { Key = "BE-11", Summary = "[BE] Кэширование ответов API", ShirtType = TShirtType.M, RequiredSkills = ["backend", "qa"] },
                new() { Key = "BE-12", Summary = "[BE] Rate limiting", ShirtType = TShirtType.S, RequiredSkills = ["backend", "qa"] },

                // Frontend задачи (6 задач)
                new() { Key = "FE-1", Summary = "[FE] Верстка формы регистрации", ShirtType = TShirtType.S, RequiredSkills = ["frontend", "qa"] },
                new() { Key = "FE-2", Summary = "[FE] Верстка формы входа", ShirtType = TShirtType.S, RequiredSkills = ["frontend", "qa"] },
                new() { Key = "FE-3", Summary = "[FE] Компонент аватара пользователя", ShirtType = TShirtType.XS, RequiredSkills = ["frontend", "qa"] },
                new() { Key = "FE-4", Summary = "[FE] Страница профиля", ShirtType = TShirtType.M, RequiredSkills = ["frontend", "qa"] },
                new() { Key = "FE-5", Summary = "[FE] Валидация форм на клиенте", ShirtType = TShirtType.S, RequiredSkills = ["frontend", "qa"] },
                new() { Key = "FE-6", Summary = "[FE] Адаптивная верстка", ShirtType = TShirtType.S, RequiredSkills = ["frontend", "qa"] },
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
