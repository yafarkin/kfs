// KanbanFlow Configuration Editor
// Единый редактор конфигурации: процесс + команда + задачи

// ============================================================================
// Переключение вкладок
// ============================================================================

function switchConfigTab(tabName) {
    
    // Скрываем все вкладки
    document.querySelectorAll('.config-tab-pane').forEach(pane => {
        pane.classList.remove('active');
    });
    
    // Убираем активный класс у кнопок
    document.querySelectorAll('.config-tab-btn').forEach(btn => {
        btn.classList.remove('active');
    });
    
    // Показываем нужную вкладку
    const tabPane = document.getElementById(`${tabName}TabContent`);
    if (tabPane) {
        tabPane.classList.add('active');
    }
    
    // Подсвечиваем кнопку
    const activeBtn = document.querySelector(`.config-tab-btn[onclick="switchConfigTab('${tabName}')"]`);
    if (activeBtn) {
        activeBtn.classList.add('active');
    }
    
    // Рендерим контент вкладки при переключении
    try {
        if (tabName === 'process') {
            renderStages();
        } else if (tabName === 'workers') {
            renderWorkersEditor();
        } else if (tabName === 'tasks') {
            renderTasks();
        }
    } catch (error) {
        console.error('[switchConfigTab] Error rendering:', error);
    }
}

// ============================================================================
// Глобальные переменные
// ============================================================================
// workflow.stages — переходы хранятся внутри каждой стадии (stage.transitions), в том же
// формате, что и backend API (ApiStageDto.Transitions) — отдельного плоского списка нет.
let configEditorData = {
    workflow: { stages: [] },
    workers: [],
    tasks: []
};

// Пресеты грейдов воркера (роль+уровень), подгружаются один раз при открытии редактора —
// используются только для quick-fill полей Performance/Deviation/CostPerDay.
let workerGradePresets = [];

// Диапазон дней (min, max) по T-Shirt размеру задачи — зеркалит
// KanbanFlowSerivce/Enums/TShirtTypeExtensions.GetDaysToComplete на бэке.
// Используется только для оценочного счётчика "общая длительность" в редакторе задач,
// на сам расчёт симуляции не влияет — если поменяете диапазоны на бэке, поправьте и тут.
const SHIRT_TYPE_DAYS_RANGE = {
    XS: [1, 1],
    S: [2, 3],
    M: [4, 6],
    L: [7, 15],
    XL: [16, 30]
};

// ============================================================================
// Открытие/закрытие модального окна
// ============================================================================

function openConfigEditor() {

    // Приоритет загрузки данных:
    // 1. configTemplate (редактируемый шаблон) — полный конфиг с workflow, workers, tasks
    // 2. Пустая конфигурация (если configTemplate сброшен)
    // simulationState.config НЕ используется чтобы не загружать старую симуляцию

    let workflow = null;
    let workers = null;
    let tasks = null;

    // Берём только из configTemplate
    if (configTemplate) {
        workflow = configTemplate.workflow;
        workers = configTemplate.workers;
        tasks = configTemplate.tasks;
    }


    // Глубокое копирование для редактирования (переходы уже вложены в стадии)
    configEditorData = {
        workflow: workflow ? {
            stages: workflow.stages ? JSON.parse(JSON.stringify(workflow.stages)) : []
        } : { stages: [] },
        workers: workers ? JSON.parse(JSON.stringify(workers)) : [],
        tasks: tasks ? JSON.parse(JSON.stringify(tasks)) : []
    };


    // Показываем модальное окно
    const modal = document.getElementById('configEditorModal');

    if (modal) {
        modal.classList.add('show');
        modal.style.display = 'flex';

        // Загружаем шаблоны процессов и воркеров
        loadProcessTemplates();
        loadWorkerTemplates();
        loadWorkerGradePresets();

        // Рендерим все вкладки сразу (без setTimeout)
        try {
            renderStages();
        } catch (e) { console.error('renderStages error:', e); }

        try {
            renderWorkersEditor();
        } catch (e) { console.error('renderWorkersEditor error:', e); }

        try {
            renderTasks();
        } catch (e) { console.error('renderTasks error:', e); }

    }
}

function closeConfigEditor() {
    const modal = document.getElementById('configEditorModal');
    if (modal) {
        modal.classList.remove('show');
        modal.style.display = 'none';
    }
}

// ============================================================================
// Сохранение конфигурации
// ============================================================================

