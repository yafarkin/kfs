// ВНИМАНИЕ: файл сгенерирован из OpenAPI-схемы бэкенда. Руками не править.
// Перегенерировать:  node tools/generate-api-types.mjs
// Источник:          KanbanFlowApi/openapi/KanbanFlowApi.json  (из C#-DTO через Swashbuckle)
// Подробнее:         docs/api-contract.md
//
// Использование во фронтовых .js:
//   /** @typedef {import('./api-types').ApiSimulationStateDto} ApiSimulationStateDto */
//   /** @type {import('./api-types').StartSimulationRequestDto} */

export type ActivityType = "WorkerTookTask" | "WorkerCompletedTask" | "TaskMoved" | "TaskProgressUpdated" | "LeadTimeStarted" | "TaskWaiting" | "TaskResumed";

/**
 * Единый DTO для всех метрик симуляции.
 * Включает общие метрики, метрики работников, задач и стадий.
 */
export interface AllMetricsDto {
  simulationMetrics?: ApiMetricsDto;
  /** Метрики работников (Throughput, Lead Time, Efficiency). */
  workerMetrics?: ApiWorkerMetricsDto[] | null;
  /** Метрики задач (Lead Time, Flow Efficiency, время по стадиям). */
  taskMetrics?: TaskMetricsDto[] | null;
  /** Агрегированные метрики стадий (P50, P85, P95, Avg, Max). */
  stageMetrics?: StageMetricsAggregatedDto[] | null;
}

/** DTO для доски (полное состояние всех задач, стадий и исполнителей). */
export interface ApiBoardDto {
  /** Список стадий доски. */
  stages?: ApiBoardStageDto[] | null;
  /** Список исполнителей (воркеров). */
  workers?: ApiBoardWorkerDto[] | null;
  /** Список задач на доске. */
  tasks?: ApiBoardTaskDto[] | null;
}

/** DTO для стадии на доске (состояние стадии в симуляции). */
export interface ApiBoardStageDto {
  /** Имя стадии. */
  name?: string | null;
  type?: StageType;
  /** Является ли стадия началом для измерения Lead Time. */
  isLeadTimeStart?: boolean;
  /** WIP-лимит (максимум задач одновременно). Null = без лимита. */
  wipLimit?: number | null;
  /** Текущее количество задач на стадии. */
  wipCount?: number;
  /** Может ли стадия принять ещё задачи (с учётом WIP-лимита). */
  canAcceptTasks?: boolean;
  /** Ключи задач, находящихся на этой стадии. */
  taskKeys?: string[] | null;
  /** Имена следующих стадий, куда можно перейти из текущей. */
  nextStageNames?: string[] | null;
  /** Имя исключающей стадии (для RequiresDifferentResource). */
  excludedStageName?: string | null;
}

/** DTO для задачи на доске (состояние задачи в симуляции). */
export interface ApiBoardTaskDto {
  /** Уникальный ключ задачи (например, TASK-1). */
  key?: string | null;
  /** Краткое описание задачи. */
  summary?: string | null;
  shirtType?: TShirtType;
  /** Навыки, необходимые для выполнения задачи. */
  requiredSkills?: string[] | null;
  /** Прогресс выполнения (0-100). */
  progress?: number;
  /** Логин исполнителя, работающего над задачей. */
  workerLogin?: string | null;
  /** Имя текущей стадии, где находится задача. */
  currentStageName?: string | null;
  /** Имя выбранной следующей стадии (для вероятностных переходов). */
  selectedNextStageName?: string | null;
}

/** DTO для воркера на доске (состояние исполнителя в симуляции). */
export interface ApiBoardWorkerDto {
  /** Логин исполнителя. */
  login?: string | null;
  /** Навыки исполнителя. */
  skills?: string[] | null;
  /** Персональный WIP-лимит (максимум задач одновременно). Null = без лимита. */
  wipLimit?: number | null;
  /** Текущее количество задач у исполнителя. */
  wipCount?: number;
  /** Доступен ли исполнитель для взятия новых задач. */
  isAvailable?: boolean;
  /** Детали назначений задач (с DaysRequired/DaysWorked) для сериализации состояния. */
  assignedAssignments?: ApiTaskAssignmentDto[] | null;
}

