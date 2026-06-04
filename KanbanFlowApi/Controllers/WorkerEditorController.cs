using KanbanFlowApi.Dtos.Config;
using KanbanFlowApi.Factories;
using Microsoft.AspNetCore.Mvc;

namespace KanbanFlowApi.Controllers;

/// <summary>
/// Контроллер для редактирования пулов воркеров (команд).
/// Предоставляет CRUD операции для создания и редактирования конфигураций команд.
/// Backend stateless — сохранение происходит в LocalStorage браузера.
/// </summary>
[ApiController]
[Route("api/editor/workers")]
public class WorkerEditorController : ControllerBase
{
    /// <summary>
    /// Получить список системных пресетов воркеров (read-only).
    /// </summary>
    [HttpGet("presets")]
    public ActionResult<List<WorkerPoolPresetDto>> GetPresets()
    {
        var presets = WorkerPoolPresetsFactory.GetAllPresets();
        return Ok(presets);
    }

    /// <summary>
    /// Получить конкретный системный пресет по имени для редактирования.
    /// </summary>
    [HttpGet("presets/{presetName}")]
    public ActionResult<WorkerPoolPresetDto> GetPreset(string presetName)
    {
        var preset = WorkerPoolPresetsFactory.GetPresetByName(presetName);
        if (preset == null)
        {
            return NotFound($"Пресет '{presetName}' не найден");
        }

        return Ok(preset);
    }

    /// <summary>
    /// Валидировать и сохранить пользовательский пресет воркеров.
    /// Backend выполняет валидацию, сохранение происходит в LocalStorage браузера.
    /// </summary>
    [HttpPost("presets")]
    public ActionResult<WorkerPoolPresetDto> SavePreset([FromBody] WorkerPoolPresetDto preset)
    {
        // Валидация имени
        if (string.IsNullOrWhiteSpace(preset.Name))
        {
            return BadRequest(new { error = "Имя пресета не может быть пустым" });
        }

        // Проверка: имя не должно совпадать с системными пресетами (чтобы не перезаписать)
        var systemPreset = WorkerPoolPresetsFactory.GetPresetByName(preset.Name);
        if (systemPreset != null)
        {
            return BadRequest(new { error = $"Имя '{preset.Name}' зарезервировано для системного пресета" });
        }

        // Валидация работников
        if (preset.Workers == null || preset.Workers.Count == 0)
        {
            return BadRequest(new { error = "Пресет должен содержать хотя бы одного воркера" });
        }

        // Проверка уникальности логинов
        var duplicateLogins = preset.Workers
            .GroupBy(w => w.Login)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateLogins.Count > 0)
        {
            return BadRequest(new { error = $"Дублирующиеся логины воркеров: {string.Join(", ", duplicateLogins)}" });
        }

        // Валидация каждого воркера
        foreach (var worker in preset.Workers)
        {
            if (string.IsNullOrWhiteSpace(worker.Login))
            {
                return BadRequest(new { error = "Логин воркера не может быть пустым" });
            }

            if (worker.Skills == null || worker.Skills.Count == 0)
            {
                return BadRequest(new { error = $"Воркер '{worker.Login}' должен иметь хотя бы один навык" });
            }

            if (worker.WipLimit <= 0)
            {
                return BadRequest(new { error = $"WIP-лимит воркера '{worker.Login}' должен быть больше 0" });
            }

            if (worker.Performance <= 0)
            {
                return BadRequest(new { error = $"Производительность воркера '{worker.Login}' должна быть больше 0" });
            }

            if (worker.DeviationDownPercent < 0 || worker.DeviationDownPercent > 100)
            {
                return BadRequest(new { error = $"Отклонение вниз воркера '{worker.Login}' должно быть от 0 до 100%" });
            }

            if (worker.DeviationUpPercent < 0 || worker.DeviationUpPercent > 100)
            {
                return BadRequest(new { error = $"Отклонение вверх воркера '{worker.Login}' должно быть от 0 до 100%" });
            }
        }

        // Возвращаем валидный пресет (клиент сохранит его в LocalStorage)
        return Ok(preset);
    }

    /// <summary>
    /// Удалить пользовательский пресет воркеров.
    /// Backend только подтверждает удаление, реальное удаление происходит в LocalStorage браузера.
    /// </summary>
    [HttpDelete("presets/{presetName}")]
    public ActionResult DeletePreset(string presetName)
    {
        // Проверка: нельзя удалить системный пресет
        var systemPreset = WorkerPoolPresetsFactory.GetPresetByName(presetName);
        if (systemPreset != null)
        {
            return BadRequest(new { error = $"Нельзя удалить системный пресет '{presetName}'" });
        }

        // Возвращаем подтверждение (клиент удалит из LocalStorage)
        return Ok(new { message = $"Пресет '{presetName}' готов к удалению" });
    }
}
