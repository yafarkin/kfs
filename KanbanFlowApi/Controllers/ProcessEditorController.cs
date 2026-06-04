using KanbanFlowApi.Dtos.Config;
using KanbanFlowApi.Factories;
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

        // Валидация workflow
        if (preset.Workflow == null || preset.Workflow.Stages == null || preset.Workflow.Stages.Count == 0)
        {
            return BadRequest(new { error = "Процесс должен содержать хотя бы одну стадию" });
        }

        // Валидация стадий
        var stageNames = new HashSet<string>();
        foreach (var stage in preset.Workflow.Stages)
        {
            if (string.IsNullOrWhiteSpace(stage.Name))
            {
                return BadRequest(new { error = "Название стадии не может быть пустым" });
            }

            if (stageNames.Contains(stage.Name))
            {
                return BadRequest(new { error = $"Дублирующееся название стадии: '{stage.Name}'" });
            }
            stageNames.Add(stage.Name);

            // Валидация переходов
            if (stage.Transitions != null)
            {
                foreach (var transition in stage.Transitions)
                {
                    if (string.IsNullOrWhiteSpace(transition.TargetStageName))
                    {
                        return BadRequest(new { error = $"Целевая стадия перехода не может быть пустой (стадия '{stage.Name}')" });
                    }

                    if (transition.Probability <= 0 || transition.Probability > 1)
                    {
                        return BadRequest(new { error = $"Вероятность перехода должна быть от 0 до 1 (стадия '{stage.Name}')" });
                    }
                }

                // Сумма вероятностей переходов должна быть <= 1
                var totalProbability = stage.Transitions.Sum(t => t.Probability);
                if (totalProbability > 1.0)
                {
                    return BadRequest(new { error = $"Сумма вероятностей переходов стадии '{stage.Name}' не может превышать 1.0 (текущая: {totalProbability:F2})" });
                }
            }

            // Валидация WIP-лимита
            if (stage.WipLimit.HasValue && stage.WipLimit <= 0)
            {
                return BadRequest(new { error = $"WIP-лимит стадии '{stage.Name}' должен быть больше 0" });
            }

            // Валидация прогресса
            if (stage.StageProgressPercent < 0 || stage.StageProgressPercent > 100)
            {
                return BadRequest(new { error = $"Прогресс стадии '{stage.Name}' должен быть от 0 до 100%" });
            }
        }

        // Валидация задач
        if (preset.Tasks == null || preset.Tasks.Count == 0)
        {
            return BadRequest(new { error = "Процесс должен содержать хотя бы одну задачу" });
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
