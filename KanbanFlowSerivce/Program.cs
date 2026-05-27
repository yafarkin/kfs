using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Enums;
using KanbanFlowSerivce.Services;
using BoardTask = KanbanFlowSerivce.Dtos.Config.Task;

// Пример симуляции канбан-потока
var config = CreateSampleConfig();
var simulation = new Simulation();
simulation.InitFromConfig(config);

var movementService = new TaskMovementService(simulation);
var progressService = new WorkProgressService(simulation);

Console.WriteLine("=== Симуляция канбан-потока ===");
Console.WriteLine($"Задач: {config.Tasks.Count}");
Console.WriteLine($"Воркеров: {config.Workers.Count}");
Console.WriteLine($"Стадий: {config.Workflow.Stages.Count}");
Console.WriteLine();

// Запускаем симуляцию
var daysElapsed = progressService.SimulateUntilCompletion(maxDays: 50);

Console.WriteLine();
Console.WriteLine("=== Результаты симуляции ===");
Console.WriteLine($"Дней симуляции: {daysElapsed}");
Console.WriteLine();

// Выводим историю по дням
foreach (var day in simulation.History)
{
    Console.WriteLine($"День {day.DayNumber}:");
    foreach (var activity in day.Activities)
    {
        Console.WriteLine($"  [{activity.Type}] {activity.Description}");
    }
}

Console.WriteLine();
Console.WriteLine("=== Статус задач ===");
foreach (var task in simulation.Board.Tasks)
{
    Console.WriteLine($"{task.Task.Key}: {task.CurrentStage?.Stage.Name} (прогресс: {task.Progress}%)");
    if (task.TransitionHistory.Any())
    {
        Console.WriteLine($"  Переходов: {task.TransitionHistory.Count}");
    }
}

static SimulationConfig CreateSampleConfig()
{
    var todo = new Stage
    {
        Name = "Todo",
        Type = StageType.Buffer,
        IsLeadTimeStart = true,
        Transitions = new List<StageTransition>()
    };

    var developing = new Stage
    {
        Name = "Developing",
        Type = StageType.Work,
        IsLeadTimeStart = false,
        StageProgressPercent = 100,
        RequiredSkills = new List<string> { "Developer" },
        Transitions = new List<StageTransition>()
    };

    var readyForQa = new Stage
    {
        Name = "Ready for QA",
        Type = StageType.Buffer,
        IsLeadTimeStart = false,
        Transitions = new List<StageTransition>()
    };

    var qa = new Stage
    {
        Name = "QA",
        Type = StageType.Work,
        IsLeadTimeStart = false,
        StageProgressPercent = 30,
        RequiredSkills = new List<string> { "QA" },
        Transitions = new List<StageTransition>()
    };

    var done = new Stage
    {
        Name = "Done",
        Type = StageType.Buffer,
        IsLeadTimeStart = false,
        Transitions = new List<StageTransition>()
    };

    todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
    developing.Transitions.Add(new StageTransition { Stage = readyForQa, Probability = 1.0 });
    readyForQa.Transitions.Add(new StageTransition { Stage = qa, Probability = 1.0 });
    qa.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

    return new SimulationConfig
    {
        Seed = 42,
        Workers =
        [
            new() {Login = "dev1", Skills = ["dev"], Performance = 100},
            new() {Login = "qa1", Skills = ["qa"], Performance = 100}
        ],
        Workflow = new Workflow
        {
            Stages = [todo, developing, readyForQa, qa, done]
        },
        Tasks =
        [
            new() {Key = "TASK-1", Summary = "Большая задача", ShirtType = TShirtType.L, RequiredSkills = ["dev", "qa"]},
            new() {Key = "TASK-2", Summary = "Средняя задача", ShirtType = TShirtType.M, RequiredSkills = ["dev", "qa"]},
            new() {Key = "TASK-3", Summary = "Маленькая задача", ShirtType = TShirtType.S, RequiredSkills = ["dev", "qa"]}
        ]
    };
}