# KanbanFlow — Симулятор производственной линии

Веб-приложение для симуляции работы Kanban-доски с метриками потока (Flow Metrics). Основано на принципах теории ограничений (TOC) из книги Элияху Голдратта "Цель".

## Возможности

- **Гибкое конфигурирование** — раздельный выбор процесса, команды и задач (комбинаторика пресетов)
- **Редакторы пресетов** — создание и редактирование пользовательских наборов процессов, команд и задач (CRUD, валидация, экспорт/импорт)
- **Генератор задач** — автоматическая генерация задач для спринта с настраиваемыми параметрами
- **Симуляция потока задач** — задачи перемещаются по стадиям workflow, воркеры выполняют работу с учётом производительности и WIP-лимитов
- **Редактирование WIP-лимитов** — быстрое изменение WIP-лимита стадии по двойному клику на колонке
- **Расчёт метрик** — Lead Time, Throughput, Flow Efficiency, Frequency Distribution
- **Метрики работников** — Throughput, Lead Time, Efficiency (с разделением на ценные и вспомогательные стадии)
- **Метрики стадий** — P50, P85, P95, Avg, Max время прохождения
- **Cumulative Flow Diagram (CFD)** — визуализация потока задач по стадиям с интерактивной подсветкой (наведение на области и легенду, тултипы)
- **Импорт/экспорт конфигурации** — сохранение состояния в JSON
- **Автосимуляция** — запуск симуляции по дням с анимацией
- **Local Storage** — автоматическое сохранение и восстановление состояния симуляции и пресетов

## Структура проекта

```
KanbanFlow/
├── KanbanFlowApi/              # Веб-API и UI
│   ├── Controllers/
│   │   ├── SimulationController.cs    # Endpoint'ы: GET /process-presets, /worker-pools, /task-presets; POST /start, /simulate-day, /all-metrics
│   │   ├── ProcessEditorController.cs # CRUD для пресетов процессов
│   │   ├── WorkerEditorController.cs  # CRUD для пресетов команд
│   │   ├── TaskEditorController.cs    # CRUD для пресетов задач
│   │   └── EditorController.cs        # Базовый контроллер для редакторов
│   ├── Dtos/
│   │   ├── Board/             # DTO состояния доски
│   │   ├── Config/            # DTO конфигурации: ProcessPresetDto, WorkerPoolPresetDto, TaskPresetDto
│   │   ├── History/           # DTO истории активностей
│   │   ├── Metrics/           # DTO метрик (AllMetricsDto, ApiMetricsDto, etc.)
│   │   └── Task/              # DTO задач и стадий
│   ├── Factories/
│   │   ├── ProcessPresetsFactory.cs   # Фабрика пресетов процессов
│   │   ├── WorkerPoolPresetsFactory.cs # Фабрика пресетов команд
│   │   └── TaskPresetsFactory.cs      # Фабрика пресетов задач
│   ├── Mappers/
│   │   └── ApiMapper.cs       # Маппинг между API DTO и доменными моделями
│   ├── Services/
│   │   ├── MetricsService.cs        # Общие метрики симуляции
│   │   ├── WorkerMetricsService.cs  # Метрики работников
│   │   └── TaskMetricsService.cs    # Метрики задач и стадий
│   ├── wwwroot/
│   │   ├── index.html         # UI приложения (3 селектора пресетов, доска, воркеры, метрики)
│   │   ├── app.js             # Клиентская логика + LocalStorage + mergePresets()
│   │   └── editor/
│   │       ├── processes.html # Редактор процессов
│   │       ├── process-editor.js
│   │       ├── workers.html   # Редактор команд
│   │       ├── worker-editor.js
│   │       ├── tasks.html     # Редактор задач
│   │       └── task-editor.js
│   └── Program.cs             # Точка входа API
│
├── KanbanFlowSerivce/         # Доменная логика (без зависимостей от API)
│   ├── Dtos/
│   │   └── Config/            # Доменные DTO конфигурации
│   ├── Enums/
│   │   ├── ActivityType.cs    # Типы событий истории
│   │   ├── StageType.cs       # Типы стадий (Buffer, Work)
│   │   └── TShirtType.cs      # Размеры задач (S, M, L, XL)
│   ├── Factories/
│   │   ├── ConfigFactory.cs   # Фабрика тестовых конфигураций
│   │   └── SimulationFactory.cs # Фабрика симуляций
│   ├── Mappers/
│   │   └── DomainMapper.cs    # Маппинг доменных DTO
│   └── Services/
│       ├── SimulationValidationService.cs  # Валидация конфигурации
│       ├── TaskMovementService.cs          # Перемещение задач между стадиями
│       └── WorkProgressService.cs          # Симуляция работы воркеров
│
└── KanbanFlow.Tests/          # Юнит-тесты
    ├── ApiMapperTests.cs
    ├── BoardStageTests.cs
    ├── BoardWorkerTests.cs
    ├── EdgeCaseTests.cs
    ├── HistoryActivityTests.cs
    ├── TaskMovementTests.cs
    └── WorkProgressTests.cs
```

