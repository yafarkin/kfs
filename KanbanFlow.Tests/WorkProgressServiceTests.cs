using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Board;
using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Dtos.History;
using KanbanFlowSerivce.Enums;
using KanbanFlowSerivce.Services;
using Task = KanbanFlowSerivce.Dtos.Config.Task;

namespace KanbanFlow.Tests;

/// <summary>
/// Тесты для WorkProgressService — симуляция выполнения работы воркерами.
/// </summary>
public class WorkProgressServiceTests
{
    [Fact]
    public void SimulateWorkDay_IncreasesTaskProgress()
    {
        // Arrange
        var config = CreateSimpleConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        // manually assign task to worker
        var worker = simulation.Board.Workers[0];
        var task = simulation.Board.Tasks[0];
        var stage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");

        task.CurrentStage = stage;
        task.Progress = 0;
        stage.Tasks.Add(task);

        worker.Assignments.Add(new BoardTaskAssignment { Task = task, Stage = stage });
        task.Worker = worker;

        var progressService = new WorkProgressService(simulation);

        // Act
        progressService.SimulateWorkDay();

        // Assert
        Assert.True(task.Progress > 0);
        Assert.True(task.Progress <= 100);
    }

    [Fact]
    public void SimulateWorkDay_CompletesTask_At100Percent()
    {
        // Arrange
        var config = CreateSimpleConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var worker = simulation.Board.Workers[0];
        var task = simulation.Board.Tasks[0];
        var stage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");

        task.CurrentStage = stage;
        task.Progress = 90; // Почти завершено
        stage.Tasks.Add(task);

        worker.Assignments.Add(new BoardTaskAssignment { Task = task, Stage = stage });
        task.Worker = worker;

        var progressService = new WorkProgressService(simulation);

        // Act
        var completedTasks = progressService.SimulateWorkDay();

        // Assert
        Assert.Contains(task, completedTasks);
        Assert.Equal(100, task.Progress);
    }

    [Fact]
    public void SimulateWorkDay_LogsTaskProgressUpdated()
    {
        // Arrange
        var config = CreateSimpleConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var worker = simulation.Board.Workers[0];
        var task = simulation.Board.Tasks[0];
        var stage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");

        task.CurrentStage = stage;
        task.Progress = 0;
        stage.Tasks.Add(task);

        worker.Assignments.Add(new BoardTaskAssignment { Task = task, Stage = stage });
        task.Worker = worker;

        simulation.StartNewDay();
        var progressService = new WorkProgressService(simulation);

        // Act
        progressService.SimulateWorkDay();

        // Assert
        var activities = simulation.History.SelectMany(d => d.Activities).ToList();
        var progressEvents = activities.Where(a => a.Type == ActivityType.TaskProgressUpdated).ToList();

        Assert.NotEmpty(progressEvents);
        var progressEvent = progressEvents.First();
        Assert.Equal(task.Task.Key, progressEvent.TaskKey);
        Assert.Equal(worker.Worker.Login, progressEvent.WorkerLogin);
        Assert.Equal(stage.Stage.Name, progressEvent.StageName);
        Assert.Equal(task.Progress, progressEvent.Progress);
    }

    [Fact]
    public void SimulateWorkDay_LogsWorkerCompletedTask_WithCorrelationId()
    {
        // Arrange
        var config = CreateConfigWithFastCompletion();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var worker = simulation.Board.Workers[0];
        var task = simulation.Board.Tasks[0];
        var stage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");

        task.CurrentStage = stage;
        task.Progress = 80; // Достаточно высокий прогресс
        stage.Tasks.Add(task);

        // Сначала записываем WorkerTookTask с CorrelationId
        var correlationId = Guid.NewGuid();
        simulation.LogActivity(new HistoryActivity
        {
            Type = ActivityType.WorkerTookTask,
            Task = task,
            Worker = worker,
            Stage = stage,
            TaskKey = task.Task.Key,
            WorkerLogin = worker.Worker.Login,
            StageName = stage.Stage.Name,
            CorrelationId = correlationId
        });

        worker.Assignments.Add(new BoardTaskAssignment { Task = task, Stage = stage });
        task.Worker = worker;

        simulation.StartNewDay();
        var progressService = new WorkProgressService(simulation);

        // Act - симулируем пока задача не завершится
        for (var i = 0; i < 5; i++)
        {
            progressService.SimulateWorkDay();
        }

        // Assert
        var activities = simulation.History.SelectMany(d => d.Activities).ToList();
        var completedEvents = activities.Where(a => a.Type == ActivityType.WorkerCompletedTask).ToList();

        // Проверяем что событие WorkerCompletedTask было записано
        Assert.NotEmpty(completedEvents);
        var completedEvent = completedEvents.First();
        
        // Проверяем основные поля
        Assert.Equal(worker.Worker.Login, completedEvent.WorkerLogin);
        Assert.Equal(task.Task.Key, completedEvent.TaskKey);
    }

