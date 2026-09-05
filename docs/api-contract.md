# Контракт API между бэкендом (C#) и фронтендом (JS)

Раньше формат JSON нигде не был зафиксирован — фронт «читал C#-класс и угадывал».
Отсюда все баги на границе бэк/фронт этого проекта (`costPerDay`, `createsValue`,
формат `transitions`). Теперь контракт генерируется из C#-DTO и коммитится, а CI
падает при рассинхроне.

## Как это устроено

```
C#-DTO (+ XML-докстринги)
      │  Swashbuckle + Microsoft.Extensions.ApiDescription.Server
      ▼
KanbanFlowApi/openapi/KanbanFlowApi.json      ← OpenAPI 3.0, коммитится
      │  tools/generate-api-types.mjs (Node, без зависимостей)
      ▼
KanbanFlowApi/wwwroot/api-types.d.ts          ← TS-типы для фронта, коммитится
      │  /** @typedef {import('./api-types').X} X */  +  jsconfig.json
      ▼
IDE/редактор: автодополнение и проверка типов по JSDoc во фронтовых .js
```

- **OpenAPI-схема** генерируется MSBuild-таргетом `GenerateOpenApiDocuments`
  (пакет `Microsoft.Extensions.ApiDescription.Server`) — **без запуска сервера**.
  Таргет вызывается явно из скрипта, не на каждый `dotnet build`
  (`OpenApiGenerateDocumentsOnBuild` намеренно не включён).
- **XML-докстринги** DTO попадают в схему и дальше в `.d.ts` как JSDoc-комментарии
  (`GenerateDocumentationFile=true` в `KanbanFlowApi.csproj`, `IncludeXmlComments`
  в `Program.cs`). `CS1591` заглушён — документируем только контракт.
- **Генератор** `tools/generate-api-types.mjs` — чистый Node, без `package.json` и
  `node_modules`. Маппинг: `object → interface`, string-`enum → union литералов`,
  `nullable → ?: T | null`, `additionalProperties → Record<string, T>`.

## Как пользоваться во фронте

```js
/** @typedef {import('./api-types').ApiSimulationStateDto} ApiSimulationStateDto */
/** @typedef {import('./api-types').StartSimulationRequestDto} StartSimulationRequestDto */

/** @type {StartSimulationRequestDto} */
const request = { seed: 42, workflow, workers, tasks, daysToSimulate };

/** @returns {Promise<ApiSimulationStateDto>} */
async function requestSimulationStart(days) { /* ... */ }
```

`KanbanFlowApi/wwwroot/jsconfig.json` задаёт границы JS-проекта для редактора.
`checkJs` намеренно `false` (большие исторические файлы без типов) — включай
проверку точечно через `// @ts-check` в начале файла.

## Регенерация

```bash
node tools/generate-api-types.mjs            # dotnet build + генерация обоих артефактов
node tools/generate-api-types.mjs --no-build # только генерация из готового json
node tools/generate-api-types.mjs --check    # CI: проверка без записи, ненулевой код при расхождении
```

Поменял C#-DTO → запусти генератор → закоммить `KanbanFlowApi/openapi/` и
`KanbanFlowApi/wwwroot/api-types.d.ts` вместе с изменением DTO. CI-шаг
**Check API types are up to date** в `.github/workflows/build.yml` перегенерирует
и делает `git diff --exit-code`.

## Что можно улучшить дальше

- Докстринги enum-ов из `KanbanFlowSerivce` (`ActivityType`, `StageType`,
  `TShirtType`) в схему не попадают — у самого проекта `KanbanFlowSerivce` нет
  `GenerateDocumentationFile`. Значения говорящие, но при желании подключается так же.
- Типизировать тело запросов/ответов по эндпоинтам (сейчас типы только на уровне
  схем DTO, не операций).
- Включить `// @ts-check` в `app.js` / `config-editor.js` после разбора текущих
  ошибок типов.
