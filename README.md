# KanbanFlow — Симулятор производственной линии

Веб-приложение для симуляции работы Kanban-доски с метриками потока (Flow Metrics). Основано на принципах теории ограничений (TOC) из книги Элияху Голдратта "Цель".

## Возможности

- **Симуляция потока задач** — задачи перемещаются по стадиям workflow, воркеры выполняют работу с учётом производительности и WIP-лимитов
- **Расчёт метрик** — Lead Time, Throughput, Flow Efficiency, Frequency Distribution
- **Метрики работников** — Throughput, Lead Time, Efficiency (с разделением на ценные и вспомогательные стадии)
- **Метрики стадий** — P50, P85, P95, Avg, Max время прохождения
- **Импорт/экспорт конфигурации** — сохранение состояния в JSON
- **Автосимуляция** — запуск симуляции по дням с анимацией

## Структура проекта

```
KanbanFlow/
├── KanbanFlowApi/              # Веб-API и UI
│   ├── Controllers/
│   │   └── SimulationController.cs    # Единый endpoint /api/simulation/all-metrics
│   ├── Dtos/
│   │   ├── Board/             # DTO состояния доски
│   │   ├── Config/            # DTO конфигурации workflow
│   │   ├── History/           # DTO истории активностей
│   │   ├── Metrics/           # DTO метрик (AllMetricsDto, ApiMetricsDto, etc.)
│   │   └── Task/              # DTO задач и стадий
│   ├── Mappers/
│   │   └── ApiMapper.cs       # Маппинг между API DTO и доменными моделями
│   ├── Services/
│   │   ├── MetricsService.cs        # Общие метрики симуляции
│   │   ├── WorkerMetricsService.cs  # Метрики работников
│   │   └── TaskMetricsService.cs    # Метрики задач и стадий
│   ├── wwwroot/
│   │   ├── index.html         # UI приложения
│   │   └── app.js             # Клиентская логика
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
│   │   └── ConfigFactory.cs   # Фабрика тестовых конфигураций
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

### Основной endpoint

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