function saveConfigFromEditor() {
    try {
        // Проверяем валидность конфигурации
        if (!configEditorData.workflow.stages || configEditorData.workflow.stages.length === 0) {
            showToast('Добавьте хотя бы одну стадию', 'warning');
            return;
        }

        if (!configEditorData.workers || configEditorData.workers.length === 0) {
            showToast('Добавьте хотя бы одного воркера', 'warning');
            return;
        }

        // Логины воркеров должны быть уникальны и непустыми — иначе backend падает
        // (ApiMapper.ToDomainBoard ищет воркера по логину через .Single), а UI просто
        // показывает малопонятную ошибку симуляции на первом же дне.
        const workerLogins = configEditorData.workers.map(w => (w.login || '').trim());
        if (workerLogins.some(l => !l)) {
            showToast('У всех воркеров должен быть логин', 'danger');
            return;
        }
        const dupLogin = workerLogins.find((l, i) => workerLogins.indexOf(l) !== i);
        if (dupLogin) {
            showToast(`Дублирующийся логин воркера: ${dupLogin}. Логины должны быть уникальны.`, 'danger');
            return;
        }

        if (!configEditorData.tasks || configEditorData.tasks.length === 0) {
            showToast('Добавьте хотя бы одну задачу', 'warning');
            return;
        }

        // Ключи задач должны быть уникальны и непустыми — иначе backend падает
        // (ApiMapper.ToDomainBoard ищет задачу по ключу через .Single).
        const taskKeys = configEditorData.tasks.map(t => (t.key || '').trim());
        if (taskKeys.some(k => !k)) {
            showToast('У всех задач должен быть ключ', 'danger');
            return;
        }
        const dupKey = taskKeys.find((k, i) => taskKeys.indexOf(k) !== i);
        if (dupKey) {
            showToast(`Дублирующийся ключ задачи: ${dupKey}. Ключи должны быть уникальны.`, 'danger');
            return;
        }

        // Сохраняем в configTemplate
        if (!configTemplate) {
            configTemplate = {};
        }

        configTemplate.workflow = configEditorData.workflow;
        configTemplate.workers = configEditorData.workers;
        configTemplate.tasks = configEditorData.tasks;

        // Сохраняем в LocalStorage
        saveConfigTemplateToStorage();

        // Закрываем редактор
        closeConfigEditor();

        showToast('Конфигурация сохранена', 'success');

        // Если симуляция уже запущена, предлагаем перезагрузить
        if (simulationState) {
            showToast('Нажмите "Перезагрузить" для применения конфигурации', 'info');
        }

    } catch (error) {
        console.error('Error saving config:', error);
        showToast('Ошибка сохранения: ' + error.message, 'danger');
    }
}

// ============================================================================
// Вкладка: Процесс (стадии и переходы)
// ============================================================================

function renderStages() {
    const container = document.getElementById('stagesContainer');
    if (!container) {
        console.error('[renderStages] Container not found!');
        return;
    }

    const stages = configEditorData.workflow.stages || [];

    if (stages.length === 0) {
        container.innerHTML = '<div class="text-muted text-center py-4"><i class="bi bi-inbox me-2"></i>Нет стадий. Добавьте первую стадию.</div>';
        return;
    }

    let html = '';
    stages.forEach((stage, index) => {
        // API возвращает lowercase поля: name, type, wipLimit, isLeadTimeStart
        const stageName = stage.name || stage.Name || '';
        const stageType = stage.type || stage.Type || 'Buffer';
        const wipLimit = stage.wipLimit ?? stage.WipLimit;
        const isLeadTimeStart = stage.isLeadTimeStart ?? stage.IsLeadTimeStart ?? false;
        const stageProgressPercent = stage.stageProgressPercent ?? stage.StageProgressPercent ?? 100;
        const requiredSkills = stage.requiredSkills || stage.RequiredSkills || [];
        const requiredSkillsString = Array.isArray(requiredSkills) ? requiredSkills.join(', ') : '';
        const createsValue = stage.createsValue ?? stage.CreatesValue ?? (stageType === 'Work');

        const stageTypeClass = stageType === 'Work' ? 'work' : 'buffer';
        const stageTypeLabel = stageType === 'Work' ? 'Рабочая' : 'Буфер';

        // Переходы хранятся внутри самой стадии
        const stageTransitions = stage.transitions || stage.Transitions || [];

        html += `
            <div class="config-item" data-stage-index="${index}">
                <div class="config-item-header">
                    <div class="config-item-title">
                        <span class="badge bg-${stageTypeClass === 'work' ? 'warning' : 'info'} me-2">${stageTypeLabel}</span>
                        ${stageName || 'Без названия'}
                    </div>
                    <div class="config-item-actions">
                        <button class="btn btn-sm btn-outline-secondary" onclick="moveStageUp(${index})" ${index === 0 ? 'disabled' : ''} title="Выше">
                            <i class="bi bi-arrow-up"></i>
                        </button>
                        <button class="btn btn-sm btn-outline-secondary" onclick="moveStageDown(${index})" ${index === stages.length - 1 ? 'disabled' : ''} title="Ниже">
                            <i class="bi bi-arrow-down"></i>
                        </button>
                        <button class="btn btn-sm btn-outline-danger" onclick="deleteStage(${index})" title="Удалить">
                            <i class="bi bi-trash"></i>
                        </button>
                    </div>
                </div>
                <div class="config-item-body">
                    <div class="config-field">
                        <label>Название</label>
                        <input type="text" value="${escapeHtml(stageName)}" onchange="updateStage(${index}, 'name', this.value)">
                    </div>
                    <div class="config-field">
                        <label>Тип</label>
                        <select onchange="updateStage(${index}, 'type', this.value)">
                            <option value="Buffer" ${stageType === 'Buffer' ? 'selected' : ''}>Буфер</option>
                            <option value="Work" ${stageType === 'Work' ? 'selected' : ''}>Рабочая</option>
                        </select>
                    </div>
                    <div class="config-field">
                        <label>WIP-лимит</label>
                        <input type="number" value="${wipLimit ?? ''}" placeholder="∞" onchange="updateStage(${index}, 'wipLimit', this.value ? parseInt(this.value) : null)">
                    </div>
                    <div class="config-field">
                        <label>Lead Time Start</label>
                        <select onchange="updateStage(${index}, 'isLeadTimeStart', this.value === 'true')">
                            <option value="false" ${!isLeadTimeStart ? 'selected' : ''}>Нет</option>
                            <option value="true" ${isLeadTimeStart ? 'selected' : ''}>Да</option>
                        </select>
                    </div>
                    <div class="config-field">
                        <label>Создаёт ценность</label>
                        <select onchange="updateStage(${index}, 'createsValue', this.value === 'true')" ${stageType === 'Buffer' ? 'disabled' : ''}>
                            <option value="false" ${!createsValue ? 'selected' : ''}>Нет</option>
                            <option value="true" ${createsValue ? 'selected' : ''}>Да</option>
                        </select>
                        ${stageType === 'Buffer' ? '<small class="text-muted">Буферные стадии не создают ценность</small>' : ''}
                    </div>
                    ${stageType === 'Work' ? `
                    <div class="config-field">
                        <label>Прогресс за день (%)</label>
                        <input type="number" value="${stageProgressPercent}" min="0" max="100" onchange="updateStage(${index}, 'stageProgressPercent', parseInt(this.value) || 0)" title="Какой процент от размера задачи выполняется за 1 день">
                        <small class="text-muted">Процент от размера задачи: XS=1д, S=2-3д, M=4-6д, L=7-15д. 100% = задача выполняется за базовое время</small>
                    </div>
                    <div class="config-field">
                        <label>Навыки (через запятую)</label>
                        <input type="text" value="${escapeHtml(requiredSkillsString)}" onchange="updateStageRequiredSkills(${index}, this.value)" placeholder="backend, frontend, qa">
                        <small class="text-muted">Только воркеры с этими навыками смогут работать на стадии</small>
                    </div>
                    ` : ''}
                </div>

                <!-- Переходы -->
                <div class="config-sublist">
                    <div class="config-sublist-header">
                        <h5><i class="bi bi-arrow-right me-1"></i>Переходы из стадии</h5>
                        <button class="btn btn-sm btn-outline-primary" onclick="addTransition(${index})">
                            <i class="bi bi-plus-lg"></i> Добавить переход
                        </button>
                    </div>
                    ${renderTransitions(stageTransitions, index)}
                </div>
            </div>
        `;
    });

    container.innerHTML = html;
}

