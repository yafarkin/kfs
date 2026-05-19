using KanbanFlowConsole.Dtos;
using KanbanFlowConsole.Dtos.Board;
using KanbanFlowConsole.Dtos.History;
using KanbanFlowConsole.Enums;
using KanbanFlowConsole.Services;
using KanbanFlowConsole.Dtos.Config;
using TaskDto = KanbanFlowConsole.Dtos.Config.Task;

namespace KanbanFlow.Tests;

public class TaskMovementServiceTests
{
    [Fact]
    public void ProcessMovements_MovesTask_FromTodoToDeveloping()
    {
        // Arrange
        var config = CreateSimpleWorkflowConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);
        var service = new TaskMovementService(simulation);

        // Act
        service.ProcessMovements();

        // Assert
        var todoStage = simulation.Board.Stages.First(s => s.Stage.Name == "Todo");
        var developingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");

        Assert.Empty(todoStage.Tasks);
        Assert.Single(developingStage.Tasks);
        Assert.Equal("TASK-1", developingStage.Tasks[0].Task.Key);
    }

    [Fact]
    public void ProcessMovements_DoesNotMove_WhenWorkerNotAvailable()
    {
        // Arrange
        var config = CreateSimpleWorkflowConfig();
        // Устанавливаем WIP лимит воркера = 1
        config.Workers[0].WipLimit = 1;
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        // Добавляем задачу в работу воркеру
        var worker = simulation.Board.Workers[0];
        var taskInWork = simulation.Board.Tasks[0];
        worker.Assignments.Add(new BoardTaskAssignment
        {
            Task = taskInWork,
            Stage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing")
        });

        // Добавляем вторую задачу в Todo
        var todoStage = simulation.Board.Stages.First(s => s.Stage.Name == "Todo");
        var newTask = new BoardTask
        {
            Task = new TaskDto { Key = "TASK-2", Role = "Backend Developer" },
            Progress = 0
        };
        todoStage.Tasks.Add(newTask);
        simulation.Board.Tasks.Add(newTask);

        var service = new TaskMovementService(simulation);

        // Act
        service.ProcessMovements();

        // Assert
        Assert.Contains(newTask, todoStage.Tasks);
    }

    [Fact]
    public void ProcessMovements_MovesThroughNonWorkingStages()
    {
        // Arrange
        var config = CreateWorkflowWithBuffers();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);
        var service = new TaskMovementService(simulation);

        // Act
        service.ProcessMovements();

        // Assert
        var waitingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Waiting");
        var todoStage = simulation.Board.Stages.First(s => s.Stage.Name == "Todo");
        var developingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");

        // Задача должна пройти через Waiting и Todo в Developing
        Assert.Empty(waitingStage.Tasks);
        Assert.Empty(todoStage.Tasks);
        Assert.Single(developingStage.Tasks);
    }

    [Fact]
    public void ProcessMovements_DoesNotMove_FromWorkStage_IfTaskNotCompleted()
    {
        // Arrange
        var config = CreateTwoWorkStagesConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        // Устанавливаем прогресс задачи в Developing на 50%
        var developingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");
        developingStage.Tasks[0].Progress = 50;

        var service = new TaskMovementService(simulation);

        // Act
        service.ProcessMovements();

        // Assert
        Assert.Single(developingStage.Tasks);
        var reviewStage = simulation.Board.Stages.First(s => s.Stage.Name == "Code Review");
        Assert.Empty(reviewStage.Tasks);
    }

    [Fact]
    public void ProcessMovements_Moves_FromWorkStage_IfTaskCompleted()
    {
        // Arrange
        var config = CreateTwoWorkStagesConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        // Устанавливаем прогресс задачи в Developing на 100%
        var developingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");
        developingStage.Tasks[0].Progress = 100;

        var service = new TaskMovementService(simulation);

        // Act
        service.ProcessMovements();

        // Assert
        Assert.Empty(developingStage.Tasks);
        var reviewStage = simulation.Board.Stages.First(s => s.Stage.Name == "Code Review");
        Assert.Single(reviewStage.Tasks);
    }

    [Fact]
    public void ProcessMovements_ResetsProgress_OnMove()
    {
        // Arrange
        var config = CreateSimpleWorkflowConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);
        var service = new TaskMovementService(simulation);

        // Act
        service.ProcessMovements();

        // Assert
        var developingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");
        Assert.Equal(0, developingStage.Tasks[0].Progress);
    }

    [Fact]
    public void ProcessMovements_AddsToHistory_OnMove()
    {
        // Arrange
        var config = CreateSimpleWorkflowConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);
        var service = new TaskMovementService(simulation);

        // Act
        service.ProcessMovements();

        // Assert
        Assert.NotEmpty(simulation.History);
        Assert.Contains(simulation.History.SelectMany(d => d.Activities),
            a => a.Type == ActivityType.TaskMoved);
    }

    [Fact]
    public void ProcessMovements_DoesNotMove_WhenWipLimitExceeded()
    {
        // Arrange
        var config = CreateSimpleWorkflowConfig();
        // Устанавливаем WIP лимит стадии Developing = 1
        config.Workflow.Stages.First(s => s.Name == "Developing").WipLimit = 1;
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        // Добавляем вторую задачу в Todo
        var todoStage = simulation.Board.Stages.First(s => s.Stage.Name == "Todo");
        var newTask = new BoardTask
        {
            Task = new TaskDto { Key = "TASK-2", Role = "Backend Developer" },
            Progress = 0
        };
        todoStage.Tasks.Add(newTask);
        simulation.Board.Tasks.Add(newTask);

        var service = new TaskMovementService(simulation);

        // Act
        service.ProcessMovements();

        // Assert
        // Только одна задача должна перейти в Developing
        var developingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");
        Assert.Single(developingStage.Tasks);
        Assert.Contains(newTask, todoStage.Tasks);
    }

    [Fact]
    public void ProcessMovements_MovesToDone_WithoutWipLimit()
    {
        // Arrange
        var config = CreateWorkflowWithDone();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        // Сначала продвигаем задачу в Code Review (через Developing)
        var developingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");
        developingStage.Tasks[0].Progress = 100;

        var service = new TaskMovementService(simulation);
        service.ProcessMovements();

        // Задача должна быть в Code Review
        var reviewStage = simulation.Board.Stages.First(s => s.Stage.Name == "Code Review");
        Assert.Single(reviewStage.Tasks);

        // Завершаем задачу в Code Review
        reviewStage.Tasks[0].Progress = 100;

        // Запускаем обработку снова
        service.ProcessMovements();

        // Assert
        var doneStage = simulation.Board.Stages.First(s => s.Stage.Name == "Done");
        Assert.Single(doneStage.Tasks);
    }

    private SimulationConfig CreateSimpleWorkflowConfig()
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
            AllowedRoles = ["Backend Developer"],
            Transitions = new List<StageTransition>()
        };

        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = new List<Worker>
            {
                new() { Login = "dev1", Role = "Backend Developer", Performance = 100 }
            },
            Workflow = new Workflow
            {
                Stages = new List<Stage> { todo, developing }
            },
            Tasks = new List<TaskDto>
            {
                new() { Key = "TASK-1", Role = "Backend Developer" }
            }
        };
    }

    private SimulationConfig CreateWorkflowWithBuffers()
    {
        var waiting = new Stage
        {
            Name = "Waiting",
            Type = StageType.Buffer,
            IsStart = true,
            IsLeadTimeStart = true,
            AllowedRoles = [],
            Transitions = new List<StageTransition>()
        };

        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            IsStart = false,
            IsLeadTimeStart = false,
            AllowedRoles = [],
            Transitions = new List<StageTransition>()
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            IsStart = false,
            IsLeadTimeStart = false,
            AllowedRoles = ["Backend Developer"],
            Transitions = new List<StageTransition>()
        };

        waiting.Transitions.Add(new StageTransition { Stage = todo, Probability = 1.0 });
        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = new List<Worker>
            {
                new() { Login = "dev1", Role = "Backend Developer", Performance = 100 }
            },
            Workflow = new Workflow
            {
                Stages = new List<Stage> { waiting, todo, developing }
            },
            Tasks = new List<TaskDto>
            {
                new() { Key = "TASK-1", Role = "Backend Developer" }
            }
        };
    }

    private SimulationConfig CreateTwoWorkStagesConfig()
    {
        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            IsStart = true,
            IsLeadTimeStart = true,
            AllowedRoles = ["Backend Developer"],
            Transitions = new List<StageTransition>()
        };

        var review = new Stage
        {
            Name = "Code Review",
            Type = StageType.Work,
            IsStart = false,
            IsLeadTimeStart = false,
            AllowedRoles = ["Backend Developer"],
            Transitions = new List<StageTransition>()
        };

        developing.Transitions.Add(new StageTransition { Stage = review, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = new List<Worker>
            {
                new() { Login = "dev1", Role = "Backend Developer", Performance = 100 },
                new() { Login = "dev2", Role = "Backend Developer", Performance = 100 }
            },
            Workflow = new Workflow
            {
                Stages = new List<Stage> { developing, review }
            },
            Tasks = new List<TaskDto>
            {
                new() { Key = "TASK-1", Role = "Backend Developer" }
            }
        };
    }

    private SimulationConfig CreateWorkflowWithDone()
    {
        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            IsStart = true,
            IsLeadTimeStart = true,
            AllowedRoles = ["Backend Developer"],
            Transitions = new List<StageTransition>()
        };

        var review = new Stage
        {
            Name = "Code Review",
            Type = StageType.Work,
            IsStart = false,
            IsLeadTimeStart = false,
            AllowedRoles = ["Backend Developer"],
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

        developing.Transitions.Add(new StageTransition { Stage = review, Probability = 1.0 });
        review.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = new List<Worker>
            {
                new() { Login = "dev1", Role = "Backend Developer", Performance = 100 },
                new() { Login = "dev2", Role = "Backend Developer", Performance = 100 }
            },
            Workflow = new Workflow
            {
                Stages = new List<Stage> { developing, review, done }
            },
            Tasks = new List<TaskDto>
            {
                new() { Key = "TASK-1", Role = "Backend Developer" }
            }
        };
    }
}
