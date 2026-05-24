using KanbanFlowApi.Dtos;
using KanbanFlowConsole.Dtos.Config;
using BoardTask = KanbanFlowConsole.Dtos.Config.Task;

namespace KanbanFlowApi.Mappers;

/// <summary>
/// Маппер для конвертации между доменными моделями и API DTO
/// </summary>
public static class ApiMapper
{
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

    private static ApiTaskDto ToApiDto(BoardTask task)
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

    private static BoardTask ToDomainTask(ApiTaskDto dto)
    {
        return new BoardTask
        {
            Key = dto.Key,
            Summary = dto.Summary,
            ShirtType = dto.ShirtType,
            Role = dto.Role
        };
    }
}
