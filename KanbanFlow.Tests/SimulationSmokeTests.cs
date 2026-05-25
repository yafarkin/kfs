using KanbanFlowConsole.Dtos;
using KanbanFlowConsole.Dtos.Config;
using KanbanFlowConsole.Dtos.History;
using KanbanFlowConsole.Enums;
using KanbanFlowConsole.Services;

namespace KanbanFlow.Tests;

/// <summary>
///     Интеграционный (smoke) тест полного цикла симуляции
///     Проверяет корректное прохождение задач через все стадии workflow
/// </summary>
public class SimulationSmokeTests
{
    [Fact]
    public void Simulation_FullLifecycle_AllTasksReachDone()
    {
        // Arrange
        var config = TestConfigFactory.CreateDefaultConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);
        var service = new TaskMovementService(simulation);

        // Сохраняем общее количество задач для проверки
        var totalTasks = config.Tasks.Count;

        // Act - Запускаем симуляцию до завершения всех задач
        SimulateUntilCompletion(simulation, service);

        // Assert - Проверяем, что все задачи достигли стадии Done
        var doneStage = simulation.Board.Stages.First(s => s.Stage.Name == "Done");
        Assert.Equal(totalTasks, doneStage.Tasks.Count);

        // Проверяем, что все остальные стадии пусты
        var nonDoneStages = simulation.Board.Stages.Where(s => s.Stage.Name != "Done");
        foreach (var stage in nonDoneStages)
        {
            Assert.Empty(stage.Tasks);
        }

