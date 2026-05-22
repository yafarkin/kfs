using KanbanFlowConsole.Dtos;
using KanbanFlowConsole.Dtos.Config;
using KanbanFlowConsole.Enums;
using KanbanFlowConsole.Services;
using BoardTask = KanbanFlowConsole.Dtos.Config.Task;

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
        IsStart = true,
        IsLeadTimeStart = true,
        AllowedRoles = [],
        Transitions = new List<StageTransition>()
    };

    var developing = new Stage
    {
        Name = "Developing",
        Type = StageType.Work,
        IsStart = false,
        IsLeadTimeStart = false,
        AllowedRoles = ["Developer"],
        StageProgressPercent = 100,
        Transitions = new List<StageTransition>()
    };

    var readyForQa = new Stage
    {
        Name = "Ready for QA",
        Type = StageType.Buffer,
        IsStart = false,
        IsLeadTimeStart = false,
        AllowedRoles = [],
        Transitions = new List<StageTransition>()
    };

    var qa = new Stage
    {
        Name = "QA",
        Type = StageType.Work,
        IsStart = false,
        IsLeadTimeStart = false,
        AllowedRoles = ["QA"],
        StageProgressPercent = 30,
        Transitions = new List<StageTransition>()
    };

    var done = new Stage
    {
        Name = "Done",
        Type = StageType.Buffer,
        IsStart = false,
        IsLeadTimeStart = false,
        AllowedRoles = [],
        Transitions = new List<StageTransition>()
    };

    todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
    developing.Transitions.Add(new StageTransition { Stage = readyForQa, Probability = 1.0 });
    readyForQa.Transitions.Add(new StageTransition { Stage = qa, Probability = 1.0 });
    qa.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

    return new SimulationConfig
    {
        Seed = 42,
        Workers = new List<Worker>
        {
            new() { Login = "dev1", Role = "Developer", Performance = 100 },
            new() { Login = "qa1", Role = "QA", Performance = 100 }
        },
        Workflow = new Workflow
        {
            Stages = new List<Stage> { todo, developing, readyForQa, qa, done }
        },
        Tasks = new List<BoardTask>
        {
            new() { Key = "TASK-1", Summary = "Большая задача", ShirtType = TShirtType.L, Role = "Developer" },
            new() { Key = "TASK-2", Summary = "Средняя задача", ShirtType = TShirtType.M, Role = "Developer" },
            new() { Key = "TASK-3", Summary = "Маленькая задача", ShirtType = TShirtType.S, Role = "Developer" }
        }
    };
}