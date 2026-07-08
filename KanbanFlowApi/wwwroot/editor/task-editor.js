// Состояние редактора задач
let currentPreset = null;
let tasks = [];
let editingTaskId = null;

// Ключ LocalStorage для пресетов задач (должен совпадать с app.js)
const TASK_PRESETS_KEY = 'kanbanflow_task_presets';

// Доступные размеры задач
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
});

// Загрузка списка пресетов (сервер + LocalStorage)
async function loadPresets() {
    try {
        // Загружаем серверные пресеты
        const serverResponse = await fetch('/api/editor/tasks/presets');
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
        const saved = localStorage.getItem(TASK_PRESETS_KEY);
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

        localStorage.setItem(TASK_PRESETS_KEY, JSON.stringify(existing));
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
        localStorage.setItem(TASK_PRESETS_KEY, JSON.stringify(filtered));
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
        tasks = [];
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
            const response = await fetch(`/api/editor/tasks/presets/${presetName}`);
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
    tasks = currentPreset.tasks.map((t, index) => ({
        ...t,
        id: index,
        isEditing: false
    }));

    showPresetInfo(currentPreset);
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
        tasks: []
    };
    tasks = [];

    document.getElementById('presetSelector').value = '';
    hidePresetInfo();
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

// Рендеринг списка задач
function renderTasks() {
    const container = document.getElementById('tasksContainer');
    const countElement = document.getElementById('taskCount');
    
    console.log('[renderTasks] Вызов, tasks.length:', tasks.length);
    
    if (!container) {
        console.error('[renderTasks] container не найден');
        return;
    }
    if (!countElement) {
        console.error('[renderTasks] countElement не найден');
        return;
    }
    
    countElement.textContent = tasks.length;

    if (tasks.length === 0) {
        console.log('[renderTasks] Задач нет, рендерим empty state');
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

    console.log('[renderTasks] Рендерим', tasks.length, 'задач');
    container.innerHTML = tasks.map((task, index) => {
        if (task.isEditing) {
            return renderTaskForm(task, index);
        }
        return renderTaskCard(task, index);
    }).join('');
    console.log('[renderTasks] HTML обновлён');
}

// Рендеринг карточки задачи (режим просмотра)
function renderTaskCard(task, index) {
    const skills = task.requiredSkills?.join(', ') || 'Нет навыков';
    const shirtType = task.shirtType || 'S';
    
    // Находим описание размера
    const shirtInfo = TSHIRT_TYPES.find(t => t.value === shirtType);
    const shirtLabel = shirtInfo ? `${shirtType} (${shirtInfo.days} дн.)` : shirtType;

    return `
        <div class="task-card">
            <div class="task-card-header">
                <h3>📋 ${task.key}</h3>
                <div class="task-actions">
                    <button class="btn btn-primary btn-sm" onclick="editTask(${index})">✏️ Редактировать</button>
                    <button class="btn btn-danger btn-sm" onclick="deleteTask(${index})">🗑️ Удалить</button>
                </div>
            </div>
            <div class="task-summary">
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
        <div class="task-card editing">
            <div class="task-card-header">
                <h3>${task.id !== undefined && task.key ? '✏️ Редактирование' : '➕ Новая задача'}</h3>
                <div class="task-actions">
                    <button class="btn btn-success btn-sm" onclick="saveTask(${index})">💾 Сохранить</button>
                    <button class="btn btn-secondary btn-sm" onclick="cancelEdit(${index})">Отмена</button>
                </div>
            </div>
            <div class="task-form">
                <div class="form-group">
                    <label for="task-key-${index}">Ключ задачи *</label>
                    <input type="text" id="task-key-${index}" value="${task.key || ''}" placeholder="TASK-1">
                    <small>Уникальный идентификатор (например, TASK-1, FEAT-123)</small>
                </div>
                <div class="form-group">
                    <label for="task-summary-${index}">Описание *</label>
                    <input type="text" id="task-summary-${index}" value="${task.summary || ''}" placeholder="Разработка API...">
                    <small>Краткое описание задачи</small>
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
                    <input type="text" id="task-skills-${index}" value="${skillsString}" placeholder="backend, frontend, qa">
                    <small>Перечислите через запятую (например: backend, frontend, qa)</small>
                </div>
            </div>
            <div class="skills-helper">
                <strong>💡 Примеры навыков:</strong> backend, frontend, qa, qa-auto, devops, database, api, react, angular, mobile
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

    // Парсинг навыков из строки
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

// Отменить редактирование
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

// Сохранить пресет
function saveCurrentPreset() {
    // Проверка: есть ли задачи
    if (tasks.length === 0) {
        alert('Добавьте хотя бы одну задачу перед сохранением');
        return;
    }

    // Проверка: все ли задачи сохранены (не в режиме редактирования)
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

    // Валидация имени (только латиница, цифры, дефисы)
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
        tasks: tasks.map(t => ({
            key: t.key,
            summary: t.summary,
            shirtType: t.shirtType,
            requiredSkills: t.requiredSkills
        }))
    };

    // Отправляем на валидацию backend
    try {
        const response = await fetch('/api/editor/tasks/presets', {
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
            // Обновляем currentPreset и tasks из валидированного пресета
            currentPreset = validatedPreset;
            tasks = validatedPreset.tasks.map((t, index) => ({
                ...t,
                id: index,
                isEditing: false
            }));

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
    const instructions = `1. Откройте главную страницу в новой вкладке\n2. Нажмите "Настройки симуляции"\n3. Выберите пресет "${currentPreset?.displayName || 'сохранённый'}" в секции "Задачи"\n4. Нажмите "Запустить симуляцию"`;
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

    // Сначала проверяем на backend (нельзя удалить системный пресет)
    try {
        const response = await fetch(`/api/editor/tasks/presets/${currentPreset.name}`, {
            method: 'DELETE'
        });

        if (!response.ok) {
            const errorData = await response.json();
            throw new Error(errorData.error || 'Ошибка удаления');
        }

        // Backend подтвердил - удаляем из LocalStorage
        if (deleteUserPreset(currentPreset.name)) {
            currentPreset = null;
            tasks = [];

            // Обновляем селектор
            loadPresets();
            document.getElementById('presetSelector').value = '';
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
        tasks
    };
    localStorage.setItem('kanbanflow_task_editor', JSON.stringify(data));
    console.log('[LocalStorage] Saved:', {
        currentPresetName: currentPreset?.name || null,
        tasksCount: tasks.length,
        firstTask: tasks[0]
    });
}

function loadFromLocalStorage() {
    const saved = localStorage.getItem('kanbanflow_task_editor');
    if (saved) {
        try {
            const data = JSON.parse(saved);
            currentPreset = data.currentPreset;
            tasks = data.tasks || [];

            console.log('[LocalStorage] Loaded:', {
                currentPresetName: currentPreset?.name || null,
                tasksCount: tasks.length,
                firstTask: tasks[0]
            });

            if (currentPreset) {
                showPresetInfo(currentPreset);
                document.getElementById('presetSelector').value = currentPreset.name;
            }

            renderTasks();
        } catch (error) {
            console.error('Ошибка загрузки из LocalStorage:', error);
        }
    }
}

// Экспорт/Импорт
function exportPreset() {
    if (!currentPreset || tasks.length === 0) {
        alert('Нечего экспортировать');
        return;
    }

    const data = {
        ...currentPreset,
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
    a.download = `${currentPreset.name}-tasks.json`;
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

        tasks = (data.tasks || []).map((t, index) => ({
            ...t,
            id: index,
            isEditing: false
        }));

        showPresetInfo(currentPreset);
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

// Обновление отладочной информации
function updateDebugInfo() {
    const debugInfo = document.getElementById('debugInfo');
    if (!debugInfo) return;

    const debugData = {
        localStorage: {
            taskPresets: localStorage.getItem(TASK_PRESETS_KEY),
            selection: localStorage.getItem('kanbanflow_selection'),
            simulation: localStorage.getItem('kanbanflow_simulation')
        },
        currentState: {
            currentPreset: currentPreset,
            tasksCount: tasks.length,
            editingTaskId: editingTaskId
        },
        userAgent: navigator.userAgent
    };

    debugInfo.textContent = JSON.stringify(debugData, null, 2);
    console.log('[DEBUG] Debug info updated:', debugData);
}

// Очистка LocalStorage
function clearDebugInfo() {
    if (confirm('Вы уверены, что хотите очистить все пресеты из LocalStorage?')) {
        localStorage.removeItem(TASK_PRESETS_KEY);
        localStorage.removeItem('kanbanflow_selection');
        localStorage.removeItem('kanbanflow_simulation');
        localStorage.removeItem('kanbanflow_task_editor');
        alert('LocalStorage очищен. Перезагрузите страницу.');
        location.reload();
    }
}

// Автоматическое обновление отладки при загрузке
document.addEventListener('DOMContentLoaded', () => {
    setTimeout(() => {
        updateDebugInfo();
    }, 500);
});

// ==================== ГЕНЕРАТОР ЗАДАЧ ====================

let generatorRowCounter = 0;

// Открытие генератора задач
function openGenerator() {
    const modal = document.getElementById('generatorModal');
    const container = document.getElementById('generatorRowsContainer');
    
    // Очищаем контейнер и добавляем одну строку по умолчанию
    container.innerHTML = '';
    generatorRowCounter = 0;
    addGeneratorRow();
    
    updateGeneratorSummary();
    modal.classList.add('active');
}

// Закрытие генератора задач
function closeGenerator() {
    const modal = document.getElementById('generatorModal');
    modal.classList.remove('active');
}

// Добавление строки в генератор
function addGeneratorRow() {
    const container = document.getElementById('generatorRowsContainer');
    const rowId = generatorRowCounter++;
    
    const row = document.createElement('div');
    row.className = 'generator-row';
    row.dataset.rowId = rowId;
    
    row.innerHTML = `
        <div>
            <label class="size-label">Навыки (через запятую)</label>
            <input type="text" class="generator-skills" placeholder="backend, qa" value="">
        </div>
        <div>
            <label class="size-label">XS</label>
            <input type="number" class="generator-count" data-size="XS" value="0" min="0" onchange="updateGeneratorSummary()">
        </div>
        <div>
            <label class="size-label">S</label>
            <input type="number" class="generator-count" data-size="S" value="0" min="0" onchange="updateGeneratorSummary()">
        </div>
        <div>
            <label class="size-label">M</label>
            <input type="number" class="generator-count" data-size="M" value="0" min="0" onchange="updateGeneratorSummary()">
        </div>
        <div>
            <label class="size-label">L</label>
            <input type="number" class="generator-count" data-size="L" value="0" min="0" onchange="updateGeneratorSummary()">
        </div>
        <div>
            <label class="size-label">XL</label>
            <input type="number" class="generator-count" data-size="XL" value="0" min="0" onchange="updateGeneratorSummary()">
        </div>
        <div>
            <label class="size-label">&nbsp;</label>
            <button class="btn-remove-row" onclick="removeGeneratorRow(${rowId})">
                <i class="bi bi-trash"></i>
            </button>
        </div>
    `;
    
    container.appendChild(row);
    updateGeneratorSummary();
}

// Удаление строки из генератора
function removeGeneratorRow(rowId) {
    const row = document.querySelector(`.generator-row[data-row-id="${rowId}"]`);
    if (row) {
        row.remove();
        updateGeneratorSummary();
    }
}

// Подсчёт количества задач для генерации
function updateGeneratorSummary() {
    let totalCount = 0;
    
    const rows = document.querySelectorAll('.generator-row');
    rows.forEach(row => {
        const countInputs = row.querySelectorAll('.generator-count');
        countInputs.forEach(input => {
            const count = parseInt(input.value) || 0;
            totalCount += count;
        });
    });
    
    document.getElementById('totalTasksCount').textContent = totalCount;
}

// Перемешивание массива (Fisher-Yates shuffle)
function shuffleArray(array) {
    const shuffled = [...array];
    for (let i = shuffled.length - 1; i > 0; i--) {
        const j = Math.floor(Math.random() * (i + 1));
        [shuffled[i], shuffled[j]] = [shuffled[j], shuffled[i]];
    }
    return shuffled;
}

// Генерация задач на основе настроек
function generateTasks() {
    const rows = document.querySelectorAll('.generator-row');
    let taskKeyCounter = tasks.length + 1;
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
                    id: Date.now() + generatedCount,
                    key: `TASK-${taskKeyCounter++}`,
                    summary: `Задача ${requiredSkills.join('+')} #${i + 1}`,
                    shirtType: size,
                    requiredSkills: requiredSkills,
                    isEditing: false
                };
                
                newTasks.push(newTask);
                generatedCount++;
            }
        });
    });
    
    if (generatedCount === 0) {
        alert('Укажите количество задач хотя бы для одной строки');
        return;
    }

    // Перемешиваем задачи перед добавлением
    const shuffledTasks = shuffleArray(newTasks);
    
    // Обновляем номера TASK-N после перемешивания
    shuffledTasks.forEach((task, index) => {
        task.key = `TASK-${tasks.length + index + 1}`;
    });
    
    closeGenerator();
    
    // Сбрасываем currentPreset, так как задачи сгенерированы вручную
    currentPreset = null;
    document.getElementById('presetSelector').value = '';
    hidePresetInfo();
    
    // Добавляем перемешанные задачи
    tasks.push(...shuffledTasks);
    
    renderTasks();
    saveToLocalStorage();

    alert(`✅ Сгенерировано ${generatedCount} задач(и) (перемешаны)`);
}
