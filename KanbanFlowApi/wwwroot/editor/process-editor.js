// Состояние редактора процессов
let currentPreset = null;
let stages = [];
let tasks = [];
let editingStageId = null;
let editingTaskId = null;

// Ключ LocalStorage для пресетов процессов
const PROCESS_PRESETS_KEY = 'kanbanflow_process_presets';

// Типы стадий
const STAGE_TYPES = [
    { value: 'Buffer', label: 'Буфер (очередь)' },
    { value: 'Work', label: 'Рабочая (создаёт ценность)' }
];

// Типы размеров задач
const TSHIRT_TYPES = [
    { value: 'XS', label: 'XS (0.5 дня)', days: 0.5 },
    { value: 'S', label: 'S (1 день)', days: 1 },
    { value: 'M', label: 'M (3 дня)', days: 3 },
    { value: 'L', label: 'L (5 дней)', days: 5 },
    { value: 'XL', label: 'XL (8 дней)', days: 8 }
];

// Доступные навыки
const AVAILABLE_SKILLS = [
    'backend', 'frontend', 'qa', 'qa-auto', 'devops',
    'database', 'api', 'react', 'angular', 'mobile'
];

// Инициализация при загрузке
document.addEventListener('DOMContentLoaded', async () => {
    await loadPresets();
    loadFromLocalStorage();
    initEventDelegation();
    
    // Автоматическое обновление отладки при загрузке
    setTimeout(() => {
        updateDebugInfo();
    }, 500);
});

// Загрузка списка пресетов (сервер + LocalStorage)
async function loadPresets() {
    try {
        // Загружаем серверные пресеты
        const serverResponse = await fetch('/api/editor/processes/presets');
        const serverPresets = serverResponse.ok ? await serverResponse.json() : [];

        // Загружаем пользовательские пресеты из LocalStorage
        const userPresets = getUserPresets();

        // Объединяем
        const allPresets = [...serverPresets, ...userPresets];

        const selector = document.getElementById('presetSelector');
        selector.innerHTML = '<option value="">-- Выберите пресет или создайте новый --</option>';

        allPresets.forEach(preset => {
            const option = document.createElement('option');
            option.value = preset.name;
            option.textContent = preset.displayName;
            selector.appendChild(option);
        });

        selector.addEventListener('change', onPresetSelected);
    } catch (error) {
        console.error('Ошибка загрузки пресетов:', error);
        alert('Не удалось загрузить пресеты');
    }
}

// Загрузка пользовательских пресетов из LocalStorage
function getUserPresets() {
    try {
        const saved = localStorage.getItem(PROCESS_PRESETS_KEY);
        if (!saved) return [];
        const presets = JSON.parse(saved);
        return Array.isArray(presets) ? presets : [];
    } catch (error) {
        console.error('Ошибка загрузки пользовательских пресетов:', error);
        return [];
    }
}

// Сохранение пользовательского пресета в LocalStorage
function saveUserPreset(preset) {
    try {
        const existing = getUserPresets();

        // Проверяем, есть ли пресет с таким именем — если да, обновляем
        const existingIndex = existing.findIndex(p => p.name === preset.name);
        if (existingIndex >= 0) {
            existing[existingIndex] = preset;
        } else {
            existing.push(preset);
        }

        localStorage.setItem(PROCESS_PRESETS_KEY, JSON.stringify(existing));
        return true;
    } catch (error) {
        console.error('Ошибка сохранения пользовательского пресета:', error);
        return false;
    }
}

// Удаление пользовательского пресета из LocalStorage
function deleteUserPreset(presetName) {
    try {
        const existing = getUserPresets();
        const filtered = existing.filter(p => p.name !== presetName);
        localStorage.setItem(PROCESS_PRESETS_KEY, JSON.stringify(filtered));
        return true;
    } catch (error) {
        console.error('Ошибка удаления пользовательского пресета:', error);
        return false;
    }
}

// Выбор пресета
async function onPresetSelected(event) {
    const presetName = event.target.value;

    if (!presetName) {
        currentPreset = null;
        stages = [];
        tasks = [];
        renderStages();
        renderTasks();
        hidePresetInfo();
        return;
    }

    // Сначала пробуем загрузить из LocalStorage
    const userPresets = getUserPresets();
    let preset = userPresets.find(p => p.name === presetName);

    // Если не нашли в LocalStorage, загружаем с сервера
    if (!preset) {
        try {
            const response = await fetch(`/api/editor/processes/presets/${presetName}`);
            if (response.ok) {
                preset = await response.json();
            }
        } catch (error) {
            console.error('Ошибка загрузки пресета с сервера:', error);
        }
    }

    if (!preset) {
        alert('Пресет не найден');
        return;
    }

    currentPreset = preset;
    stages = (preset.workflow?.stages || []).map((s, index) => ({
        ...s,
        id: index,
        isEditing: false
    }));
    tasks = (preset.tasks || []).map((t, index) => ({
        ...t,
        id: index,
        isEditing: false
    }));

    showPresetInfo(currentPreset);
    renderStages();
    renderTasks();
    saveToLocalStorage();
}

