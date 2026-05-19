using KanbanFlowConsole.Dtos;
using KanbanFlowConsole.Dtos.Board;
using KanbanFlowConsole.Dtos.Config;

namespace KanbanFlowConsole.Mappers;

public static class BoardMapper
{
    public static Board MapToBoard(SimulationConfig config)
    {
        var boardWorkers = BoardWorkerMapper.MapToBoardWorkers(config.Workers);
        var boardStages = BoardStageMapper.MapToBoardStages(config.Workflow.Stages);
        var boardTasks = BoardTaskMapper.MapToBoardTasks(config.Tasks);

        var stageMap = boardStages.ToDictionary(s => s.Stage.Name);

        BoardTaskMapper.DistributeTasksToStartStages(boardTasks, stageMap, config.Workflow.Stages);

        return new Board
        {
            Stages = boardStages,
            Workers = boardWorkers,
            Tasks = boardTasks
        };
    }
}
