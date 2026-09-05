#!/usr/bin/env node
// Генератор TS/JSDoc-типов фронтенда из OpenAPI-схемы бэкенда.
//
// Поток:
//   1. dotnet build + MSBuild-таргет GenerateOpenApiDocuments (Swashbuckle CLI,
//      без запуска сервера) -> KanbanFlowApi/openapi/KanbanFlowApi.json
//   2. этот скрипт читает схему и пишет KanbanFlowApi/wwwroot/api-types.d.ts
//      (ambient-модуль с interface/type на каждый DTO + enum).
//
// Оба артефакта коммитятся. CI гоняет `--check`: перегенерирует и падает, если
// в рабочем дереве что-то разъехалось (значит, кто-то поменял C#-DTO и забыл
// перегенерировать типы для фронта).
//
// Использование:
//   node tools/generate-api-types.mjs            # build + генерация
//   node tools/generate-api-types.mjs --no-build # только генерация из готового json
//   node tools/generate-api-types.mjs --check    # build + проверка без записи (для CI)

import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const REPO_ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const CSPROJ = join(REPO_ROOT, 'KanbanFlowApi', 'KanbanFlowApi.csproj');
const OPENAPI_JSON = join(REPO_ROOT, 'KanbanFlowApi', 'openapi', 'KanbanFlowApi.json');
const OUT_DTS = join(REPO_ROOT, 'KanbanFlowApi', 'wwwroot', 'api-types.d.ts');

const args = new Set(process.argv.slice(2));
const noBuild = args.has('--no-build');
const checkOnly = args.has('--check');

function runDotnet(extraArgs) {
  execFileSync('dotnet', ['build', CSPROJ, '-c', 'Debug', '--nologo', '-v', 'q', ...extraArgs], {
    cwd: REPO_ROOT,
    stdio: 'inherit',
  });
}

if (!noBuild) {
  console.log('> dotnet build KanbanFlowApi');
  runDotnet([]);
  console.log('> dotnet build -t:GenerateOpenApiDocuments');
  runDotnet(['-t:GenerateOpenApiDocuments']);
}

if (!existsSync(OPENAPI_JSON)) {
  console.error(`OpenAPI-схема не найдена: ${OPENAPI_JSON}\nЗапусти без --no-build.`);
  process.exit(1);
}

const spec = JSON.parse(readFileSync(OPENAPI_JSON, 'utf8'));
const schemas = spec.components?.schemas ?? {};

const refName = (ref) => ref.replace('#/components/schemas/', '');

/** OpenAPI-схема свойства -> строка TS-типа. */
function tsType(schema) {
  if (!schema) return 'unknown';
  if (schema.$ref) return refName(schema.$ref);
  if (Array.isArray(schema.enum)) {
    return schema.enum.map((v) => JSON.stringify(v)).join(' | ');
  }
  switch (schema.type) {
    case 'integer':
    case 'number':
      return 'number';
    case 'string':
      return 'string';
    case 'boolean':
      return 'boolean';
    case 'array': {
      const item = tsType(schema.items);
      return /[ |]/.test(item) ? `Array<${item}>` : `${item}[]`;
    }
    case 'object': {
      const ap = schema.additionalProperties;
      if (ap && typeof ap === 'object') return `Record<string, ${tsType(ap)}>`;
      return 'Record<string, unknown>';
    }
    default:
      return 'unknown';
  }
}

/** Многострочный JSDoc-блок с отступом, либо '' если описания нет. */
function jsDoc(description, indent) {
  if (!description) return '';
  const lines = String(description).trim().split('\n');
  if (lines.length === 1) return `${indent}/** ${lines[0]} */\n`;
  return `${indent}/**\n${lines.map((l) => `${indent} * ${l}`.trimEnd()).join('\n')}\n${indent} */\n`;
}

function emitEnum(name, schema) {
  const doc = jsDoc(schema.description, '');
  const union = schema.enum.map((v) => JSON.stringify(v)).join(' | ');
  return `${doc}export type ${name} = ${union};\n`;
}

function emitInterface(name, schema) {
  const doc = jsDoc(schema.description, '');
  const required = new Set(schema.required ?? []);
  const props = schema.properties ?? {};
  const body = Object.entries(props)
    .map(([propName, propSchema]) => {
      const nullable = propSchema.nullable === true;
      const optional = nullable || !required.has(propName);
      let type = tsType(propSchema);
      if (nullable) type += ' | null';
      return `${jsDoc(propSchema.description, '  ')}  ${propName}${optional ? '?' : ''}: ${type};`;
    })
    .join('\n');
  return `${doc}export interface ${name} {\n${body}\n}\n`;
}

const header = `// ВНИМАНИЕ: файл сгенерирован из OpenAPI-схемы бэкенда. Руками не править.
// Перегенерировать:  node tools/generate-api-types.mjs
// Источник:          KanbanFlowApi/openapi/KanbanFlowApi.json  (из C#-DTO через Swashbuckle)
// Подробнее:         docs/api-contract.md
//
// Использование во фронтовых .js:
//   /** @typedef {import('./api-types').ApiSimulationStateDto} ApiSimulationStateDto */
//   /** @type {import('./api-types').StartSimulationRequestDto} */

`;

const blocks = Object.keys(schemas)
  .sort()
  .map((name) => {
    const schema = schemas[name];
    return Array.isArray(schema.enum) ? emitEnum(name, schema) : emitInterface(name, schema);
  });

const output = header + blocks.join('\n');

if (checkOnly) {
  const current = existsSync(OUT_DTS) ? readFileSync(OUT_DTS, 'utf8') : '';
  if (current !== output) {
    console.error(
      'api-types.d.ts устарел относительно C#-DTO.\n' +
        'Запусти `node tools/generate-api-types.mjs` и закоммить KanbanFlowApi/openapi/ + KanbanFlowApi/wwwroot/api-types.d.ts.',
    );
    process.exit(1);
  }
  console.log('api-types.d.ts актуален.');
} else {
  writeFileSync(OUT_DTS, output);
  console.log(`Записано ${OUT_DTS} (${blocks.length} типов).`);
}