## Архитектура

### Доменная модель

**Симуляция** работает на уровне дней (без тиков). Каждый день:
1. `TaskMovementService` — перемещает задачи между стадиями (с учётом WIP-лимитов)
2. `WorkProgressService` — воркеры выполняют задачи (с учётом performance и skill)
3. Все события логируются в `History` с `CorrelationId` для отслеживания

**Стадии** делятся на:
- **Buffer** — буферные (не создают ценность, например Todo, Done)
- **Work** — рабочие (создают ценность, например Developing, Testing)

**WIP-лимиты** ограничивают количество задач на стадии одновременно.

### Метрики

**Общие метрики симуляции:**
- **Lead Time** — время от начала (IsLeadTimeStart стадии) до завершения задачи
- **Throughput** — количество завершённых задач за период
- **Flow Efficiency** — отношение активного времени к общему (active / (active + wait))
- **Frequency** — распределение времени выполнения задач

**Метрики работников (stage-based подход):**
- **Throughput** — задачи на ценных стадиях / дни
- **Lead Time** — среднее время на ценных стадиях
- **Efficiency** — (время работы) / (общее время симуляции)
- **BufferTime** — простой между задачами (считается только если > 1 дня)

**Метрики стадий:**
- **P50, P85, P95, Avg, Max** — перцентили и среднее время прохождения

## Запуск

### Требования
- .NET 9.0 SDK

### Команды

```bash
# Сборка
dotnet build

# Запуск API (с Swagger и UI)
dotnet run --project KanbanFlowApi

# Запуск тестов
dotnet test

# Запуск тестов с покрытием
dotnet test /p:CollectCoverage=true
```

После запуска API откройте:
- **UI**: http://localhost:5000
- **Swagger**: http://localhost:5000/swagger

## API

### Endpoint'ы пресетов

```http
GET /api/simulation/process-presets
GET /api/simulation/worker-pools
GET /api/simulation/task-presets
```

Возвращают списки доступных пресетов (процессы, команды, задачи) для выбора.

### Endpoint'ы редакторов

```http
GET /api/editor/processes/presets
GET /api/editor/workers/presets
GET /api/editor/tasks/presets
```

Возвращают пресеты для редакторов (серверные + пользовательские из LocalStorage).

### Запуск симуляции

```http
POST /api/simulation/start
Content-Type: application/json

{
  "processPresetName": "kanban-software",
  "workerPoolPresetName": "small-team",
  "taskPresetName": "standard-sprint",  // опционально
  "seed": 42,
  "useVariability": true,
  "daysToSimulate": 0  // опционально: 0 = до конца, N = на N дней
}
```

**Ответ:** Состояние симуляции на день 0 (или после N дней симуляции)

### Симуляция дня

```http
POST /api/simulation/simulate-day
Content-Type: application/json

{
  "config": { ... },
  "board": { ... },
  "history": [ ... ],
  "currentDay": 5
}
```

**Ответ:** Обновлённое состояние симуляции после одного дня

### Расчёт метрик

```http
POST /api/simulation/all-metrics
Content-Type: application/json

{
  "config": { ... },
  "board": { ... },
  "history": [ ... ],
  "currentDay": 6
}
```

**Ответ:**
```json
{
  "simulationMetrics": { ... },
  "workerMetrics": [ ... ],
  "taskMetrics": [ ... ],
  "stageMetrics": [ ... ]
}
```

### Структура запроса

- `config` — конфигурация (workflow, workers, tasks, seed)
- `board` — текущее состояние доски (stages, workers, tasks)
- `history` — история всех событий симуляции
- `currentDay` — текущий день симуляции

## Конфигурация

### Пресеты

Приложение использует **раздельные пресеты** для гибкого конфигурирования:

**Процессы** (`ProcessPresetDto`):
- `simple-process` — 3 стадии: Todo → Developing → Done
- `kanban-software` — 7 стадий: полный цикл разработки
- `twork-process` — 19 стадий: расширенный workflow с Code Review

**Команды** (`WorkerPoolPresetDto`):
- `solo-developer` — 1 универсал (backend + frontend)
- `small-team` — 3 человека: 2 разработчика + 1 QA
- `twork-team` — 7 человек: 4 backend, 1 frontend, 2 QA

**Задачи** (`TaskPresetDto`):
- `quick-sprint` — 4 задачи (2 S, 2 M)
- `standard-sprint` — 10 задач (3 XS, 4 S, 3 M)
- `large-backlog` — 20 задач (8 BE, 6 FE, 6 QA)

### Workflow

