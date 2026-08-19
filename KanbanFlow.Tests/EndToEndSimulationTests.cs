using KanbanFlowApi.Controllers;
using KanbanFlowApi.Dtos;
using KanbanFlowApi.Dtos.Config;
using KanbanFlowApi.Factories;
using KanbanFlowApi.Mappers;
using KanbanFlowApi.Services;
using KanbanFlowSerivce.Dtos.History;
using KanbanFlowSerivce.Enums;
using Microsoft.AspNetCore.Mvc;

namespace KanbanFlow.Tests;

/// <summary>
///     Сквозной (end-to-end) тест: гоняет симуляцию через тот же путь, что реальный клиент —
///     StartSimulation → цикл SimulateDay (день за днём, с полным DTO↔domain roundtrip через
///     ApiMapper на каждом дне, как это реально происходит между HTTP-вызовами) — и проверяет
///     не только что задачи дошли до Done, но и что метрики на выходе разумны.
///
///     В отличие от SimulationSmokeTests, который сам имитирует прогресс задач
///     (Progress += 25 в день) в обход WorkProgressService, здесь используется ровно тот код,
///     который вызывает SimulationController: ValidateCanContinue → StartNewDay →
///     ProcessMovements → SimulateWorkDay → ProcessMovements(completed).
/// </summary>
public class EndToEndSimulationTests
{
    private const int MaxDays = 250;

    [Fact]
    public void FullDayByDayLifecycle_AllTasksReachDone_WorkersAreFreed()
    {
        var controller = new SimulationController();
        var request = BuildRequest();

        var state = ExtractOk(controller.StartSimulation(request));
        Assert.Equal(0, state.CurrentDay);
        Assert.Equal(request.Tasks.Count, state.Board.Tasks.Count);

        state = RunUntilDone(controller, state, out var daysUsed);

        Assert.True(daysUsed < MaxDays,
            $"Симуляция не завершилась за {MaxDays} дней — похоже на зависание/дедлок задач.");

        // Все задачи должны быть в Done
        var doneStage = state.Board.Stages.Single(s => s.Name == "Done");
        Assert.Equal(state.Board.Tasks.Count, doneStage.TaskKeys.Count);
        Assert.All(state.Board.Tasks, t => Assert.Equal("Done", t.CurrentStageName));

        // Регрессия для бага "воркер не освобождался после завершения задачи":
        // после того как все задачи дошли до Done, все воркеры должны быть свободны.
        Assert.All(state.Board.Workers, w =>
        {
            Assert.Equal(0, w.WipCount);
            Assert.True(w.IsAvailable, $"Воркер {w.Login} не освободился после завершения всех задач");
        });

        // История непустая, и каждая задача реально попала в Done через TaskMoved-событие
        // (а не просто оказалась там при инициализации) — проверяет, что перемещения
        // действительно писались в историю на каждом из дней, а не потерялись при DTO-roundtrip.
        Assert.NotEmpty(state.History);
        var tasksMovedToDone = state.History
            .SelectMany(d => d.Activities)
            .Where(a => a.Type == ActivityType.TaskMoved && a.StageName == "Done")
            .Select(a => a.TaskKey)
            .Distinct()
            .ToList();
        Assert.Equal(state.Board.Tasks.Count, tasksMovedToDone.Count);
    }

    [Fact]
    public void FullDayByDayLifecycle_MetricsAreCalculatedAndConsistent()
    {
        var controller = new SimulationController();
        var request = BuildRequest();

        var state = ExtractOk(controller.StartSimulation(request));
        state = RunUntilDone(controller, state, out var daysUsed);
        Assert.True(daysUsed < MaxDays, $"Симуляция не завершилась за {MaxDays} дней.");

        // Метрики считаем так же, как /api/simulation/all-metrics: восстанавливаем доменную
        // симуляцию из финального DTO (полный roundtrip config+board+history через ApiMapper).
        var simulation = ApiMapper.ToDomainSimulation(state);

        var metrics = new MetricsService(simulation).CalculateAllMetrics();
        Assert.Equal(state.Board.Tasks.Count, metrics.LeadTime.TaskCount);
        Assert.True(metrics.LeadTime.P50 > 0, "P50 Lead Time должен быть положительным для завершённой симуляции");
        Assert.True(metrics.LeadTime.P85 >= metrics.LeadTime.P50, "P85 не может быть меньше P50");
        Assert.True(metrics.Throughput.Overall > 0);
        Assert.InRange(metrics.FlowEfficiency.EfficiencyPercent, 0m, 100m);
        Assert.True(metrics.TotalCost > 0);
        Assert.Equal(metrics.TotalCost, metrics.WorkCost + metrics.BufferCost, precision: 2);

        var workerMetrics = new WorkerMetricsService(simulation).CalculateAllWorkersMetrics();
        Assert.Equal(request.Workers.Count, workerMetrics.Count);
        Assert.All(workerMetrics, wm => Assert.True(wm.TotalCost > 0, $"У воркера {wm.Login} нулевая стоимость"));
        // Сумма стоимости по воркерам должна сходиться с общей стоимостью проекта из MetricsService —
        // это две независимые реализации расчёта стоимости, они не должны расходиться.
        Assert.Equal(metrics.TotalCost, workerMetrics.Sum(w => w.TotalCost), precision: 2);

        var taskMetricsService = new TaskMetricsService(simulation);
        var taskMetrics = taskMetricsService.CalculateAllTasksMetrics();
        Assert.Equal(state.Board.Tasks.Count, taskMetrics.Count);
        Assert.All(taskMetrics, tm => Assert.Equal("Done", tm.Status));
        Assert.All(taskMetrics, tm => Assert.True(tm.LeadTimeDays > 0, $"Задача {tm.TaskKey} с нулевым Lead Time"));

        var stageMetrics = taskMetricsService.CalculateStageMetricsAggregated();
        Assert.NotEmpty(stageMetrics);
        Assert.All(stageMetrics, sm => Assert.True(sm.TaskCount > 0));
    }

