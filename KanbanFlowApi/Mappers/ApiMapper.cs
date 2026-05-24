using KanbanFlowApi.Dtos;
using KanbanFlowConsole.Dtos;
using KanbanFlowConsole.Dtos.Board;
using KanbanFlowConsole.Dtos.Config;
using KanbanFlowConsole.Dtos.History;
using DomainBoard = KanbanFlowConsole.Dtos.Board;
using DomainTask = KanbanFlowConsole.Dtos.Config.Task;

namespace KanbanFlowApi.Mappers;

/// <summary>
/// Маппер для конвертации между доменными моделями и API DTO
/// </summary>
public static class ApiMapper
{
    #region Config

    /// <summary>
    /// Конвертирует доменную конфигурацию в API DTO (без циклических ссылок)
    /// </summary>
    public static ApiSimulationConfigDto ToApiDto(SimulationConfig config)
    {
        return new ApiSimulationConfigDto
        {
            Seed = config.Seed,
            Workers = config.Workers.Select(ToApiDto).ToList(),
            Workflow = new ApiWorkflowDto
            {
                Stages = config.Workflow.Stages.Select(ToApiDto).ToList()
            },
            Tasks = config.Tasks.Select(ToApiDto).ToList()
        };
    }

    /// <summary>
    /// Конвертирует API DTO в доменную конфигурацию
    /// </summary>
    public static SimulationConfig ToDomainConfig(ApiSimulationConfigDto dto)
    {
        // Сначала создаём все стадии без переходов
        var stagesMap = new Dictionary<string, Stage>();
        foreach (var stageDto in dto.Workflow.Stages)
        {
            stagesMap[stageDto.Name] = new Stage
            {
                Name = stageDto.Name,
                Type = stageDto.Type,
                IsStart = stageDto.IsStart,
                IsLeadTimeStart = stageDto.IsLeadTimeStart,
                WipLimit = stageDto.WipLimit,
                AllowedRoles = stageDto.AllowedRoles.ToArray(),
                RequiresDifferentResource = stageDto.RequiresDifferentResource,
                RequiresDifferentResourceFromStage = stageDto.RequiresDifferentResourceFromStage,
                StageProgressPercent = stageDto.StageProgressPercent,
                Transitions = new List<StageTransition>()
            };
        }

        // Затем устанавливаем переходы
        foreach (var stageDto in dto.Workflow.Stages)
        {
            var stage = stagesMap[stageDto.Name];
            foreach (var transitionName in stageDto.TransitionStageNames)
            {
                if (stagesMap.TryGetValue(transitionName, out var targetStage))
                {
                    stage.Transitions.Add(new StageTransition
                    {
                        Stage = targetStage,
                        Probability = 1.0
                    });
                }
            }
        }

        return new SimulationConfig
        {
            Seed = dto.Seed,
            Workers = dto.Workers.Select(ToDomainWorker).ToList(),
            Workflow = new Workflow
            {
                Stages = stagesMap.Values.ToList()
            },
            Tasks = dto.Tasks.Select(ToDomainTask).ToList()
        };
    }

    #endregion

    #region Simulation State

    /// <summary>
    /// Конвертирует доменную симуляцию в API DTO (полное состояние)
    /// </summary>
    public static ApiSimulationStateDto ToApiDto(Simulation simulation)
    {
        return new ApiSimulationStateDto
        {
            Config = ToApiDto(simulation.Config),
            Board = ToApiDto(simulation.Board),
            History = simulation.History.Select(ToApiDto).ToList(),
            CurrentDay = simulation.CurrentDay,
            CurrentTick = simulation.CurrentTick
        };
    }

    /// <summary>
    /// Конвертирует API DTO состояния в доменную симуляцию
    /// </summary>
    public static Simulation ToDomainSimulation(ApiSimulationStateDto dto)
    {
        var config = ToDomainConfig(dto.Config);
        var simulation = new Simulation();
        
        // Инициализируем симуляцию (создаёт Config, Board, пустую History)
        simulation.InitFromConfig(config);
        
        // Пересоздаём Board из DTO (с правильными связями)
        simulation.Board = ToDomainBoard(dto.Board, config);
        
        // Восстанавливаем историю из DTO (перезаписываем пустую)
        simulation.History = dto.History.Select(ToDomainHistoryDay).ToList();

        return simulation;
    }

