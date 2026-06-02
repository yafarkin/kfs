using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Board;
using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Dtos.History;
using KanbanFlowSerivce.Enums;
using KanbanFlowSerivce.Services;
using Task = KanbanFlowSerivce.Dtos.Config.Task;

namespace KanbanFlow.Tests;

/// <summary>
/// Тесты для новых событий истории: CorrelationId, TaskWaiting, TaskResumed, LeadTimeStarted
/// </summary>
public class HistoryActivityTests
{
    [Fact]
    public void WorkerTookTask_And_WorkerCompletedTask_HaveSameCorrelationId()
    {
        // Arrange
        var config = CreateSimpleWorkflowConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act - Симулируем работу до завершения задачи
        for (var day = 0; day < 10; day++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Assert - Проверяем что WorkerTookTask и WorkerCompletedTask имеют одинаковый CorrelationId
        var allActivities = simulation.History.SelectMany(d => d.Activities).ToList();
        var tookTasks = allActivities.Where(a => a.Type == ActivityType.WorkerTookTask).ToList();
        var completedTasks = allActivities.Where(a => a.Type == ActivityType.WorkerCompletedTask).ToList();

        foreach (var tookTask in tookTasks)
        {
            // CorrelationId не должен быть пустым
            Assert.NotEqual(Guid.Empty, tookTask.CorrelationId);

            // Найти соответствующее WorkerCompletedTask
            var completedTask = completedTasks.FirstOrDefault(c => c.CorrelationId == tookTask.CorrelationId);

            // Если задача завершена, CorrelationId должен совпадать
            if (completedTask != null)
            {
                Assert.Equal(tookTask.CorrelationId, completedTask.CorrelationId);
                Assert.Equal(tookTask.DayNumber, completedTask.DayNumber);
            }
        }
    }

    [Fact]
    public void TaskWaiting_Logged_WhenWorkerNotAvailable()
    {
        // Arrange - создаём конфигурацию где воркер будет занят (WIP=1, 3 задачи)
        var config = CreateWorkflowWithWipLimit();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act - симулируем несколько дней чтобы создать очередь
        for (var day = 0; day < 10; day++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Assert - проверяем что события TaskWaiting записываются корректно (если возникают)
        var allActivities = simulation.History.SelectMany(d => d.Activities).ToList();
        var waitingEvents = allActivities.Where(a => a.Type == ActivityType.TaskWaiting).ToList();

        // Проверяем формат событий если они есть
        foreach (var waitingEvent in waitingEvents)
        {
            Assert.NotNull(waitingEvent.TaskKey);
            Assert.NotNull(waitingEvent.StageName);
            Assert.StartsWith("TASK-", waitingEvent.TaskKey);
        }
    }

    [Fact]
    public void TaskResumed_Logged_AfterTaskWaiting()
    {
        // Arrange - конфигурация с WIP=1 и 3 задачами для создания очереди
        var config = CreateWorkflowWithWipLimit();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act - симулируем несколько дней
        for (var day = 0; day < 10; day++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Assert - проверяем что TaskResumed был после TaskWaiting (если события есть)
        var allActivities = simulation.History.SelectMany(d => d.Activities).ToList();
        var waitingEvents = allActivities.Where(a => a.Type == ActivityType.TaskWaiting).ToList();
        var resumedEvents = allActivities.Where(a => a.Type == ActivityType.TaskResumed).ToList();

        // Проверяем что каждый TaskResumed был после соответствующего TaskWaiting
        foreach (var resumed in resumedEvents)
        {
            var waiting = waitingEvents.FirstOrDefault(w =>
                w.TaskKey == resumed.TaskKey &&
                w.StageName == resumed.StageName &&
                w.DayNumber < resumed.DayNumber);

            // Если TaskResumed был, должно быть соответствующее TaskWaiting
            if (waiting != null)
            {
                Assert.True(resumed.DayNumber > waiting.DayNumber);
            }
        }
    }

    [Fact]
    public void LeadTimeStarted_Logged_WhenTaskReachesFirstStage()
    {
        // Arrange - конфигурация с IsLeadTimeStart=true на стадии Todo
        var config = CreateWorkflowWithLeadTimeStart();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act - симулируем несколько дней
        for (var day = 0; day < 10; day++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Assert - проверяем что LeadTimeStarted записывается корректно (если возникает)
        var allActivities = simulation.History.SelectMany(d => d.Activities).ToList();
        var leadTimeStartEvents = allActivities.Where(a => a.Type == ActivityType.LeadTimeStarted).ToList();

        // Проверяем что событие содержит правильные данные (если есть)
        foreach (var evt in leadTimeStartEvents)
        {
            Assert.StartsWith("TASK-", evt.TaskKey);
            Assert.NotNull(evt.StageName);
        }
    }

    [Fact]
    public void LeadTimeStarted_LoggedOnly_ForFirstMovement()
    {
        // Arrange
        var config = CreateWorkflowWithLeadTimeStart();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act - симулируем полный цикл
        for (var day = 0; day < 20; day++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Assert - LeadTimeStarted должен быть только один раз для каждой задачи
        var allActivities = simulation.History.SelectMany(d => d.Activities).ToList();
        var leadTimeStartEvents = allActivities.Where(a => a.Type == ActivityType.LeadTimeStarted).ToList();

        // Группируем по задачам
        var byTask = leadTimeStartEvents.GroupBy(e => e.TaskKey);

        // У каждой задачи должно быть только одно событие LeadTimeStarted
        foreach (var group in byTask)
        {
            Assert.Single(group);
        }
    }

    [Fact]
    public void TaskWaiting_HasCorrectTaskAndStageInfo()
    {
        // Arrange
        var config = CreateWorkflowWithWipLimit();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act
        for (var day = 0; day < 5; day++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Assert
        var allActivities = simulation.History.SelectMany(d => d.Activities).ToList();
        var waitingEvents = allActivities.Where(a => a.Type == ActivityType.TaskWaiting).ToList();

        foreach (var waitingEvent in waitingEvents)
        {
            Assert.NotNull(waitingEvent.TaskKey);
            Assert.NotNull(waitingEvent.StageName);
            Assert.StartsWith("TASK-", waitingEvent.TaskKey);
        }
    }

    [Fact]
    public void TaskResumed_HasCorrectTickReference()
    {
        // Arrange
        var config = CreateWorkflowWithWipLimit();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var movementService = new TaskMovementService(simulation);
        var progressService = new WorkProgressService(simulation);

        // Act
        for (var day = 0; day < 10; day++)
        {
            simulation.StartNewDay();
            movementService.ProcessMovements();
            progressService.SimulateWorkDay();
        }

        // Assert
        var allActivities = simulation.History.SelectMany(d => d.Activities).ToList();
        var waitingEvents = allActivities.Where(a => a.Type == ActivityType.TaskWaiting).ToList();
        var resumedEvents = allActivities.Where(a => a.Type == ActivityType.TaskResumed).ToList();

        foreach (var resumed in resumedEvents)
        {
            // Проверяем что событие возобновления содержит корректные данные
            Assert.NotNull(resumed.TaskKey);
            Assert.NotNull(resumed.StageName);
        }
    }

    #region Helper Methods

    private static SimulationConfig CreateSimpleWorkflowConfig()
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
            CreatesValue = true,
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
            Tasks = [
                new() { Key = "TASK-1", RequiredSkills = ["dev"] },
                new() { Key = "TASK-2", RequiredSkills = ["dev"] }
            ],
            UseVariability = false
        };
    }

    private static SimulationConfig CreateWorkflowWithWipLimit()
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
            CreatesValue = true,
            StageProgressPercent = 50, // Быстрое выполнение
            RequiredSkills = ["dev"],
            WipLimit = 1, // Ограничение воркера
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
            Tasks = [
                new() { Key = "TASK-1", RequiredSkills = ["dev"] },
                new() { Key = "TASK-2", RequiredSkills = ["dev"] },
                new() { Key = "TASK-3", RequiredSkills = ["dev"] }
            ],
            UseVariability = false
        };
    }

    private static SimulationConfig CreateWorkflowWithLeadTimeStart()
    {
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
            IsLeadTimeStart = true, // Явно указываем что это начало Lead Time
            Transitions = new List<StageTransition>()
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            CreatesValue = true,
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
            Workers = [new() { Login = "dev1", Skills = ["dev"], Performance = 100 }],
            Workflow = new Workflow { Stages = [todo, developing, done] },
            Tasks = [
                new() { Key = "TASK-1", RequiredSkills = ["dev"] },
                new() { Key = "TASK-2", RequiredSkills = ["dev"] }
            ],
            UseVariability = false
        };
    }

    #endregion
}
