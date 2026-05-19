namespace KanbanFlowConsole.Mappers;

public static class BoardWorkerMapper
{
    public static Dtos.BoardWorker MapToBoardWorker(Dtos.Worker worker)
    {
        return new Dtos.BoardWorker
        {
            Worker = worker
        };
    }

    public static List<Dtos.BoardWorker> MapToBoardWorkers(IEnumerable<Dtos.Worker> workers)
    {
        return workers.Select(MapToBoardWorker).ToList();
    }
}