function renderTransitions(transitions, stageIndex) {
    if (transitions.length === 0) {
        return '<div class="text-muted small">Нет переходов</div>';
    }

    let html = '<div class="d-flex flex-wrap gap-2">';
    transitions.forEach((t, idx) => {
        // API возвращает lowercase: targetStageName, probability
        const targetStageName = t.targetStageName || t.TargetStageName || '';
        const probability = t.probability ?? t.Probability ?? 1;

        html += `
            <div class="skill-badge" style="cursor: pointer;" onclick="deleteTransition(${stageIndex}, ${idx})" title="Удалить переход">
                <i class="bi bi-arrow-right"></i> ${targetStageName} (${(probability * 100).toFixed(0)}%)
            </div>
        `;
    });
    html += '</div>';
    return html;
}

function addStage() {
    if (!configEditorData.workflow.stages) {
        configEditorData.workflow.stages = [];
    }

    const newStage = {
        name: `Stage ${configEditorData.workflow.stages.length + 1}`,
        type: 'Buffer',
        wipLimit: null,
        isLeadTimeStart: false,
        createsValue: false,  // Буферные стадии не создают ценность
        stageProgressPercent: 100,  // По умолчанию 100%
        requiredSkills: [],  // По умолчанию нет требований к навыкам
        transitions: []
    };

    configEditorData.workflow.stages.push(newStage);
    renderStages();
}

function deleteStage(index) {
    const stage = configEditorData.workflow.stages[index];
    const stageName = stage.name;

    // Удаляем саму стадию (вместе с её переходами) и переходы В неё из остальных стадий
    configEditorData.workflow.stages.splice(index, 1);
    configEditorData.workflow.stages.forEach(s => {
        s.transitions = (s.transitions || []).filter(t => t.targetStageName !== stageName);
    });

    renderStages();
}

function updateStage(index, field, value) {
    configEditorData.workflow.stages[index][field] = value;
    
    // Если изменили тип стадии — автоматически сбрасываем createsValue для буферов
    if (field === 'type' && value === 'Buffer') {
        configEditorData.workflow.stages[index].createsValue = false;
    }
    
    renderStages();
}

function updateStageRequiredSkills(index, value) {
    // Преобразуем строку "backend, frontend, qa" в массив ["backend", "frontend", "qa"]
    const skills = value
        .split(',')
        .map(s => s.trim())
        .filter(s => s.length > 0);
    configEditorData.workflow.stages[index].requiredSkills = skills;
    renderStages();
}

function moveStageUp(index) {
    if (index === 0) return;
    [configEditorData.workflow.stages[index - 1], configEditorData.workflow.stages[index]] =
    [configEditorData.workflow.stages[index], configEditorData.workflow.stages[index - 1]];
    renderStages();
}

function moveStageDown(index) {
    if (index === configEditorData.workflow.stages.length - 1) return;
    [configEditorData.workflow.stages[index], configEditorData.workflow.stages[index + 1]] =
    [configEditorData.workflow.stages[index + 1], configEditorData.workflow.stages[index]];
    renderStages();
}

function addTransition(stageIndex) {
    const fromStage = configEditorData.workflow.stages[stageIndex];
    if (!fromStage.transitions) {
        fromStage.transitions = [];
    }

    // Создаём переход к следующей стадии (если есть)
    let targetStageName = '';
    if (stageIndex < configEditorData.workflow.stages.length - 1) {
        targetStageName = configEditorData.workflow.stages[stageIndex + 1].name;
    } else {
        targetStageName = 'Done';
    }

    fromStage.transitions.push({
        targetStageName: targetStageName,
        probability: 1.0
    });

    renderStages();
}