// Создание нового пресета
function createNewPreset() {
    currentPreset = {
        name: '',
        displayName: '',
        description: '',
        isDefault: false,
        workflow: { stages: [] },
        tasks: []
    };
    stages = [];
    tasks = [];

    document.getElementById('presetSelector').value = '';
    hidePresetInfo();
    renderStages();
    renderTasks();
}

// Отображение информации о пресете
function showPresetInfo(preset) {
    const infoPanel = document.getElementById('presetInfo');
    document.getElementById('presetName').textContent = preset.displayName;
    document.getElementById('presetDescription').textContent = preset.description;
    infoPanel.style.display = 'block';
}

function hidePresetInfo() {
    document.getElementById('presetInfo').style.display = 'none';
}

// ==================== СТАДИИ ====================

// Рендеринг списка стадий
function renderStages() {
    const container = document.getElementById('stagesContainer');
    document.getElementById('stageCount').textContent = stages.length;

    if (stages.length === 0) {
        container.innerHTML = `
            <div class="empty-state">
                <p class="mb-3">Список стадий пуст</p>
                <button class="btn btn-gradient" onclick="addStage()">
                    <i class="bi bi-plus-circle me-2"></i>Добавить первую стадию
                </button>
            </div>
        `;
        return;
    }

    container.innerHTML = stages.map((stage, index) => {
        if (stage.isEditing) {
            return renderStageForm(stage, index);
        }
        return renderStageCard(stage, index);
    }).join('');
}

// Рендеринг карточки стадии (режим просмотра)
function renderStageCard(stage, index) {
    const typeLabel = stage.type === 'Buffer' ? 'Буфер' : 'Рабочая';
    const typeClass = stage.type === 'Buffer' ? 'badge-buffer' : 'badge-work';
    const wipLimit = stage.wipLimit ?? '∞';
    const progress = stage.stageProgressPercent ?? 0;
    const transitions = stage.transitions?.length || 0;
    const skills = stage.requiredSkills?.join(', ') || 'Нет навыков';

    return `
        <div class="stage-card" data-stage-index="${index}">
            <div class="stage-card-header">
                <h3>
                    📍 ${stage.name}
                    <span class="badge-stage-type ${typeClass}">${typeLabel}</span>
                    ${stage.isLeadTimeStart ? '<span class="badge-leadtime">Lead Time Start</span>' : ''}
                </h3>
                <div class="stage-actions">
                    <button class="btn btn-primary btn-sm btn-edit-stage">✏️ Редактировать</button>
                    <button class="btn btn-danger btn-sm btn-delete-stage">🗑️ Удалить</button>
                </div>
            </div>
            <div class="stage-summary">
                <span>📋 <strong>WIP:</strong> ${wipLimit}</span>
                <span>⚡ <strong>Прогресс:</strong> ${progress}%</span>
                <span>🔀 <strong>Переходов:</strong> ${transitions}</span>
                <span>🛠️ <strong>Навыки:</strong> ${skills}</span>
            </div>
        </div>
    `;
}

