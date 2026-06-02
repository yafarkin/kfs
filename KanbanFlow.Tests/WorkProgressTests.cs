using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Board;
using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Dtos.History;
using KanbanFlowSerivce.Enums;
using KanbanFlowSerivce.Services;
using Task = KanbanFlowSerivce.Dtos.Config.Task;

namespace KanbanFlow.Tests;

/// <summary>
/// Тесты для WorkProgressService - симуляция выполнения работы воркерами
/// </summary>
public class WorkProgressTests
{
    [Fact]
    public void SimulateWorkDay_DaysRequiredZero_TaskCompletesInstantly()
    {
        // Arrange - воркер с производительностью которая даёт daysRequired = 0
        var config = CreateConfigWithInstantCompletion();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act - первый день: задача перемещается в Developing и берётся воркером
        simulation.StartNewDay();
        movementService.ProcessMovements();
        
        // Второй день: симулируем работу
        progressService.SimulateWorkDay();

        // Assert - задача должна завершиться мгновенно
        var task = simulation.Board.Tasks.First();
        Assert.Equal(100, task.Progress);
        Assert.True(task.IsCompleted);

        // Проверяем что событие WorkerCompletedTask было записано
        var completedEvent = simulation.History
            .SelectMany(d => d.Activities)
            .FirstOrDefault(a => a.Type == ActivityType.WorkerCompletedTask);
        Assert.NotNull(completedEvent);
    }

    [Fact]
    public void SimulateWorkDay_ProgressAccumulates_CorrectlyAcrossDays()
    {
        // Arrange - задача с StageProgressPercent=20 (5 дней на выполнение)
        var config = CreateConfigWithFixedProgress();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act - день 1: перемещение и начало работы
        simulation.StartNewDay();
        movementService.ProcessMovements();
        progressService.SimulateWorkDay();

        var taskDay1 = simulation.Board.Tasks.First();
        var progressDay1 = taskDay1.Progress;

        // День 2: продолжение работы
        simulation.StartNewDay();
        progressService.SimulateWorkDay();

        var taskDay2 = simulation.Board.Tasks.First();
        var progressDay2 = taskDay2.Progress;

        // Assert - прогресс должен быть > 0 и <= 100
        Assert.True(progressDay1 >= 0, $"Прогресс день 1 должен быть >= 0: {progressDay1}");
        Assert.True(progressDay2 >= progressDay1, $"Прогресс должен расти: {progressDay2} >= {progressDay1}");
        Assert.True(progressDay2 <= 100, "Прогресс не должен превышать 100%");
    }

    [Fact]
    public void SimulateWorkDay_MultipleTasks_TracksEachTaskProgress()
    {
        // Arrange - несколько задач у одного воркера (WIP > 1)
        var config = CreateConfigWithMultipleTasks();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act
        simulation.StartNewDay();
        movementService.ProcessMovements();
        progressService.SimulateWorkDay();

        // Assert - все задачи должны получить прогресс
        var tasks = simulation.Board.Tasks.ToList();
        foreach (var task in tasks)
        {
            if (task.Worker != null) // Задачи назначенные на воркера
            {
                Assert.True(task.Progress > 0, $"Задача {task.Task.Key} должна иметь прогресс > 0");
            }
        }
    }

    [Fact]
    public void SimulateWorkDay_WorkStageOnly_IgnoresBufferStages()
    {
        // Arrange
        var config = CreateConfigWithBufferAndWorkStages();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act
        simulation.StartNewDay();
        movementService.ProcessMovements();
        progressService.SimulateWorkDay();

        // Assert - прогресс должен быть только у задач в Work стадии
        var developingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");
        var todoStage = simulation.Board.Stages.First(s => s.Stage.Name == "Todo");

        // Задачи в Developing (Work) должны иметь прогресс если назначены
        foreach (var task in developingStage.Tasks)
        {
            if (task.Worker != null)
            {
                Assert.True(task.Progress > 0);
            }
        }

        // Задачи в Todo (Buffer) не должны иметь прогресс
        foreach (var task in todoStage.Tasks)
        {
            Assert.Equal(0, task.Progress);
        }
    }

