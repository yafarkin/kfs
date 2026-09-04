# KanbanFlow — Справочник для Nessy CLI

## О проекте

**KanbanFlow** — симулятор производственной линии (Kanban-доски) с расчётом метрик потока. Основан на принципах теории ограничений (TOC) из книги "Цель" Голдратта.

**Стек:**
- .NET 9.0
- ASP.NET Core Web API + статический UI (HTML/JS)
- xUnit для тестов
- 3 проекта: `KanbanFlowApi`, `KanbanFlowSerivce`, `KanbanFlow.Tests`

**Справка по JSON**: компактное описание входной модели (конфигурация),
состояния симуляции (round-trip `simulate-day`) и модели метрик — см.
[`json-model.md`](./docs/json-model.md).

**Документация** — вся в каталоге [`docs/`](./docs/): `json-model.md`,
`roadmap.md`, `todo.md` (техдолг), `training-scenarios.md` (дизайн обучающих
сценариев) и `training-scenarios/` (JSON-файлы сценариев + README фасилитатору).

**Статус:** Активная разработка. Последние изменения:
- ✅ **Раздельные пресеты**: процесс + команда + задачи (комбинаторика)
- ✅ **LocalStorage**: сохранение и восстановление состояния симуляции
- ✅ **Редакторы пресетов**: задачи и команды (CRUD, валидация на backend, сохранение в LocalStorage)
- ✅ Объединены 4 endpoint'а метрик в один `/api/simulation/all-metrics`
- ✅ Исправлен расчёт `BufferTimeDays` для работников (теперь только реальные простои >1 дня)
- ✅ **Интерактивный CFD**: подсветка областей при наведении, синхронизация с легендой, тултипы

---

## Структура проекта

```
KanbanFlow/
├── docs/                       # Документация проекта
│   ├── json-model.md           # Справка по JSON-модели (конфигурация, состояние, метрики)
│   ├── roadmap.md              # Продуктовый роадмап
│   ├── todo.md                 # Технический долг и TODO
│   ├── training-scenarios.md   # Дизайн обучающих сценариев (тренинг по TOC)
│   └── training-scenarios/     # JSON-сценарии (импорт через UI) + README фасилитатору
├── KanbanFlowApi/              # Веб-API + UI (wwwroot)
│   ├── Controllers/
│   │   ├── SimulationController.cs    # GET /process-presets, /worker-pools, /task-presets; POST /start, /all-metrics
│   │   ├── ProcessEditorController.cs # CRUD для пресетов процессов
│   │   ├── WorkerEditorController.cs  # CRUD для пресетов команд
│   │   └── TaskEditorController.cs    # CRUD для пресетов задач
│   ├── Dtos/
│   │   ├── Board/             # ApiBoardDto, ApiStageDto, ApiWorkerDto, ApiTaskDto
│   │   ├── Config/            # ProcessPresetDto, WorkerPoolPresetDto, TaskPresetDto, StartSimulationRequestDto
│   │   ├── History/           # ApiHistoryDayDto, ApiHistoryActivityDto
│   │   ├── Metrics/           # AllMetricsDto, ApiMetricsDto, ApiWorkerMetricsDto
│   │   └── Task/              # TaskMetricsDto, StageMetricsAggregatedDto
│   ├── Factories/
│   │   ├── ProcessPresetsFactory.cs   # Фабрика пресетов процессов (3 пресета)
│   │   ├── WorkerPoolPresetsFactory.cs # Фабрика пресетов команд (3 пресета)
│   │   └── TaskPresetsFactory.cs      # Фабрика пресетов задач (3 пресета)
│   ├── Mappers/
│   │   └── ApiMapper.cs       # ToApiDto/ToDomainConfig/ToDomainSimulation
│   ├── Services/
│   │   ├── MetricsService.cs        # Общие метрики (LeadTime, Throughput, FlowEfficiency, Frequency)
│   │   ├── WorkerMetricsService.cs  # Метрики работников (Throughput, LeadTime, Efficiency)
│   │   └── TaskMetricsService.cs    # Метрики задач + агрегированные метрики стадий
│   ├── wwwroot/
│   │   ├── index.html         # UI: 3 селектора пресетов, доска, воркеры, история, 4 панели метрик
│   │   ├── app.js             # Клиентская логика: startSimulation(), LocalStorage, рендеринг, mergePresets()
│   │   └── editor/
│   │       ├── workers.html   # Редактор команд
│   │       ├── worker-editor.js
│   │       ├── tasks.html     # Редактор задач
│   │       └── task-editor.js
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
│   │   ├── ConfigFactory.cs   # Фабрика тестовых конфигураций
│   │   └── SimulationFactory.cs # Фабрика симуляций
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

### Endpoint'ы пресетов

```http
GET /api/simulation/process-presets
GET /api/simulation/worker-pools
GET /api/simulation/task-presets
```

Возвращают списки доступных пресетов для выбора.

### Запуск симуляции

```http
POST /api/simulation/start
Content-Type: application/json

