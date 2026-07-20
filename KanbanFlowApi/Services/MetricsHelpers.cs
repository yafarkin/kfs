using KanbanFlowSerivce.Dtos;
using KanbanFlowSerivce.Dtos.History;

namespace KanbanFlowApi.Services;

/// <summary>
/// Общие хелперы для расчёта метрик.
/// </summary>
public static class MetricsHelpers
{
    /// <summary>
    /// Извлечь ключ задачи из активности.
    /// Возвращает null, если TaskKey не заполнен — regex-фоллбек не используется.
    /// </summary>
    public static string? GetTaskKeyFromActivity(HistoryActivity activity)
    {
        return activity.TaskKey;
    }

    /// <summary>
    /// Получить имена финальных стадий (стоков) — стадии без исходящих переходов.
    /// Задача считается завершённой при входе в любую из этих стадий.
    /// </summary>
    public static HashSet<string> GetFinalStageNames(Simulation simulation)
    {
        return simulation.Board.Stages
            .Where(s => s.NextStages.Count == 0)
            .Select(s => s.Stage.Name)
            .ToHashSet();
    }

    /// <summary>
    /// Рассчитать Lead Time задачи от события LeadTimeStarted.
    /// Если события нет — возвращает null (задача ещё не вошла в измеряемую зону).
    /// Не фоллбечится на первый TaskMoved.
    /// </summary>
    public static decimal? CalculateLeadTimeFromStartEvent(
        List<HistoryActivity> activities,
        HashSet<string> finalStageNames,
        int currentDay)
    {
        if (activities.Count == 0)
            return null;

        // Найти начало Lead Time — только событие LeadTimeStarted
        var leadTimeStartEvent = activities
            .FirstOrDefault(a => a.Type == ActivityType.LeadTimeStarted);

        if (leadTimeStartEvent == null)
        {
            // Задача ещё не вошла в измеряемую зону — не фоллбечимся на TaskMoved
            return null;
        }

        var startDay = leadTimeStartEvent.DayNumber;

        // Найти конец — вход в любую финальную стадию
        var endActivity = activities
            .FirstOrDefault(a => a.Type == ActivityType.TaskMoved && a.StageName != null && finalStageNames.Contains(a.StageName));

        var endDay = endActivity?.DayNumber ?? currentDay;

        return (endDay - startDay);
    }

    /// <summary>
    /// Рассчитать Active Time и Wait Time для задачи.
    /// Active = время в Work стадиях, Wait = время в Buffer стадиях.
    /// Время считается до входа в финальную стадию (не включая её).
    /// </summary>
    /// <param name="movements">Список TaskMoved активностей задачи</param>
    /// <param name="finalStageNames">Имена финальных стадий</param>
    /// <param name="currentDay">Текущий день симуляции</param>
    /// <param name="getCurrentStageName">Функция получения текущей стадии задачи (для незавершённых)</param>
    /// <param name="isWorkStage">Функция определения, является ли стадия рабочей</param>
    public static (decimal ActiveTime, decimal WaitTime) CalculateFlowEfficiencyTimes(
        List<HistoryActivity> movements,
        HashSet<string> finalStageNames,
        int currentDay,
        Func<string?> getCurrentStageName,
        Func<string, bool> isWorkStage)
    {
        var activeTime = 0m;
        var waitTime = 0m;

        if (movements.Count == 0)
            return (0, 0);

        // Проверяем, достигла ли задача финальной стадии
        var isCompleted = movements.Any(m => m.StageName != null && finalStageNames.Contains(m.StageName));

        // Если задача завершена, считаем только до входа в финальную стадию
        var movementsToProcess = isCompleted
            ? movements.TakeWhile(m => m.StageName == null || !finalStageNames.Contains(m.StageName)).ToList()
            : movements;

        if (movementsToProcess.Count == 0)
            return (0, 0);

        // День завершения (вход в финальную стадию) или текущий день если не завершена
        var completionDay = isCompleted
            ? movements.First(m => m.StageName != null && finalStageNames.Contains(m.StageName)).DayNumber
            : currentDay;

        // Проходим по всем переходам и считаем время в каждой стадии
        for (var i = 0; i < movementsToProcess.Count; i++)
        {
            var currentMove = movementsToProcess[i];
            var stageName = currentMove.StageName;
            if (stageName == null)
                continue;

            var enterDay = currentMove.DayNumber;

            // Выход из стадии — следующий переход или день завершения
            var exitDay = (i < movementsToProcess.Count - 1)
                ? movementsToProcess[i + 1].DayNumber
                : completionDay;

            var timeInStage = exitDay - enterDay;

            if (isWorkStage(stageName))
            {
                activeTime += timeInStage;
            }
            else
            {
                waitTime += timeInStage;
            }
        }

        // Если задача ещё в процессе, добавляем время до текущего дня
        if (!isCompleted && movementsToProcess.Count > 0)
        {
            var lastActivity = movementsToProcess.LastOrDefault();
            if (lastActivity != null)
            {
                var remainingDays = currentDay - lastActivity.DayNumber;
                if (remainingDays > 0)
                {
                    var currentStageName = getCurrentStageName();
                    if (currentStageName != null)
                    {
                        if (isWorkStage(currentStageName))
                        {
                            activeTime += remainingDays;
                        }
                        else
                        {
                            waitTime += remainingDays;
                        }
                    }
                }
            }
        }

        return (activeTime, waitTime);
    }
}
