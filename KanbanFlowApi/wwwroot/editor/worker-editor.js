// Состояние редактора
let currentPreset = null;
let workers = [];
let editingWorkerId = null;

// Ключ LocalStorage для пресетов воркеров (должен совпадать с app.js)
const WORKER_PRESETS_KEY = 'kanbanflow_worker_presets';

// Инициализация при загрузке
document.addEventListener('DOMContentLoaded', async () => {
    await loadPresets();
    loadFromLocalStorage();
});

// Загрузка списка пресетов (сервер + LocalStorage)
async function loadPresets() {
    try {
        // Загружаем серверные пресеты (новый endpoint)
        const serverResponse = await fetch('/api/editor/workers/presets');
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
        const saved = localStorage.getItem(WORKER_PRESETS_KEY);
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
    console.log('[DEBUG] saveUserPreset вызвана:', preset);
    try {
        const existing = getUserPresets();
        console.log('[DEBUG] Существующие пресеты:', existing);
        
        // Проверяем, есть ли пресет с таким именем — если да, обновляем
        const existingIndex = existing.findIndex(p => p.name === preset.name);
        console.log('[DEBUG] existingIndex:', existingIndex);
        
        if (existingIndex >= 0) {
            existing[existingIndex] = preset;
            console.log('[DEBUG] Обновляем существующий пресет');
        } else {
            existing.push(preset);
            console.log('[DEBUG] Добавляем новый пресет');
        }
        
        const json = JSON.stringify(existing);
        console.log('[DEBUG] Сохраняем в LocalStorage:', WORKER_PRESETS_KEY, json);
        localStorage.setItem(WORKER_PRESETS_KEY, json);
        
        // Проверяем, что сохранилось
        const saved = localStorage.getItem(WORKER_PRESETS_KEY);
        console.log('[DEBUG] После сохранения в LocalStorage:', saved);
        
        return true;
    } catch (error) {
        console.error('[DEBUG] Ошибка сохранения пользовательского пресета:', error);
        return false;
    }
}

// Удаление пользовательского пресета из LocalStorage
function deleteUserPreset(presetName) {
    try {
        const existing = getUserPresets();
        const filtered = existing.filter(p => p.name !== presetName);
        localStorage.setItem(WORKER_PRESETS_KEY, JSON.stringify(filtered));
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
        workers = [];
        renderWorkers();
        hidePresetInfo();
        return;
    }

    // Сначала пробуем загрузить из LocalStorage
    const userPresets = getUserPresets();
    let preset = userPresets.find(p => p.name === presetName);

    // Если не нашли в LocalStorage, загружаем с сервера (новый endpoint)
    if (!preset) {
        try {
            const response = await fetch(`/api/editor/workers/presets/${presetName}`);
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
    workers = currentPreset.workers.map((w, index) => ({
        ...w,
        id: index,
        isEditing: false
    }));

    showPresetInfo(currentPreset);
    renderWorkers();
    saveToLocalStorage();
}

// Создание нового пресета
function createNewPreset() {
    currentPreset = {
        name: '',
        displayName: '',
        description: '',
        isDefault: false,
        workers: []
    };
    workers = [];
    
    document.getElementById('presetSelector').value = '';
    hidePresetInfo();
    renderWorkers();
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

// Рендеринг списка воркеров
function renderWorkers() {
    const container = document.getElementById('workersContainer');
    document.getElementById('workerCount').textContent = workers.length;

    if (workers.length === 0) {
        container.innerHTML = `
            <div class="empty-state">
                <p class="mb-3">Список воркеров пуст</p>
                <button class="btn btn-gradient" onclick="addWorker()">
                    <i class="bi bi-person-plus me-2"></i>Добавить первого воркера
                </button>
            </div>
        `;
        return;
    }

    container.innerHTML = workers.map((worker, index) => {
        if (worker.isEditing) {
            return renderWorkerForm(worker, index);
        }
        return renderWorkerCard(worker, index);
    }).join('');
}

// Рендеринг карточки воркера (режим просмотра)
function renderWorkerCard(worker, index) {
    const skills = worker.skills?.join(', ') || 'Нет навыков';
    const wipLimit = worker.wipLimit ?? '∞';

    return `
        <div class="worker-card">
            <div class="worker-card-header">
                <h3>👤 ${worker.login}</h3>
                <div class="worker-actions">
                    <button class="btn btn-primary btn-sm" onclick="editWorker(${index})">✏️ Редактировать</button>
                    <button class="btn btn-danger btn-sm" onclick="deleteWorker(${index})">🗑️ Удалить</button>
                </div>
            </div>
            <div class="worker-summary">
                <span>🛠️ <strong>Навыки:</strong> ${skills}</span>
                <span>📋 <strong>WIP:</strong> ${wipLimit}</span>
                <span>⚡ <strong>Производительность:</strong> ${worker.performance}%</span>
                <span>📉 <strong>Отклонение ↓:</strong> ${worker.deviationDownPercent || 0}%</span>
                <span>📈 <strong>Отклонение ↑:</strong> ${worker.deviationUpPercent || 0}%</span>
            </div>
        </div>
    `;
}

// Рендеринг формы воркера (режим редактирования)
function renderWorkerForm(worker, index) {
    return `
        <div class="worker-card editing">
            <div class="worker-card-header">
                <h3>${worker.id !== undefined ? '✏️ Редактирование' : '➕ Новый воркер'}</h3>
                <div class="worker-actions">
                    <button class="btn btn-success btn-sm" onclick="saveWorker(${index})">💾 Сохранить</button>
                    <button class="btn btn-secondary btn-sm" onclick="cancelEdit(${index})">Отмена</button>
                </div>
            </div>
            <div class="worker-form">
                <div class="form-group">
                    <label for="login-${index}">Логин *</label>
                    <input type="text" id="login-${index}" value="${worker.login || ''}" placeholder="dev1">
                    <small>Уникальный идентификатор воркера</small>
                </div>
                <div class="form-group">
                    <label for="skills-${index}">Навыки</label>
                    <input type="text" id="skills-${index}" value="${worker.skills?.join(', ') || ''}" placeholder="backend, frontend">
                    <small>Перечислите через запятую</small>
                </div>
                <div class="form-group">
                    <label for="wip-${index}">WIP-лимит</label>
                    <input type="number" id="wip-${index}" value="${worker.wipLimit ?? ''}" placeholder="1" min="1">
                    <small>Макс. задач одновременно (пусто = без лимита)</small>
                </div>
                <div class="form-group">
                    <label for="performance-${index}">Производительность (%)</label>
                    <input type="number" id="performance-${index}" value="${worker.performance ?? 100}" min="1" max="500">
                    <small>100 = базовая, 150 = на 50% быстрее</small>
                </div>
                <div class="form-group">
                    <label for="deviation-down-${index}">Отклонение вниз (%)</label>
                    <input type="number" id="deviation-down-${index}" value="${worker.deviationDownPercent ?? 0}" min="0" max="100">
                    <small>На сколько % может быть быстрее</small>
                </div>
                <div class="form-group">
                    <label for="deviation-up-${index}">Отклонение вверх (%)</label>
                    <input type="number" id="deviation-up-${index}" value="${worker.deviationUpPercent ?? 0}" min="0" max="100">
                    <small>На сколько % может быть медленнее</small>
                </div>
            </div>
            <div class="skills-helper">
                <strong>💡 Примеры навыков:</strong> backend, frontend, qa, qa-auto, devops, database, api, react, angular
            </div>
        </div>
    `;
}

// Добавить нового воркера
function addWorker() {
    const newWorker = {
        id: Date.now(),
        login: '',
        skills: [],
        wipLimit: 1,
        performance: 100,
        deviationDownPercent: 0,
        deviationUpPercent: 0,
        isEditing: true
    };
    
    workers.push(newWorker);
    editingWorkerId = newWorker.id;
    renderWorkers();
}

// Редактировать воркера
function editWorker(index) {
    workers[index].isEditing = true;
    editingWorkerId = workers[index].id;
    renderWorkers();
}

// Сохранить воркера
function saveWorker(index) {
    const login = document.getElementById(`login-${index}`).value.trim();
    const skillsString = document.getElementById(`skills-${index}`).value.trim();
    const wipLimit = document.getElementById(`wip-${index}`).value;
    const performance = document.getElementById(`performance-${index}`).value;
    const deviationDown = document.getElementById(`deviation-down-${index}`).value;
    const deviationUp = document.getElementById(`deviation-up-${index}`).value;
    
    // Валидация
    if (!login) {
        alert('Логин воркера обязателен');
        return;
    }
    
    // Проверка уникальности логина
    const duplicateIndex = workers.findIndex((w, i) => 
        w.login.toLowerCase() === login.toLowerCase() && i !== index
    );
    
    if (duplicateIndex !== -1) {
        alert(`Логин "${login}" уже используется другим воркером`);
        return;
    }
    
    // Парсинг навыков
    const skills = skillsString
        ? skillsString.split(',').map(s => s.trim()).filter(s => s)
        : [];
    
    // Сохранение данных
    workers[index] = {
        ...workers[index],
        login,
        skills,
        wipLimit: wipLimit ? parseInt(wipLimit) : null,
        performance: parseFloat(performance) || 100,
        deviationDownPercent: parseFloat(deviationDown) || 0,
        deviationUpPercent: parseFloat(deviationUp) || 0,
        isEditing: false
    };
    
    editingWorkerId = null;
    renderWorkers();
    saveToLocalStorage();
}

// Отменить редактирование
function cancelEdit(index) {
    // Если новый воркер - удалить
    if (workers[index].id === editingWorkerId && !workers[index].login) {
        workers.splice(index, 1);
    } else {
        workers[index].isEditing = false;
    }
    
    editingWorkerId = null;
    renderWorkers();
}

// Удалить воркера
function deleteWorker(index) {
    if (confirm(`Удалить воркера "${workers[index].login}"?`)) {
        workers.splice(index, 1);
        renderWorkers();
        saveToLocalStorage();
    }
}

// Сохранить пресет
function saveCurrentPreset() {
    // Проверка: есть ли воркеры
    if (workers.length === 0) {
        alert('Добавьте хотя бы одного воркера перед сохранением');
        return;
    }
    
    // Проверка: все ли воркеры сохранены (не в режиме редактирования)
    const editingWorkers = workers.filter(w => w.isEditing);
    if (editingWorkers.length > 0) {
        alert('Сначала завершите редактирование воркеров');
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
    console.log('[DEBUG] confirmSavePreset вызвана');

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

    // Подготовка данных - используем ApiWorkerDto напрямую
    const presetToSave = {
        name,
        displayName: displayName || name,
        description: description || '',
        isDefault: false,
        workers: workers.map(w => ({
            login: w.login,
            skills: w.skills,
            wipLimit: w.wipLimit,
            performance: w.performance,
            deviationDownPercent: w.deviationDownPercent,
            deviationUpPercent: w.deviationUpPercent
        }))
    };

    console.log('[DEBUG] presetToSave:', presetToSave);

    // Отправляем на валидацию backend
    try {
        const response = await fetch('/api/editor/workers/presets', {
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
        console.log('[DEBUG] Валидированный пресет от backend:', validatedPreset);

        if (saveUserPreset(validatedPreset)) {
            console.log('[DEBUG] Пресет успешно сохранён в LocalStorage');
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
            console.error('[DEBUG] Ошибка сохранения пресета');
            alert('Ошибка сохранения пресета');
        }
    } catch (error) {
        console.error('[DEBUG] Ошибка валидации/сохранения:', error);
        alert('Ошибка: ' + error.message);
    }
}

// Показать панель экспорта
function showExportPanel() {
    const instructions = `1. Откройте главную страницу в новой вкладке\n2. Нажмите "Настройки симуляции"\n3. Выберите пресет "${currentPreset?.displayName || 'сохранённый'}" в секции "Команда исполнителей"\n4. Нажмите "Запустить симуляцию"`;
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
        const response = await fetch(`/api/editor/workers/presets/${currentPreset.name}`, {
            method: 'DELETE'
        });

        if (!response.ok) {
            const errorData = await response.json();
            throw new Error(errorData.error || 'Ошибка удаления');
        }

        // Backend подтвердил - удаляем из LocalStorage
        if (deleteUserPreset(currentPreset.name)) {
            currentPreset = null;
            workers = [];

            // Обновляем селектор
            loadPresets();
            document.getElementById('presetSelector').value = '';
            renderWorkers();
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
        workers
    };
    localStorage.setItem('kanbanflow_worker_editor', JSON.stringify(data));
}

function loadFromLocalStorage() {
    const saved = localStorage.getItem('kanbanflow_worker_editor');
    if (saved) {
        try {
            const data = JSON.parse(saved);
            currentPreset = data.currentPreset;
            workers = data.workers || [];
            
            if (currentPreset) {
                showPresetInfo(currentPreset);
                document.getElementById('presetSelector').value = currentPreset.name;
            }
            
            renderWorkers();
        } catch (error) {
            console.error('Ошибка загрузки из LocalStorage:', error);
        }
    }
}

// Экспорт/Импорт
function exportPreset() {
    if (!currentPreset || workers.length === 0) {
        alert('Нечего экспортировать');
        return;
    }
    
    const data = {
        ...currentPreset,
        workers: workers.map(w => ({
            login: w.login,
            skills: w.skills,
            wipLimit: w.wipLimit,
            performance: w.performance,
            deviationDownPercent: w.deviationDownPercent,
            deviationUpPercent: w.deviationUpPercent
        }))
    };
    
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${currentPreset.name}-workers.json`;
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
        
        workers = (data.workers || []).map((w, index) => ({
            ...w,
            id: index,
            isEditing: false
        }));
        
        showPresetInfo(currentPreset);
        renderWorkers();
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
            workerPresets: localStorage.getItem(WORKER_PRESETS_KEY),
            selection: localStorage.getItem('kanbanflow_selection'),
            simulation: localStorage.getItem('kanbanflow_simulation')
        },
        currentState: {
            currentPreset: currentPreset,
            workersCount: workers.length,
            editingWorkerId: editingWorkerId
        },
        userAgent: navigator.userAgent
    };

    debugInfo.textContent = JSON.stringify(debugData, null, 2);
    console.log('[DEBUG] Debug info updated:', debugData);
}

// Очистка LocalStorage
function clearDebugInfo() {
    if (confirm('Вы уверены, что хотите очистить все пресеты из LocalStorage?')) {
        localStorage.removeItem(WORKER_PRESETS_KEY);
        localStorage.removeItem('kanbanflow_selection');
        localStorage.removeItem('kanbanflow_simulation');
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
