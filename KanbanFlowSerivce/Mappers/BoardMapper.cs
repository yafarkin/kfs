using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Board;
using KanbanFlowSerivce.Dtos.Config;

namespace KanbanFlowSerivce.Mappers;

public static class BoardMapper
{
    public static Board MapToBoard(SimulationConfig config)
    {
        var boardWorkers = BoardWorkerMapper.MapToBoardWorkers(config.Workers);
        var boardStages = BoardStageMapper.MapToBoardStages(config.Workflow.Stages);
        var boardTasks = BoardTaskMapper.MapToBoardTasks(config.Tasks);

        var stageMap = boardStages.ToDictionary(s => s.Stage.Name);

        BoardTaskMapper.DistributeTasksToStartStages(boardTasks, stageMap);

        return new Board
        {
            Stages = boardStages,
            Workers = boardWorkers,
            Tasks = boardTasks
        };
    }
}
