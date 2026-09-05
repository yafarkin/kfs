using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using KanbanFlowApi.Dtos;
using KanbanFlowApi.Dtos.Config;
using KanbanFlowApi.Dtos.Metrics;
using KanbanFlowSerivce.Enums;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KanbanFlow.Tests;

/// <summary>
///     Тесты на уровне настоящего HTTP: поднимают приложение через WebApplicationFactory и
///     бьют по `/api/...` реальными запросами, с настоящим JSON-биндингом ASP.NET в обе стороны.
///
///     В отличие от <see cref="EndToEndSimulationTests"/> (вызывает контроллер как C#-класс
///     в процессе), здесь проверяется именно граница запрос↔DTO: имена полей (camelCase),
///     сериализация enum строкой, плоский формат унаследованного
///     <see cref="StartSimulationRequestDto"/>, коды ответов (400 с JSON вместо 500 со stack
///     trace). Ровно тот класс багов, что в этом проекте ловился только руками в браузере.
/// </summary>
public class HttpEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    // Совпадает с конфигурацией сериализации сервера (Program.cs): camelCase + enum строкой.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public HttpEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    // ---------------------------------------------------------------------
    // Полный жизненный цикл через HTTP
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Start_Then_SimulateDay_Then_AllMetrics_OverHttp()
    {
        var client = _factory.CreateClient();

        // /start
        var startResp = await client.PostAsJsonAsync("/api/simulation/start", BuildValidRequest(), Json);
        Assert.Equal(HttpStatusCode.OK, startResp.StatusCode);

        var state = await startResp.Content.ReadFromJsonAsync<ApiSimulationStateDto>(Json);
        Assert.NotNull(state);
        Assert.Equal(0, state!.CurrentDay);
        Assert.Equal(2, state.Board.Tasks.Count);
        Assert.Equal(2, state.Board.Workers.Count);

        // /simulate-day принимает то, что вернул /start
        var dayResp = await client.PostAsJsonAsync("/api/simulation/simulate-day", state, Json);
        Assert.Equal(HttpStatusCode.OK, dayResp.StatusCode);

        var afterDay = await dayResp.Content.ReadFromJsonAsync<ApiSimulationStateDto>(Json);
        Assert.NotNull(afterDay);
        Assert.Equal(1, afterDay!.CurrentDay);

        // /all-metrics
        var metricsResp = await client.PostAsJsonAsync("/api/simulation/all-metrics", afterDay, Json);
        Assert.Equal(HttpStatusCode.OK, metricsResp.StatusCode);

        var metrics = await metricsResp.Content.ReadFromJsonAsync<AllMetricsDto>(Json);
        Assert.NotNull(metrics);
        Assert.NotNull(metrics!.SimulationMetrics);
        Assert.NotNull(metrics.WorkerMetrics);
    }

    [Fact]
    public async Task SimulateToEnd_ViaDaysToSimulate_OverHttp_AllTasksDone()
    {
        var client = _factory.CreateClient();

        var request = BuildValidRequest();
        request.DaysToSimulate = 0; // 0 = до завершения всех задач

        var resp = await client.PostAsJsonAsync("/api/simulation/start", request, Json);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var state = await resp.Content.ReadFromJsonAsync<ApiSimulationStateDto>(Json);
        Assert.NotNull(state);
        Assert.True(state!.CurrentDay > 0, "Симуляция с DaysToSimulate=0 должна была прогнать хотя бы день");

        var done = state.Board.Stages.Single(s => s.Name == "Done");
        Assert.Equal(state.Board.Tasks.Count, done.TaskKeys.Count);
    }

    // ---------------------------------------------------------------------
    // Граница сериализации
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Start_Response_SerializesEnumsAsStrings_NotNumbers()
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/simulation/start", BuildValidRequest(), Json);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var raw = await resp.Content.ReadAsStringAsync();

        // StageType в доске должен быть строкой ("Work"/"Buffer"), а не числом ("type":0)
        Assert.Contains("\"type\":\"", raw);
        Assert.DoesNotContain("\"type\":0", raw);
        Assert.DoesNotContain("\"type\":1", raw);
    }

    [Fact]
    public async Task Start_AcceptsHandwrittenCamelCaseJson_LikeFrontendSends()
    {
        var client = _factory.CreateClient();

        // Литеральный JSON в том виде, в котором его шлёт фронт (app.js): camelCase,
        // enum строкой, унаследованный daysToSimulate в том же плоском объекте.
        // Не сериализуется из C#-объекта — чтобы поймать рассинхрон имён, который
        // C#-сериализация с обеих сторон замаскировала бы.
        const string body = """
        {
          "seed": 7,
          "useVariability": false,
          "daysToSimulate": null,
          "workflow": {
            "stages": [
              { "name": "Todo", "type": "Buffer", "isLeadTimeStart": true,
                "transitions": [ { "targetStageName": "Dev", "probability": 1.0 } ] },
              { "name": "Dev", "type": "Work", "stageProgressPercent": 100,
                "requiredSkills": ["backend"], "createsValue": true,
                "transitions": [ { "targetStageName": "Done", "probability": 1.0 } ] },
              { "name": "Done", "type": "Buffer", "transitions": [] }
            ]
          },
          "workers": [
            { "login": "dev1", "skills": ["backend"], "wipLimit": 1, "performance": 100,
              "costPerDay": 100 }
          ],
          "tasks": [
            { "key": "TASK-1", "summary": "x", "shirtType": "S", "requiredSkills": ["backend"] }
          ]
        }
        """;

        var resp = await client.PostAsync(
            "/api/simulation/start",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var state = await resp.Content.ReadFromJsonAsync<ApiSimulationStateDto>(Json);
        Assert.NotNull(state);
        Assert.Single(state!.Board.Tasks);
        Assert.Equal("TASK-1", state.Board.Tasks[0].Key);
        // shirtType долетел как S, а не молча превратился в XS (дефолт enum)
        Assert.Equal(TShirtType.S, state.Board.Tasks[0].ShirtType);
    }

    // ---------------------------------------------------------------------
    // Ошибки: 400 c JSON, а не 500 со stack trace
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Start_WithDuplicateWorkerLogins_Returns400_WithJsonError()
    {
        var client = _factory.CreateClient();

        var request = BuildValidRequest();
        request.Workers[1].Login = request.Workers[0].Login;

        var resp = await client.PostAsJsonAsync("/api/simulation/start", request, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("application/json", resp.Content.Headers.ContentType?.MediaType ?? "");

        var problem = await resp.Content.ReadFromJsonAsync<ErrorResponse>(Json);
        Assert.NotNull(problem);
        Assert.Contains("уникальны", problem!.Error);
    }

    [Fact]
    public async Task Start_WithEmptyJsonObject_Returns400_NotServerError()
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsync(
            "/api/simulation/start",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.True((int)resp.StatusCode < 500);
    }

    [Fact]
    public async Task Start_WithMalformedJson_Returns400()
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsync(
            "/api/simulation/start",
            new StringContent("{ not valid json", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ---------------------------------------------------------------------
    // GET-эндпоинты редактора
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GetProcessPresets_Returns200_NonEmpty_WithWorkflow()
    {
        var client = _factory.CreateClient();

        var presets = await client.GetFromJsonAsync<List<ProcessPresetDto>>(
            "/api/editor/processes/presets", Json);

        Assert.NotNull(presets);
        Assert.NotEmpty(presets!);
        Assert.All(presets!, p => Assert.NotNull(p.Workflow));
    }

    [Fact]
    public async Task GetWorkerGradePresets_Returns200_NonEmpty()
    {
        var client = _factory.CreateClient();

        var presets = await client.GetFromJsonAsync<List<WorkerGradePresetDto>>(
            "/api/editor/workers/grade-presets", Json);

        Assert.NotNull(presets);
        Assert.NotEmpty(presets!);
    }

    [Fact]
    public async Task GetProcessPreset_UnknownName_Returns404()
    {
        var client = _factory.CreateClient();

        var resp = await client.GetAsync("/api/editor/processes/presets/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ---------------------------------------------------------------------
    // POST /api/editor/tasks/validate
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ValidateTasks_HappyPath_Returns200()
    {
        var client = _factory.CreateClient();

        ApiTaskDto[] tasks =
        [
            new() { Key = "T-1", Summary = "a", ShirtType = TShirtType.S, RequiredSkills = ["backend"] },
            new() { Key = "T-2", Summary = "b", ShirtType = TShirtType.M, RequiredSkills = ["qa"] }
        ];

        var resp = await client.PostAsJsonAsync("/api/editor/tasks/validate", tasks, Json);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task ValidateTasks_DuplicateKeys_Returns400()
    {
        var client = _factory.CreateClient();

        ApiTaskDto[] tasks =
        [
            new() { Key = "DUP", Summary = "a", ShirtType = TShirtType.S, RequiredSkills = ["backend"] },
            new() { Key = "DUP", Summary = "b", ShirtType = TShirtType.M, RequiredSkills = ["qa"] }
        ];

        var resp = await client.PostAsJsonAsync("/api/editor/tasks/validate", tasks, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ---------------------------------------------------------------------
    // Хелперы
    // ---------------------------------------------------------------------

    private sealed record ErrorResponse
    {
        public string Error { get; init; } = "";
    }

    /// <summary>
    ///     Минимальная валидная конфигурация: Todo(buffer) → Dev(work/backend) → Done(buffer),
    ///     плюс параллельная QA-ветка не нужна — держим маленькой, чтобы прогон был быстрым.
    /// </summary>
    private static StartSimulationRequestDto BuildValidRequest()
    {
        return new StartSimulationRequestDto
        {
            Seed = 42,
            UseVariability = false,
            DaysToSimulate = null,
            Workflow = new ApiWorkflowDto
            {
                Stages =
                [
                    new ApiStageDto
                    {
                        Name = "Todo",
                        Type = StageType.Buffer,
                        IsLeadTimeStart = true,
                        Transitions = [new ApiStageTransitionDto { TargetStageName = "Dev", Probability = 1 }]
                    },
                    new ApiStageDto
                    {
                        Name = "Dev",
                        Type = StageType.Work,
                        StageProgressPercent = 100,
                        RequiredSkills = ["backend"],
                        CreatesValue = true,
                        Transitions = [new ApiStageTransitionDto { TargetStageName = "Done", Probability = 1 }]
                    },
                    new ApiStageDto
                    {
                        Name = "Done",
                        Type = StageType.Buffer,
                        Transitions = []
                    }
                ]
            },
            Workers =
            [
                new ApiWorkerDto { Login = "dev1", Skills = ["backend"], WipLimit = 1, Performance = 100, CostPerDay = 100 },
                new ApiWorkerDto { Login = "dev2", Skills = ["backend"], WipLimit = 1, Performance = 100, CostPerDay = 100 }
            ],
            Tasks =
            [
                new ApiTaskDto { Key = "TASK-1", Summary = "one", ShirtType = TShirtType.S, RequiredSkills = ["backend"] },
                new ApiTaskDto { Key = "TASK-2", Summary = "two", ShirtType = TShirtType.S, RequiredSkills = ["backend"] }
            ]
        };
    }
}