    #endregion

    #region Board

    private static ApiBoardDto ToApiDto(DomainBoard.Board board)
    {
        return new ApiBoardDto
        {
            Stages = board.Stages.Select(ToApiDto).ToList(),
            Workers = board.Workers.Select(ToApiDto).ToList(),
            Tasks = board.Tasks.Select(ToApiDto).ToList()
        };
    }

    private static ApiBoardStageDto ToApiDto(DomainBoard.BoardStage stage)
    {
        return new ApiBoardStageDto
        {
            Name = stage.Stage.Name,
            Type = stage.Stage.Type,
            IsStart = stage.Stage.IsStart,
            IsLeadTimeStart = stage.Stage.IsLeadTimeStart,
            WipLimit = stage.WipLimit,
            WipCount = stage.WipCount,
            CanAcceptTasks = stage.CanAcceptTasks,
            TaskKeys = stage.Tasks.Select(t => t.Task.Key).ToList(),
            NextStageNames = stage.NextStages.Select(s => s.Stage.Name).ToList()
        };
    }

    private static ApiBoardWorkerDto ToApiDto(DomainBoard.BoardWorker worker)
    {
        return new ApiBoardWorkerDto
        {
            Login = worker.Worker.Login,
            Role = worker.Worker.Role,
            WipLimit = worker.WipLimit,
            WipCount = worker.WipCount,
            IsAvailable = worker.IsAvailable,
            AssignedTaskKeys = worker.Assignments.Select(a => a.Task.Task.Key).ToList()
        };
    }

    private static ApiBoardTaskDto ToApiDto(DomainBoard.BoardTask task)
    {
        return new ApiBoardTaskDto
        {
            Key = task.Task.Key,
            Summary = task.Task.Summary,
            ShirtType = task.Task.ShirtType,
            Role = task.Task.Role,
            Progress = task.Progress,
            WorkerLogin = task.Worker?.Worker.Login,
            CurrentStageName = task.CurrentStage?.Stage.Name
        };
    }

    private static DomainBoard.Board ToDomainBoard(ApiBoardDto dto, SimulationConfig config)
    {
        // Создаём маппинг стадий по имени
        var stagesMap = new Dictionary<string, DomainBoard.BoardStage>();
        foreach (var stageDto in dto.Stages)
        {
            var configStage = config.Workflow.Stages.First(s => s.Name == stageDto.Name);
            stagesMap[stageDto.Name] = new DomainBoard.BoardStage
            {
                Stage = configStage,
                Tasks = new List<DomainBoard.BoardTask>(),
                NextStages = new List<DomainBoard.BoardStage>(),
                PrevStages = new List<DomainBoard.BoardStage>()
            };
        }

        // Устанавливаем связи между стадиями
        foreach (var stageDto in dto.Stages)
        {
            var boardStage = stagesMap[stageDto.Name];
            var configStage = config.Workflow.Stages.First(s => s.Name == stageDto.Name);

            foreach (var nextName in stageDto.NextStageNames)
            {
                if (stagesMap.TryGetValue(nextName, out var nextStage))
                {
                    boardStage.NextStages.Add(nextStage);
                    // Устанавливаем обратную связь: prevStage для nextStage
                    nextStage.PrevStages.Add(boardStage);
                }
            }
        }

        // Создаём воркеров
        var workersMap = new Dictionary<string, DomainBoard.BoardWorker>();
        foreach (var workerDto in dto.Workers)
        {
            var configWorker = config.Workers.First(w => w.Login == workerDto.Login);
            workersMap[workerDto.Login] = new DomainBoard.BoardWorker
            {
                Worker = configWorker,
                Assignments = new List<DomainBoard.BoardTaskAssignment>()
            };
        }

        // Создаём задачи
        var tasksMap = new Dictionary<string, DomainBoard.BoardTask>();
        foreach (var taskDto in dto.Tasks)
        {
            var configTask = config.Tasks.First(t => t.Key == taskDto.Key);
            var boardTask = new DomainBoard.BoardTask
            {
                Task = configTask,
                Progress = taskDto.Progress,
                TransitionHistory = new List<TaskTransitionHistory>(),
                CurrentStage = taskDto.CurrentStageName != null
                    ? stagesMap[taskDto.CurrentStageName]
                    : null,
                Worker = taskDto.WorkerLogin != null
                    ? workersMap[taskDto.WorkerLogin]
                    : null
            };
            tasksMap[taskDto.Key] = boardTask;
        }

        // Заполняем Tasks в стадиях
        foreach (var stageDto in dto.Stages)
        {
            var boardStage = stagesMap[stageDto.Name];
            boardStage.Tasks = stageDto.TaskKeys
                .Select(key => tasksMap[key])
                .ToList();
        }

        // Заполняем Assignments в воркерах
        foreach (var workerDto in dto.Workers)
        {
            var boardWorker = workersMap[workerDto.Login];
            boardWorker.Assignments = workerDto.AssignedTaskKeys
                .Select(key => new DomainBoard.BoardTaskAssignment
                {
                    Task = tasksMap[key],
                    Stage = tasksMap[key].CurrentStage
                })
                .ToList();
        }

        return new DomainBoard.Board
        {
            Stages = stagesMap.Values.ToList(),
            Workers = workersMap.Values.ToList(),
            Tasks = tasksMap.Values.ToList()
        };
    }