```json
{
  "stages": [
    {
      "name": "Todo",
      "type": "Buffer",
      "isLeadTimeStart": false,
      "wipLimit": null,
      "requiredSkills": [],
      "createsValue": true,
      "transitions": [{"targetStageName": "Developing", "probability": 1}]
    },
    {
      "name": "Developing",
      "type": "Work",
      "isLeadTimeStart": true,
      "wipLimit": 3,
      "requiredSkills": ["backend"],
      "createsValue": true,
      "transitions": [{"targetStageName": "Done", "probability": 1}]
    }
  ]
}
```

### Workers

```json
{
  "login": "dev1",
  "skills": ["backend", "frontend"],
  "wipLimit": 1,
  "performance": 100
}
```

### Tasks

```json
{
  "key": "TASK-1",
  "summary": "Реализовать фичу",
  "shirtType": "M",
  "requiredSkills": ["backend"]
}
```

**Размеры задач (T-Shirt):**
- S = 1 день
- M = 3 дня
- L = 5 дней
- XL = 8 дней

## Тестирование

### Запуск всех тестов
```bash
dotnet test
```

### Запуск конкретного теста
```bash
dotnet test --filter "FullyQualifiedName~WorkerMetrics"
```

### Структура тестов
- **ApiMapperTests** — маппинг между API и доменными DTO
- **BoardStageTests** — логика стадий (WIP, transitions)
- **BoardWorkerTests** — логика воркеров (skills, WIP, availability)
- **EdgeCaseTests** — граничные случаи (пустые задачи, нулевые WIP)
- **HistoryActivityTests** — логирование событий
- **TaskMovementTests** — перемещение задач
- **WorkProgressTests** — симуляция работы

## Расширение

### Добавление новой метрики

1. Создать DTO в `KanbanFlowApi/Dtos/Metrics/`
2. Добавить сервис расчёта в `KanbanFlowApi/Services/`
3. Добавить поле в `AllMetricsDto`
4. Обновить `SimulationController.CalculateAllMetrics()`
5. Добавить вызов в `app.js` и рендеринг

### Добавление типа активности

1. Добавить enum в `KanbanFlowSerivce/Enums/ActivityType.cs`
2. Обновить логику логирования в соответствующем сервисе
3. Обновить UI для отображения нового типа события

## Редакторы пресетов

Приложение включает редакторы для создания и редактирования пользовательских пресетов:

**Редактор процессов** (`/editor/processes.html`):
- CRUD операций для стадий (имя, тип, WIP-лимит, прогресс, навыки, переходы)
- Настройка переходов между стадиями с вероятностями (сумма = 1.0)
- Флаг IsLeadTimeStart для отметки начала отсчёта Lead Time
- Валидация workflow (DAG, отсутствие циклов, self-loop переходы)
- Сохранение в LocalStorage, валидация на backend
- Экспорт/импорт пресетов в JSON

**Редактор команд** (`/editor/workers.html`):
- CRUD операций для воркеров (логин, навыки, WIP-лимит, performance, отклонения)
- Навыки в виде строки через запятую
- Сохранение в LocalStorage, валидация на backend
- Экспорт/импорт пресетов в JSON

**Редактор задач** (`/editor/tasks.html`):
- CRUD операций для задач (ключ, описание, размер T-Shirt, навыки)
- Генератор задач — автоматическое создание задач для спринта с настраиваемыми параметрами (количество, соотношение размеров, навыки)
- Навыки в виде строки через запятую
- Сохранение в LocalStorage, валидация на backend
- Экспорт/импорт пресетов в JSON

**Как это работает:**
1. Откройте редактор (`/editor/processes.html`, `/editor/workers.html` или `/editor/tasks.html`)
2. Создайте новый пресет или выберите существующий
3. Добавьте/отредактируйте элементы
4. Нажмите "Сохранить" — пресет сохранится в LocalStorage браузера
5. На главной странице нажмите "Перезагрузить" — пресеты обновятся

**Архитектура:**
- Backend stateless — только валидация, сохранение в LocalStorage браузера
- Пользовательские пресеты заменяют серверные с тем же именем
- Изменения подтягиваются сразу после обновления пресетов

**Дополнительно:**
- Редактирование WIP-лимитов на лету — двойной клик на колонке стадии на главной странице
- Интерактивная CFD диаграмма — наведение на области графика подсвечивает соответствующую стадию

## Известные ограничения

- Симуляция работает только на уровне дней (без часов/тиков)
- Все события в день происходят в тик 0
- Если задача занимает 50% дня, воркер всё равно считается занятым весь день (упрощение)

## Ссылки

- Книга: Элияху Голдратт "Цель. Процесс непрерывного совершенствования"
- Теория ограничений (TOC)
- Flow Metrics: "Essential Skills for Innovation" — Дональд Рейнертсен
