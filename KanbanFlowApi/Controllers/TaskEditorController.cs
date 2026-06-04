using KanbanFlowApi.Dtos.Config;
using KanbanFlowApi.Factories;
using Microsoft.AspNetCore.Mvc;

namespace KanbanFlowApi.Controllers;

/// <summary>
/// Контроллер для редактирования наборов задач.
/// Предоставляет CRUD операции для создания и редактирования конфигураций задач.
/// Backend stateless — сохранение происходит в LocalStorage браузера.
/// </summary>
[ApiController]
[Route("api/editor/tasks")]
public class TaskEditorController : ControllerBase
{
    /// <summary>
    /// Получить список системных пресетов задач (read-only).
    /// </summary>
    [HttpGet("presets")]
    public ActionResult<List<TaskPresetDto>> GetPresets()
    {
        var presets = TaskPresetsFactory.GetAllPresets();
        return Ok(presets);
    }

    /// <summary>
    /// Получить конкретный системный пресет по имени для редактирования.
    /// </summary>
    [HttpGet("presets/{presetName}")]
    public ActionResult<TaskPresetDto> GetPreset(string presetName)
    {
        var preset = TaskPresetsFactory.GetPresetByName(presetName);
        if (preset == null)
        {
            return NotFound($"Пресет '{presetName}' не найден");
        }

        return Ok(preset);
    }

    /// <summary>
    /// Валидировать и сохранить пользовательский пресет задач.
    /// Backend выполняет валидацию, сохранение происходит в LocalStorage браузера.
    /// </summary>
    [HttpPost("presets")]
    public ActionResult<TaskPresetDto> SavePreset([FromBody] TaskPresetDto preset)
    {
        // Валидация имени
        if (string.IsNullOrWhiteSpace(preset.Name))
        {
            return BadRequest(new { error = "Имя пресета не может быть пустым" });
        }

        // Проверка: имя не должно совпадать с системными пресетами (чтобы не перезаписать)
        var systemPreset = TaskPresetsFactory.GetPresetByName(preset.Name);
        if (systemPreset != null)
        {
            return BadRequest(new { error = $"Имя '{preset.Name}' зарезервировано для системного пресета" });
        }

        // Валидация задач
        if (preset.Tasks == null || preset.Tasks.Count == 0)
        {
            return BadRequest(new { error = "Пресет должен содержать хотя бы одну задачу" });
        }

        var taskKeys = new HashSet<string>();
        foreach (var task in preset.Tasks)
        {
            if (string.IsNullOrWhiteSpace(task.Key))
            {
                return BadRequest(new { error = "Ключ задачи не может быть пустым" });
            }

            if (taskKeys.Contains(task.Key))
            {
                return BadRequest(new { error = $"Дублирующийся ключ задачи: '{task.Key}'" });
            }
            taskKeys.Add(task.Key);

            if (string.IsNullOrWhiteSpace(task.Summary))
            {
                return BadRequest(new { error = $"Описание задачи '{task.Key}' не может быть пустым" });
            }

            if (task.RequiredSkills == null || task.RequiredSkills.Count == 0)
            {
                return BadRequest(new { error = $"Задача '{task.Key}' должна иметь хотя бы один навык" });
            }
        }

        // Возвращаем валидный пресет (клиент сохранит его в LocalStorage)
        return Ok(preset);
    }

    /// <summary>
    /// Удалить пользовательский пресет задач.
    /// Backend только подтверждает удаление, реальное удаление происходит в LocalStorage браузера.
    /// </summary>
    [HttpDelete("presets/{presetName}")]
    public ActionResult DeletePreset(string presetName)
    {
        // Проверка: нельзя удалить системный пресет
        var systemPreset = TaskPresetsFactory.GetPresetByName(presetName);
        if (systemPreset != null)
        {
            return BadRequest(new { error = $"Нельзя удалить системный пресет '{presetName}'" });
        }

        // Возвращаем подтверждение (клиент удалит из LocalStorage)
        return Ok(new { message = $"Пресет '{presetName}' готов к удалению" });
    }
}
