namespace KanbanFlowConsole.Mappers;

public static class BoardTaskMapper
{
    public static Dtos.BoardTask MapToBoardTask(Dtos.Task task, decimal initialProgress = 0)
    {
        return new Dtos.BoardTask
        {
            Task = task,
            Progress = initialProgress
        };
    }

    public static List<Dtos.BoardTask> MapToBoardTasks(IEnumerable<Dtos.Task> tasks, decimal initialProgress = 0)
    {
        return tasks.Select(t => MapToBoardTask(t, initialProgress)).ToList();
    }

    public static void DistributeTasksToStartStages(
        List<Dtos.BoardTask> boardTasks,
        Dictionary<string, Dtos.BoardStage> stageMap,
        IEnumerable<Dtos.Stage> stageConfigs)
    {
        var startStages = stageConfigs.Where(s => s.IsStart).ToList();
        if (startStages.Count == 0)
        {
            return;
        }

        // Пока все задачи распределяем на первую стартовую стадию
        var firstStartStage = startStages[0];
        foreach (var boardTask in boardTasks)
        {
            stageMap[firstStartStage.Name].Tasks.Add(boardTask);
        }
    }
}