/** DTO для метрик Flow Efficiency. */
export interface ApiFlowEfficiencyMetricsDto {
  /** Общее время в рабочих статусах (в днях). */
  activeTime?: number;
  /** Общее время в нерабочих статусах (ожидание, буферы) в днях. */
  waitTime?: number;
  /** Процент активного времени (ActiveTime / (ActiveTime + WaitTime) * 100). */
  efficiencyPercent?: number;
}

/** DTO для частотной метрики (распределение задач по времени выполнения). */
export interface ApiFrequencyMetricsDto {
  /**
   * Распределение задач по диапазонам времени (в днях).
   * Ключ - диапазон времени (например, "0-7", "7-14"), значение - количество задач.
   */
  distribution?: Record<string, number> | null;
  /** Общее количество задач. */
  taskCount?: number;
}

/** DTO для события истории симуляции. */
export interface ApiHistoryActivityDto {
  type?: ActivityType;
  /** Текстовое описание события. */
  description?: string | null;
  /** Ключ задачи, связанной с событием. */
  taskKey?: string | null;
  /** Логин исполнителя, связанного с событием. */
  workerLogin?: string | null;
  /** Имя стадии, связанной с событием. */
  stageName?: string | null;
  /** Прогресс задачи после события (если применимо). */
  progress?: number | null;
  /** Уникальный идентификатор для корреляции связанных событий. */
  correlationId?: string;
}

/** DTO для дня истории симуляции (содержит все события за день). */
export interface ApiHistoryDayDto {
  /** Номер дня симуляции. */
  dayNumber?: number;
  /** Список событий, произошедших в этот день. */
  activities?: ApiHistoryActivityDto[] | null;
}

/** DTO для метрик Lead Time. */
export interface ApiLeadTimeMetricsDto {
  /** 50-й перцентиль Lead Time (медиана) в днях. */
  p50?: number;
  /** 85-й перцентиль Lead Time в днях. */
  p85?: number;
  /** Общее количество задач, использованных для расчёта. */
  taskCount?: number;
}

/** DTO для всех рассчитанных метрик симуляции. */
export interface ApiMetricsDto {
  leadTime?: ApiLeadTimeMetricsDto;
  throughput?: ApiThroughputMetricsDto;
  flowEfficiency?: ApiFlowEfficiencyMetricsDto;
  frequency?: ApiFrequencyMetricsDto;
  /** Общая стоимость проекта (сумма по всем воркерам). */
  totalCost?: number;
  /** Стоимость полезной работы (сумма по всем воркерам). */
  workCost?: number;
  /** Стоимость простоя (сумма по всем воркерам). */
  bufferCost?: number;
}

/**
 * DTO для конфигурации симуляции (без циклических ссылок).
 * Используется для описания параметров симуляции: воркфлоу, воркеры, задачи.
 * Базовый тип для KanbanFlowApi.Dtos.Config.StartSimulationRequestDto — набор полей конфигурации один и тот
 * же что при запуске, что внутри состояния запущенной симуляции, отличается только тем,
 * что запрос на запуск дополнительно несёт одноразовую инструкцию DaysToSimulate.
 */
export interface ApiSimulationConfigDto {
  /** Seed для генератора случайных чисел (воспроизводимость симуляции). */
  seed?: number;
  /** Список исполнителей (воркеров). */
  workers?: ApiWorkerDto[] | null;
  workflow?: ApiWorkflowDto;
  /** Список задач для симуляции. */
  tasks?: ApiTaskDto[] | null;
  /** Использовать ли вариативность при расчёте времени выполнения задач. */
  useVariability?: boolean;
}