// Рендеринг формы стадии (режим редактирования)
function renderStageForm(stage, index) {
    const typeOptions = STAGE_TYPES.map(t =>
        `<option value="${t.value}" ${stage.type === t.value ? 'selected' : ''}>${t.label}</option>`
    ).join('');

    const skillsString = (stage.requiredSkills || []).join(', ');

    // Генерация переходов с использованием data-атрибутов
    const transitions = stage.transitions || [];
    const transitionsHtml = transitions.length > 0 ? transitions.map((t, tIndex) => `
        <div class="transition-item" data-stage-index="${index}" data-transition-index="${tIndex}">
            <select class="form-select form-select-sm transition-target" style="width: auto; min-width: 150px;">
                ${getStageOptionsForTransition(t.targetStageName)}
            </select>
            <input type="number" class="form-control form-control-sm transition-prob"
                   value="${t.probability}" step="0.1" min="0" max="1" style="width: 80px;">
            <button class="btn btn-danger btn-sm btn-remove-transition" title="Удалить переход">✕</button>
        </div>
    `).join('') : '<p class="text-muted">Нет переходов</p>';

    return `
        <div class="stage-card editing" data-stage-index="${index}">
            <div class="stage-card-header">
                <h3>${stage.id !== undefined && stage.name ? '✏️ Редактирование' : '➕ Новая стадия'}</h3>
                <div class="stage-actions">
                    <button class="btn btn-success btn-sm btn-save-stage">💾 Сохранить</button>
                    <button class="btn btn-secondary btn-sm btn-cancel-stage">Отмена</button>
                </div>
            </div>
            <div class="stage-form">
                <div class="form-group">
                    <label for="stage-name-${index}">Имя стадии *</label>
                    <input type="text" id="stage-name-${index}" class="stage-name-input" value="${stage.name || ''}" placeholder="Developing">
                    <small>Уникальное имя стадии</small>
                </div>
                <div class="form-group">
                    <label for="stage-type-${index}">Тип стадии *</label>
                    <select id="stage-type-${index}" class="stage-type-select">
                        ${typeOptions}
                    </select>
                    <small>Buffer = очередь, Work = работа</small>
                </div>
                <div class="form-group">
                    <label for="stage-wip-${index}">WIP-лимит</label>
                    <input type="number" id="stage-wip-${index}" class="stage-wip-input" value="${stage.wipLimit ?? ''}"
                           placeholder="∞" min="1">
                    <small>Пусто = без ограничений</small>
                </div>
                <div class="form-group">
                    <label for="stage-progress-${index}">Прогресс (%)</label>
                    <input type="number" id="stage-progress-${index}" class="stage-progress-input" value="${stage.stageProgressPercent ?? 0}"
                           min="0" max="100">
                    <small>Для рабочих стадий</small>
                </div>
                <div class="form-group">
                    <label for="stage-skills-${index}">Навыки</label>
                    <input type="text" id="stage-skills-${index}" class="stage-skills-input" value="${skillsString}"
                           placeholder="backend, frontend">
                    <small>Через запятую</small>
                </div>
            </div>

            <div class="form-check">
                <input type="checkbox" class="form-check-input stage-leadtime-checkbox" id="stage-leadtime-${index}"
                       ${stage.isLeadTimeStart ? 'checked' : ''}>
                <label class="form-check-label" for="stage-leadtime-${index}">
                    Начало отсчёта Lead Time
                </label>
            </div>

            <div class="transitions-section">
                <h4>🔀 Переходы (сумма вероятностей = 1.0)</h4>
                <div class="transitions-container" id="stage-${index}-transitions">
                    ${transitionsHtml}
                </div>
                <button class="btn btn-sm btn-secondary mt-2 btn-add-transition" data-stage-index="${index}">
                    <i class="bi bi-plus-circle me-1"></i>Добавить переход
                </button>
            </div>

            <div class="skills-helper">
                <strong>💡 Примеры навыков:</strong> backend, frontend, qa, qa-auto, devops, database, api, react, angular
            </div>
        </div>
    `;
}

// Получить список стадий для выбора в переходе
function getStageOptionsForTransition(selectedName) {
    return stages.map(s => 
        `<option value="${s.name}" ${s.name === selectedName ? 'selected' : ''}>${s.name}</option>`
    ).join('');
}

// Добавить новую стадию
function addStage() {
    const newStage = {
        id: Date.now(),
        name: '',
        type: 'Buffer',
        wipLimit: null,
        stageProgressPercent: 0,
        isLeadTimeStart: false,
        requiredSkills: [],
        transitions: [],
        isEditing: true
    };

    stages.push(newStage);
    editingStageId = newStage.id;
    renderStages();
}

// Редактировать стадию
function editStage(index) {
    stages[index].isEditing = true;
    editingStageId = stages[index].id;
    renderStages();
}

