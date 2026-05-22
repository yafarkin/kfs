using KanbanFlowConsole.Dtos;
using KanbanFlowConsole.Dtos.Config;
using KanbanFlowConsole.Enums;
using BoardTask = KanbanFlowConsole.Dtos.Config.Task;

namespace KanbanFlow.Tests;

public static class TestConfigFactory
{
    public static SimulationConfig CreateDefaultConfig()
    {
        // Создаём стадии
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
            StageProgressPercent = 100,
            Transitions = new List<StageTransition>()
        };

        var readyForTesting = new Stage
        {
            Name = "Ready for Testing",
            Type = StageType.Buffer,
            IsStart = false,
            IsLeadTimeStart = false,
            AllowedRoles = [],
            Transitions = new List<StageTransition>()
        };

        var testing = new Stage
        {
            Name = "Testing",
            Type = StageType.Work,
            IsStart = false,
            IsLeadTimeStart = false,
            AllowedRoles = ["QA Engineer"],
            StageProgressPercent = 30,
            Transitions = new List<StageTransition>()
        };

        var releasePreparation = new Stage
        {
            Name = "Release Preparation",
            Type = StageType.Work,
            IsStart = false,
            IsLeadTimeStart = false,
            AllowedRoles = ["Backend Developer"],
            StageProgressPercent = 20,
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

        // Устанавливаем DAG переходы (прямые ссылки)
        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = readyForTesting, Probability = 1.0 });
        readyForTesting.Transitions.Add(new StageTransition { Stage = testing, Probability = 1.0 });
        testing.Transitions.Add(new StageTransition { Stage = releasePreparation, Probability = 1.0 });
        releasePreparation.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers = new List<Worker>
            {
                new()
                {
                    Login = "dev1",
                    Role = "Backend Developer",
                    Performance = 100
                },
                new()
                {
                    Login = "qa1",
                    Role = "QA Engineer",
                    Performance = 100
                }
            },
            Workflow = new Workflow
            {
                Stages = new List<Stage> { todo, developing, readyForTesting, testing, releasePreparation, done }
            },
            Tasks = new List<BoardTask>
            {
                new()
                {
                    Key = "TASK-1",
                    Summary = "Реализовать API для пользователей",
                    ShirtType = TShirtType.L,
                    Role = "Backend Developer"
                },
                new()
                {
                    Key = "TASK-2",
                    Summary = "Написать тесты для сервиса",
                    ShirtType = TShirtType.M,
                    Role = "Backend Developer"
                }
            }
        };
    }
}