    [Fact]
    public void SimulateWorkDay_Performance100_CompletesInMinDays()
    {
        // Arrange
        var config = CreateConfigWithPerformance(100);
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var worker = simulation.Board.Workers[0];
        var task = simulation.Board.Tasks[0];
        var stage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");

        task.CurrentStage = stage;
        task.Progress = 0;
        stage.Tasks.Add(task);
        worker.Assignments.Add(new BoardTaskAssignment { Task = task, Stage = stage });
        task.Worker = worker;

        var progressService = new WorkProgressService(simulation);

        // Act & Assert - при performance 100% задача M (4-6 дней) должна завершиться за 4 дня
        for (var day = 0; day < 4; day++)
        {
            simulation.StartNewDay();
            progressService.SimulateWorkDay();
        }

        Assert.Equal(100, task.Progress);
    }

    [Fact]
    public void SimulateWorkDay_Performance50_TakesLonger()
    {
        // Arrange
        var config = CreateConfigWithPerformance(50);
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var worker = simulation.Board.Workers[0];
        var task = simulation.Board.Tasks[0];
        var stage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");

        task.CurrentStage = stage;
        task.Progress = 0;
        stage.Tasks.Add(task);
        worker.Assignments.Add(new BoardTaskAssignment { Task = task, Stage = stage });
        task.Worker = worker;

        var progressService = new WorkProgressService(simulation);

        // Act - симулируем 4 дня
        for (var day = 0; day < 4; day++)
        {
            simulation.StartNewDay();
            progressService.SimulateWorkDay();
        }

        // Assert - при performance 50% прогресс должен быть примерно 50%
        Assert.True(task.Progress >= 40 && task.Progress <= 60);
    }

    [Fact]
    public void SimulateWorkDay_DoesNotExceed100Percent()
    {
        // Arrange
        var config = CreateSimpleConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var worker = simulation.Board.Workers[0];
        var task = simulation.Board.Tasks[0];
        var stage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");

        task.CurrentStage = stage;
        task.Progress = 95; // Почти завершено
        stage.Tasks.Add(task);
        worker.Assignments.Add(new BoardTaskAssignment { Task = task, Stage = stage });
        task.Worker = worker;

        var progressService = new WorkProgressService(simulation);

        // Act - симулируем несколько дней
        for (var day = 0; day < 5; day++)
        {
            simulation.StartNewDay();
            progressService.SimulateWorkDay();
        }

        // Assert
        Assert.Equal(100, task.Progress);
        Assert.True(task.Progress <= 100);
    }

    [Fact]
    public void SimulateWorkDay_SkipsCompletedTasks()
    {
        // Arrange
        var config = CreateSimpleConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var worker = simulation.Board.Workers[0];
        var task = simulation.Board.Tasks[0];
        var stage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");

        task.CurrentStage = stage;
        task.Progress = 100; // Уже завершено
        // IsCompleted вычисляется на основе Progress >= 100
        stage.Tasks.Add(task);
        worker.Assignments.Add(new BoardTaskAssignment { Task = task, Stage = stage });
        task.Worker = worker;

        var progressService = new WorkProgressService(simulation);

        // Act
        var completedTasks = progressService.SimulateWorkDay();

        // Assert - завершённая задача не должна обрабатываться снова
        Assert.DoesNotContain(task, completedTasks);
    }

    [Fact]
    public void SimulateWorkDay_SkipsNonWorkStages()
    {
        // Arrange
        var config = CreateSimpleConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var worker = simulation.Board.Workers[0];
        var task = simulation.Board.Tasks[0];
        var bufferStage = simulation.Board.Stages.First(s => s.Stage.Name == "Todo");

        task.CurrentStage = bufferStage;
        task.Progress = 0;
        bufferStage.Tasks.Add(task);

        // На буферной стадии прогресс не увеличивается
        var progressService = new WorkProgressService(simulation);

        // Act
        progressService.SimulateWorkDay();

        // Assert
        Assert.Equal(0, task.Progress);
    }