function deleteTransition(stageIndex, transitionIndex) {
    const stage = configEditorData.workflow.stages[stageIndex];
    const transitions = stage?.transitions || [];
    if (transitionIndex >= 0 && transitionIndex < transitions.length) {
        transitions.splice(transitionIndex, 1);
        renderStages();
    }
}

// ============================================================================
// Вкладка: Команда (воркеры)
// ============================================================================

function renderWorkersEditor() {
    const container = document.getElementById('workersContainer');
    if (!container) {
        console.error('[renderWorkersEditor] Container not found!');
        return;
    }

    const workers = configEditorData.workers || [];

    if (workers.length === 0) {
        container.innerHTML = '<div class="text-muted text-center py-4"><i class="bi bi-inbox me-2"></i>Нет воркеров. Добавьте первого воркера.</div>';
        return;
    }

    let html = '';
    workers.forEach((worker, index) => {
        // Используем lowercase формат
        const login = worker.login || '';
        const skillsArray = worker.skills || [];
        const skillsString = Array.isArray(skillsArray) ? skillsArray.join(', ') : (skillsArray || '');
        const wipLimit = worker.wipLimit ?? 1;
        const performance = worker.performance ?? 100;
        const deviationDown = worker.deviationDownPercent ?? 0;
        const deviationUp = worker.deviationUpPercent ?? 0;
        const costPerDay = worker.costPerDay ?? worker.CostPerDay ?? 100;

        html += `
            <div class="config-item" data-worker-index="${index}">
                <div class="config-item-header">
                    <div class="config-item-title">
                        <i class="bi bi-person-circle me-2"></i>
                        ${login || 'Без имени'}
                    </div>
                    <div class="config-item-actions">
                        <button class="btn btn-sm btn-outline-secondary" onclick="moveWorkerUp(${index})" ${index === 0 ? 'disabled' : ''} title="Выше">
                            <i class="bi bi-arrow-up"></i>
                        </button>
                        <button class="btn btn-sm btn-outline-secondary" onclick="moveWorkerDown(${index})" ${index === workers.length - 1 ? 'disabled' : ''} title="Ниже">
                            <i class="bi bi-arrow-down"></i>
                        </button>
                        <button class="btn btn-sm btn-outline-secondary" onclick="cloneWorker(${index})" title="Склонировать">
                            <i class="bi bi-copy"></i>
                        </button>
                        <button class="btn btn-sm btn-outline-danger" onclick="deleteWorker(${index})" title="Удалить">
                            <i class="bi bi-trash"></i>
                        </button>
                    </div>
                </div>
                <div class="config-item-body">
                    <div class="config-field">
                        <label>Логин</label>
                        <input type="text" value="${escapeHtml(login)}" onchange="updateWorker(${index}, 'login', this.value)">
                    </div>
                    <div class="config-field">
                        <label>Навыки (через запятую)</label>
                        <input type="text" value="${escapeHtml(skillsString)}" onchange="updateWorkerSkills(${index}, this.value)" placeholder="backend, frontend, qa">
                    </div>
                    <div class="config-field">
                        <label>WIP-лимит</label>
                        <input type="number" value="${wipLimit}" onchange="updateWorker(${index}, 'wipLimit', parseInt(this.value) || 1)">
                    </div>
                    <div class="config-field">
                        <label>Грейд (быстрое заполнение)</label>
                        <select onchange="applyWorkerGrade(${index}, this.value)">
                            <option value="">— выбрать —</option>
                            ${workerGradePresets.map(preset => {
                                const presetName = preset.name || preset.Name;
                                const presetDisplayName = preset.displayName || preset.DisplayName || presetName;
                                return `<option value="${escapeHtml(presetName)}">${escapeHtml(presetDisplayName)}</option>`;
                            }).join('')}
                        </select>
                        <small class="text-muted">Подставляет Performance/Отклонения/Стоимость — поля ниже остаются редактируемыми вручную</small>
                    </div>
                    <div class="config-field">
                        <label>Performance (%)</label>
                        <input type="number" value="${performance}" min="0" max="200" onchange="updateWorker(${index}, 'performance', parseInt(this.value) || 100)">
                    </div>
                    <div class="config-field">
                        <label>Отклонение вниз (%)</label>
                        <input type="number" value="${deviationDown}" min="0" max="100" onchange="updateWorker(${index}, 'deviationDownPercent', parseInt(this.value) || 0)">
                    </div>
                    <div class="config-field">
                        <label>Отклонение вверх (%)</label>
                        <input type="number" value="${deviationUp}" min="0" max="100" onchange="updateWorker(${index}, 'deviationUpPercent', parseInt(this.value) || 0)">
                    </div>
                    <div class="config-field">
                        <label>Стоимость дня (¤)</label>
                        <input type="number" value="${costPerDay}" min="0" onchange="updateWorker(${index}, 'costPerDay', parseInt(this.value) || 0)">
                        <small class="text-muted">Стоимость одного дня работы воркера в условных единицах</small>
                    </div>
                </div>
            </div>
        `;
    });

    container.innerHTML = html;
}