    /// <summary>
    ///     "Рассчитать до конца" (StartSimulation с DaysToSimulate=0, весь цикл дней выполняется
    ///     сервером за один HTTP-вызов) и день-за-днём через SimulateDay — это два независимых
    ///     цикла в контроллере. При одинаковом seed и без вариативности они обязаны сойтись
    ///     к одному и тому же финальному состоянию — если поведение разошлось, это регрессия
    ///     в одном из двух путей.
    /// </summary>
    [Fact]
    public void RunToCompletion_MatchesDayByDaySimulation()
    {
        var controllerA = new SimulationController();
        var bulkRequest = BuildRequest() with { DaysToSimulate = 0 };
        var bulkState = ExtractOk(controllerA.StartSimulation(bulkRequest));

        var controllerB = new SimulationController();
        var dayByDayState = ExtractOk(controllerB.StartSimulation(BuildRequest()));
        dayByDayState = RunUntilDone(controllerB, dayByDayState, out _);

        Assert.Equal(dayByDayState.CurrentDay, bulkState.CurrentDay);

        var bulkDoneKeys = bulkState.Board.Stages.Single(s => s.Name == "Done").TaskKeys.Order().ToList();
        var dayByDayDoneKeys = dayByDayState.Board.Stages.Single(s => s.Name == "Done").TaskKeys.Order().ToList();
        Assert.Equal(dayByDayDoneKeys, bulkDoneKeys);
    }

    /// <summary>
    ///     Гоняет SimulateDay в цикле — ровно так, как это делает клиент, нажимая "Следующий день"
    ///     (или авто-режим) — до тех пор, пока все задачи не окажутся в Done либо контроллер
    ///     не откажется продолжать (BadRequest от ValidateCanContinue).
    /// </summary>
    private static ApiSimulationStateDto RunUntilDone(
        SimulationController controller,
        ApiSimulationStateDto state,
        out int daysUsed)
    {
        var day = 0;
        for (; day < MaxDays; day++)
        {
            var doneStage = state.Board.Stages.Single(s => s.Name == "Done");
            if (doneStage.TaskKeys.Count == state.Board.Tasks.Count)
            {
                break;
            }

            var dayResult = controller.SimulateDay(state);
            if (dayResult.Result is BadRequestObjectResult badRequest)
            {
                Assert.Fail(
                    $"Симуляция остановилась на дне {state.CurrentDay}, не все задачи дошли до Done: " +
                    $"{badRequest.Value}");
            }

            state = ExtractOk(dayResult);
        }

        daysUsed = day;
        return state;
    }

    private static T ExtractOk<T>(ActionResult<T> result)
    {
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<T>(okResult.Value);
    }

    /// <summary>
    ///     Реальные пресеты процесса ("Kanban Software Dev") и команды ("Маленькая команда"),
    ///     как они грузятся в UI-редакторе конфигурации, плюс сгенерированный пакет задач —
    ///     аналог того, что делает "Генератор задач" в config-editor.js.
    /// </summary>
    private static StartSimulationRequestDto BuildRequest()
    {
        var processPreset = ProcessPresetsFactory.GetPresetByName("kanban-software")
            ?? throw new InvalidOperationException("Пресет процесса 'kanban-software' не найден");
        var workerPreset = WorkerPoolPresetsFactory.GetPresetByName("small-team")
            ?? throw new InvalidOperationException("Пресет команды 'small-team' не найден");

        var sizes = new[] { TShirtType.XS, TShirtType.S, TShirtType.M, TShirtType.L };
        var tasks = Enumerable.Range(1, 12)
            .Select(i => new ApiTaskDto
            {
                Key = $"TASK-{i}",
                Summary = $"Сквозная задача #{i}",
                ShirtType = sizes[(i - 1) % sizes.Length],
                // Пересекается со всеми рабочими стадиями пресета (Developing, Testing,
                // Release Preparation), чтобы вся команда реально работала над задачами,
                // а не пропускала стадии транзитом из-за отсутствия общих навыков.
                RequiredSkills = ["backend", "frontend", "qa"]
            })
            .ToList();

        return new StartSimulationRequestDto
        {
            Seed = 42,
            UseVariability = false, // детерминированный прогон, без разброса по дням
            Workflow = processPreset.Workflow,
            Workers = workerPreset.Workers,
            Tasks = tasks,
            DaysToSimulate = null
        };
    }
}
