# KanbanFlow — Справочник для Nessy CLI

## О проекте

**KanbanFlow** — симулятор производственной линии (Kanban-доски) с расчётом метрик потока. Основан на принципах теории ограничений (TOC) из книги "Цель" Голдратта.

**Стек:**
- .NET 9.0
- ASP.NET Core Web API + статический UI (HTML/JS)
- xUnit для тестов
- 3 проекта: `KanbanFlowApi`, `KanbanFlowSerivce`, `KanbanFlow.Tests`

**Статус:** Активная разработка. Последние изменения:
- ✅ Объединены 4 endpoint'а метрик в один `/api/simulation/all-metrics`
- ✅ Исправлен расчёт `BufferTimeDays` для работников (теперь только реальные простои >1 дня)
- ✅ Обновлён README.md

---

## Структура проекта

```
KanbanFlow/
├── KanbanFlowApi/              # Веб-API + UI (wwwroot)
│   ├── Controllers/
│   │   └── SimulationController.cs    # POST /api/simulation/all-metrics
│   ├── Dtos/
│   │   ├── Board/             # ApiBoardDto, ApiStageDto, ApiWorkerDto, ApiTaskDto
│   │   ├── Config/            # ApiConfigDto, ApiWorkflowDto, ApiStageConfigDto
│   │   ├── History/           # ApiHistoryDayDto, ApiHistoryActivityDto
│   │   ├── Metrics/           # AllMetricsDto, ApiMetricsDto, ApiWorkerMetricsDto
│   │   └── Task/              # TaskMetricsDto, StageMetricsAggregatedDto
│   ├── Mappers/
│   │   └── ApiMapper.cs       # ToApiDto/ToDomainConfig/ToDomainSimulation
│   ├── Services/
│   │   ├── MetricsService.cs        # Общие метрики (LeadTime, Throughput, FlowEfficiency, Frequency)
│   │   ├── WorkerMetricsService.cs  # Метрики работников (Throughput, LeadTime, Efficiency)
│   │   └── TaskMetricsService.cs    # Метрики задач + агрегированные метрики стадий
│   ├── wwwroot/
│   │   ├── index.html         # UI: доска, воркеры, история, 4 панели метрик
│   │   └── app.js             # Клиентская логика: calculateAllMetrics(), рендеринг
│   ├── appsettings.json
│   └── Program.cs             # Точка входа API
│
├── KanbanFlowSerivce/         # Доменная логика (class library)
│   ├── Dtos/
│   │   └── Config/            # Config, Workflow, Stage, Worker, Task (доменные DTO)
│   ├── Enums/
│   │   ├── ActivityType.cs    # WorkerTookTask, WorkerCompletedTask, TaskMoved, TaskProgressUpdated, TaskWaiting, TaskResumed, LeadTimeStarted
│   │   ├── StageType.cs       # Buffer, Work
│   │   └── TShirtType.cs      # S=1день, M=3дня, L=5дней, XL=8дней
│   ├── Factories/
│   │   └── ConfigFactory.cs   # Фабрика тестовых конфигураций
│   ├── Mappers/
│   │   └── DomainMapper.cs    # Маппинг доменных DTO
│   └── Services/
│       ├── Simulation.cs              # Состояние симуляции: Config, Board, History, CurrentDay
│       ├── SimulationValidationService.cs  # Валидация конфигурации
│       ├── TaskMovementService.cs          # ProcessMovements(): перемещение задач между стадиями
│       └── WorkProgressService.cs          # SimulateWorkDay(): работа воркеров над задачами
│
└── KanbanFlow.Tests/          # Юнит-тесты (xUnit)
    ├── ApiMapperTests.cs      # Маппинг API ↔ Domain
    ├── BoardStageTests.cs     # Стадии: WIP, transitions, canAcceptTasks
    ├── BoardWorkerTests.cs    # Воркеры: skills, WIP, availability
    ├── EdgeCaseTests.cs       # Граничные случаи
    ├── HistoryActivityTests.cs # Логирование событий
    ├── TaskMovementTests.cs   # Перемещение задач
    └── WorkProgressTests.cs   # Симуляция работы
```

---

## Ключевые концепции

### Модель времени
- **Симуляция работает на уровне дней** (без тиков)
- Все события в день происходят в `Tick = 0`
- Формула абсолютного времени: `(DayNumber - 1) * 24` (часы)
- Если задача занимает 50% дня, воркер всё равно считается занятым весь день (упрощение)

### Типы стадий
- **Buffer** — буферные (не создают ценность): Todo, Done
- **Work** — рабочие (создают ценность): Developing, Testing, Code Review

