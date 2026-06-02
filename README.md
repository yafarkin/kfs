# KanbanFlow — Симулятор производственной линии

Веб-приложение для симуляции работы Kanban-доски с метриками потока (Flow Metrics). Основано на принципах теории ограничений (TOC) из книги Элияху Голдратта "Цель".

## Возможности

- **Гибкое конфигурирование** — раздельный выбор процесса, команды и задач (комбинаторика пресетов)
- **Симуляция потока задач** — задачи перемещаются по стадиям workflow, воркеры выполняют работу с учётом производительности и WIP-лимитов
- **Расчёт метрик** — Lead Time, Throughput, Flow Efficiency, Frequency Distribution
- **Метрики работников** — Throughput, Lead Time, Efficiency (с разделением на ценные и вспомогательные стадии)
- **Метрики стадий** — P50, P85, P95, Avg, Max время прохождения
- **Импорт/экспорт конфигурации** — сохранение состояния в JSON
- **Автосимуляция** — запуск симуляции по дням с анимацией
- **Local Storage** — автоматическое сохранение и восстановление состояния симуляции

## Структура проекта

```
KanbanFlow/
├── KanbanFlowApi/              # Веб-API и UI
│   ├── Controllers/
│   │   └── SimulationController.cs    # Endpoint'ы: GET /process-presets, /worker-pools, /task-presets; POST /start
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
│   │   ├── index.html         # UI приложения (3 селектора пресетов)
│   │   └── app.js             # Клиентская логика + LocalStorage
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
```

**Ответ:** Список пресетов процессов
```json
[
  {
    "name": "kanban-software",
    "displayName": "Kanban Software Dev",
    "description": "7 стадий: Todo → Developing → ...",
    "workflow": { ... },
    "tasks": [ ... ],
    "isDefault": true
  }
]
```

```http
GET /api/simulation/worker-pools
```

**Ответ:** Список пресетов команд
```json
[
  {
    "name": "small-team",
    "displayName": "Маленькая команда",
    "description": "3 человека: 2 разработчика + 1 QA",
    "workers": [ ... ],
    "isDefault": true
  }
]
```

```http
GET /api/simulation/task-presets
```

**Ответ:** Список пресетов задач
```json
[
  {
    "name": "standard-sprint",
    "displayName": "Стандартный спринт",
    "description": "10 задач: 3 XS, 4 S, 3 M",
    "tasks": [ ... ],
    "isDefault": true
  }
]
```

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

**Ответ:** Состояние симуляции на день 0 (готово к запуску)

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

## Известные ограничения

- Симуляция работает только на уровне дней (без часов/тиков)
- Все события в день происходят в тик 0
- Если задача занимает 50% дня, воркер всё равно считается занятым весь день (упрощение)

## Ссылки

- Книга: Элияху Голдратт "Цель. Процесс непрерывного совершенствования"
- Теория ограничений (TOC)
- Flow Metrics: "Essential Skills for Innovation" — Дональд Рейнертсен
