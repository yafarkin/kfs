using KanbanFlowConsole.Dtos.Board;
using KanbanFlowConsole.Dtos.Config;

namespace KanbanFlowConsole.Mappers;

public static class BoardStageMapper
{
    public static BoardStage MapToBoardStage(Stage stage)
    {
        return new BoardStage
        {
            Stage = stage,
            PrevStages = new List<BoardStage>(),
            NextStages = new List<BoardStage>(),
            Tasks = new List<BoardTask>()
        };
    }

    public static Dictionary<string, BoardStage> MapToBoardStageDictionary(IEnumerable<Stage> stages)
    {
        return stages.ToDictionary(s => s.Name, MapToBoardStage);
    }

    public static void LinkStages(Dictionary<string, BoardStage> stageMap, IEnumerable<Stage> stageConfigs)
    {
        foreach (var stageConfig in stageConfigs)
        {
            var boardStage = stageMap[stageConfig.Name];

            // Находим предыдущие стадии через обратные переходы
            foreach (var otherStageConfig in stageConfigs)
            {
                if (otherStageConfig.Transitions.Any(t => t.Stage.Name == stageConfig.Name))
                {
                    boardStage.PrevStages.Add(stageMap[otherStageConfig.Name]);
                }
            }

            // Устанавливаем следующие стадии через DAG переходы
            foreach (var transition in stageConfig.Transitions)
            {
                if (stageMap.TryGetValue(transition.Stage.Name, out var nextStage))
                {
                    boardStage.NextStages.Add(nextStage);
                }
            }
        }
    }

    public static List<BoardStage> MapToBoardStages(IEnumerable<Stage> stages)
    {
        var stageMap = MapToBoardStageDictionary(stages);
        LinkStages(stageMap, stages);
        return stageMap.Values.ToList();
    }
}
