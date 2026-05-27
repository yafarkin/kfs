using KanbanFlowSerivce.Dtos.Board;
using KanbanFlowSerivce.Dtos.Config;
using Config_Task = KanbanFlowSerivce.Dtos.Config.Task;
using Task = KanbanFlowSerivce.Dtos.Config.Task;

namespace KanbanFlowSerivce.Mappers;

public static class BoardTaskMapper
{
    public static BoardTask MapToBoardTask(Config_Task task, decimal initialProgress = 0)
    {
        return new BoardTask
        {
            Task = task,
            Progress = initialProgress
        };
    }

    public static List<BoardTask> MapToBoardTasks(IEnumerable<Config_Task> tasks, decimal initialProgress = 0)
    {
        return tasks.Select(t => MapToBoardTask(t, initialProgress)).ToList();
    }

    public static void DistributeTasksToStartStages(
        List<BoardTask> boardTasks,
        Dictionary<string, BoardStage> stageMap)
    {
        // Пока все задачи распределяем на первую стартовую стадию
        var firstStartStage = stageMap.Single(s => s.Value.PrevStages.Count == 0).Key;
        foreach (var boardTask in boardTasks)
        {
            stageMap[firstStartStage].Tasks.Add(boardTask);
        }
    }
}
