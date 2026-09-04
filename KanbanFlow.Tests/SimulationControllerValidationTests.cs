using KanbanFlowApi.Controllers;
using KanbanFlowApi.Dtos.Config;
using KanbanFlowSerivce.Enums;
using Microsoft.AspNetCore.Mvc;

namespace KanbanFlow.Tests;

/// <summary>
///     Регрессия: конфигурация с дублирующимися логинами воркеров раньше проходила
///     /start без единой ошибки (200 OK), но валила первый же /simulate-day
///     необработанным исключением — ApiMapper.ToDomainBoard ищет воркера по логину
///     через .Single(), а с дублем в config.Workers таких совпадений два.
///     Клиент получал вместо JSON голый stack trace (500) и показывал в тосте
///     нечитаемую ошибку парсинга. StartSimulation теперь отклоняет такую
///     конфигурацию явным 400 ещё до того, как она попадёт в движок.
/// </summary>
public class SimulationControllerValidationTests
{
    [Fact]
    public void StartSimulation_WithDuplicateWorkerLogins_ReturnsBadRequest()
    {
        var controller = new SimulationController();
        var request = BuildRequestWithDuplicateWorkerLogins();

        var result = controller.StartSimulation(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var error = Assert.IsType<string>(GetErrorMessage(badRequest.Value));
        Assert.Contains("dev1", error);
        Assert.Contains("уникальны", error);
    }

    [Fact]
    public void StartSimulation_WithUniqueWorkerLogins_Succeeds()
    {
        var controller = new SimulationController();
        var request = BuildRequestWithDuplicateWorkerLogins();
        request.Workers[1].Login = "dev2";

        var result = controller.StartSimulation(request);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    private static string? GetErrorMessage(object? badRequestValue)
    {
        var property = badRequestValue?.GetType().GetProperty("error");
        return property?.GetValue(badRequestValue) as string;
    }

    private static StartSimulationRequestDto BuildRequestWithDuplicateWorkerLogins()
    {
        return new StartSimulationRequestDto
        {
            Seed = 1,
            UseVariability = false,
            Workflow = new ApiWorkflowDto
            {
                Stages =
                [
                    new ApiStageDto
                    {
                        Name = "Todo",
                        Type = StageType.Buffer,
                        Transitions = [new ApiStageTransitionDto { TargetStageName = "Dev", Probability = 1 }]
                    },
                    new ApiStageDto
                    {
                        Name = "Dev",
                        Type = StageType.Work,
                        IsLeadTimeStart = true,
                        StageProgressPercent = 100,
                        RequiredSkills = ["backend"],
                        CreatesValue = true
                    }
                ]
            },
            Workers =
            [
                new ApiWorkerDto { Login = "dev1", Skills = ["backend"], WipLimit = 1, Performance = 100 },
                new ApiWorkerDto { Login = "dev1", Skills = ["backend"], WipLimit = 1, Performance = 100 }
            ],
            Tasks = [new ApiTaskDto { Key = "TASK-1", ShirtType = TShirtType.S, RequiredSkills = ["backend"] }],
            DaysToSimulate = null
        };
    }
}