/**
 * DTO для полного состояния симуляции (конфигурация + доска + история + текущий день/тик).
 * Используется для итеративной симуляции: POST /api/simulation/simulate-day принимает и возвращает это состояние.
 */
export interface ApiSimulationStateDto {
  config?: ApiSimulationConfigDto;
  board?: ApiBoardDto;
  /** История симуляции по дням. */
  history?: ApiHistoryDayDto[] | null;
  /** Текущий день симуляции (0 = ещё не началась). */
  currentDay?: number;
  /** Количество вызовов Random.NextDouble для детерминированной перемотки. */
  randomCallCount?: number;
}

/** DTO для стадии workflow (без циклических ссылок). */
export interface ApiStageDto {
  /** Имя стадии. */
  name?: string | null;
  type?: StageType;
  /** Является ли стадия началом для измерения Lead Time. */
  isLeadTimeStart?: boolean;
  /** WIP-лимит (максимум задач одновременно). Null = без лимита. */
  wipLimit?: number | null;
  /**
   * Навыки, требуемые для работы на стадии.
   * Например: ["backend"], ["qa-manual"], ["qa-auto"].
   */
  requiredSkills?: string[] | null;
  /** Требует ли стадия отдельного ресурса (например, Code Review). */
  requiresDifferentResource?: boolean;
  /** Имя стадии, от которой требуется отдельный ресурс. */
  requiresDifferentResourceFromStage?: string | null;
  /** Процент прогресса, который даёт стадия (для Work-стадий). */
  stageProgressPercent?: number;
  /**
   * Создаёт ли стадия ценность для бизнеса.
   * Например: Developing = true, Testing = true, Code Review = false.
   */
  createsValue?: boolean;
  /** Список переходов в другие стадии с вероятностями. */
  transitions?: ApiStageTransitionDto[] | null;
}

/** Переход из стадии в другую стадию. */
export interface ApiStageTransitionDto {
  /** Имя целевой стадии. */
  targetStageName?: string | null;
  /** Вероятность перехода (0.0 - 1.0). */
  probability?: number;
}

/** DTO для назначения задачи воркеру. */
export interface ApiTaskAssignmentDto {
  /** Ключ задачи. */
  taskKey?: string | null;
  /** Имя стадии, на которой воркер работает над задачей. */
  stageName?: string | null;
  /** Сколько дней требуется для выполнения задачи. */
  daysRequired?: number;
  /** Сколько дней уже отработано. */
  daysWorked?: number;
}

/** DTO для задачи (тип, описание). */
export interface ApiTaskDto {
  /** Уникальный ключ задачи (например, TASK-1). */
  key?: string | null;
  /** Краткое описание задачи. */
  summary?: string | null;
  shirtType?: TShirtType;
  /**
   * Навыки, необходимые для выполнения задачи.
   * Например: ["backend"], ["frontend", "react"], ["qa-manual"].
   * Задача подходит стадии, если есть хотя бы один общий навык.
   */
  requiredSkills?: string[] | null;
  /** Дочерние задачи (для иерархических структур). */
  children?: ApiTaskDto[] | null;
  /** Предпочтительные исполнители для стадий (ключ: имя стадии, значение: логин воркера). */
  acceptableWorkers?: Record<string, string> | null;
}

/** DTO для пропускной способности за один день. */
export interface ApiThroughputDayDto {
  /** Номер дня. */
  dayNumber?: number;
  /** Количество завершённых задач в этот день. */
  completedTasksCount?: number;
}

/** DTO для метрик Throughput (пропускная способность). */
export interface ApiThroughputMetricsDto {
  /** Общая пропускная способность (задач в день). */
  overall?: number;
  /** История пропускной способности по дням. */
  dailyHistory?: ApiThroughputDayDto[] | null;
}