// Сохранить стадию
function saveStage(index) {
    // Находим карточку стадии по data-атрибуту
    const stageCard = document.querySelector(`.stage-card.editing[data-stage-index="${index}"]`);
    if (!stageCard) {
        console.error('Stage card not found for index:', index);
        return;
    }

    const name = stageCard.querySelector('.stage-name-input').value.trim();
    const type = stageCard.querySelector('.stage-type-select').value;
    const wipLimitStr = stageCard.querySelector('.stage-wip-input').value.trim();
    const progress = stageCard.querySelector('.stage-progress-input').value;
    const skillsString = stageCard.querySelector('.stage-skills-input').value.trim();
    const isLeadTimeStart = stageCard.querySelector('.stage-leadtime-checkbox').checked;

    // Валидация
    if (!name) {
        alert('Имя стадии обязательно');
        return;
    }

    // Проверка уникальности имени
    const duplicateIndex = stages.findIndex((s, i) =>
        s.name.toLowerCase() === name.toLowerCase() && i !== index
    );

    if (duplicateIndex !== -1) {
        alert(`Имя "${name}" уже используется другой стадией`);
        return;
    }

    // Парсинг WIP-лимита
    const wipLimit = wipLimitStr ? parseInt(wipLimitStr) : null;
    if (wipLimit !== null && wipLimit <= 0) {
        alert('WIP-лимит должен быть больше 0');
        return;
    }

    // Парсинг прогресса
    const stageProgressPercent = parseInt(progress) || 0;
    if (stageProgressPercent < 0 || stageProgressPercent > 100) {
        alert('Прогресс должен быть от 0 до 100');
        return;
    }

    // Парсинг навыков
    const requiredSkills = skillsString
        ? skillsString.split(',').map(s => s.trim()).filter(s => s)
        : [];

    // Сбор переходов
    const transitions = [];
    const transitionItems = stageCard.querySelectorAll('.transition-item');

    transitionItems.forEach((item) => {
        const targetSelect = item.querySelector('.transition-target');
        const probInput = item.querySelector('.transition-prob');

        if (targetSelect && probInput) {
            transitions.push({
                targetStageName: targetSelect.value,
                probability: parseFloat(probInput.value) || 0
            });
        }
    });

    // Проверка суммы вероятностей
    const totalProbability = transitions.reduce((sum, t) => sum + t.probability, 0);
    if (transitions.length > 0 && (totalProbability < 0.99 || totalProbability > 1.01)) {
        alert(`Сумма вероятностей переходов должна быть равна 1.0 (сейчас ${totalProbability.toFixed(2)})`);
        return;
    }

    // Сохранение данных
    stages[index] = {
        ...stages[index],
        name,
        type,
        wipLimit,
        stageProgressPercent,
        isLeadTimeStart,
        requiredSkills,
        transitions,
        isEditing: false
    };

    editingStageId = null;
    renderStages();
    saveToLocalStorage();
}

// Отменить редактирование стадии
function cancelEditStage(index) {
    // Если новая стадия - удалить
    if (stages[index].id === editingStageId && !stages[index].name) {
        stages.splice(index, 1);
    } else {
        stages[index].isEditing = false;
    }

    editingStageId = null;
    renderStages();
}

// Удалить стадию
function deleteStage(index) {
    if (confirm(`Удалить стадию "${stages[index].name}"?`)) {
        stages.splice(index, 1);
        renderStages();
        saveToLocalStorage();
    }
}

// Добавить переход
function addTransition(stageIndex) {
    // Сначала сохраняем текущие значения из формы
    syncStageFormToData(stageIndex);
    
    if (!stages[stageIndex].transitions) {
        stages[stageIndex].transitions = [];
    }

    // Добавляем переход на первую доступную стадию
    const firstOtherStage = stages.find((s, i) => i !== stageIndex);
    stages[stageIndex].transitions.push({
        targetStageName: firstOtherStage?.name || 'Next',
        probability: 1.0
    });

    renderStages();
}

// Удалить переход
function removeTransition(stageIndex, transitionIndex) {
    // Сначала сохраняем текущие значения из формы
    syncStageFormToData(stageIndex);
    
    if (stages[stageIndex].transitions) {
        stages[stageIndex].transitions.splice(transitionIndex, 1);
        renderStages();
    }
}

// Синхронизировать значения из формы редактирования в массив данных
function syncStageFormToData(stageIndex) {
    const stageCard = document.querySelector(`.stage-card.editing[data-stage-index="${stageIndex}"]`);
    if (!stageCard) return;
    
    const name = stageCard.querySelector('.stage-name-input')?.value.trim() || stages[stageIndex].name;
    const type = stageCard.querySelector('.stage-type-select')?.value || stages[stageIndex].type;
    const wipLimitStr = stageCard.querySelector('.stage-wip-input')?.value.trim();
    const progress = stageCard.querySelector('.stage-progress-input')?.value;
    const skillsString = stageCard.querySelector('.stage-skills-input')?.value.trim();
    const isLeadTimeStart = stageCard.querySelector('.stage-leadtime-checkbox')?.checked ?? false;
    
    // Сбор переходов из формы
    const transitions = [];
    const transitionItems = stageCard.querySelectorAll('.transition-item');
    transitionItems.forEach((item) => {
        const targetSelect = item.querySelector('.transition-target');
        const probInput = item.querySelector('.transition-prob');
        if (targetSelect && probInput) {
            transitions.push({
                targetStageName: targetSelect.value,
                probability: parseFloat(probInput.value) || 0
            });
        }
    });
    
    stages[stageIndex] = {
        ...stages[stageIndex],
        name,
        type,
        wipLimit: wipLimitStr ? parseInt(wipLimitStr) : null,
        stageProgressPercent: parseInt(progress) || 0,
        isLeadTimeStart,
        requiredSkills: skillsString ? skillsString.split(',').map(s => s.trim()).filter(s => s) : [],
        transitions
    };
}