{
  "processPresetName": "kanban-software",
  "workerPoolPresetName": "small-team",
  "taskPresetName": "standard-sprint",  // опционально
  "seed": 42,
  "useVariability": true
}
```

**Ответ:** Состояние симуляции на день 0 (готово к запуску `simulate-day`)

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
- **Панель настроек** (раскрывающаяся):
  - **Процесс** — селектор пресета процесса (workflow + задачи по умолчанию)
  - **Команда** — селектор пресета работников
  - **Задачи** — селектор пресета задач (опционально, переопределяет задачи процесса)
  - **Seed** — поле для воспроизводимости
  - **Вариативность** — toggle использования случайных отклонений
  - **Экспорт/Импорт** — кнопки для работы с JSON
- **Кнопки управления**: следующий день, авто-режим, перезагрузить
- **Доска**: стадии с задачами (карточки с прогрессом)
- **Воркеры**: карточки с назначенными задачами
- **История**: лог событий по дням
- **CFD панель**: Cumulative Flow Diagram с интерактивной подсветкой
  - Подсветка области при наведении курсора
  - Синхронизация с легендой (наведение на легенду ↔ подсветка области)
  - Всплывающая подсказка с названием стадии и количеством задач
  - Затемнение неактивных областей
- **4 панели метрик**:
  - Simulation Metrics (Lead Time, Throughput, Flow Efficiency, Frequency)
  - Worker Metrics (Throughput, Lead Time, Efficiency)
  - Task Metrics (таблица задач с деталями)
  - Stage Metrics (P50, P85, P95, Avg, Max по стадиям)

### Ключевые функции app.js
```javascript
// Хранение пресетов
let processPresets = [];
let workerPoolPresets = [];
let taskPresetPresets = [];

// Загрузка пресетов при старте
async function loadAllPresets() {
    const [processResponse, workerResponse, taskResponse] = await Promise.all([
        fetch('/api/simulation/process-presets'),
        fetch('/api/simulation/worker-pools'),
        fetch('/api/simulation/task-presets')
    ]);
    // Заполнение селекторов + восстановление из LocalStorage
}

// Запуск симуляции из комбинации пресетов
async function startSimulation() {
    const request = {
        processPresetName: document.getElementById('processSelector').value,
        workerPoolPresetName: document.getElementById('workerPoolSelector').value,
        taskPresetName: document.getElementById('taskPresetSelector').value || null,
        seed: parseInt(document.getElementById('seedInput').value) || 42,
        useVariability: document.getElementById('variabilityToggle').checked
    };
    
    const response = await fetch('/api/simulation/start', {
        method: 'POST',
        body: JSON.stringify(request)
    });
    
    simulationState = await response.json();
    saveSimulationToStorage();  // Сохранение в localStorage
    // Рендеринг...
}

// LocalStorage
function saveSelectionToStorage() {
    localStorage.setItem('kanbanflow_selection', JSON.stringify({
        processPresetName, workerPoolPresetName, taskPresetName, seed, useVariability
    }));
}

function saveSimulationToStorage() {
    localStorage.setItem('kanbanflow_simulation', JSON.stringify(simulationState));
}

function restoreFromLocalStorage() {
    const saved = localStorage.getItem('kanbanflow_simulation');
    if (saved) {
        simulationState = JSON.parse(saved);
        // Восстановление UI...
    }
}

// CFD интерактивность
function renderCfdChart() {
    // Рендеринг CFD с data-stage-name атрибутами для интерактивности
    // Возвращает SVG с областями и легендой
}

function initCfdInteractivity(chartContainer, stageAreas, data) {
    // Навешивание обработчиков mouseenter/mouseleave/mousemove
    // на области графика и элементы легенды
}

function handleCfdHover(event, stageName, stageMap, data, tooltip, isArea) {
    // Подсветка активной области + затемнение остальных
    // Показ тултипа с данными
}

function handleCfdLeave(stageMap, tooltip) {
    // Сброс всех стилей и скрытие тултипа
}

function showTooltip(stageName, data, tooltip) {
    // Отображение названия стадии и количества задач
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

## Редакторы пресетов

Приложение включает редакторы для создания и редактирования пользовательских пресетов:

**Редактор команд** (`/editor/workers.html`):
- CRUD операций для воркеров (логин, навыки, WIP-лимит, performance, отклонения)
- Навыки в виде строки через запятую
- Грейд воркера (`/api/editor/workers/grade-presets`) — выпадающий список «роль,
  уровень» (напр. «Backend, джун»), одноразово заполняет Performance/Отклонения/
  CostPerDay; поля остаются редактируемыми вручную после применения
- Сохранение в LocalStorage, валидация на backend (`/api/editor/workers/presets`)
- Экспорт/импорт пресетов в JSON

**Редактор задач** (`/editor/tasks.html`):
- CRUD операций для задач (ключ, описание, размер T-Shirt, навыки)
- Навыки в виде строки через запятую
- Сохранение в LocalStorage, валидация на backend (`/api/editor/tasks/presets`)
- Экспорт/импорт пресетов в JSON

**Архитектура редакторов:**
- Backend stateless — только валидация, сохранение в LocalStorage браузера
- `app.js` функция `mergePresets()` — заменяет серверные пресеты пользовательскими
- `startSimulation()` и `reloadConfig()` обновляют пресеты перед использованием

**Доступ к редакторам:**
- Кнопки в шапке главной страницы
- Кнопка "Редактор" в секции настроек задач/команд

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

### Добавление нового пресета

**Процесс:**
1. Добавить метод в `ProcessPresetsFactory.cs`
2. Создать `ApiWorkflowDto` со стадиями и переходами
3. Добавить задачи по умолчанию
4. Добавить в `GetAllPresets()`

**Команда:**
1. Добавить метод в `WorkerPoolPresetsFactory.cs`
2. Создать список `ApiWorkerDto`
3. Добавить в `GetAllPresets()`

**Задачи:**
1. Добавить метод в `TaskPresetsFactory.cs`
2. Создать список `ApiTaskDto`
3. Добавить в `GetAllPresets()`

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