/** DTO для исполнителя (воркера). */
export interface ApiWorkerDto {
  /** Логин исполнителя. */
  login?: string | null;
  /** Навыки исполнителя. Например: ["backend", "api"], ["frontend", "react"], ["qa-manual", "qa-auto"]. */
  skills?: string[] | null;
  /** Персональный WIP-лимит (максимум задач одновременно). Null = без лимита. */
  wipLimit?: number | null;
  /** Производительность (100 = базовая, 150 = на 50% быстрее). */
  performance?: number;
  /** Отклонение вниз в процентах (на сколько % может быть быстрее базовой оценки). */
  deviationDownPercent?: number;
  /** Отклонение вверх в процентах (на сколько % может быть медленнее базовой оценки). */
  deviationUpPercent?: number;
  /** Стоимость дня работы исполнителя (в условных единицах). */
  costPerDay?: number;
}

/** DTO для метрик работника. */
export interface ApiWorkerMetricsDto {
  /** Логин работника. */
  login?: string | null;
  /** Throughput — количество задач с ценными стадиями, завершённых за период / количество дней. */
  throughput?: number;
  /** Lead Time — среднее время задач (от isLeadTimeStart до Done/сейчас), где работник участвовал в ценной стадии. */
  leadTime?: number;
  /** Количество задач с ценными стадиями, где работник участвовал. */
  valuableTasksCount?: number;
  /** Flow Efficiency — процент активного времени (Work стадии) от общего. */
  efficiencyPercent?: number;
  /** Активное время (на Work стадиях) в днях. */
  workTimeDays?: number;
  /** Время ожидания (на Buffer стадиях) в днях. */
  bufferTimeDays?: number;
  /** Стоимость дня работы исполнителя (в условных единицах). */
  costPerDay?: number;
  /** Общая стоимость работы исполнителя (WorkCost + BufferCost). */
  totalCost?: number;
  /** Стоимость полезной работы (Work-стадии). */
  workCost?: number;
  /** Стоимость простоя (Buffer-стадии). */
  bufferCost?: number;
}

/** DTO для workflow (набор стадий и переходов). */
export interface ApiWorkflowDto {
  /** Список стадий workflow. */
  stages?: ApiStageDto[] | null;
}

/** DTO для пресета производственного процесса — содержит workflow и задачи по умолчанию. */
export interface ProcessPresetDto {
  /** Уникальное имя пресета (ключ для загрузки). */
  name?: string | null;
  /** Отображаемое название пресета. */
  displayName?: string | null;
  /** Описание пресета. */
  description?: string | null;
  /** Является ли этот пресет пресетом по умолчанию. */
  isDefault?: boolean;
  workflow?: ApiWorkflowDto;
  /** Задачи по умолчанию для этого процесса. */
  tasks?: ApiTaskDto[] | null;
}

/** DTO для агрегированных метрик стадий (P50, P85) по всем задачам. */
export interface StageMetricsAggregatedDto {
  /** Название стадии. */
  stageName?: string | null;
  /** Тип стадии (Work, Buffer). */
  stageType?: string | null;
  /** P50 (медиана) времени в стадии в днях. */
  p50Days?: number;
  /** P85 времени в стадии в днях. */
  p85Days?: number;
  /** P95 времени в стадии в днях. */
  p95Days?: number;
  /** Среднее время в стадии в днях. */
  avgDays?: number;
  /** Максимальное время в стадии в днях. */
  maxDays?: number;
  /** Количество задач, прошедших через стадию. */
  taskCount?: number;
}

/** DTO для метрик стадии в рамках задачи. */
export interface StageMetricsDto {
  /** Название стадии. */
  stageName?: string | null;
  /** Тип стадии (Work, Buffer). */
  stageType?: string | null;
  /** Время проведённое в стадии в днях. */
  timeInStageDays?: number;
  /** Воркер(и), которые работали над задачей на этой стадии. */
  workers?: string[] | null;
}

export type StageType = "Work" | "Buffer";