// Инициализация обработчиков событий (event delegation)
function initEventDelegation() {
    // Обработчик для кнопок добавления перехода
    document.addEventListener('click', function(e) {
        const addBtn = e.target.closest('.btn-add-transition');
        if (addBtn) {
            e.preventDefault();
            const stageIndex = parseInt(addBtn.getAttribute('data-stage-index'));
            addTransition(stageIndex);
        }

        // Обработчик для кнопок удаления перехода
        const removeBtn = e.target.closest('.btn-remove-transition');
        if (removeBtn) {
            e.preventDefault();
            const transitionItem = removeBtn.closest('.transition-item');
            const stageIndex = parseInt(transitionItem.getAttribute('data-stage-index'));
            const transitionIndex = parseInt(transitionItem.getAttribute('data-transition-index'));
            removeTransition(stageIndex, transitionIndex);
        }

        // Обработчик для кнопок сохранения стадии
        const saveBtn = e.target.closest('.btn-save-stage');
        if (saveBtn) {
            e.preventDefault();
            const stageCard = saveBtn.closest('.stage-card');
            const stageIndex = parseInt(stageCard.getAttribute('data-stage-index'));
            saveStage(stageIndex);
        }

        // Обработчик для кнопок отмены
        const cancelBtn = e.target.closest('.btn-cancel-stage');
        if (cancelBtn) {
            e.preventDefault();
            const stageCard = cancelBtn.closest('.stage-card');
            const stageIndex = parseInt(stageCard.getAttribute('data-stage-index'));
            cancelEditStage(stageIndex);
        }

        // Обработчик для кнопок редактирования стадии
        const editBtn = e.target.closest('.btn-edit-stage');
        if (editBtn) {
            e.preventDefault();
            const stageCard = editBtn.closest('.stage-card');
            const stageIndex = parseInt(stageCard.getAttribute('data-stage-index'));
            editStage(stageIndex);
        }

        // Обработчик для кнопок удаления стадии
        const deleteBtn = e.target.closest('.btn-delete-stage');
        if (deleteBtn) {
            e.preventDefault();
            const stageCard = deleteBtn.closest('.stage-card');
            const stageIndex = parseInt(stageCard.getAttribute('data-stage-index'));
            deleteStage(stageIndex);
        }
    });
}

// ==================== ЗАДАЧИ ====================

// Рендеринг списка задач
function renderTasks() {
    const container = document.getElementById('tasksContainer');
    document.getElementById('taskCount').textContent = tasks.length;

    if (tasks.length === 0) {
        container.innerHTML = `
            <div class="empty-state">
                <p class="mb-3">Список задач пуст</p>
                <button class="btn btn-gradient" onclick="addTask()">
                    <i class="bi bi-plus-circle me-2"></i>Добавить первую задачу
                </button>
            </div>
        `;
        return;
    }

    container.innerHTML = tasks.map((task, index) => {
        if (task.isEditing) {
            return renderTaskForm(task, index);
        }
        return renderTaskCard(task, index);
    }).join('');
}

// Рендеринг карточки задачи (режим просмотра)
function renderTaskCard(task, index) {
    const skills = task.requiredSkills?.join(', ') || 'Нет навыков';
    const shirtType = task.shirtType || 'S';

    // Находим описание размера
    const shirtInfo = TSHIRT_TYPES.find(t => t.value === shirtType);
    const shirtLabel = shirtInfo ? `${shirtType} (${shirtInfo.days} дн.)` : shirtType;

    return `
        <div class="task-card" style="background: white; border: 2px solid #e9ecef; border-radius: 10px; padding: 15px; margin-bottom: 15px;">
            <div class="task-card-header" style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 10px;">
                <h3 style="font-size: 1.1rem; margin: 0; color: #333;">📋 ${task.key}</h3>
                <div class="task-actions">
                    <button class="btn btn-primary btn-sm" onclick="editTask(${index})">✏️ Редактировать</button>
                    <button class="btn btn-danger btn-sm" onclick="deleteTask(${index})">🗑️ Удалить</button>
                </div>
            </div>
            <div class="task-summary" style="display: flex; gap: 20px; flex-wrap: wrap; font-size: 0.85rem; color: #666;">
                <span>📝 <strong>Описание:</strong> ${task.summary || 'Без описания'}</span>
                <span>👕 <strong>Размер:</strong> ${shirtLabel}</span>
                <span>🛠️ <strong>Навыки:</strong> ${skills}</span>
            </div>
        </div>
    `;
}