// Загрузка пресетов грейдов воркера (роль+уровень) при открытии редактора
async function loadWorkerGradePresets() {
    try {
        const response = await fetch('/api/editor/workers/grade-presets');
        if (!response.ok) {
            console.error('Failed to load worker grade presets:', response.status);
            return;
        }
        workerGradePresets = await response.json();

        // Пресеты подгружаются асинхронно и могли не успеть к первому рендеру карточек —
        // перерисовываем, чтобы выпадающий список грейдов в карточках воркеров заполнился.
        renderWorkersEditor();
    } catch (error) {
        console.error('Error loading worker grade presets:', error);
    }
}

// Загрузка шаблонов воркеров при открытии редактора
async function loadWorkerTemplates() {
    try {
        const response = await fetch('/api/editor/workers/presets');
        if (!response.ok) {
            console.error('Failed to load worker templates:', response.status);
            return;
        }
        const templates = await response.json();
        
        const selector = document.getElementById('workerTemplateSelector');
        if (selector) {
            selector.innerHTML = '<option value="">— Выбрать шаблон —</option>';
            templates.forEach(template => {
                const option = document.createElement('option');
                option.value = template.name || template.Name;
                option.textContent = template.displayName || template.DisplayName || (template.name || template.Name);
                option.title = template.description || template.Description || '';
                selector.appendChild(option);
            });
        }
    } catch (error) {
        console.error('Error loading worker templates:', error);
    }
}

// Применение шаблона воркеров
async function loadWorkerTemplate(presetName) {
    if (!presetName) return;
    
    try {
        const response = await fetch(`/api/editor/workers/presets/${presetName}`);
        if (!response.ok) {
            console.error('Failed to load worker template:', presetName);
            return;
        }
        const template = await response.json();
        
        const workersData = template.workers || template.Workers;
        if (workersData && workersData.length > 0) {
            // Заменяем текущих воркеров на шаблон — используем только lowercase формат
            configEditorData.workers = workersData.map(w => ({
                login: w.login || w.Login,
                skills: w.skills || w.Skills || [],
                wipLimit: w.wipLimit ?? w.WipLimit ?? 1,
                performance: w.performance ?? w.Performance ?? 100,
                deviationDownPercent: w.deviationDownPercent ?? w.DeviationDownPercent ?? 0,
                deviationUpPercent: w.deviationUpPercent ?? w.DeviationUpPercent ?? 0,
                costPerDay: w.costPerDay ?? w.CostPerDay ?? 100
            }));
            renderWorkersEditor();
            
            // Сбрасываем селектор
            const selector = document.getElementById('workerTemplateSelector');
            if (selector) selector.value = '';
        }
    } catch (error) {
        console.error('Error applying worker template:', error);
    }
}

// Загрузка шаблонов процессов при открытии редактора
async function loadProcessTemplates() {
    try {
        const response = await fetch('/api/editor/processes/presets');
        if (!response.ok) {
            console.error('Failed to load process templates:', response.status);
            return;
        }
        const templates = await response.json();
        
        const selector = document.getElementById('processTemplateSelector');
        if (selector) {
            selector.innerHTML = '<option value="">— Выбрать шаблон —</option>';
            templates.forEach(template => {
                const option = document.createElement('option');
                option.value = template.name || template.Name;
                option.textContent = template.displayName || template.DisplayName || (template.name || template.Name);
                option.title = template.description || template.Description || '';
                selector.appendChild(option);
            });
        }
    } catch (error) {
        console.error('Error loading process templates:', error);
    }
}

// Применение шаблона процесса
async function loadProcessTemplate(presetName) {
    if (!presetName) return;
    
    try {
        const response = await fetch(`/api/editor/processes/presets/${presetName}`);
        if (!response.ok) {
            console.error('Failed to load process template:', presetName);
            return;
        }
        const template = await response.json();
        
        // workflow может быть в template.workflow или template.Workflow
        const workflowData = template.workflow || template.Workflow;
        if (!workflowData) {
            console.error('No workflow data in template');
            return;
        }
        
        // стадии могут быть в workflow.stages или workflow.Stages; переходы — внутри
        // каждой стадии (stage.transitions), как и в остальном приложении
        const stagesData = workflowData.stages || workflowData.Stages || [];

        if (stagesData.length > 0) {
            // Заменяем текущий workflow на шаблон — используем только один формат полей (как в API)
            const stages = stagesData.map(s => {
                const type = s.type || s.Type;
                const stageTransitions = s.transitions || s.Transitions || [];
                return {
                    name: s.name || s.Name,
                    type: type,
                    wipLimit: s.wipLimit ?? s.WipLimit,
                    isLeadTimeStart: s.isLeadTimeStart ?? s.IsLeadTimeStart ?? false,
                    stageProgressPercent: s.stageProgressPercent ?? s.StageProgressPercent ?? 100,
                    requiredSkills: s.requiredSkills || s.RequiredSkills || [],
                    createsValue: s.createsValue ?? s.CreatesValue ?? (type === 'Work'),
                    requiresDifferentResource: s.requiresDifferentResource ?? s.RequiresDifferentResource ?? false,
                    requiresDifferentResourceFromStage: s.requiresDifferentResourceFromStage ?? s.RequiresDifferentResourceFromStage ?? null,
                    transitions: stageTransitions.map(t => ({
                        targetStageName: t.targetStageName || t.TargetStageName,
                        probability: t.probability ?? t.Probability ?? 1
                    }))
                };
            });

            // Гарантируем, что только одна стадия имеет isLeadTimeStart = true
            let hasLeadTimeStart = false;
            stages.forEach(s => {
                if (s.isLeadTimeStart) {
                    if (hasLeadTimeStart) {
                        s.isLeadTimeStart = false; // Убираем дубликат
                    } else {
                        hasLeadTimeStart = true;
                    }
                }
            });

            configEditorData.workflow = { stages };
            renderStages();
            
            // Сбрасываем селектор
            const selector = document.getElementById('processTemplateSelector');
            if (selector) selector.value = '';
        }
    } catch (error) {
        console.error('Error applying process template:', error);
    }
}