### WIP-лимиты
- Ограничивают количество задач на стадии одновременно
- `WIP = null` — без ограничений
- Если стадия не может принять задачу (WIP достигнут), задача ждёт

### История активностей
Все события логируются с `CorrelationId` для отслеживания пар:
- `WorkerTookTask` → `WorkerCompletedTask` (один CorrelationId)
- `TaskMoved` — перемещение между стадиями
- `TaskProgressUpdated` — прогресс выполнения (0-100%)
- `TaskWaiting` / `TaskResumed` — ожидание/возобновление
- `LeadTimeStarted` — задача достигла стадии начала Lead Time

---

## API

### Основной endpoint

```http
POST /api/simulation/all-metrics
Content-Type: application/json

{
  "config": { ... },      // ApiConfigDto
  "board": { ... },       // ApiBoardDto (текущее состояние)
  "history": [ ... ],     // List<ApiHistoryDayDto>
  "currentDay": 6         // int
}
```

**Ответ:**
```json
{
  "simulationMetrics": {
    "leadTime": { "p50": 2, "p85": 2.7, "taskCount": 2 },
    "throughput": { "overall": 0.33, "dailyHistory": [...] },
    "flowEfficiency": { "activeTime": 4, "waitTime": 0, "efficiencyPercent": 100 },
    "frequency": { "distribution": { "0-7": 2 }, "taskCount": 2 }
  },
  "workerMetrics": [
    {
      "login": "dev1",
      "throughput": 0.33,
      "leadTime": 2,
      "valuableTasksCount": 2,
      "efficiencyPercent": 80,
      "workTimeDays": 4,
      "bufferTimeDays": 0  // Только реальные простои >1 дня
    }
  ],
  "taskMetrics": [ ... ],
  "stageMetrics": [ ... ]
}
```

---

## Метрики — детали реализации

### 1. Общие метрики симуляции (`MetricsService.cs`)

**Lead Time:**
- Считается от стадии с `IsLeadTimeStart = true` до завершения задачи
- Расчёт перцентилей: P50, P85, P95

**Throughput:**
- Количество задач, завершённых за период / количество дней
- История по дням: `dailyHistory[]`

**Flow Efficiency:**
- `activeTime` = Σ время работы над задачами
- `waitTime` = Σ время ожидания между задачами
- `efficiencyPercent` = activeTime / (activeTime + waitTime) * 100

**Frequency:**
- Распределение времени выполнения задач (группировка по диапазонам дней)

### 2. Метрики работников (`WorkerMetricsService.cs`)

**Stage-based подход:**
- **Ценные стадии** (создают ценность): Developing, Testing
- **Вспомогательные стадии** (не создают ценность): Code Review, Release Prep, буферы

**Throughput (ценность):**
- Задачи на **ценных стадиях**, завершённые за период / дни
- Работа на вспомогательных стадиях не считается

**Lead Time (ценность):**
- Среднее время задач на **ценных стадиях**, где worker был задействован

**Efficiency (утилизация):**
- (Σ время работы на **всех** стадиях) / (Общее время симуляции)
- Показывает общую загрузку, включая вспомогательные стадии

**BufferTimeDays (простой):**
```csharp
// Формула: BufferDays = NextTookTask.DayNumber - CompletedTask.DayNumber - 1
// Если результат <= 0, простоя не было
var waitDuration = (nextTookTask.DayNumber - completedDay - 1);
if (waitDuration > 0)
{
    waitTime += waitDuration;
}
```

**Пример:**
- TASK-1: завершена в День 2
- TASK-2: начата в День 3
- Простой: 3 - 2 - 1 = **0 дней** (worker начал новую задачу на следующий день)

### 3. Метрики задач (`TaskMetricsService.cs`)

**Per-task метрики:**
- `leadTimeDays` — от IsLeadTimeStart до Done
- `flowEfficiencyPercent` — activeTime / (activeTime + waitTime)
- `stages[]` — время в каждой стадии + воркеры

**Агрегированные метрики стадий:**
- `p50Days`, `p85Days`, `p95Days`, `avgDays`, `maxDays`
- `taskCount` — количество задач, прошедших стадию

---

## UI (wwwroot)

### Структура index.html
- **Панель управления**: загрузка конфига, симуляция по дням, автосимуляция, импорт/экспорт JSON
- **Доска**: стадии с задачами (карточки с прогрессом)
- **Воркеры**: карточки с назначенными задачами
- **История**: лог событий по дням
- **4 панели метрик**:
  - Simulation Metrics (Lead Time, Throughput, Flow Efficiency, Frequency)
  - Worker Metrics (Throughput, Lead Time, Efficiency)
  - Task Metrics (таблица задач с деталями)
  - Stage Metrics (P50, P85, P95, Avg, Max по стадиям)

