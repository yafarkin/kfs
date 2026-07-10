using KanbanFlowSerivce.Dtos.Board;
using KanbanFlowSerivce.Dtos.Config;

namespace KanbanFlowSerivce.Mappers;

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

            // Находим предыдущие стадии через обратные переходы (self-loop не считается)
            foreach (var otherStageConfig in stageConfigs)
            {
                if (otherStageConfig.Name != stageConfig.Name && 
                    otherStageConfig.Transitions.Any(t => t.Stage.Name == stageConfig.Name))
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

            // Устанавливаем исключаемую стадию для RequiresDifferentResource
            if (stageConfig.RequiresDifferentResource && stageConfig.RequiresDifferentResourceFromStage != null)
            {
                if (stageMap.TryGetValue(stageConfig.RequiresDifferentResourceFromStage, out var excludedStage))
                {
                    boardStage.ExcludedStage = excludedStage;
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