function addWorker() {
    if (!configEditorData.workers) {
        configEditorData.workers = [];
    }

    const newWorker = {
        login: `worker${configEditorData.workers.length + 1}`,
        skills: [],
        wipLimit: 1,
        performance: 100,
        deviationDownPercent: 0,
        deviationUpPercent: 0
    };

    configEditorData.workers.push(newWorker);
    renderWorkersEditor();
}

function deleteWorker(index) {
    configEditorData.workers.splice(index, 1);
    renderWorkersEditor();
}

// Склонировать воркера: копия всех полей, кроме логина (логин должен быть уникален —
// backend отклоняет пресет с дублирующимися логинами). Копия вставляется сразу после
// оригинала, чтобы было видно, что это его клон.
function cloneWorker(index) {
    const original = configEditorData.workers[index];
    const clone = JSON.parse(JSON.stringify(original));
    clone.login = nextClonedWorkerLogin(original.login || 'worker');

    configEditorData.workers.splice(index + 1, 0, clone);
    renderWorkersEditor();
}

// Подбирает свободный логин вида "login-copy", "login-copy-2", "login-copy-3", ...
function nextClonedWorkerLogin(baseLogin) {
    const existingLogins = new Set((configEditorData.workers || []).map(w => w.login));

    let candidate = `${baseLogin}-copy`;
    let suffix = 2;
    while (existingLogins.has(candidate)) {
        candidate = `${baseLogin}-copy-${suffix}`;
        suffix++;
    }
    return candidate;
}

function updateWorker(index, field, value) {
    configEditorData.workers[index][field] = value;
    renderWorkersEditor();
}

function updateWorkerSkills(index, value) {
    const skills = value.split(',').map(s => s.trim()).filter(s => s);
    configEditorData.workers[index].skills = skills;
    renderWorkersEditor();
}

// Применение пресета грейда к воркеру — одноразовый quick-fill: подставляет числа
// в существующие поля, ничего не запоминает. Поля остаются доступны для ручной правки,
// и пресет можно применить повторно (селектор сбрасывается на плейсхолдер).
function applyWorkerGrade(index, presetName) {
    if (!presetName) return;

    const preset = workerGradePresets.find(p => (p.name || p.Name) === presetName);
    if (!preset) {
        console.error('Worker grade preset not found:', presetName);
        return;
    }

    const worker = configEditorData.workers[index];
    worker.performance = preset.performance ?? preset.Performance;
    worker.deviationDownPercent = preset.deviationDownPercent ?? preset.DeviationDownPercent;
    worker.deviationUpPercent = preset.deviationUpPercent ?? preset.DeviationUpPercent;
    worker.costPerDay = preset.costPerDay ?? preset.CostPerDay;

    renderWorkersEditor();
}

function moveWorkerUp(index) {
    if (index === 0) return;
    [configEditorData.workers[index - 1], configEditorData.workers[index]] =
    [configEditorData.workers[index], configEditorData.workers[index - 1]];
    renderWorkersEditor();
}

function moveWorkerDown(index) {
    if (index === configEditorData.workers.length - 1) return;
    [configEditorData.workers[index], configEditorData.workers[index + 1]] =
    [configEditorData.workers[index + 1], configEditorData.workers[index]];
    renderWorkersEditor();
}

// ============================================================================
// Вкладка: Задачи
// ============================================================================

// Суммарная оценочная длительность всех задач (сумма диапазонов min-max по каждой),
// чтобы было легче оценивать масштаб набора задач при ручной нарезке/генерации.
// Дети (task.children) не учитываются — движок их сейчас не разворачивает при симуляции,
// это просто вспомогательная группировка в модели.
function renderTasksTotalDuration(tasks) {
    const totalEl = document.getElementById('tasksTotalDuration');
    if (!totalEl) return;

    if (tasks.length === 0) {
        totalEl.innerHTML = '';
        return;
    }

    let minTotal = 0;
    let maxTotal = 0;
    tasks.forEach(task => {
        const shirtType = task.shirtType || 'S';
        const range = SHIRT_TYPE_DAYS_RANGE[shirtType] || SHIRT_TYPE_DAYS_RANGE.S;
        minTotal += range[0];
        maxTotal += range[1];
    });

    totalEl.innerHTML = `<i class="bi bi-hourglass-split me-1"></i>${tasks.length} ` +
        `${pluralizeTasks(tasks.length)} · ${minTotal}–${maxTotal} дней суммарно (по оценке размера, без учёта воркеров)`;
}

// Склонение слова "задача" под число (1 задача, 2 задачи, 5 задач).
function pluralizeTasks(count) {
    const mod10 = count % 10;
    const mod100 = count % 100;
    if (mod10 === 1 && mod100 !== 11) return 'задача';
    if (mod10 >= 2 && mod10 <= 4 && (mod100 < 10 || mod100 >= 20)) return 'задачи';
    return 'задач';
}

