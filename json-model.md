# JSON-модель симуляции

Справка по структурам JSON, которыми обмениваются `/api/simulation/*`.
Три роли: **вход** (конфигурация для старта), **состояние** (вход *и* выход
итеративных вызовов), **метрики** (результат для анализа). Компактно, без
описаний C#-типов — только форма JSON и что в ней важно.

## 1. Конфигурация (вход в `POST /start`)

```jsonc
{
  "seed": 42,                    // long, детерминизм
  "useVariability": true,
  "workflow": { "stages": [ Stage, ... ] },
  "workers": [ Worker, ... ],
  "tasks": [ Task, ... ],
  "daysToSimulate": 0            // null=1 день, 0=до конца, N=N дней
}
```

**Stage**
```jsonc
{
  "name": "Developing",
  "type": "Work" | "Buffer",
  "isLeadTimeStart": true,       // старт отсчёта Lead Time
  "wipLimit": 3,                 // null = без лимита
  "requiredSkills": ["backend"],
  "requiresDifferentResource": false,
  "requiresDifferentResourceFromStage": null,
  "stageProgressPercent": 0,
  "createsValue": true,          // false = не создаёт ценность (напр. Code Review)
  "transitions": [ { "targetStageName": "Done", "probability": 1 } ]
}
```

**Worker**
```jsonc
{
  "login": "dev1",
  "skills": ["backend", "frontend"],
  "wipLimit": 1,
  "performance": 100,            // 100 = база, 150 = +50% скорость
  "deviationDownPercent": 0,
  "deviationUpPercent": 0,
  "costPerDay": 100              // стоимость дня работы
}
```

**Task**
```jsonc
{
  "key": "TASK-1",
  "summary": "...",
  "shirtType": "S|M|L|XL",       // S=1д, M=3д, L=5д, XL=8д
  "requiredSkills": ["backend"], // достаточно одного общего навыка со стадией
  "children": [ Task, ... ],     // опционально, иерархия
  "acceptableWorkers": { "Developing": "dev1" } // опционально: stage -> login
}
```

## 2. Состояние симуляции (вход/выход `simulate-day`, вход `all-metrics`)

Полное состояние — приходит и уходит в одном формате (round-trip).

```jsonc
{
  "config": { /* как в StartSimulationRequest, без daysToSimulate */
    "seed": 42, "useVariability": true,
    "workers": [ ... ], "workflow": { "stages": [...] }, "tasks": [ ... ]
  },
  "board": {
    "stages": [ BoardStage, ... ],
    "workers": [ BoardWorker, ... ],
    "tasks": [ BoardTask, ... ]
  },
  "history": [ HistoryDay, ... ],
  "currentDay": 6,
  "randomCallCount": 123          // для детерминированной перемотки RNG
}
```

**BoardStage** — конфиг стадии + рантайм:
`name, type, isLeadTimeStart, wipLimit, wipCount, canAcceptTasks,
taskKeys[], nextStageNames[], excludedStageName`

**BoardWorker** — конфиг воркера + рантайм:
`login, skills[], wipLimit, wipCount, isAvailable, assignedTaskKeys[],
assignedAssignments: [{ taskKey, stageName, daysRequired, daysWorked }]`

**BoardTask** — конфиг задачи + рантайм:
`key, summary, shirtType, requiredSkills[], progress (0-100),
workerLogin, currentStageName, selectedNextStageName`

**HistoryDay / Activity**
```jsonc
{
  "dayNumber": 5,
  "activities": [
    {
      "type": "TaskPulled|TaskMoved|WorkPerformed|...",
      "description": "...",
      "taskKey": "TASK-1", "workerLogin": "dev1", "stageName": "Developing",
      "progress": 33.3,
      "correlationId": "guid"    // связывает события одного события
    }
  ]
}
```

Ключевое: `config` внутри состояния не имеет циклических ссылок (стадии/
воркеры/задачи ссылаются друг на друга по `string`-имени/ключу, не по
объекту) — это чисто транспортный, плоский граф.

## 3. Метрики (выход `POST /all-metrics`)

```jsonc
{
  "simulationMetrics": {
    "leadTime": { "p50": 12.5, "p85": 18.0, "taskCount": 10 },
    "throughput": {
      "overall": 0.83,           // задач/день
      "dailyHistory": [ { "dayNumber": 1, "completedTasksCount": 0 }, ... ]
    },
    "flowEfficiency": { "activeTime": 40, "waitTime": 60, "efficiencyPercent": 40 },
    "frequency": { "distribution": { "0-7": 3, "7-14": 5 }, "taskCount": 10 },
    "totalCost": 5000, "workCost": 3200, "bufferCost": 1800   // сумма по воркерам
  },
  "workerMetrics": [
    {
      "login": "dev1",
      "throughput": 0.5, "leadTime": 10.2, "valuableTasksCount": 5,
      "efficiencyPercent": 75, "workTimeDays": 15, "bufferTimeDays": 5,
      "costPerDay": 100, "totalCost": 2000, "workCost": 1500, "bufferCost": 500
    }
  ],
  "taskMetrics": [
    {
      "taskKey": "TASK-1", "summary": "...", "shirtType": "M",
      "leadTimeDays": 12.5, "flowEfficiencyPercent": 60,
      "activeTimeDays": 7.5, "waitTimeDays": 5, "status": "Done",
      "stages": [ { "stageName": "Developing", "stageType": "Work",
                     "timeInStageDays": 3, "workers": ["dev1"] } ]
    }
  ],
  "stageMetrics": [
    { "stageName": "Developing", "stageType": "Work",
      "p50Days": 3, "p85Days": 5, "p95Days": 6, "avgDays": 3.4, "maxDays": 8,
      "taskCount": 10 }
  ]
}
```

**Смысл величин** (для интерпретации при анализе):
- `leadTime` — от `isLeadTimeStart`-стадии до завершения задачи.
- `throughput` — завершённые задачи / дни (общий и по воркерам, только
  ценные (`createsValue`) стадии).
- `flowEfficiency` — `active / (active + wait)`, `wait` — время в Buffer.
- `*Cost` — `costPerDay × дни`, раздельно work/buffer/total, по воркеру и в
  сумме по проекту.
- `stageMetrics` — перцентили времени прохождения стадии по всем задачам,
  для поиска узких мест (TOC-подход).

## Источники (для сверки при изменениях)

- Конфиг/вход: `KanbanFlowApi/Dtos/Config/*`
- Состояние: `KanbanFlowApi/Dtos/{ApiSimulationStateDto.cs,Board/*,History/*}`
- Метрики: `KanbanFlowApi/Dtos/Metrics/*`, `KanbanFlowApi/Dtos/Task/*`
- Расчёт метрик: `KanbanFlowApi/Services/{MetricsService,WorkerMetricsService,TaskMetricsService}.cs`