// Рендеринг формы задачи (режим редактирования)
function renderTaskForm(task, index) {
    const skillsString = (task.requiredSkills || []).join(', ');
    const tshirtOptions = TSHIRT_TYPES.map(t =>
        `<option value="${t.value}" ${task.shirtType === t.value ? 'selected' : ''}>${t.label}</option>`
    ).join('');

    return `
        <div class="task-card editing" style="background: white; border: 2px solid #667eea; border-radius: 10px; padding: 15px; margin-bottom: 15px;">
            <div class="task-card-header" style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 10px;">
                <h3 style="font-size: 1.1rem; margin: 0; color: #333;">${task.id !== undefined && task.key ? '✏️ Редактирование' : '➕ Новая задача'}</h3>
                <div class="task-actions">
                    <button class="btn btn-success btn-sm" onclick="saveTask(${index})">💾 Сохранить</button>
                    <button class="btn btn-secondary btn-sm" onclick="cancelEdit(${index})">Отмена</button>
                </div>
            </div>
            <div class="task-form" style="display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 15px; margin-top: 15px;">
                <div class="form-group">
                    <label for="task-key-${index}">Ключ задачи *</label>
                    <input type="text" id="task-key-${index}" value="${task.key || ''}" placeholder="TASK-1">
                    <small>Уникальный идентификатор</small>
                </div>
                <div class="form-group">
                    <label for="task-summary-${index}">Описание *</label>
                    <input type="text" id="task-summary-${index}" value="${task.summary || ''}" placeholder="Разработка API...">
                    <small>Краткое описание</small>
                </div>
                <div class="form-group">
                    <label for="task-shirt-${index}">Размер *</label>
                    <select id="task-shirt-${index}">
                        ${tshirtOptions}
                    </select>
                    <small>Влияет на время выполнения</small>
                </div>
                <div class="form-group">
                    <label for="task-skills-${index}">Навыки *</label>
                    <input type="text" id="task-skills-${index}" value="${skillsString}" placeholder="backend, frontend">
                    <small>Через запятую</small>
                </div>
            </div>
            <div class="skills-helper" style="margin-top: 15px; padding: 12px; background: #f8f9fa; border-radius: 8px; font-size: 0.8rem; color: #666;">
                <strong>💡 Примеры навыков:</strong> backend, frontend, qa, qa-auto, devops, database, api, react, angular
            </div>
        </div>
    `;
}

// Добавить новую задачу
function addTask() {
    const newTask = {
        id: Date.now(),
        key: '',
        summary: '',
        shirtType: 'S',
        requiredSkills: [],
        isEditing: true
    };

    tasks.push(newTask);
    editingTaskId = newTask.id;
    renderTasks();
}

// Редактировать задачу
function editTask(index) {
    tasks[index].isEditing = true;
    editingTaskId = tasks[index].id;
    renderTasks();
}

// Сохранить задачу
function saveTask(index) {
    const key = document.getElementById(`task-key-${index}`).value.trim();
    const summary = document.getElementById(`task-summary-${index}`).value.trim();
    const shirtType = document.getElementById(`task-shirt-${index}`).value;
    const skillsString = document.getElementById(`task-skills-${index}`).value.trim();

    // Валидация
    if (!key) {
        alert('Ключ задачи обязателен');
        return;
    }

    // Проверка уникальности ключа
    const duplicateIndex = tasks.findIndex((t, i) =>
        t.key.toLowerCase() === key.toLowerCase() && i !== index
    );

    if (duplicateIndex !== -1) {
        alert(`Ключ "${key}" уже используется другой задачей`);
        return;
    }

    if (!summary) {
        alert('Описание задачи обязательно');
        return;
    }

    // Парсинг навыков
    const requiredSkills = skillsString
        ? skillsString.split(',').map(s => s.trim()).filter(s => s)
        : [];

    if (requiredSkills.length === 0) {
        alert('Задача должна иметь хотя бы один навык');
        return;
    }

    // Сохранение данных
    tasks[index] = {
        ...tasks[index],
        key,
        summary,
        shirtType,
        requiredSkills,
        isEditing: false
    };

    editingTaskId = null;
    renderTasks();
    saveToLocalStorage();
}

// Отменить редактирование задачи
function cancelEdit(index) {
    // Если новая задача - удалить
    if (tasks[index].id === editingTaskId && !tasks[index].key) {
        tasks.splice(index, 1);
    } else {
        tasks[index].isEditing = false;
    }

    editingTaskId = null;
    renderTasks();
}

// Удалить задачу
function deleteTask(index) {
    if (confirm(`Удалить задачу "${tasks[index].key}"?`)) {
        tasks.splice(index, 1);
        renderTasks();
        saveToLocalStorage();
    }
}

// ==================== СОХРАНЕНИЕ ПРЕСЕТА ====================

