using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Enums;
using BoardTask = KanbanFlowSerivce.Dtos.Config.Task;

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
            IsLeadTimeStart = true,
            RequiredSkills = [],
            Transitions = []
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            RequiredSkills = ["backend"],
            StageProgressPercent = 100,
            Transitions = []
        };

        var readyForTesting = new Stage
        {
            Name = "Ready for Testing",
            Type = StageType.Buffer,
            IsLeadTimeStart = false,
            RequiredSkills = [],
            Transitions = []
        };

        var testing = new Stage
        {
            Name = "Testing",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            RequiredSkills = ["qa"],
            StageProgressPercent = 30,
            Transitions = []
        };

        var releasePreparation = new Stage
        {
            Name = "Release Preparation",
            Type = StageType.Work,
            IsLeadTimeStart = false,
            RequiredSkills = ["backend"],
            StageProgressPercent = 20,
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

        // Устанавливаем DAG переходы (прямые ссылки)
        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = readyForTesting, Probability = 1.0 });
        readyForTesting.Transitions.Add(new StageTransition { Stage = testing, Probability = 1.0 });
        testing.Transitions.Add(new StageTransition { Stage = releasePreparation, Probability = 1.0 });
        releasePreparation.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers =
            [
                new()
                {
                    Login = "dev1",
                    Skills = ["backend"],
                    Performance = 100
                },

                new()
                {
                    Login = "qa1",
                    Skills = ["qa"],
                    Performance = 100
                }
            ],
            Workflow = new Workflow
            {
                Stages = [todo, developing, readyForTesting, testing, releasePreparation, done]
            },
            Tasks =
            [
                new()
                {
                    Key = "TASK-1",
                    Summary = "Реализовать API для пользователей",
                    ShirtType = TShirtType.L,
                    RequiredSkills = ["backend", "qa"]
                },

                new()
                {
                    Key = "TASK-2",
                    Summary = "Написать тесты для сервиса",
                    ShirtType = TShirtType.M,
                    RequiredSkills = ["backend", "qa"]
                }
            ]
        };
    }
}
