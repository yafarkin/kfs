using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Board;
using KanbanFlowSerivce.Dtos.History;
using KanbanFlowSerivce.Enums;
using KanbanFlowSerivce.Services;
using KanbanFlowSerivce.Dtos.Config;
using TaskDto = KanbanFlowSerivce.Dtos.Config.Task;

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
            Progress = 0,
            Task = new TaskDto { Key = "TASK-2", RequiredSkills = ["dev"] }
        };
        todoStage.Tasks.Add(newTask);
        simulation.Board.Tasks.Add(newTask);

        var service = new TaskMovementService(simulation);

        // Act
        service.ProcessMovements();

        // Assert
        // Задача НЕ переместится в Developing, потому что воркер занят
        // Задача остаётся в Todo и ждёт доступного воркера
        var developingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");
        Assert.DoesNotContain(newTask, developingStage.Tasks);
        Assert.Contains(newTask, todoStage.Tasks);

        // Задача должна быть без воркера (всё ещё в Todo)
        Assert.Null(newTask.Worker);
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
            IsLeadTimeStart = true,
            
            Transitions = new List<StageTransition>()
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            Transitions = new List<StageTransition>()
        };

        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = [new() {Login = "dev1", Skills = ["dev"], Performance = 100}],
            Workflow = new Workflow
            {
                Stages = [todo, developing]
            },
            Tasks = [new() {Key = "TASK-1", RequiredSkills = ["dev"]}]
        };
    }

    private SimulationConfig CreateWorkflowWithBuffers()
    {
        var waiting = new Stage
        {
            Name = "Waiting",
            Type = StageType.Buffer,
            IsLeadTimeStart = true,
            
            Transitions = new List<StageTransition>()
        };

        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            IsLeadTimeStart = false,
            
            Transitions = new List<StageTransition>()
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            Transitions = new List<StageTransition>()
        };

        waiting.Transitions.Add(new StageTransition { Stage = todo, Probability = 1.0 });
        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = [new() {Login = "dev1", Skills = ["dev"], Performance = 100}],
            Workflow = new Workflow
            {
                Stages = [waiting, todo, developing]
            },
            Tasks = [new() {Key = "TASK-1", RequiredSkills = ["dev"]}]
        };
    }

    private SimulationConfig CreateTwoWorkStagesConfig()
    {
        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            IsLeadTimeStart = true,
            Transitions = new List<StageTransition>()
        };

        var review = new Stage
        {
            Name = "Code Review",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            Transitions = new List<StageTransition>()
        };

        developing.Transitions.Add(new StageTransition { Stage = review, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers =
            [
                new() {Login = "dev1", Skills = ["dev"], Performance = 100},
                new() {Login = "dev2", Skills = ["dev"], Performance = 100}
            ],
            Workflow = new Workflow
            {
                Stages = [developing, review]
            },
            Tasks = [new() {Key = "TASK-1", RequiredSkills = ["dev"]}]
        };
    }

    private SimulationConfig CreateWorkflowWithDone()
    {
        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            IsLeadTimeStart = true,
            Transitions = []
        };

        var review = new Stage
        {
            Name = "Code Review",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            Transitions = []
        };

        var done = new Stage
        {
            Name = "Done",
            Type = StageType.Buffer,
            IsLeadTimeStart = false,
            Transitions = []
        };

        developing.Transitions.Add(new StageTransition { Stage = review, Probability = 1.0 });
        review.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers =
            [
                new() {Login = "dev1", Skills = ["dev"], Performance = 100},
                new() {Login = "dev2", Skills = ["dev"], Performance = 100}
            ],
            Workflow = new Workflow
            {
                Stages = [developing, review, done]
            },
            Tasks = [new() {Key = "TASK-1", RequiredSkills = ["dev"]}]
        };
    }

    [Fact]
    public void ProcessMovements_RespectsSpecificWorkerRequirements()
    {
        // Arrange
        // Workflow: todo -> developing -> ready for qa -> qa -> done
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            IsLeadTimeStart = true,
            RequiredSkills = [],
            Transitions = []
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            RequiredSkills = ["dev"],
            Transitions = []
        };

        var readyForQa = new Stage
        {
            Name = "Ready for QA",
            Type = StageType.Buffer,
            IsLeadTimeStart = false,
            RequiredSkills = [],
            Transitions = []
        };

        var qa = new Stage
        {
            Name = "QA",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            RequiredSkills = ["qa"],
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

        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = readyForQa, Probability = 1.0 });
        readyForQa.Transitions.Add(new StageTransition { Stage = qa, Probability = 1.0 });
        qa.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        var config = new SimulationConfig
        {
            Seed = 42,
            Workers =
            [
                new() {Login = "dev1", Skills = ["dev"], Performance = 100, WipLimit = 1},
                new() {Login = "dev2", Skills = ["dev"], Performance = 100, WipLimit = 1},
                new() {Login = "qa1", Skills = ["qa"], Performance = 100, WipLimit = 1},
                new() {Login = "qa2", Skills = ["qa"], Performance = 100, WipLimit = 1}
            ],
            Workflow = new Workflow
            {
                Stages = [todo, developing, readyForQa, qa, done]
            },
            Tasks =
            [
                new() {Key = "TASK-1", RequiredSkills = ["dev"]},
                // Задача 2: в работе у qa1 (50% прогресс - ещё не готова)
                new() {Key = "TASK-2", RequiredSkills = ["qa"]},
                // Задача 3: требует именно dev1 для разработки и qa1 для тестирования
                new()
                {
                    Key = "TASK-3",
                    RequiredSkills = ["dev"],
                    AcceptableWorkers = new Dictionary<string, string>
                    {
                        {"Developing", "dev1"},
                        {"QA", "qa1"}
                    }
                }
            ]
        };

        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        // Получаем ссылки на стадии и worker'ов
        var developingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");
        var qaStage = simulation.Board.Stages.First(s => s.Stage.Name == "QA");
        var todoStage = simulation.Board.Stages.First(s => s.Stage.Name == "Todo");
        var dev1 = simulation.Board.Workers.First(w => w.Worker.Login == "dev1");
        var qa1 = simulation.Board.Workers.First(w => w.Worker.Login == "qa1");

        // Настраиваем начальное состояние:
        // TASK-1 в developing у dev1 с прогрессом 50%
        var task1 = simulation.Board.Tasks.First(t => t.Task.Key == "TASK-1");
        // Удаляем из Todo (куда она попала при инициализации)
        todoStage.Tasks.Remove(task1);
        // Добавляем в developing
        developingStage.Tasks.Add(task1);
        task1.CurrentStage = developingStage;
        task1.Progress = 50;
        task1.Worker = dev1;
        dev1.Assignments.Add(new BoardTaskAssignment { Task = task1, Stage = developingStage });

        // TASK-2 в qa у qa1 с прогрессом 50%
        var task2 = simulation.Board.Tasks.First(t => t.Task.Key == "TASK-2");
        // TASK-2 не в Todo, а сразу в QA - удаляем из Todo если там есть
        todoStage.Tasks.RemoveAll(t => t.Task.Key == "TASK-2");
        qaStage.Tasks.Add(task2);
        task2.CurrentStage = qaStage;
        task2.Progress = 50;
        task2.Worker = qa1;
        qa1.Assignments.Add(new BoardTaskAssignment { Task = task2, Stage = qaStage });

        // TASK-3 в todo (ожидает начала работы)
        var task3 = simulation.Board.Tasks.First(t => t.Task.Key == "TASK-3");
        task3.CurrentStage = todoStage;
        task3.Progress = 0;

        var service = new TaskMovementService(simulation);

        // Act - Шаг 1: dev1 и qa1 заняты, задача 3 не должна двигаться в developing
        service.ProcessMovements();

        // Assert - Шаг 1
        // TASK-1 всё ещё в developing (прогресс 50%, не готова к перемещению)
        Assert.Equal("Developing", task1.CurrentStage?.Stage.Name);
        Assert.Equal(50, task1.Progress);
        Assert.Equal(dev1, task1.Worker);

        // TASK-2 всё ещё в qa (прогресс 50%, не готова к перемещению)
        Assert.Equal("QA", task2.CurrentStage?.Stage.Name);
        Assert.Equal(50, task2.Progress);
        Assert.Equal(qa1, task2.Worker);

        // TASK-3 всё ещё в todo (dev1 занят, а задача требует именно dev1)
        Assert.Equal("Todo", task3.CurrentStage?.Stage.Name);
        Assert.Null(task3.Worker);

        // Act - Шаг 2: завершаем TASK-1 (dev1 освобождается)
        task1.Progress = 100;
        service.ProcessMovements();

        // Assert - Шаг 2
        // TASK-1 перемещена дальше (через Ready for QA в QA или Done)
        Assert.NotEqual("Developing", task1.CurrentStage?.Stage.Name);
        
        // TASK-3 должна быть взята именно dev1 (не dev2!), несмотря на то что dev2 свободен
        Assert.Equal("Developing", task3.CurrentStage?.Stage.Name);
        Assert.Equal(dev1, task3.Worker);

        // Act - Шаг 3: завершаем TASK-1, TASK-2 и TASK-3
        task1.Progress = 100;
        task2.Progress = 100;
        task3.Progress = 100;
        service.ProcessMovements();

        // Assert - Шаг 3
        // TASK-1 и TASK-2 перемещены в Done
        var doneStage = simulation.Board.Stages.First(s => s.Stage.Name == "Done");
        Assert.Contains(doneStage.Tasks, t => t.Task.Key == "TASK-1");
        Assert.Contains(doneStage.Tasks, t => t.Task.Key == "TASK-2");

        // TASK-3 имеет навык "dev", а стадия QA требует "qa" — нет пересечения
        // Поэтому TASK-3 пропускает QA и Ready for QA, уходит сразу в Done
        // Это правильное поведение: задача не должна заходить на стадию, для которой у неё нет навыков
        Assert.Contains(doneStage.Tasks, t => t.Task.Key == "TASK-3");
        Assert.Equal("Done", task3.CurrentStage?.Stage.Name);
    }

    [Fact]
    public void ProcessMovements_MovesMultipleTasksWithDifferentRoles()
    {
        // Arrange
        // Workflow: todo -> developing (принимает dev-be и dev-fe) -> done
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            IsLeadTimeStart = true,
            RequiredSkills = [],
            Transitions = []
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            RequiredSkills = ["dev-be", "dev-fe"],
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

        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        var config = new SimulationConfig
        {
            Seed = 42,
            Workers =
            [
                new() {Login = "dev-be-worker", Skills = ["dev-be"], Performance = 100},
                new() {Login = "dev-fe-worker", Skills = ["dev-fe"], Performance = 100}
            ],
            Workflow = new Workflow
            {
                Stages = [todo, developing, done]
            },
            Tasks =
            [
                new() {Key = "TASK-1-FE", RequiredSkills = ["dev-fe"]},
                new() {Key = "TASK-2-BE", RequiredSkills = ["dev-be"]}
            ]
        };

        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var service = new TaskMovementService(simulation);

        // Act
        service.ProcessMovements();

        // Assert
        var todoStage = simulation.Board.Stages.First(s => s.Stage.Name == "Todo");
        var developingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");
        var doneStage = simulation.Board.Stages.First(s => s.Stage.Name == "Done");

        var task1Fe = simulation.Board.Tasks.First(t => t.Task.Key == "TASK-1-FE");
        var task2Be = simulation.Board.Tasks.First(t => t.Task.Key == "TASK-2-BE");
        var devBeWorker = simulation.Board.Workers.First(w => w.Worker.Skills.Contains("dev-be"));
        var devFeWorker = simulation.Board.Workers.First(w => w.Worker.Skills.Contains("dev-fe"));

        // Обе задачи должны покинуть Todo
        Assert.DoesNotContain(task1Fe, todoStage.Tasks);
        Assert.DoesNotContain(task2Be, todoStage.Tasks);

        // Обе задачи должны быть в Developing одновременно
        Assert.Contains(task1Fe, developingStage.Tasks);
        Assert.Contains(task2Be, developingStage.Tasks);

        // TASK-1-FE должна быть у worker'а с ролью dev-fe
        Assert.Equal("dev-fe-worker", task1Fe.Worker?.Worker.Login);
        Assert.Contains("dev-fe", task1Fe.Worker?.Worker.Skills ?? new List<string>());

        // TASK-2-BE должна быть у worker'а с ролью dev-be
        Assert.Equal("dev-be-worker", task2Be.Worker?.Worker.Login);
        Assert.Contains("dev-be", task2Be.Worker?.Worker.Skills ?? new List<string>());

        // Act - Шаг 2: завершаем обе задачи
        task1Fe.Progress = 100;
        task2Be.Progress = 100;
        service.ProcessMovements();

        // Assert - Шаг 2
        // Обе задачи должны перейти в Done
        Assert.Contains(task1Fe, doneStage.Tasks);
        Assert.Contains(task2Be, doneStage.Tasks);

        // Worker'ы должны освободиться
        Assert.Empty(devFeWorker.Assignments);
        Assert.Empty(devBeWorker.Assignments);
    }

    [Fact]
    public void WorkerExtensions_GetDaysForTask_CalculatesCorrectly()
    {
        // Arrange
        var worker = new Worker { Login = "dev1", Skills = ["dev-be"], Performance = 100 };
        
        var developingStage = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            StageProgressPercent = 100
        };

        var qaStage = new Stage
        {
            Name = "QA",
            Type = StageType.Work,
            StageProgressPercent = 30
        };

        var reviewStage = new Stage
        {
            Name = "Code Review",
            Type = StageType.Work,
            StageProgressPercent = 20
        };

        // Act & Assert
        // Developing (100%): M = 6 дней
        Assert.Equal(6, worker.GetDaysForTask(developingStage, TShirtType.M));
        
        // QA (30%): M = 6 * 0.3 = 1.8 -> округляем вверх = 2 дня
        Assert.Equal(2, worker.GetDaysForTask(qaStage, TShirtType.M));
        
        // Code Review (20%): M = 6 * 0.2 = 1.2 -> округляем вверх = 2 дня
        Assert.Equal(2, worker.GetDaysForTask(reviewStage, TShirtType.M));
        
        // Developing (100%): L = 15 дней
        Assert.Equal(15, worker.GetDaysForTask(developingStage, TShirtType.L));
        
        // QA (30%): L = 15 * 0.3 = 4.5 -> округляем вверх = 5 дней
        Assert.Equal(5, worker.GetDaysForTask(qaStage, TShirtType.L));
        
        // Developing (100%): XS = 1 день
        Assert.Equal(1, worker.GetDaysForTask(developingStage, TShirtType.XS));
        
        // QA (30%): XS = 1 * 0.3 = 0.3 -> округляем вверх = 1 день
        Assert.Equal(1, worker.GetDaysForTask(qaStage, TShirtType.XS));
        
        // Задача без размера = 1 день
        Assert.Equal(1, worker.GetDaysForTask(developingStage, null));
    }

    [Fact]
    public void FindAvailableWorker_QaCannotWorkOnDeveloping_WhenSkillsDontMatch()
    {
        // Arrange
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            IsLeadTimeStart = true,
            RequiredSkills = [],
            Transitions = []
        };

        // Стадия Developing требует навыки backend или frontend
        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            RequiredSkills = ["backend", "frontend"],
            Transitions = []
        };

        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });

        var config = new SimulationConfig
        {
            Seed = 42,
            Workers = [new() {Login = "qa1", Skills = ["qa"], Performance = 100}],
            Workflow = new Workflow
            {
                Stages = [todo, developing]
            },
            Tasks = [new() {Key = "TASK-1", RequiredSkills = ["backend", "qa"]}]
        };

        var simulation = new Simulation();
        simulation.InitFromConfig(config);
        var service = new TaskMovementService(simulation);

        // Act
        service.ProcessMovements();

        // Assert
        // Задача имеет requiredSkills ["backend", "qa"], Developing требует ["backend", "frontend"]
        // Есть пересечение (backend) → задача требует эту стадию
        // Но у qa1 нет backend → задача НЕ перемещается, остаётся в Todo
        var developingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");
        Assert.Empty(developingStage.Tasks);

        var todoStage = simulation.Board.Stages.First(s => s.Stage.Name == "Todo");
        Assert.Single(todoStage.Tasks);
    }

    [Fact]
    public void FindAvailableWorker_BackendCanWorkOnDeveloping_WhenSkillsMatch()
    {
        // Arrange
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            IsLeadTimeStart = true,
            RequiredSkills = [],
            Transitions = []
        };

        // Стадия Developing требует навыки backend или frontend
        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            RequiredSkills = ["backend", "frontend"],
            Transitions = []
        };

        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });

        var config = new SimulationConfig
        {
            Seed = 42,
            Workers = [new() {Login = "dev1", Skills = ["backend"], Performance = 100}],
            Workflow = new Workflow
            {
                Stages = [todo, developing]
            },
            Tasks = [new() {Key = "TASK-1", RequiredSkills = ["backend", "qa"]}]
        };

        var simulation = new Simulation();
        simulation.InitFromConfig(config);
        var service = new TaskMovementService(simulation);

        // Act
        service.ProcessMovements();

        // Assert
        // Задача должна быть назначена на dev1, потому что у него есть backend для стадии Developing
        var developingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");
        Assert.Single(developingStage.Tasks);
        
        // Задача должна быть назначена на dev1
        var task = developingStage.Tasks[0];
        Assert.NotNull(task.Worker);
        Assert.Equal("dev1", task.Worker?.Worker.Login);
    }
}
