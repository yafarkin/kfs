using KanbanFlowApi.Dtos.Config;
using KanbanFlowApi.Factories;
using KanbanFlowApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace KanbanFlowApi.Controllers;

/// <summary>
/// Контроллер для редактирования производственных процессов.
/// Предоставляет CRUD операции для создания и редактирования конфигураций процессов.
/// Backend stateless — сохранение происходит в LocalStorage браузера.
/// </summary>
[ApiController]
[Route("api/editor/processes")]
public class ProcessEditorController : ControllerBase
{
    /// <summary>
    /// Получить список системных пресетов процессов (read-only).
    /// </summary>
    [HttpGet("presets")]
    public ActionResult<List<ProcessPresetDto>> GetPresets()
    {
        var presets = ProcessPresetsFactory.GetAllPresets();
        return Ok(presets);
    }

    /// <summary>
    /// Получить конкретный системный пресет по имени для редактирования.
    /// </summary>
    [HttpGet("presets/{presetName}")]
    public ActionResult<ProcessPresetDto> GetPreset(string presetName)
    {
        var preset = ProcessPresetsFactory.GetPresetByName(presetName);
        if (preset == null)
        {
            return NotFound($"Пресет '{presetName}' не найден");
        }

        return Ok(preset);
    }

    /// <summary>
    /// Валидировать и сохранить пользовательский пресет процесса.
    /// Backend выполняет валидацию, сохранение происходит в LocalStorage браузера.
    /// </summary>
    [HttpPost("presets")]
    public ActionResult<ProcessPresetDto> SavePreset([FromBody] ProcessPresetDto preset)
    {
        // Валидация имени
        if (string.IsNullOrWhiteSpace(preset.Name))
        {
            return BadRequest(new { error = "Имя пресета не может быть пустым" });
        }

        // Проверка: имя не должно совпадать с системными пресетами (чтобы не перезаписать)
        var systemPreset = ProcessPresetsFactory.GetPresetByName(preset.Name);
        if (systemPreset != null)
        {
            return BadRequest(new { error = $"Имя '{preset.Name}' зарезервировано для системного пресета" });
        }

        // Валидация workflow через сервис
        var validationService = new ProcessValidationService();
        var validationResult = validationService.Validate(preset);
        if (!validationResult.IsValid)
        {
            return BadRequest(new { error = validationResult.ErrorMessage });
        }

        // Возвращаем валидный пресет (клиент сохранит его в LocalStorage)
        return Ok(preset);
    }

    /// <summary>
    /// Удалить пользовательский пресет процесса.
    /// Backend только подтверждает удаление, реальное удаление происходит в LocalStorage браузера.
    /// </summary>
    [HttpDelete("presets/{presetName}")]
    public ActionResult DeletePreset(string presetName)
    {
        // Проверка: нельзя удалить системный пресет
        var systemPreset = ProcessPresetsFactory.GetPresetByName(presetName);
        if (systemPreset != null)
        {
            return BadRequest(new { error = $"Нельзя удалить системный пресет '{presetName}'" });
        }

        // Возвращаем подтверждение (клиент удалит из LocalStorage)
        return Ok(new { message = $"Пресет '{presetName}' готов к удалению" });
    }
}