function saveCurrentPreset() {
    // Проверка: есть ли стадии
    if (stages.length === 0) {
        alert('Добавьте хотя бы одну стадию');
        return;
    }

    // Проверка: есть ли задачи
    if (tasks.length === 0) {
        alert('Добавьте хотя бы одну задачу');
        return;
    }

    // Проверка: все ли стадии сохранены
    const editingStages = stages.filter(s => s.isEditing);
    if (editingStages.length > 0) {
        alert('Сначала завершите редактирование стадий');
        return;
    }

    // Проверка: все ли задачи сохранены
    const editingTasks = tasks.filter(t => t.isEditing);
    if (editingTasks.length > 0) {
        alert('Сначала завершите редактирование задач');
        return;
    }

    // Заполнение модального окна
    const saveNameInput = document.getElementById('savePresetName');
    const saveDisplayNameInput = document.getElementById('savePresetDisplayName');
    const saveDescriptionInput = document.getElementById('savePresetDescription');

    if (currentPreset?.name) {
        saveNameInput.value = currentPreset.name;
        saveDisplayNameInput.value = currentPreset.displayName || '';
        saveDescriptionInput.value = currentPreset.description || '';
    } else {
        saveNameInput.value = '';
        saveDisplayNameInput.value = '';
        saveDescriptionInput.value = '';
    }

    document.getElementById('saveModal').classList.add('active');
}

function closeSaveModal() {
    document.getElementById('saveModal').classList.remove('active');
}

async function confirmSavePreset() {
    const name = document.getElementById('savePresetName').value.trim();
    const displayName = document.getElementById('savePresetDisplayName').value.trim();
    const description = document.getElementById('savePresetDescription').value.trim();

    if (!name) {
        alert('Имя пресета обязательно');
        return;
    }

    // Валидация имени
    if (!/^[a-z0-9-]+$/.test(name)) {
        alert('Имя пресета должно содержать только латинские буквы, цифры и дефисы');
        return;
    }

    // Подготовка данных
    const presetToSave = {
        name,
        displayName: displayName || name,
        description: description || '',
        isDefault: false,
        workflow: { stages: stages.map(s => ({
            name: s.name,
            type: s.type,
            wipLimit: s.wipLimit,
            stageProgressPercent: s.stageProgressPercent,
            isLeadTimeStart: s.isLeadTimeStart || false,
            requiredSkills: s.requiredSkills,
            transitions: s.transitions
        }))},
        tasks: tasks.map(t => ({
            key: t.key,
            summary: t.summary,
            shirtType: t.shirtType,
            requiredSkills: t.requiredSkills
        }))
    };

    // Отправляем на валидацию backend
    try {
        const response = await fetch('/api/editor/processes/presets', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(presetToSave)
        });

        if (!response.ok) {
            const errorData = await response.json();
            throw new Error(errorData.error || 'Ошибка валидации');
        }

        // Backend вернул валидный пресет - сохраняем в LocalStorage
        const validatedPreset = await response.json();

        if (saveUserPreset(validatedPreset)) {
            currentPreset = validatedPreset;

            // Обновление селектора
            await loadPresets();
            document.getElementById('presetSelector').value = name;
            showPresetInfo(currentPreset);

            closeSaveModal();
            saveToLocalStorage();

            updateDebugInfo();
            showExportPanel();
        } else {
            alert('Ошибка сохранения пресета');
        }
    } catch (error) {
        console.error('Ошибка валидации/сохранения:', error);
        alert('Ошибка: ' + error.message);
    }
}

// Показать панель экспорта
function showExportPanel() {
    const instructions = `1. Откройте главную страницу в новой вкладке\n2. Нажмите "Настройки симуляции"\n3. Выберите пресет "${currentPreset?.displayName || 'сохранённый'}" в секции "Процесс"\n4. Нажмите "Запустить симуляцию"`;
    const exportInstructions = document.getElementById('exportInstructions');
    const exportPanel = document.getElementById('exportPanel');

    if (exportInstructions) {
        exportInstructions.textContent = instructions;
    }
    if (exportPanel) {
        exportPanel.classList.add('show');

        // Скрыть панель через 10 секунд
        setTimeout(() => {
            exportPanel.classList.remove('show');
        }, 10000);
    }
}

// Копировать инструкцию
function copyExportInstructions() {
    const text = document.getElementById('exportInstructions')?.textContent || '';
    navigator.clipboard.writeText(text).then(() => {
        alert('Инструкция скопирована в буфер обмена');
    });
}