    #endregion

    #region History

    private static ApiHistoryDayDto ToApiDto(HistoryDay day)
    {
        return new ApiHistoryDayDto
        {
            DayNumber = day.DayNumber,
            Activities = day.Activities.Select(ToApiDto).ToList()
        };
    }

    private static ApiHistoryActivityDto ToApiDto(HistoryActivity activity)
    {
        return new ApiHistoryActivityDto
        {
            Type = activity.Type,
            Tick = activity.Tick,
            Description = activity.Description,
            TaskKey = activity.Task?.Task.Key,
            WorkerLogin = activity.Worker?.Worker.Login,
            StageName = activity.Stage?.Stage.Name,
            Progress = activity.Progress
        };
    }

    private static HistoryDay ToDomainHistoryDay(ApiHistoryDayDto dto)
    {
        return new HistoryDay
        {
            DayNumber = dto.DayNumber,
            Activities = dto.Activities.Select(ToDomainActivity).ToList()
        };
    }

    private static HistoryActivity ToDomainActivity(ApiHistoryActivityDto dto)
    {
        return new HistoryActivity
        {
            Type = dto.Type,
            Tick = dto.Tick,
            Description = dto.Description,
            Progress = dto.Progress
            // Task, Worker, Stage не восстанавливаются — это только для чтения
        };
    }

    #endregion

    #region Private Helpers - Config

    private static ApiStageDto ToApiDto(Stage stage)
    {
        return new ApiStageDto
        {
            Name = stage.Name,
            Type = stage.Type,
            IsStart = stage.IsStart,
            IsLeadTimeStart = stage.IsLeadTimeStart,
            WipLimit = stage.WipLimit,
            AllowedRoles = stage.AllowedRoles.ToList(),
            RequiresDifferentResource = stage.RequiresDifferentResource,
            RequiresDifferentResourceFromStage = stage.RequiresDifferentResourceFromStage,
            StageProgressPercent = stage.StageProgressPercent,
            TransitionStageNames = stage.Transitions.Select(t => t.Stage.Name).ToList()
        };
    }

    private static ApiWorkerDto ToApiDto(Worker worker)
    {
        return new ApiWorkerDto
        {
            Login = worker.Login,
            Role = worker.Role,
            WipLimit = worker.WipLimit,
            Performance = worker.Performance
        };
    }

    private static ApiTaskDto ToApiDto(DomainTask task)
    {
        return new ApiTaskDto
        {
            Key = task.Key,
            Summary = task.Summary,
            ShirtType = task.ShirtType,
            Role = task.Role
        };
    }

    private static Worker ToDomainWorker(ApiWorkerDto dto)
    {
        return new Worker
        {
            Login = dto.Login,
            Role = dto.Role ?? string.Empty,
            WipLimit = dto.WipLimit,
            Performance = dto.Performance
        };
    }

    private static DomainTask ToDomainTask(ApiTaskDto dto)
    {
        return new DomainTask
        {
            Key = dto.Key,
            Summary = dto.Summary,
            ShirtType = dto.ShirtType,
            Role = dto.Role
        };
    }

    #endregion
}