function renderTasks() {
    const container = document.getElementById('tasksContainer');
    if (!container) {
        console.error('[renderTasks] Container not found!');
        return;
    }

    const tasks = configEditorData.tasks || [];

    renderTasksTotalDuration(tasks);

    if (tasks.length === 0) {
        container.innerHTML = '<div class="text-muted text-center py-4"><i class="bi bi-inbox me-2"></i>Нет задач. Добавьте первую задачу.</div>';
        return;
    }

    let html = '';
    tasks.forEach((task, index) => {
        // Используем lowercase формат
        const key = task.key || '';
        const description = task.description || task.summary || '';
        const shirtType = task.shirtType || 'S';
        const skillsArray = task.requiredSkills || [];
        const skillsString = Array.isArray(skillsArray) ? skillsArray.join(', ') : (skillsArray || '');

        html += `
            <div class="config-item" data-task-index="${index}">
                <div class="config-item-header">
                    <div class="config-item-title">
                        <i class="bi bi-card-checklist me-2"></i>
                        ${key || 'Без ключа'}
                    </div>
                    <div class="config-item-actions">
                        <button class="btn btn-sm btn-outline-secondary" onclick="moveTaskUp(${index})" ${index === 0 ? 'disabled' : ''} title="Выше">
                            <i class="bi bi-arrow-up"></i>
                        </button>
                        <button class="btn btn-sm btn-outline-secondary" onclick="moveTaskDown(${index})" ${index === tasks.length - 1 ? 'disabled' : ''} title="Ниже">
                            <i class="bi bi-arrow-down"></i>
                        </button>
                        <button class="btn btn-sm btn-outline-danger" onclick="deleteTask(${index})" title="Удалить">
                            <i class="bi bi-trash"></i>
                        </button>
                    </div>
                </div>
                <div class="config-item-body">
                    <div class="config-field">
                        <label>Ключ</label>
                        <input type="text" value="${escapeHtml(key)}" onchange="updateTask(${index}, 'key', this.value)">
                    </div>
                    <div class="config-field">
                        <label>Описание</label>
                        <input type="text" value="${escapeHtml(description)}" onchange="updateTask(${index}, 'description', this.value)">
                    </div>
                    <div class="config-field">
                        <label>Размер</label>
                        <select onchange="updateTask(${index}, 'shirtType', this.value)">
                            <option value="XS" ${shirtType === 'XS' ? 'selected' : ''}>XS</option>
                            <option value="S" ${shirtType === 'S' || !shirtType ? 'selected' : ''}>S</option>
                            <option value="M" ${shirtType === 'M' ? 'selected' : ''}>M</option>
                            <option value="L" ${shirtType === 'L' ? 'selected' : ''}>L</option>
                            <option value="XL" ${shirtType === 'XL' ? 'selected' : ''}>XL</option>
                        </select>
                    </div>
                    <div class="config-field">
                        <label>Навыки (через запятую)</label>
                        <input type="text" value="${escapeHtml(skillsString)}" onchange="updateTaskSkills(${index}, this.value)" placeholder="backend, frontend, qa">
                    </div>
                </div>
            </div>
        `;
    });

    container.innerHTML = html;
}

// Следующий свободный номер для ключа TASK-N: максимум существующего суффикса + 1.
// Нельзя опираться на длину массива — после удаления задач она расходится с номерами,
// и новые ключи начинают конфликтовать с уже существующими (backend падает на .Single).
function nextTaskKeyNumber() {
    const nums = (configEditorData.tasks || []).map(t => {
        const m = /^TASK-(\d+)$/.exec((t.key || '').trim());
        return m ? parseInt(m[1], 10) : 0;
    });
    return (nums.length ? Math.max(...nums) : 0) + 1;
}

function addTask() {
    if (!configEditorData.tasks) {
        configEditorData.tasks = [];
    }

    const newTask = {
        key: `TASK-${nextTaskKeyNumber()}`,
        summary: 'Новая задача',
        description: 'Новая задача',
        shirtType: 'S',
        requiredSkills: []
    };

    configEditorData.tasks.push(newTask);
    renderTasks();
}

function deleteTask(index) {
    configEditorData.tasks.splice(index, 1);
    renderTasks();
}

function updateTask(index, field, value) {
    const task = configEditorData.tasks[index];
    if (task) {
        task[field] = value;
    }
    renderTasks();
}

function updateTaskSkills(index, value) {
    const skills = value.split(',').map(s => s.trim()).filter(s => s);
    const task = configEditorData.tasks[index];
    if (task) {
        task.requiredSkills = skills;
    }
    renderTasks();
}

function moveTaskUp(index) {
    if (index === 0) return;
    [configEditorData.tasks[index - 1], configEditorData.tasks[index]] =
    [configEditorData.tasks[index], configEditorData.tasks[index - 1]];
    renderTasks();
}

function moveTaskDown(index) {
    if (index === configEditorData.tasks.length - 1) return;
    [configEditorData.tasks[index], configEditorData.tasks[index + 1]] =
    [configEditorData.tasks[index + 1], configEditorData.tasks[index]];
    renderTasks();
}

// ============================================================================
// Утилиты
// ============================================================================

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// ============================================================================
// Генератор задач
// ============================================================================

let taskGeneratorRowCount = 0;

function openTaskGenerator() {
    const modal = document.getElementById('taskGeneratorModal');
    if (modal) {
        modal.style.display = 'flex';
        setTimeout(() => modal.classList.add('show'), 10);
        
        // Очищаем и добавляем одну строку по умолчанию
        document.getElementById('generatorRowsContainer').innerHTML = '';
        taskGeneratorRowCount = 0;
        addGeneratorRow();
        updateGeneratorTotal();
    }
}