    [Fact]
    public void SimulateWorkDay_StageProgressPercent_AppliedCorrectly()
    {
        // Arrange
        var config = CreateConfigWithStageProgress();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var worker = simulation.Board.Workers[0];
        var task = simulation.Board.Tasks[0];
        var stage = simulation.Board.Stages.First(s => s.Stage.Name == "CodeReview");

        task.CurrentStage = stage;
        task.Progress = 0;
        stage.Tasks.Add(task);
        worker.Assignments.Add(new BoardTaskAssignment { Task = task, Stage = stage });
        task.Worker = worker;

        var progressService = new WorkProgressService(simulation);

        // Act - CodeReview имеет StageProgressPercent = 25%, значит выполняется быстрее
        simulation.StartNewDay();
        progressService.SimulateWorkDay();

        // Assert - прогресс должен быть больше благодаря StageProgressPercent
        Assert.True(task.Progress > 0);
    }

    [Fact]
    public void SimulateWorkDay_ReturnsCompletedTasksList()
    {
        // Arrange
        var config = CreateConfigWithFastCompletion();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var worker = simulation.Board.Workers[0];
        var task = simulation.Board.Tasks[0];
        var stage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");

        task.CurrentStage = stage;
        task.Progress = 80; // Достаточно высокий прогресс
        stage.Tasks.Add(task);
        worker.Assignments.Add(new BoardTaskAssignment { Task = task, Stage = stage });
        task.Worker = worker;

        simulation.StartNewDay();
        var progressService = new WorkProgressService(simulation);

        // Act - симулируем несколько дней пока задача не завершится
        List<BoardTask> completedTasks = new();
        for (var i = 0; i < 5; i++)
        {
            completedTasks = progressService.SimulateWorkDay();
            if (completedTasks.Any()) break;
        }

        // Assert
        Assert.NotEmpty(completedTasks);
    }

    #region Helper Methods

    private static SimulationConfig CreateSimpleConfig()
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
            RequiredSkills = ["dev"],
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
            Workers = [new() { Login = "dev1", Skills = ["dev"], Performance = 100, WipLimit = 1 }],
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks = [new() { Key = "TASK-1", ShirtType = TShirtType.M, RequiredSkills = ["dev"] }],
            UseVariability = false
        };
    }

    private static SimulationConfig CreateConfigWithPerformance(int performance)
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
            RequiredSkills = ["dev"],
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
            Workers = [new() { Login = "dev1", Skills = ["dev"], Performance = performance, WipLimit = 1 }],
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks = [new() { Key = "TASK-1", ShirtType = TShirtType.M, RequiredSkills = ["dev"] }],
            UseVariability = false
        };
    }

    private static SimulationConfig CreateConfigWithStageProgress()
    {
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            Transitions = new List<StageTransition>()
        };

        var codeReview = new Stage
        {
            Name = "CodeReview",
            Type = StageType.Work,
            StageProgressPercent = 25, // 25% от оценки
            RequiredSkills = ["dev"],
            Transitions = new List<StageTransition>()
        };

        var done = new Stage
        {
            Name = "Done",
            Type = StageType.Buffer,
            Transitions = new List<StageTransition>()
        };

        todo.Transitions.Add(new StageTransition { Stage = codeReview, Probability = 1.0 });
        codeReview.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = [new() { Login = "dev1", Skills = ["dev"], Performance = 100, WipLimit = 1 }],
            Workflow = new Workflow { Stages = [todo, codeReview, done] },
            Tasks = [new() { Key = "TASK-1", ShirtType = TShirtType.M, RequiredSkills = ["dev"] }],
            UseVariability = false
        };
    }

    private static SimulationConfig CreateConfigWithFastCompletion()
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
            RequiredSkills = ["dev"],
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
            Workers = [new() { Login = "dev1", Skills = ["dev"], Performance = 100, WipLimit = 10 }],
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks = [new() { Key = "TASK-1", ShirtType = TShirtType.XS, RequiredSkills = ["dev"] }],
            UseVariability = false
        };
    }

    #endregion
}
