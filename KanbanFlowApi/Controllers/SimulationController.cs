using KanbanFlowApi.Dtos;
using KanbanFlowApi.Mappers;
using KanbanFlowConsole.Dtos;
using KanbanFlowConsole.Factories;
using KanbanFlowConsole.Services;
using Microsoft.AspNetCore.Mvc;

namespace KanbanFlowApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SimulationController : ControllerBase
{
    /// <summary>
    /// Получить конфигурацию симуляции по умолчанию
    /// </summary>
    [HttpGet("default-config")]
    public ActionResult<ApiSimulationConfigDto> GetDefaultConfig()
    {
        var config = SimulationFactory.CreateDefaultConfig();
        return Ok(ApiMapper.ToApiDto(config));
    }

    /// <summary>
    /// Выполнить расчёт одного дня симуляции
    /// </summary>
    [HttpPost("simulate-day")]
    public ActionResult<SimulateDayResponse> SimulateDay([FromBody] ApiSimulationConfigDto config)
    {
        // Конвертируем API DTO в доменную модель
        var domainConfig = ApiMapper.ToDomainConfig(config);
        
        // Создаём объект симуляции
        var simulation = new Simulation();
        simulation.InitFromConfig(domainConfig);

        // Начинаем новый день
        simulation.StartNewDay();

        // Обрабатываем перемещения задач
        var movementService = new TaskMovementService(simulation);
        movementService.ProcessMovements();

        // Симулируем выполнение работы
        var workProgressService = new WorkProgressService(simulation);
        workProgressService.SimulateWorkDay();

        // Увеличиваем тик на 24 часа
        simulation.AdvanceTick(24);

        // Возвращаем обновлённое состояние
        return Ok(new SimulateDayResponse
        {
            Config = ApiMapper.ToApiDto(simulation.Config),
            CurrentDay = simulation.CurrentDay,
            CurrentTick = simulation.CurrentTick
        });
    }
}