/**
 * DTO для запроса на запуск симуляции с полной конфигурацией.
 * Backend stateless — конфигурация передаётся полностью с клиента.
 * Набор полей конфигурации (Seed/UseVariability/Workflow/Workers/Tasks) наследуется от
 * KanbanFlowApi.Dtos.Config.ApiSimulationConfigDto — это та же конфигурация, что живёт внутри состояния
 * запущенной симуляции, плюс одноразовая инструкция DaysToSimulate. JSON-формат при этом не
 * меняется: System.Text.Json сериализует унаследованные свойства в тот же плоский объект.
 */
export interface StartSimulationRequestDto {
  /** Seed для генератора случайных чисел (воспроизводимость симуляции). */
  seed?: number;
  /** Список исполнителей (воркеров). */
  workers?: ApiWorkerDto[] | null;
  workflow?: ApiWorkflowDto;
  /** Список задач для симуляции. */
  tasks?: ApiTaskDto[] | null;
  /** Использовать ли вариативность при расчёте времени выполнения задач. */
  useVariability?: boolean;
  /**
   * Количество дней для симуляции (опционально).
   * Если null - симуляция выполняется на 1 день.
   * Если 0 - симуляция выполняется до завершения всех задач.
   * Если > 0 - симуляция выполняется на указанное количество дней.
   */
  daysToSimulate?: number | null;
}

export type TShirtType = "XS" | "S" | "M" | "L" | "XL";

/** DTO для метрик отдельной задачи. */
export interface TaskMetricsDto {
  /** Ключ задачи (например, TASK-1). */
  taskKey?: string | null;
  /** Краткое описание задачи. */
  summary?: string | null;
  /** Размер задачи (S, M, L, XL). */
  shirtType?: string | null;
  /** Lead Time задачи в днях (от первой стадии до Done). */
  leadTimeDays?: number;
  /** Flow Efficiency задачи в процентах. */
  flowEfficiencyPercent?: number;
  /** Активное время (работа) в днях. */
  activeTimeDays?: number;
  /** Время ожидания (буфер) в днях. */
  waitTimeDays?: number;
  /** Статус задачи (In Progress, Done). */
  status?: string | null;
  /** Детальная информация по стадиям. */
  stages?: StageMetricsDto[] | null;
}

/**
 * DTO для пресета «грейда» воркера — готовый набор Performance/Deviation/CostPerDay
 * для конкретной роли (backend/frontend/qa) и уровня (стажёр..лид).
 * Используется фронтом только для одноразового заполнения полей воркера (quick-fill) —
 * после применения значения становятся обычными редактируемыми числами.
 */
export interface WorkerGradePresetDto {
  /** Уникальное имя пресета (ключ для загрузки). */
  name?: string | null;
  /** Отображаемое название пресета. */
  displayName?: string | null;
  /** Описание пресета. */
  description?: string | null;
  /** Является ли этот пресет пресетом по умолчанию. */
  isDefault?: boolean;
  /** Роль, для которой подобраны параметры. Например: "backend", "frontend", "qa". */
  role?: string | null;
  /** Уровень грейда. Например: "intern", "junior", "middle", "senior", "lead". */
  grade?: string | null;
  /** Производительность (100 = базовая, 150 = на 50% быстрее). */
  performance?: number;
  /** Отклонение вниз в процентах (на сколько % может быть быстрее базовой оценки). */
  deviationDownPercent?: number;
  /** Отклонение вверх в процентах (на сколько % может быть медленнее базовой оценки). */
  deviationUpPercent?: number;
  /** Стоимость дня работы исполнителя (в условных единицах). */
  costPerDay?: number;
}

/** DTO для пресета пула работников — содержит набор исполнителей. */
export interface WorkerPoolPresetDto {
  /** Уникальное имя пресета (ключ для загрузки). */
  name?: string | null;
  /** Отображаемое название пресета. */
  displayName?: string | null;
  /** Описание пресета. */
  description?: string | null;
  /** Является ли этот пресет пресетом по умолчанию. */
  isDefault?: boolean;
  /** Список работников в пуле. */
  workers?: ApiWorkerDto[] | null;
}