function closeTaskGenerator() {
    const modal = document.getElementById('taskGeneratorModal');
    if (modal) {
        modal.classList.remove('show');
        setTimeout(() => modal.style.display = 'none', 200);
    }
}

function addGeneratorRow() {
    taskGeneratorRowCount++;
    const container = document.getElementById('generatorRowsContainer');
    
    const row = document.createElement('div');
    row.className = 'generator-row';
    row.dataset.rowId = taskGeneratorRowCount;
    row.innerHTML = `
        <div class="generator-row-header">
            <span><i class="bi bi-lightning-charge me-2"></i>Генерация по навыкам</span>
            <button class="btn-remove-row" onclick="removeGeneratorRow(${taskGeneratorRowCount})" title="Удалить строку">
                <i class="bi bi-x-lg"></i>
            </button>
        </div>
        <div class="mb-3">
            <input type="text" class="form-control generator-skills" placeholder="Навыки через запятую (например: backend, qa, frontend)" onchange="updateGeneratorTotal()">
        </div>
        <div class="generator-counts">
            <div class="generator-count-wrapper">
                <label>XS:</label>
                <input type="number" class="form-control generator-count" data-size="XS" value="0" min="0" onchange="updateGeneratorTotal()">
            </div>
            <div class="generator-count-wrapper">
                <label>S:</label>
                <input type="number" class="form-control generator-count" data-size="S" value="0" min="0" onchange="updateGeneratorTotal()">
            </div>
            <div class="generator-count-wrapper">
                <label>M:</label>
                <input type="number" class="form-control generator-count" data-size="M" value="0" min="0" onchange="updateGeneratorTotal()">
            </div>
            <div class="generator-count-wrapper">
                <label>L:</label>
                <input type="number" class="form-control generator-count" data-size="L" value="0" min="0" onchange="updateGeneratorTotal()">
            </div>
            <div class="generator-count-wrapper">
                <label>XL:</label>
                <input type="number" class="form-control generator-count" data-size="XL" value="0" min="0" onchange="updateGeneratorTotal()">
            </div>
        </div>
    `;
    
    container.appendChild(row);
}

function removeGeneratorRow(rowId) {
    const row = document.querySelector(`.generator-row[data-row-id="${rowId}"]`);
    if (row) {
        row.remove();
        updateGeneratorTotal();
    }
}

function updateGeneratorTotal() {
    let total = 0;
    const rows = document.querySelectorAll('.generator-row');
    
    rows.forEach(row => {
        const skillsInput = row.querySelector('.generator-skills');
        const skillsString = skillsInput.value.trim();
        
        if (!skillsString) return;
        
        const countInputs = row.querySelectorAll('.generator-count');
        countInputs.forEach(input => {
            const count = parseInt(input.value) || 0;
            total += count;
        });
    });
    
    document.getElementById('totalTasksCount').textContent = total;
}

function generateTasks() {
    const rows = document.querySelectorAll('.generator-row');
    let taskKeyCounter = nextTaskKeyNumber();
    let generatedCount = 0;
    const newTasks = [];

    rows.forEach(row => {
        const skillsInput = row.querySelector('.generator-skills');
        const skillsString = skillsInput.value.trim();

        if (!skillsString) {
            return; // Пропускаем строки без навыков
        }

        // Парсим навыки
        const requiredSkills = skillsString
            .split(',')
            .map(s => s.trim())
            .filter(s => s);

        if (requiredSkills.length === 0) {
            return;
        }

        // Генерируем задачи для каждого размера
        const countInputs = row.querySelectorAll('.generator-count');
        countInputs.forEach(input => {
            const size = input.dataset.size;
            const count = parseInt(input.value) || 0;

            for (let i = 0; i < count; i++) {
                const newTask = {
                    key: `TASK-${taskKeyCounter++}`,
                    summary: `Задача ${requiredSkills.join('+')} #${generatedCount + 1}`,
                    description: `Задача ${requiredSkills.join('+')} #${generatedCount + 1}`,
                    shirtType: size,
                    requiredSkills: [...requiredSkills]
                };

                newTasks.push(newTask);
                generatedCount++;
            }
        });
    });

    if (generatedCount === 0) {
        showToast('Укажите количество задач хотя бы для одной строки', 'warning');
        return;
    }

    // Перемешиваем задачи перед добавлением
    const shuffledTasks = shuffleArray(newTasks);

    // Присваиваем ключи после перемешивания, продолжая нумерацию от текущего максимума
    // (не от длины массива — иначе после удалений ключи столкнутся с существующими).
    const keyBase = nextTaskKeyNumber();
    shuffledTasks.forEach((task, index) => {
        task.key = `TASK-${keyBase + index}`;
    });

    // Добавляем перемешанные задачи
    configEditorData.tasks.push(...shuffledTasks);

    closeTaskGenerator();
    renderTasks();

    showToast(`Сгенерировано ${generatedCount} задач(и) (перемешаны)`, 'success');
}

// Перемешивание массива (Fisher-Yates)
function shuffleArray(array) {
    const shuffled = [...array];
    for (let i = shuffled.length - 1; i > 0; i--) {
        const j = Math.floor(Math.random() * (i + 1));
        [shuffled[i], shuffled[j]] = [shuffled[j], shuffled[i]];
    }
    return shuffled;
}
