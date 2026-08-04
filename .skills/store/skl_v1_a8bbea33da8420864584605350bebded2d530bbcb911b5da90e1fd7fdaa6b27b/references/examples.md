# Example scenarios

Concrete walkthroughs of typical requests. Use them as templates for similar situations.

## Scenario 1 — documenting a new feature

**Request:** «задокументируй фичу обзора статистики в домене self-service».

1. Read `docs/domain/self-service/index.md` and one existing detail page to match style.
2. Create `docs/domain/self-service/statistics.md`:
    * frontmatter (title, sidebar_label, sidebar_position),
    * anchors on every `##` and `###`,
    * tables of parameters and statuses with exact types from code,
    * SQL examples if relevant.
3. Update `docs/domain/self-service/index.md`:
    * add the entity to the entities table,
    * add new terms to the glossary,
    * add a link to the new page in the «Разделы» section.
4. If a new REST endpoint appeared — update `docs/interfaces/rest/`.
5. If a new cache appeared — update `docs/interfaces/cache/`.
6. If a new config property appeared — update `docs/operations/configuration.md`.
7. If a new alert appeared — create a runbook in `docs/operations/runbooks/`.

Print the planned file list to chat and wait for confirmation before writing anything.

## Scenario 2 — reviewing branch changes

**Request:** «проверь документацию в моей ветке».

1. Run `git diff master...HEAD -- '*.md'`. If the base branch is unclear (could be `main`) — ask.
2. For each changed fragment check:
    * spelling and punctuation,
    * compliance with the editorial policy,
    * preservation of technical markers,
    * correct list formatting.
3. Print the list of corrections to chat with quoted rules from `editorial-policy.md`.
4. Wait for confirmation before applying.

## Scenario 3 — new REST endpoint

**Request:** «добавили `POST /api/v1/campaigns/{id}/pause` — обнови доки».

1. Read `docs/interfaces/rest/` to find the right page (usually `endpoints.md` or a per-resource file).
2. Update the consumers table with: path, method, auth, rate limit, consumers list.
3. If the endpoint changes business behavior — update the relevant `docs/domain/<domain>/<feature>.md`.
4. If the endpoint emits a Kafka event — update `docs/interfaces/async/`.

## Scenario 4 — new alert

**Request:** «настроили алерт `HighErrorRate` на сервис».

1. Create `docs/operations/runbooks/high-error-rate.md` with sections:
    * **Симптомы** — what the alert detects, thresholds.
    * **Влияние** — business impact.
    * **Диагностика** — dashboards, log queries, common causes.
    * **Действия** — step-by-step mitigation.
    * **Эскалация** — who to contact and when.
2. Update `docs/operations/monitoring.md` — add the alert to the alerts table with a link to the runbook.

## Scenario 5 — ADR

**Request:** «зафиксируй решение перейти с RabbitMQ на Kafka».

1. Read existing ADRs in `docs/architecture/adr/` to match numbering and template.
2. Create `docs/architecture/adr/NNNN-switch-to-kafka.md` with sections:
    * **Статус** — Proposed / Accepted / Superseded.
    * **Контекст** — what problem we are solving.
    * **Решение** — what we decided.
    * **Последствия** — positive and negative consequences.
    * **Альтернативы** — options considered and why rejected.
3. If the decision affects the integration map — update `docs/architecture/integrations/`.
