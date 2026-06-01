using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Enums;

namespace KanbanFlowSerivce.Factories;

/// <summary>
/// Фабрика для создания упрощённой конфигурации симуляции:
/// 1 разработчик, процесс Todo -> Developing -> Done, задачи S и M.
/// </summary>
public static class SimpleConfigFactory
{
    public static SimulationConfig CreateDefaultConfig()
    {
        // Создаём стадии
        var todo = new Stage
        {
            Name = "Todo",
            Type = StageType.Buffer,
        };

        var developing = new Stage
        {
            Name = "Developing",
            Type = StageType.Work,
            IsLeadTimeStart = true,
            RequiredSkills = ["backend"],
            StageProgressPercent = 100,
            CreatesValue = true,
        };

        var done = new Stage
        {
            Name = "Done",
            Type = StageType.Buffer,
        };

        // Устанавливаем DAG переходы (прямые ссылки)
        todo.Transitions.Add(new StageTransition { Stage = developing, Probability = 1.0 });
        developing.Transitions.Add(new StageTransition { Stage = done, Probability = 1.0 });

        return new SimulationConfig
        {
            Seed = 42,
            Workers =
            [
                new()
                {
                    Login = "dev1",
                    Skills = ["backend"],
                    Performance = 100,
                    WipLimit = 1,
                    // DeviationDownPercent = 30,
                    // DeviationUpPercent = 50,
                }
            ],
            Workflow = new Workflow
            {
                Stages = [todo, developing, done]
            },
            Tasks =
            [
                new()
                {
                    Key = "TASK-1",
                    Summary = "Задача размера S",
                    ShirtType = TShirtType.S,
                    RequiredSkills = ["backend"]
                },

                new()
                {
                    Key = "TASK-2",
                    Summary = "Задача размера M",
                    ShirtType = TShirtType.M,
                    RequiredSkills = ["backend"]
                }
            ]
        };
    }
}
