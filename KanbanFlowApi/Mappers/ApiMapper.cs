using KanbanFlowApi.Dtos;
using KanbanFlowApi.Dtos.Board;
using KanbanFlowApi.Dtos.Config;
using KanbanFlowApi.Dtos.History;
using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.Board;
using KanbanFlowSerivce.Dtos.Config;
using KanbanFlowSerivce.Dtos.History;
using DomainTask = KanbanFlowSerivce.Dtos.Config.Task;

namespace KanbanFlowApi.Mappers;

/// <summary>
///     Маппер для конвертации между доменными моделями и API DTO
/// </summary>
public static class ApiMapper
{
    #region Config

    /// <summary>
    ///     Конвертирует доменную конфигурацию в API DTO (без циклических ссылок)
    /// </summary>
    public static ApiSimulationConfigDto ToApiDto(SimulationConfig config)
    {
        return new ApiSimulationConfigDto
        {
            Seed = config.Seed,
            UseVariability = config.UseVariability,
            Workers = config.Workers.Select(ToApiDto).ToList(),
            Workflow = new ApiWorkflowDto
            {
                Stages = config.Workflow.Stages.Select(ToApiDto).ToList()
            },
            Tasks = config.Tasks.Select(ToApiDto).ToList()
        };
    }

    /// <summary>
    ///     Конвертирует API DTO в доменную конфигурацию
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
                IsLeadTimeStart = stageDto.IsLeadTimeStart,
                WipLimit = stageDto.WipLimit,
                RequiredSkills = stageDto.RequiredSkills,
                RequiresDifferentResource = stageDto.RequiresDifferentResource,
                RequiresDifferentResourceFromStage = stageDto.RequiresDifferentResourceFromStage,
                StageProgressPercent = stageDto.StageProgressPercent,
                Transitions = []
            };
        }

        // Затем устанавливаем переходы с вероятностями
        foreach (var stageDto in dto.Workflow.Stages)
        {
            var stage = stagesMap[stageDto.Name];
            foreach (var transition in stageDto.Transitions)
            {
                if (stagesMap.TryGetValue(transition.TargetStageName, out var targetStage))
                {
                    stage.Transitions.Add(new StageTransition
                    {
                        Stage = targetStage,
                        Probability = transition.Probability
                    });
                }
            }
        }

        return new SimulationConfig
        {
            Seed = dto.Seed,
            UseVariability = dto.UseVariability,
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
    ///     Конвертирует доменную симуляцию в API DTO (полное состояние)
    /// </summary>
    public static ApiSimulationStateDto ToApiDto(Simulation simulation)
    {
        var configDto = ToApiDto(simulation.Config);
        
        return new ApiSimulationStateDto
        {
            Config = configDto,
            Board = ToApiDto(simulation.Board),
            History = simulation.History.Select(ToApiDto).ToList(),
            CurrentDay = simulation.CurrentDay,
            CurrentTick = simulation.CurrentTick
        };
    }

    /// <summary>
    ///     Конвертирует API DTO состояния в доменную симуляцию
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

        // Восстанавливаем состояние (день и тик)
        simulation.RestoreState(dto.CurrentDay, dto.CurrentTick);
        
        return simulation;
    }

    #endregion

    #region Board

    private static ApiBoardDto ToApiDto(Board board)
    {
        return new ApiBoardDto
        {
            Stages = board.Stages.Select(ToApiDto).ToList(),
            Workers = board.Workers.Select(ToApiDto).ToList(),
            Tasks = board.Tasks.Select(ToApiDto).ToList()
        };
    }

    private static ApiBoardStageDto ToApiDto(BoardStage stage)
    {
        return new ApiBoardStageDto
        {
            Name = stage.Stage.Name,
            Type = stage.Stage.Type,
            IsLeadTimeStart = stage.Stage.IsLeadTimeStart,
            WipLimit = stage.WipLimit,
            WipCount = stage.WipCount,
            CanAcceptTasks = stage.CanAcceptTasks,
            TaskKeys = stage.Tasks.Select(t => t.Task.Key).ToList(),
            NextStageNames = stage.NextStages.Select(s => s.Stage.Name).ToList()
        };
    }

    private static ApiBoardWorkerDto ToApiDto(BoardWorker worker)
    {
        return new ApiBoardWorkerDto
        {
            Login = worker.Worker.Login,
            Skills = worker.Worker.Skills,
            WipLimit = worker.WipLimit,
            WipCount = worker.WipCount,
            IsAvailable = worker.IsAvailable,
            AssignedTaskKeys = worker.Assignments.Select(a => a.Task.Task.Key).ToList()
        };
    }

    private static ApiBoardTaskDto ToApiDto(BoardTask task)
    {
        return new ApiBoardTaskDto
        {
            Key = task.Task.Key,
            Summary = task.Task.Summary,
            ShirtType = task.Task.ShirtType,
            RequiredSkills = task.Task.RequiredSkills,
            Progress = task.Progress,
            WorkerLogin = task.Worker?.Worker.Login,
            CurrentStageName = task.CurrentStage?.Stage.Name
        };
    }

    private static Board ToDomainBoard(ApiBoardDto dto, SimulationConfig config)
    {
        // Создаём маппинг стадий по имени
        var stagesMap = new Dictionary<string, BoardStage>();
        foreach (var stageDto in dto.Stages)
        {
            var configStage = config.Workflow.Stages.Single(s => s.Name == stageDto.Name);
            stagesMap[stageDto.Name] = new BoardStage
            {
                Stage = configStage,
                Tasks = [],
                NextStages = [],
                PrevStages = []
            };
        }

        // Устанавливаем связи между стадиями
        foreach (var stageDto in dto.Stages)
        {
            var thisStage = stagesMap[stageDto.Name];

            foreach (var nextName in stageDto.NextStageNames)
            {
                if (!stagesMap.TryGetValue(nextName, out var nextStage))
                {
                    continue;
                }

                thisStage.NextStages.Add(nextStage);
                // Устанавливаем обратную связь: prevStage для nextStage
                nextStage.PrevStages.Add(thisStage);
            }
        }

        // Создаём воркеров
        var workersMap = new Dictionary<string, BoardWorker>();
        foreach (var workerDto in dto.Workers)
        {
            var configWorker = config.Workers.Single(w => w.Login == workerDto.Login);
            workersMap[workerDto.Login] = new BoardWorker
            {
                Worker = configWorker,
                Assignments = []
            };
        }

        // Создаём задачи
        var tasksMap = new Dictionary<string, BoardTask>();
        foreach (var taskDto in dto.Tasks)
        {
            var configTask = config.Tasks.Single(t => t.Key == taskDto.Key);
            var boardTask = new BoardTask
            {
                Task = configTask,
                Progress = taskDto.Progress,
                TransitionHistory = [],
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
                .Select(key =>
                {
                    var task = tasksMap[key];
                    return new BoardTaskAssignment
                    {
                        Task = task,
                        Stage = task.CurrentStage ?? workersMap[workerDto.Login].Assignments
                                .FirstOrDefault()?.Stage
                            ?? stagesMap.Values.Single(s => s.PrevStages.Count == 0)
                    };
                })
                .ToList();
        }

        return new Board
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
            IsLeadTimeStart = stage.IsLeadTimeStart,
            WipLimit = stage.WipLimit,
            RequiredSkills = stage.RequiredSkills,
            RequiresDifferentResource = stage.RequiresDifferentResource,
            RequiresDifferentResourceFromStage = stage.RequiresDifferentResourceFromStage,
            StageProgressPercent = stage.StageProgressPercent,
            Transitions = stage.Transitions.Select(t => new ApiStageTransitionDto
            {
                TargetStageName = t.Stage.Name,
                Probability = t.Probability
            }).ToList()
        };
    }

    private static ApiWorkerDto ToApiDto(Worker worker)
    {
        return new ApiWorkerDto
        {
            Login = worker.Login,
            Skills = worker.Skills,
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
            RequiredSkills = task.RequiredSkills,
            Children = task.Children?.Select(ToApiDto).ToList(),
            AcceptableWorkers = task.AcceptableWorkers
        };
    }

    private static Worker ToDomainWorker(ApiWorkerDto dto)
    {
        return new Worker
        {
            Login = dto.Login,
            Skills = dto.Skills,
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
            RequiredSkills = dto.RequiredSkills,
            Children = dto.Children?.Select(ToDomainTask).ToList(),
            AcceptableWorkers = dto.AcceptableWorkers
        };
    }

    #endregion
}