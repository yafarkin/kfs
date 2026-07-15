using KanbanFlowApi.Dtos.Config;
using Microsoft.AspNetCore.Mvc;

namespace KanbanFlowApi.Controllers;

/// <summary>
/// Контроллер для валидации задач.
/// Backend stateless — валидация происходит на сервере, сохранение в LocalStorage браузера.
/// </summary>
[ApiController]
[Route("api/editor/tasks")]
public class TaskEditorController : ControllerBase
{
    /// <summary>
    /// Валидировать список задач (для генератора/редактора).
    /// </summary>
    [HttpPost("validate")]
    public ActionResult ValidateTasks([FromBody] List<ApiTaskDto> tasks)
    {
        if (tasks == null || tasks.Count == 0)
        {
            return BadRequest(new { error = "Список задач не может быть пустым" });
        }

        var taskKeys = new HashSet<string>();
        foreach (var task in tasks)
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

        return Ok(new { message = $"Валидация пройдена: {tasks.Count} задач(и)" });
    }
}
