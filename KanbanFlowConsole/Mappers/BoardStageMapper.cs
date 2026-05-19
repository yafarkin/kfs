namespace KanbanFlowConsole.Mappers;

public static class BoardStageMapper
{
    public static Dtos.BoardStage MapToBoardStage(Dtos.Stage stage)
    {
        return new Dtos.BoardStage
        {
            Stage = stage,
            PrevStages = new List<Dtos.BoardStage>(),
            NextStages = new List<Dtos.BoardStage>(),
            Tasks = new List<Dtos.BoardTask>()
        };
    }

    public static Dictionary<string, Dtos.BoardStage> MapToBoardStageDictionary(IEnumerable<Dtos.Stage> stages)
    {
        return stages.ToDictionary(s => s.Name, MapToBoardStage);
    }

    public static void LinkStages(Dictionary<string, Dtos.BoardStage> stageMap, IEnumerable<Dtos.Stage> stageConfigs)
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

    public static List<Dtos.BoardStage> MapToBoardStages(IEnumerable<Dtos.Stage> stages)
    {
        var stageMap = MapToBoardStageDictionary(stages);
        LinkStages(stageMap, stages);
        return stageMap.Values.ToList();
    }
}
