using KanbanFlowConsole.Dtos.Board;
using KanbanFlowConsole.Dtos.Config;
using Task = KanbanFlowConsole.Dtos.Config.Task;

namespace KanbanFlowConsole.Mappers;

public static class BoardTaskMapper
{
    public static BoardTask MapToBoardTask(Task task, decimal initialProgress = 0)
    {
        return new BoardTask
        {
            Task = task,
            Progress = initialProgress
        };
    }

    public static List<BoardTask> MapToBoardTasks(IEnumerable<Task> tasks, decimal initialProgress = 0)
    {
        return tasks.Select(t => MapToBoardTask(t, initialProgress)).ToList();
    }

    public static void DistributeTasksToStartStages(
        List<BoardTask> boardTasks,
        Dictionary<string, BoardStage> stageMap,
        IEnumerable<Stage> stageConfigs)
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