// Удалить пресет
async function deleteCurrentPreset() {
    if (!currentPreset?.name) {
        alert('Выберите пресет для удаления');
        return;
    }

    if (!confirm(`Удалить пресет "${currentPreset.displayName}"? Это действие нельзя отменить.`)) {
        return;
    }

    try {
        const response = await fetch(`/api/editor/processes/presets/${currentPreset.name}`, {
            method: 'DELETE'
        });

        if (!response.ok) {
            const errorData = await response.json();
            throw new Error(errorData.error || 'Ошибка удаления');
        }

        // Backend подтвердил - удаляем из LocalStorage
        if (deleteUserPreset(currentPreset.name)) {
            currentPreset = null;
            stages = [];
            tasks = [];

            // Обновляем селектор
            loadPresets();
            document.getElementById('presetSelector').value = '';
            renderStages();
            renderTasks();
            hidePresetInfo();

            alert('Пресет успешно удалён');
        } else {
            alert('Ошибка удаления пресета из LocalStorage');
        }
    } catch (error) {
        console.error('Ошибка удаления:', error);
        alert('Ошибка: ' + error.message);
    }
}

// LocalStorage
function saveToLocalStorage() {
    const data = {
        currentPreset,
        stages,
        tasks
    };
    localStorage.setItem('kanbanflow_process_editor', JSON.stringify(data));
}

function loadFromLocalStorage() {
    const saved = localStorage.getItem('kanbanflow_process_editor');
    if (saved) {
        try {
            const data = JSON.parse(saved);
            currentPreset = data.currentPreset;
            stages = data.stages || [];
            tasks = data.tasks || [];

            if (currentPreset) {
                showPresetInfo(currentPreset);
                document.getElementById('presetSelector').value = currentPreset.name;
            }

            renderStages();
            renderTasks();
        } catch (error) {
            console.error('Ошибка загрузки из LocalStorage:', error);
        }
    }
}

// Экспорт/Импорт
function exportPreset() {
    if (!currentPreset || stages.length === 0) {
        alert('Нечего экспортировать');
        return;
    }

    const data = {
        ...currentPreset,
        workflow: { stages: stages.map(s => ({
            name: s.name,
            type: s.type,
            wipLimit: s.wipLimit,
            stageProgressPercent: s.stageProgressPercent,
            isLeadTimeStart: s.isLeadTimeStart,
            requiredSkills: s.requiredSkills,
            transitions: s.transitions
        }))},
        tasks: tasks.map(t => ({
            key: t.key,
            summary: t.summary,
            shirtType: t.shirtType,
            requiredSkills: t.requiredSkills
        }))
    };

    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${currentPreset.name}-process.json`;
    a.click();
    URL.revokeObjectURL(url);
}

async function importPreset(event) {
    const file = event.target.files[0];
    if (!file) return;

    try {
        const text = await file.text();
        const data = JSON.parse(text);

        currentPreset = {
            name: data.name || 'imported',
            displayName: data.displayName || 'Импортированный пресет',
            description: data.description || '',
            isDefault: false
        };

        stages = (data.workflow?.stages || []).map((s, index) => ({
            ...s,
            id: index,
            isEditing: false
        }));

        tasks = (data.tasks || []).map((t, index) => ({
            ...t,
            id: index,
            isEditing: false
        }));

        showPresetInfo(currentPreset);
        renderStages();
        renderTasks();
        saveToLocalStorage();

        alert('Пресет успешно импортирован');
    } catch (error) {
        console.error('Ошибка импорта:', error);
        alert('Ошибка импорта: неверный формат файла');
    }

    // Очистка input
    event.target.value = '';
}

// ==================== ОТЛАДКА ====================

function updateDebugInfo() {
    const debugInfo = document.getElementById('debugInfo');
    if (!debugInfo) return;

    const debugData = {
        localStorage: {
            processPresets: localStorage.getItem(PROCESS_PRESETS_KEY),
            selection: localStorage.getItem('kanbanflow_selection'),
            simulation: localStorage.getItem('kanbanflow_simulation')
        },
        currentState: {
            currentPreset: currentPreset,
            stagesCount: stages.length,
            tasksCount: tasks.length,
            editingStageId: editingStageId,
            editingTaskId: editingTaskId
        },
        userAgent: navigator.userAgent
    };

    debugInfo.textContent = JSON.stringify(debugData, null, 2);
    console.log('[DEBUG] Debug info updated:', debugData);
}

function clearDebugInfo() {
    if (confirm('Вы уверены, что хотите очистить все пресеты из LocalStorage?')) {
        localStorage.removeItem(PROCESS_PRESETS_KEY);
        localStorage.removeItem('kanbanflow_selection');
        localStorage.removeItem('kanbanflow_simulation');
        localStorage.removeItem('kanbanflow_process_editor');
        alert('LocalStorage очищен. Перезагрузите страницу.');
        location.reload();
    }
}