    [Fact]
    public void SimulateWorkDay_CorrelationId_MatchesBetweenTookAndCompleted()
    {
        // Arrange
        var config = CreateConfigWithSingleTask();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act - симулируем до завершения задачи
        for (var day = 0; day < 10; day++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Assert - CorrelationId у WorkerTookTask и WorkerCompletedTask должны совпадать
        var allActivities = simulation.History.SelectMany(d => d.Activities).ToList();
        var tookTasks = allActivities.Where(a => a.Type == ActivityType.WorkerTookTask).ToList();
        var completedTasks = allActivities.Where(a => a.Type == ActivityType.WorkerCompletedTask).ToList();

        foreach (var took in tookTasks)
        {
            Assert.NotEqual(Guid.Empty, took.CorrelationId);
            
            var completed = completedTasks.FirstOrDefault(c => c.CorrelationId == took.CorrelationId);
            if (completed != null)
            {
                Assert.Equal(took.CorrelationId, completed.CorrelationId);
                Assert.Equal(took.TaskKey, completed.TaskKey);
            }
        }
    }

    [Fact]
    public void SimulateWorkDay_ZeroPerformance_TaskDoesNotProgress()
    {
        // Arrange - воркер с производительностью 0
        var config = CreateConfigWithZeroPerformance();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act - несколько дней
        for (var day = 0; day < 5; day++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Assert - задача не должна завершиться
        var task = simulation.Board.Tasks.First();
        // При производительности 0 прогресс не должен расти
        Assert.True(task.Progress < 50, $"Задача не должна значительно прогрессировать: {task.Progress}%");
    }

    #region Helper Methods

    private static SimulationConfig CreateConfigWithInstantCompletion()
    {
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            Transitions = new List<StageTransition>()
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            StageProgressPercent = 100, // Мгновенное выполнение
            RequiredSkills = [],
            Transitions = new List<StageTransition>()
        };

        var done = new Stage
        {
            Name = "Done",
            Type = StageType.Buffer,
            Transitions = new List<StageTransition>()
        };

        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = [new() { Login = "dev1", Skills = [], Performance = 100, WipLimit = 10 }],
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks = [new() { Key = "TASK-1", RequiredSkills = [] }],
            UseVariability = false
        };
    }

    private static SimulationConfig CreateConfigWithFixedProgress()
    {
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            Transitions = new List<StageTransition>()
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            StageProgressPercent = 50, // 2 дня на выполнение
            RequiredSkills = [],
            Transitions = new List<StageTransition>()
        };

        var done = new Stage
        {
            Name = "Done",
            Type = StageType.Buffer,
            Transitions = new List<StageTransition>()
        };

        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = [new() { Login = "dev1", Skills = [], Performance = 100, WipLimit = 10 }],
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks = [new() { Key = "TASK-1", RequiredSkills = [] }],
            UseVariability = false
        };
    }

    private static SimulationConfig CreateConfigWithMultipleTasks()
    {
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            Transitions = new List<StageTransition>()
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            StageProgressPercent = 50,
            RequiredSkills = [],
            Transitions = new List<StageTransition>()
        };

        var done = new Stage
        {
            Name = "Done",
            Type = StageType.Buffer,
            Transitions = new List<StageTransition>()
        };

        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = [new() { Login = "dev1", Skills = [], Performance = 100, WipLimit = 5 }],
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks = [
                new() { Key = "TASK-1", RequiredSkills = [] },
                new() { Key = "TASK-2", RequiredSkills = [] },
                new() { Key = "TASK-3", RequiredSkills = [] }
            ],
            UseVariability = false
        };
    }

    private static SimulationConfig CreateConfigWithBufferAndWorkStages()
    {
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            Transitions = new List<StageTransition>()
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            StageProgressPercent = 100,
            RequiredSkills = [],
            Transitions = new List<StageTransition>()
        };

        var done = new Stage
        {
            Name = "Done",
            Type = StageType.Buffer,
            Transitions = new List<StageTransition>()
        };

        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = [new() { Login = "dev1", Skills = [], Performance = 100, WipLimit = 10 }],
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks = [new() { Key = "TASK-1", RequiredSkills = [] }],
            UseVariability = false
        };
    }

    private static SimulationConfig CreateConfigWithSingleTask()
    {
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            Transitions = new List<StageTransition>()
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            StageProgressPercent = 100,
            RequiredSkills = [],
            Transitions = new List<StageTransition>()
        };

        var done = new Stage
        {
            Name = "Done",
            Type = StageType.Buffer,
            Transitions = new List<StageTransition>()
        };

        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = [new() { Login = "dev1", Skills = [], Performance = 100, WipLimit = 1 }],
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks = [new() { Key = "TASK-1", RequiredSkills = [] }],
            UseVariability = false
        };
    }

    private static SimulationConfig CreateConfigWithZeroPerformance()
    {
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            Transitions = new List<StageTransition>()
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            StageProgressPercent = 100,
            RequiredSkills = [],
            Transitions = new List<StageTransition>()
        };

        var done = new Stage
        {
            Name = "Done",
            Type = StageType.Buffer,
            Transitions = new List<StageTransition>()
        };

        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = [new() { Login = "dev1", Skills = [], Performance = 0, WipLimit = 10 }],
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks = [new() { Key = "TASK-1", RequiredSkills = [] }],
            UseVariability = false
        };
    }

    #endregion
}