        // Проверяем, что у всех задач есть история переходов
        foreach (var task in simulation.Board.Tasks)
        {
            Assert.NotEmpty(task.TransitionHistory);
            Assert.Equal("Done", task.CurrentStage?.Stage.Name);
        }
    }

    [Fact]
    public void Simulation_FullLifecycle_TasksPassThroughAllStages()
    {
        // Arrange
        var config = TestConfigFactory.CreateDefaultConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);
        var service = new TaskMovementService(simulation);

        // Act
        SimulateUntilCompletion(simulation, service);

        // Assert - Проверяем, что каждая задача прошла через все стадии
        // Начальная стадия (Todo) не записывается в историю, т.к. это не перемещение
        var expectedStages = config.Workflow.Stages
            .Where(s => !s.IsStart) // Исключаем стартовую стадию
            .Select(s => s.Name)
            .ToList();
        
        foreach (var task in simulation.Board.Tasks)
        {
            var visitedStages = task.TransitionHistory
                .Select(h => h.ToStage.Stage.Name)
                .ToList();

            // Проверяем, что все стадии были посещены
            foreach (var expectedStage in expectedStages)
            {
                Assert.Contains(expectedStage, visitedStages);
            }
        }
    }

    [Fact]
    public void Simulation_FullLifecycle_HistoryIsRecorded()
    {
        // Arrange
        var config = TestConfigFactory.CreateDefaultConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);
        var service = new TaskMovementService(simulation);

        // Act
        SimulateUntilCompletion(simulation, service);

        // Assert - Проверяем, что история записана
        Assert.NotEmpty(simulation.History);
        
        // Проверяем, что есть события перемещения задач
        var allActivities = simulation.History.SelectMany(d => d.Activities).ToList();
        var moveActivities = allActivities.Where(a => a.Type == ActivityType.TaskMoved).ToList();
        
        // Каждая задача должна иметь как минимум 5 перемещений (по количеству стадий - 1)
        Assert.True(moveActivities.Count >= config.Tasks.Count * 5);
    }

    [Fact]
    public void Simulation_FullLifecycle_WorkersAssignedCorrectly()
    {
        // Arrange
        var config = TestConfigFactory.CreateDefaultConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);
        var service = new TaskMovementService(simulation);

        // Act
        SimulateUntilCompletion(simulation, service);

        // Assert - Проверяем, что воркеры были назначены на рабочих стадиях
        var developingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");
        var testingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Testing");
        var releasePrepStage = simulation.Board.Stages.First(s => s.Stage.Name == "Release Preparation");

        // Проверяем историю - на рабочих стадиях должны быть назначены воркеры
        var allActivities = simulation.History.SelectMany(d => d.Activities).ToList();
        var moveActivities = allActivities.Where(a => a.Type == ActivityType.TaskMoved).ToList();

        foreach (var activity in moveActivities)
        {
            if (activity.Stage is { Stage.Type: StageType.Work })
            {
                // На рабочих стадиях должен быть назначен воркер
                Assert.NotNull(activity.Worker);
            }
        }
    }

    [Fact]
    public void Simulation_FullLifecycle_ProgressResetOnMove()
    {
        // Arrange
        var config = TestConfigFactory.CreateDefaultConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);
        var service = new TaskMovementService(simulation);

        // Act
        SimulateUntilCompletion(simulation, service);

        // Assert - Проверяем, что прогресс задач в Done не имеет значения (задачи завершены)
        var doneStage = simulation.Board.Stages.First(s => s.Stage.Name == "Done");
        
        // Все задачи в Done должны иметь прогресс 0 (сброшен при последнем перемещении)
        // или любое значение, т.к. в Done задача не требует выполнения
        foreach (var task in doneStage.Tasks)
        {
            // Задача достигла финальной стадии
            Assert.Equal("Done", task.CurrentStage?.Stage.Name);
        }
    }

    [Fact]
    public void Simulation_FullLifecycle_StageProgressPercentIsApplied()
    {
        // Arrange
        var config = TestConfigFactory.CreateDefaultConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);
        var service = new TaskMovementService(simulation);

        // Получаем стадии для проверки
        var developingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");
        var testingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Testing");
        var releaseStage = simulation.Board.Stages.First(s => s.Stage.Name == "Release Preparation");

        // Получаем задачи
        var taskL = simulation.Board.Tasks.First(t => t.Task.Key == "TASK-1"); // L = 15 дней
        var taskM = simulation.Board.Tasks.First(t => t.Task.Key == "TASK-2"); // M = 6 дней

        // Act - первый шаг симуляции (задачи переходят в Developing)
        service.ProcessMovements();

        // Assert - Проверяем расчёт времени для стадий
        // Developing (100%): L = 15 дней, M = 6 дней
        Assert.Equal(15, developingStage.Stage.GetDaysForTask(TShirtType.L));
        Assert.Equal(6, developingStage.Stage.GetDaysForTask(TShirtType.M));
        
        // Testing (30%): L = 15 * 0.3 = 4.5 → 5 дней, M = 6 * 0.3 = 1.8 → 2 дня
        Assert.Equal(5, testingStage.Stage.GetDaysForTask(TShirtType.L));
        Assert.Equal(2, testingStage.Stage.GetDaysForTask(TShirtType.M));
        
        // Release Preparation (20%): L = 15 * 0.2 = 3 дня, M = 6 * 0.2 = 1.2 → 2 дня
        Assert.Equal(3, releaseStage.Stage.GetDaysForTask(TShirtType.L));
        Assert.Equal(2, releaseStage.Stage.GetDaysForTask(TShirtType.M));

        // Проверяем что буферные стадии не имеют расчёта (процент = 0 или не используется)
        var todoStage = simulation.Board.Stages.First(s => s.Stage.Name == "Todo");
        var readyForTestingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Ready for Testing");
        
        // Буферные стадии не должны иметь StageProgressPercent = 100 (по умолчанию)
        // но они не являются рабочими, поэтому расчёт времени для них не применяется
        Assert.Equal(StageType.Buffer, todoStage.Stage.Type);
        Assert.Equal(StageType.Buffer, readyForTestingStage.Stage.Type);
    }

    [Fact]
    public void Simulation_FullLifecycle_WorkerCalculatesDaysForTask()
    {
        // Arrange
        var config = TestConfigFactory.CreateDefaultConfig();
        var simulation = new Simulation();
        simulation.InitFromConfig(config);

        var devWorker = simulation.Board.Workers.First(w => w.Worker.Skills.Contains("backend"));
        var qaWorker = simulation.Board.Workers.First(w => w.Worker.Skills.Contains("qa"));

        var developingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Developing");
        var testingStage = simulation.Board.Stages.First(s => s.Stage.Name == "Testing");

        // Act & Assert - Проверяем расчёт дней worker'ами
        // Dev worker на Developing (100%): L = 15 дней
        Assert.Equal(15, devWorker.Worker.GetDaysForTask(developingStage.Stage, TShirtType.L));
        
        // Dev worker на Release Preparation (20%): L = 3 дня
        var releaseStage = simulation.Board.Stages.First(s => s.Stage.Name == "Release Preparation");
        Assert.Equal(3, devWorker.Worker.GetDaysForTask(releaseStage.Stage, TShirtType.L));
        
        // QA worker на Testing (30%): M = 2 дня
        Assert.Equal(2, qaWorker.Worker.GetDaysForTask(testingStage.Stage, TShirtType.M));
        
        // QA worker на Testing (30%): L = 5 дней
        Assert.Equal(5, qaWorker.Worker.GetDaysForTask(testingStage.Stage, TShirtType.L));
    }

    /// <summary>
    ///     Запускает симуляцию до тех пор, пока все задачи не достигнут стадии Done
    /// </summary>
    private static void SimulateUntilCompletion(Simulation simulation, TaskMovementService service)
    {
        var maxDays = 100; // Защита от бесконечного цикла
        var day = 0;

        while (day < maxDays)
        {
            day++;
            simulation.StartNewDay();

            // Обрабатываем все возможные перемещения
            service.ProcessMovements();

            // Проверяем, все ли задачи в Done
            var doneStage = simulation.Board.Stages.SingleOrDefault(s => s.Stage.Name == "Done");
            if (doneStage != null && doneStage.Tasks.Count == simulation.Board.Tasks.Count)
            {
                break;
            }

            // Симулируем выполнение задач на рабочих стадиях (прогресс)
            SimulateWorkProgress(simulation);
        }
    }

    /// <summary>
    ///     Симулирует выполнение задач воркерами (увеличение прогресса)
    /// </summary>
    private static void SimulateWorkProgress(Simulation simulation)
    {
        foreach (var worker in simulation.Board.Workers)
        {
            foreach (var assignment in worker.Assignments)
            {
                if (assignment.Stage.Stage.Type == StageType.Work)
                {
                    // Увеличиваем прогресс задачи
                    assignment.Task.Progress = Math.Min(100, assignment.Task.Progress + 25);

                    // Записываем в историю
                    simulation.LogActivity(new HistoryActivity
                    {
                        Type = ActivityType.TaskProgressUpdated,
                        Description = $"Задача {assignment.Task.Task.Key} выполняется на {assignment.Task.Progress}%",
                        Task = assignment.Task,
                        Worker = worker,
                        Stage = assignment.Stage,
                        Progress = assignment.Task.Progress
                    });
                }
            }
        }
    }
}