### Ключевые функции app.js
```javascript
let currentAllMetrics = null;  // Единое хранилище всех метрик

async function calculateAllMetrics() {
    const response = await fetch('/api/simulation/all-metrics', {
        method: 'POST',
        body: JSON.stringify(simulationState)
    });
    currentAllMetrics = await response.json();
    
    renderMetrics(currentAllMetrics.simulationMetrics);
    renderWorkerMetrics(currentAllMetrics.workerMetrics);
    renderTaskMetrics(currentAllMetrics.taskMetrics);
    renderStageMetrics(currentAllMetrics.stageMetrics);
}
```

---

## Тесты

### Запуск
```bash
dotnet test              # Все тесты
dotnet test --filter "FullyQualifiedName~WorkerMetrics"  # Конкретный тест
```

### Покрытие
- **156 тестов** (все проходят ✅)
- Тесты покрывают: маппинг, стадии, воркеры, перемещение задач, прогресс, историю, метрики

### Структура тестов
- **ApiMapperTests** — маппинг API ↔ Domain (ToApiDto, ToDomainConfig, ToDomainSimulation)
- **BoardStageTests** — WIP-лимиты, transitions, canAcceptTasks
- **BoardWorkerTests** — skills, WIP, availability, assignedTaskKeys
- **EdgeCaseTests** — пустые задачи, нулевые WIP, отсутствие воркеров
- **HistoryActivityTests** — логирование событий, CorrelationId
- **TaskMovementTests** — перемещение задач между стадиями
- **WorkProgressTests** — симуляция работы, прогресс задач

---

## Команды для разработки

```bash
# Сборка
dotnet build

# Запуск API (dev server)
dotnet run --project KanbanFlowApi

# Запуск тестов
dotnet test

# Проверка статуса git
git status && git diff HEAD

# Закоммитить изменения
git add -A && git commit -m "..."
```

---

## Точки расширения

### Добавление новой метрики
1. Создать DTO в `KanbanFlowApi/Dtos/Metrics/`
2. Добавить сервис расчёта в `KanbanFlowApi/Services/`
3. Добавить поле в `AllMetricsDto`
4. Обновить `SimulationController.CalculateAllMetrics()`
5. Добавить вызов в `app.js` и рендеринг в UI

### Добавление типа активности
1. Добавить enum в `KanbanFlowSerivce/Enums/ActivityType.cs`
2. Обновить логику логирования в соответствующем сервисе
3. Обновить UI для отображения нового типа события

### Изменение модели времени
- Сейчас: дни без тиков (упрощение)
- Если нужны часы/минуты: оживить `AdvanceTick()` и использовать `activity.Tick`

---

## Память (memory)

Проверь `/Users/e.yafarkin/.nessy/projects/-Users-e-yafarkin-Projects-personal-KanbanFlow/memory/MEMORY.md` для:
- **Модель времени симуляции** — дни без тиков, все события в tick 0
- **Метрики работников** — stage-based подход, разделение на ценные/вспомогательные стадии

---

## Частые задачи

### Исправление бага в метриках
1. Найти сервис: `MetricsService.cs`, `WorkerMetricsService.cs`, `TaskMetricsService.cs`
2. Проверить логику расчёта
3. Обновить тесты в соответствующем файле
4. Запустить `dotnet test`

### Добавление нового поля в конфигурацию
1. Обновить DTO: `KanbanFlowApi/Dtos/Config/` + `KanbanFlowSerivce/Dtos/Config/`
2. Обновить маппер: `ApiMapper.cs` + `DomainMapper.cs`
3. Обновить UI: `index.html` (форма) + `app.js` (сборка конфига)
4. Обновить тесты: `ApiMapperTests.cs`

### Отладка симуляции
1. Проверить `History` — все ли события логируются
2. Проверить `CorrelationId` для пар WorkerTookTask/WorkerCompletedTask
3. Использовать Swagger UI для просмотра request/response

---

## Контакты и контекст

- **Пользователь**: разработчик, работает над симулятором для обучения/анализа
- **Цель**: создать инструмент для визуализации и расчёта метрик потока (Flow Metrics)
- **Подход**: stage-based метрики с разделением на ценные/вспомогательные стадии
- **Важно**: тесты должны проходить, код должен быть чистым и понятным
