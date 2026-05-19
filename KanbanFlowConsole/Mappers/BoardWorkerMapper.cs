using KanbanFlowConsole.Dtos.Board;
using KanbanFlowConsole.Dtos.Config;

namespace KanbanFlowConsole.Mappers;

public static class BoardWorkerMapper
{
    public static BoardWorker MapToBoardWorker(Worker worker)
    {
        return new BoardWorker
        {
            Worker = worker
        };
    }

    public static List<BoardWorker> MapToBoardWorkers(IEnumerable<Worker> workers)
    {
        return workers.Select(MapToBoardWorker).ToList();
    }
}
