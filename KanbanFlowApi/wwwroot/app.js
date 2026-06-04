// KanbanFlow Simulation - Client Logic

let simulationState = null;
let autoPlayInterval = null;
let isAutoPlaying = false;
let isLoading = false;
let currentAllMetrics = null;

// Хранилище выбранных пресетов
let processPresets = [];
let workerPoolPresets = [];
let taskPresetPresets = [];

// Ключи LocalStorage для пользовательских пресетов
const STORAGE_KEYS = {
    PROCESS_PRESETS: 'kanbanflow_process_presets',
    WORKER_PRESETS: 'kanbanflow_worker_presets',
    TASK_PRESETS: 'kanbanflow_task_presets',
    SELECTION: 'kanbanflow_selection',
    SIMULATION: 'kanbanflow_simulation'
};

// Инициализация при загрузке страницы
document.addEventListener('DOMContentLoaded', () => {
    loadAllPresets();
    updateLoadingIndicator();
    initSettingsPanel();
    restoreFromLocalStorage();
});

// Загрузка всех пресетов (процессы, работники, задачи)
async function loadAllPresets() {
    try {
        // Загружаем все три типа пресетов параллельно с сервера (editor endpoint'ы)
        const [processResponse, workerResponse, taskResponse] = await Promise.all([
            fetch('/api/editor/processes/presets'),
            fetch('/api/editor/workers/presets'),
            fetch('/api/editor/tasks/presets')
        ]);

        if (!processResponse.ok || !workerResponse.ok || !taskResponse.ok) {
            throw new Error('Ошибка загрузки пресетов');
        }

        const serverProcessPresets = await processResponse.json();
        const serverWorkerPresets = await workerResponse.json();
        const serverTaskPresets = await taskResponse.json();

        // Загружаем пользовательские пресеты из LocalStorage
        const userProcessPresets = getUserPresets(STORAGE_KEYS.PROCESS_PRESETS);
        const userWorkerPresets = getUserPresets(STORAGE_KEYS.WORKER_PRESETS);
        const userTaskPresets = getUserPresets(STORAGE_KEYS.TASK_PRESETS);

        // Объединяем: пользовательские пресеты добавляем к серверным
        processPresets = [...serverProcessPresets, ...userProcessPresets];
        workerPoolPresets = [...serverWorkerPresets, ...userWorkerPresets];
        taskPresetPresets = [...serverTaskPresets, ...userTaskPresets];

        // Заполняем селекторы
        fillSelector('processSelector', processPresets, 'processDescription');
        fillSelector('workerPoolSelector', workerPoolPresets, 'workerPoolDescription');
        fillSelector('taskPresetSelector', taskPresetPresets, 'taskPresetDescription', true);

        // Восстанавливаем выбор из LocalStorage (если есть)
        restoreSelectionFromStorage();

    } catch (error) {
        console.error('Error loading presets:', error);
        showToast('Ошибка загрузки пресетов: ' + error.message, 'danger');
    }
}

// Загрузка пользовательских пресетов из LocalStorage
function getUserPresets(storageKey) {
    try {
        const saved = localStorage.getItem(storageKey);
        if (!saved) return [];
        const presets = JSON.parse(saved);
        return Array.isArray(presets) ? presets : [];
    } catch (error) {
        console.error(`Error loading user presets from ${storageKey}:`, error);
        return [];
    }
}

// Сохранение пользовательского пресета в LocalStorage
function saveUserPreset(storageKey, preset) {
    try {
        const existing = getUserPresets(storageKey);
        // Проверяем, есть ли пресет с таким именем — если да, обновляем
        const existingIndex = existing.findIndex(p => p.name === preset.name);
        if (existingIndex >= 0) {
            existing[existingIndex] = preset;
        } else {
            existing.push(preset);
        }
        localStorage.setItem(storageKey, JSON.stringify(existing));
        return true;
    } catch (error) {
        console.error(`Error saving user preset to ${storageKey}:`, error);
        return false;
    }
}

// Удаление пользовательского пресета из LocalStorage
function deleteUserPreset(storageKey, presetName) {
    try {
        const existing = getUserPresets(storageKey);
        const filtered = existing.filter(p => p.name !== presetName);
        localStorage.setItem(storageKey, JSON.stringify(filtered));
        return true;
    } catch (error) {
        console.error(`Error deleting user preset from ${storageKey}:`, error);
        return false;
    }
}

// Очистка всего LocalStorage (пользовательские пресеты и настройки)
function clearAllLocalStorage() {
    if (!confirm('Вы уверены, что хотите очистить все пользовательские пресеты и настройки? Это действие нельзя отменить.')) {
        return;
    }

    try {
        // Удаляем все ключи KanbanFlow
        const keysToRemove = [
            STORAGE_KEYS.PROCESS_PRESETS,
            STORAGE_KEYS.WORKER_PRESETS,
            STORAGE_KEYS.TASK_PRESETS,
            STORAGE_KEYS.SELECTION,
            STORAGE_KEYS.SIMULATION,
            'kanbanflow_worker_editor'  // Состояние редактора команд
        ];

        keysToRemove.forEach(key => localStorage.removeItem(key));

        showToast('LocalStorage очищен. Страница будет перезагружена.', 'success');

        // Перезагружаем страницу через 1 секунду
        setTimeout(() => {
            window.location.reload();
        }, 1000);

        return true;
    } catch (error) {
        console.error('Error clearing LocalStorage:', error);
        showToast('Ошибка очистки LocalStorage: ' + error.message, 'danger');
        return false;
    }
}

// Заполнение селектора пресетами
function fillSelector(selectorId, presets, descriptionId, addEmptyOption = false) {
    const selector = document.getElementById(selectorId);
    const descriptionEl = document.getElementById(descriptionId);
    
    if (!selector) return;

    let html = addEmptyOption ? '<option value="">Использовать задачи из процесса</option>' : '';
    html += presets.map(preset => 
        `<option value="${preset.name}" ${preset.isDefault ? 'selected' : ''}>${preset.displayName}</option>`
    ).join('');
    
    selector.innerHTML = html;

    // Сохраняем описания
    selector.dataset.presets = JSON.stringify(presets);

    // Обновляем описание при изменении
    selector.addEventListener('change', () => {
        updatePresetDescription(selector, descriptionEl);
        saveSelectionToStorage();
    });

    // Показываем описание для выбранного
    updatePresetDescription(selector, descriptionEl);
}

// Обновление описания пресета
function updatePresetDescription(selector, descriptionEl) {
    if (!descriptionEl || !selector.dataset.presets) return;

    const presets = JSON.parse(selector.dataset.presets);
    const selected = presets.find(p => p.name === selector.value);
    descriptionEl.textContent = selected ? selected.description : '';
}

// Сохранение выбора в LocalStorage
function saveSelectionToStorage() {
    const selection = {
        processPresetName: document.getElementById('processSelector')?.value,
        workerPoolPresetName: document.getElementById('workerPoolSelector')?.value,
        taskPresetName: document.getElementById('taskPresetSelector')?.value || null,
        seed: document.getElementById('seedInput')?.value || 42,
        useVariability: document.getElementById('variabilityToggle')?.checked ?? true
    };
    localStorage.setItem('kanbanflow_selection', JSON.stringify(selection));
}

// Восстановление выбора из LocalStorage
function restoreSelectionFromStorage() {
    const saved = localStorage.getItem('kanbanflow_selection');
    if (!saved) return;

    try {
        const selection = JSON.parse(saved);

        if (selection.processPresetName) {
            const selector = document.getElementById('processSelector');
            if (selector) selector.value = selection.processPresetName;
        }

        if (selection.workerPoolPresetName) {
            const selector = document.getElementById('workerPoolSelector');
            if (selector) selector.value = selection.workerPoolPresetName;
        }

        if (selection.taskPresetName) {
            const selector = document.getElementById('taskPresetSelector');
            if (selector) selector.value = selection.taskPresetName;
        }

        if (selection.seed) {
            const seedInput = document.getElementById('seedInput');
            if (seedInput) seedInput.value = selection.seed;
        }

        if (selection.useVariability !== undefined) {
            const toggle = document.getElementById('variabilityToggle');
            if (toggle) toggle.checked = selection.useVariability;
        }

        // Обновляем описания после восстановления
        updatePresetDescription(document.getElementById('processSelector'), document.getElementById('processDescription'));
        updatePresetDescription(document.getElementById('workerPoolSelector'), document.getElementById('workerPoolDescription'));
        updatePresetDescription(document.getElementById('taskPresetSelector'), document.getElementById('taskPresetDescription'));

    } catch (error) {
        console.error('Error restoring selection:', error);
    }
}

// Восстановление состояния симуляции из LocalStorage
function restoreFromLocalStorage() {
    const saved = localStorage.getItem('kanbanflow_simulation');
    if (!saved) return;

    try {
        simulationState = JSON.parse(saved);
        if (simulationState) {
            renderBoard();
            renderWorkers();
            renderHistory();
            updateControls();
            calculateAllMetrics();
            showToast('Симуляция восстановлена из localStorage', 'info');
        }
    } catch (error) {
        console.error('Error restoring simulation:', error);
    }
}

// Сохранение состояния симуляции в LocalStorage
function saveSimulationToStorage() {
    if (simulationState) {
        localStorage.setItem('kanbanflow_simulation', JSON.stringify(simulationState));
    }
}

// Инициализация панели настроек
function initSettingsPanel() {
    const header = document.querySelector('.settings-header');
    const panel = document.getElementById('settingsPanel');
    
    if (header) {
        header.addEventListener('click', (e) => {
            toggleSettingsPanel();
        });
    }
}

// Переключение панели настроек
function toggleSettingsPanel() {
    const panel = document.getElementById('settingsPanel');
    if (panel) {
        panel.classList.toggle('collapsed');
    }
}

// Обновление индикатора загрузки
function updateLoadingIndicator() {
    const gear = document.getElementById('loadingGear');
    if (gear) {
        gear.style.opacity = isLoading ? '1' : '0.3';
        gear.style.animation = isLoading ? 'spin 1s linear infinite' : 'none';
    }

    // Блокировка кнопок
    const btnSimulate = document.getElementById('btnSimulateDay');
    const btnAutoPlay = document.getElementById('btnAutoPlay');

    if (btnSimulate) btnSimulate.disabled = isLoading || !simulationState;
    if (btnAutoPlay) btnAutoPlay.disabled = isLoading;
}

// Запуск симуляции из полной конфигурации
async function startSimulation(daysToSimulate = null) {
    isLoading = true;
    updateLoadingIndicator();

    try {
        const processSelector = document.getElementById('processSelector');
        const workerPoolSelector = document.getElementById('workerPoolSelector');
        const taskPresetSelector = document.getElementById('taskPresetSelector');
        const seedInput = document.getElementById('seedInput');
        const variabilityToggle = document.getElementById('variabilityToggle');

        const processPresetName = processSelector?.value;
        const workerPoolPresetName = workerPoolSelector?.value;
        const taskPresetName = taskPresetSelector?.value || null;
        const seed = parseInt(seedInput?.value) || 42;
        const useVariability = variabilityToggle?.checked ?? true;

        if (!processPresetName || !workerPoolPresetName) {
            throw new Error('Выберите процесс и команду исполнителей');
        }

        // Ищем пресеты в загруженных массивах (серверные + пользовательские из LocalStorage)
        const processPreset = processPresets.find(p => p.name === processPresetName);
        const workerPoolPreset = workerPoolPresets.find(p => p.name === workerPoolPresetName);
        const taskPreset = taskPresetName ? taskPresetPresets.find(p => p.name === taskPresetName) : null;

        if (!processPreset) {
            throw new Error(`Процесс '${processPresetName}' не найден`);
        }
        if (!workerPoolPreset) {
            throw new Error(`Команда '${workerPoolPresetName}' не найдена`);
        }

        // Собираем полную конфигурацию для отправки на backend
        const request = {
            seed,
            useVariability,
            workflow: processPreset.workflow,
            workers: workerPoolPreset.workers,
            // Задачи: если указан taskPreset — используем его, иначе — задачи из процесса
            tasks: taskPreset ? taskPreset.tasks : processPreset.tasks,
            daysToSimulate
        };

        console.log('[DEBUG] startSimulation request:', request);

        const response = await fetch('/api/simulation/start', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(request)
        });

        if (!response.ok) {
            const errorData = await response.json();
            throw new Error(errorData.error || `HTTP error! status: ${response.status}`);
        }

        simulationState = await response.json();

        // Сохраняем в localStorage
        saveSimulationToStorage();

        renderBoard();
        renderWorkers();
        renderHistory();
        updateControls();

        // Расчёт всех метрик
        calculateAllMetrics();

        const dayMessage = daysToSimulate === 0 
            ? 'Симуляция запущена (до конца)' 
            : daysToSimulate 
                ? `Симуляция запущена на ${daysToSimulate} дн.` 
                : 'Симуляция запущена (день 0)';
        showToast(dayMessage, 'success');
    } catch (error) {
        console.error('Error starting simulation:', error);
        showToast('Ошибка запуска: ' + error.message, 'danger');
    } finally {
        isLoading = false;
        updateLoadingIndicator();
    }
}

// Быстрая симуляция до конца (кнопка "Рассчитать до конца")
async function simulateToEnd() {
    await startSimulation(0);
}

// Перезагрузка текущей конфигурации (сброс к дню 0)
async function reloadConfig() {
    isLoading = true;
    updateLoadingIndicator();

    try {
        const processSelector = document.getElementById('processSelector');
        const workerPoolSelector = document.getElementById('workerPoolSelector');
        const taskPresetSelector = document.getElementById('taskPresetSelector');
        const seedInput = document.getElementById('seedInput');
        const variabilityToggle = document.getElementById('variabilityToggle');

        const processPresetName = processSelector?.value;
        const workerPoolPresetName = workerPoolSelector?.value;
        const taskPresetName = taskPresetSelector?.value || null;
        const seed = parseInt(seedInput?.value) || 42;
        const useVariability = variabilityToggle?.checked ?? true;

        if (!processPresetName || !workerPoolPresetName) {
            throw new Error('Выберите процесс и команду исполнителей');
        }

        // Ищем пресеты в загруженных массивах (серверные + пользовательские из LocalStorage)
        const processPreset = processPresets.find(p => p.name === processPresetName);
        const workerPoolPreset = workerPoolPresets.find(p => p.name === workerPoolPresetName);
        const taskPreset = taskPresetName ? taskPresetPresets.find(p => p.name === taskPresetName) : null;

        if (!processPreset) {
            throw new Error(`Процесс '${processPresetName}' не найден`);
        }
        if (!workerPoolPreset) {
            throw new Error(`Команда '${workerPoolPresetName}' не найдена`);
        }

        // Собираем полную конфигурацию для отправки на backend (daysToSimulate = null для сброса к дню 0)
        const request = {
            seed,
            useVariability,
            workflow: processPreset.workflow,
            workers: workerPoolPreset.workers,
            tasks: taskPreset ? taskPreset.tasks : processPreset.tasks,
            daysToSimulate: null
        };

        const response = await fetch('/api/simulation/start', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(request)
        });

        if (!response.ok) {
            const errorData = await response.json();
            throw new Error(errorData.error || `HTTP error! status: ${response.status}`);
        }

        simulationState = await response.json();

        // Сохраняем в localStorage
        saveSimulationToStorage();

        renderBoard();
        renderWorkers();
        renderHistory();
        updateControls();
        calculateAllMetrics();

        showToast('Конфигурация сброшена к дню 0', 'success');
    } catch (error) {
        console.error('Error reloading config:', error);
        showToast('Ошибка перезагрузки: ' + error.message, 'danger');
    } finally {
        isLoading = false;
        updateLoadingIndicator();
    }
}

// Симуляция одного дня
async function simulateDay() {
    if (!simulationState) {
        showToast('Сначала загрузите конфигурацию', 'warning');
        return;
    }

    isLoading = true;
    updateLoadingIndicator();

    try {
        // Обновляем состояние вариативности в конфиге
        const variabilityToggle = document.getElementById('variabilityToggle');
        simulationState.config.useVariability = variabilityToggle?.checked ?? true;

        const response = await fetch('/api/simulation/simulate-day', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(simulationState)
        });

        if (!response.ok) {
            const errorData = await response.json();
            throw new Error(errorData.error || `HTTP error! status: ${response.status}`);
        }

        simulationState = await response.json();

        // Сохраняем в localStorage
        saveSimulationToStorage();

        isLoading = false;
        updateLoadingIndicator();

        // Просто обновляем доску без анимации
        updateBoard(simulationState);

        // Проверка завершения симуляции
        if (simulationState.board.stages.every(s =>
            s.taskKeys.every(key => {
                const task = simulationState.board.tasks.find(t => t.key === key);
                return task && task.currentStageName === 'Done';
            })
        )) {
            showToast('Симуляция завершена! Все задачи в Done', 'success');
            stopAutoPlay();
        } else {
            showToast(`День ${simulationState.currentDay} завершён`, 'success');
        }
    } catch (error) {
        console.error('Error simulating day:', error);
        showToast('Ошибка симуляции: ' + error.message, 'danger');
        stopAutoPlay();
        isLoading = false;
        updateLoadingIndicator();
    }
}

// Обновление состояния без анимации
function updateBoard(newState) {
    renderBoard();
    renderWorkers();
    renderHistory();
    updateControls();
    calculateAllMetrics();
}

// Анимация прогресса задачи
async function animateTaskProgress(taskKey, progress) {
    const taskCard = document.querySelector(`.task-card[data-task-key="${taskKey}"]`);
    if (!taskCard) return;
    
    const progressBar = taskCard.querySelector('.progress-bar');
    const progressText = taskCard.querySelector('.progress-value');
    
    if (progressBar) {
        progressBar.style.transition = 'width 0.3s ease';
        progressBar.style.width = `${progress}%`;
    }
    if (progressText) {
        progressText.textContent = `${progress}%`;
    }
}

// Анимация завершения задачи воркером — просто мигание
async function animateWorkerCompleteTask(workerLogin, taskKey) {
    const taskCard = document.querySelector(`.task-card[data-task-key="${taskKey}"]`);
    if (taskCard) {
        taskCard.animate([
            { background: '#d4edda' },
            { background: '#ffffff' }
        ], {
            duration: 300,
            easing: 'ease-out'
        });
    }
}

// Рендеринг канбан-доски
function renderBoard() {
    const boardContainer = document.getElementById('kanbanBoard');
    if (!boardContainer || !simulationState) return;

    const { stages, tasks } = simulationState.board;
    
    boardContainer.innerHTML = stages.map(stage => {
        const stageTasks = stage.taskKeys
            .map(key => tasks.find(t => t.key === key))
            .filter(t => t);

        const isWorkStage = stage.type === 'Work';
        const isBufferStage = stage.type === 'Buffer';
        const isDoneStage = stage.name === 'Done';
        
        const stageClass = isDoneStage ? 'done-stage' : 
                          isWorkStage ? 'work-stage' : 
                          isBufferStage ? 'buffer-stage' : '';

        return `
            <div class="stage-column ${stageClass}" data-stage-name="${stage.name}">
                <div class="stage-header">
                    <span class="stage-name">${stage.name}</span>
                    <span class="stage-wip">WIP: ${stage.wipCount}${stage.wipLimit ? '/' + stage.wipLimit : ''}</span>
                </div>
                <div class="task-cards">
                    ${stageTasks.map(task => renderTaskCard(task, stage.name)).join('')}
                </div>
            </div>
        `;
    }).join('');
}

// Рендеринг карточки задачи
function renderTaskCard(task, stageName) {
    const isCompleted = stageName === 'Done';
    const workerInfo = task.workerLogin ? 
        `<div class="worker-assignment">
            <span class="worker-icon" data-worker-login="${task.workerLogin}">${task.workerLogin.substring(0, 2).toUpperCase()}</span>
            <span>${task.workerLogin}</span>
        </div>` : '';

    const progressPercent = task.progress || 0;
    const isWorking = task.workerLogin && progressPercent > 0 && progressPercent < 100;

    return `
        <div class="task-card ${isCompleted ? 'completed' : ''}" data-task-key="${task.key}">
            <span class="task-shirt">${task.shirtType}</span>
            <div class="task-key">${task.key}</div>
            <div class="task-summary">${task.summary}</div>
            <div class="task-skills">
                ${(task.requiredSkills || []).map(skill => 
                    `<span class="skill-badge ${skill}">${skill}</span>`
                ).join('')}
            </div>
            ${workerInfo}
            <div class="progress-container">
                <div class="progress-label">
                    <span>Прогресс</span>
                    <span class="progress-value">${progressPercent}%</span>
                </div>
                <div class="progress">
                    <div class="progress-bar ${isWorking ? 'working' : ''}" 
                         style="width: ${progressPercent}%"
                         data-task-key="${task.key}"></div>
                </div>
            </div>
        </div>
    `;
}

// Рендеринг воркеров
function renderWorkers() {
    const workersGrid = document.getElementById('workersGrid');
    if (!workersGrid || !simulationState) return;

    const { workers, tasks } = simulationState.board;

    workersGrid.innerHTML = workers.map(worker => {
        const isAvailable = worker.isAvailable;
        const wipPercent = worker.wipLimit ? (worker.wipCount / worker.wipLimit) * 100 : 0;
        
        // Находим задачи, которые выполняет этот воркер
        const workerTasks = tasks.filter(t => t.workerLogin === worker.login);
        
        return `
            <div class="worker-card ${isAvailable ? 'available' : 'busy'}">
                <div class="worker-header">
                    <div class="worker-avatar" data-worker-login="${worker.login}">
                        ${worker.login.substring(0, 2).toUpperCase()}
                    </div>
                    <span class="worker-name">${worker.login}</span>
                    <span class="worker-status ${isAvailable ? 'available' : 'busy'}">
                        ${isAvailable ? 'Свободен' : 'Занят'}
                    </span>
                </div>
                <div class="worker-skills">
                    ${(worker.skills || []).map(skill =>
                        `<span class="skill-badge ${skill}">${skill}</span>`
                    ).join('')}
                </div>
                <div class="worker-wip">
                    <span>Задач: ${worker.wipCount}/${worker.wipLimit}</span>
                    <div class="wip-bar">
                        <div class="wip-fill" style="width: ${wipPercent}%"></div>
                    </div>
                </div>
                ${workerTasks.length > 0 ? `
                    <div class="worker-tasks">
                        <small class="text-muted">Активные задачи:</small>
                        <ul class="task-list">
                            ${workerTasks.map(task => {
                                const stage = simulationState.board.stages.find(s => s.name === task.currentStageName);
                                return `<li class="task-item">
                                    <span class="task-key-small">${task.key}</span>
                                    <span class="task-stage">${task.currentStageName}</span>
                                </li>`;
                            }).join('')}
                        </ul>
                    </div>
                ` : ''}
            </div>
        `;
    }).join('');
}

// Рендеринг истории
function renderHistory() {
    const historyList = document.getElementById('historyList');
    if (!historyList || !simulationState) return;

    const allActivities = simulationState.history
        .flatMap(day => day.activities.map(activity => ({
            ...activity,
            dayNumber: day.dayNumber
        })))
        .reverse();

    historyList.innerHTML = allActivities.map(activity => `
        <li class="history-item ${activity.type}">
            <strong>День ${activity.dayNumber}:</strong> ${activity.description}
        </li>
    `).join('');
}

// Обновление элементов управления
function updateControls() {
    const btnSimulateDay = document.getElementById('btnSimulateDay');
    const currentDay = document.getElementById('currentDay');

    if (btnSimulateDay) {
        btnSimulateDay.disabled = !simulationState;
    }

    if (currentDay) {
        currentDay.textContent = simulationState?.currentDay || 0;
    }
}

// Переключение авто-режима
function toggleAutoPlay() {
    if (isAutoPlaying) {
        stopAutoPlay();
    } else {
        startAutoPlay();
    }
}

function startAutoPlay() {
    if (!simulationState) {
        showToast('Сначала загрузите конфигурацию', 'warning');
        return;
    }

    isAutoPlaying = true;
    const btnAutoPlay = document.getElementById('btnAutoPlay');
    if (btnAutoPlay) {
        btnAutoPlay.innerHTML = '<i class="bi bi-pause-circle me-2"></i>Стоп';
        btnAutoPlay.classList.remove('btn-outline-secondary');
        btnAutoPlay.classList.add('btn-outline-danger');
    }

    autoPlayInterval = setInterval(simulateDay, 1500);
    showToast('Авто-режим включён', 'info');
}

function stopAutoPlay() {
    isAutoPlaying = false;
    if (autoPlayInterval) {
        clearInterval(autoPlayInterval);
        autoPlayInterval = null;
    }

    const btnAutoPlay = document.getElementById('btnAutoPlay');
    if (btnAutoPlay) {
        btnAutoPlay.innerHTML = '<i class="bi bi-play-circle me-2"></i>Авто-режим';
        btnAutoPlay.classList.remove('btn-outline-danger');
        btnAutoPlay.classList.add('btn-outline-secondary');
    }
}

// Утилиты
function showToast(message, type = 'info') {
    const container = document.getElementById('toastContainer');
    if (!container) return;

    const toastId = 'toast-' + Date.now();
    const toast = document.createElement('div');
    toast.className = `toast align-items-center text-white bg-${type} border-0 show`;
    toast.id = toastId;
    toast.setAttribute('role', 'alert');
    toast.innerHTML = `
        <div class="d-flex">
            <div class="toast-body">${message}</div>
            <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" onclick="removeToast('${toastId}')"></button>
        </div>
    `;

    container.appendChild(toast);

    // Автоудаление через 3 секунды
    setTimeout(() => removeToast(toastId), 3000);
}

function removeToast(toastId) {
    const toast = document.getElementById(toastId);
    if (toast) {
        toast.remove();
    }
}

function delay(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

// Модальное окно для импорта/экспорта
let modalMode = 'export'; // 'export' или 'import'

function openExportModal() {
    if (!simulationState) {
        showToast('Сначала загрузите конфигурацию', 'warning');
        return;
    }
    
    modalMode = 'export';
    document.getElementById('modalTitle').textContent = 'Экспорт конфигурации';
    document.getElementById('jsonTextarea').value = JSON.stringify(simulationState, null, 2);
    document.getElementById('btnImportConfirm').style.display = 'none';
    document.getElementById('btnPasteFromClipboard').style.display = 'none';
    document.getElementById('btnCopyToClipboard').style.display = 'inline-block';
    document.getElementById('jsonModal').classList.add('show');
}

function openImportModal() {
    modalMode = 'import';
    document.getElementById('modalTitle').textContent = 'Импорт конфигурации';
    document.getElementById('jsonTextarea').value = '';
    document.getElementById('btnImportConfirm').style.display = 'inline-block';
    document.getElementById('btnPasteFromClipboard').style.display = 'inline-block';
    document.getElementById('btnCopyToClipboard').style.display = 'none';
    document.getElementById('jsonModal').classList.add('show');
}

function closeJsonModal() {
    document.getElementById('jsonModal').classList.remove('show');
}

async function copyToClipboard() {
    const textarea = document.getElementById('jsonTextarea');
    try {
        await navigator.clipboard.writeText(textarea.value);
        showToast('JSON скопирован в буфер обмена', 'success');
    } catch (err) {
        // Fallback для старых браузеров
        textarea.select();
        document.execCommand('copy');
        showToast('JSON скопирован в буфер обмена', 'success');
    }
}

async function pasteFromClipboard() {
    const textarea = document.getElementById('jsonTextarea');
    try {
        const text = await navigator.clipboard.readText();
        textarea.value = text;
        showToast('JSON вставлен из буфера обмена', 'success');
    } catch (err) {
        showToast('Не удалось вставить из буфера обмена', 'danger');
    }
}

function importJson() {
    const textarea = document.getElementById('jsonTextarea');
    try {
        const data = JSON.parse(textarea.value);

        // Простая валидация
        if (!data.config || !data.board) {
            throw new Error('Неверный формат: отсутствуют config или board');
        }

        simulationState = data;
        renderBoard();
        renderWorkers();
        renderHistory();
        updateControls();
        closeJsonModal();

        // Расчёт всех метрик после импорта
        calculateAllMetrics();

        showToast('Конфигурация импортирована', 'success');
    } catch (err) {
        showToast('Ошибка JSON: ' + err.message, 'danger');
    }
}

// Закрытие модального окна по клику вне его
document.addEventListener('click', (e) => {
    const modal = document.getElementById('jsonModal');
    if (e.target === modal) {
        closeJsonModal();
    }
});

// Закрытие по Escape
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
        closeJsonModal();
    }
});

// Переключение панели метрик
function toggleMetricsPanel() {
    const panel = document.getElementById('metricsPanel');
    if (panel) {
        panel.classList.toggle('collapsed');
    }
}

// Расчёт всех метрик через единый API
async function calculateAllMetrics() {
    if (!simulationState) {
        return;
    }

    try {
        const response = await fetch('/api/simulation/all-metrics', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(simulationState)
        });

        if (!response.ok) {
            console.error('Error calculating all metrics:', response.status);
            return;
        }

        currentAllMetrics = await response.json();
        
        // Рендерим все секции метрик
        renderMetrics(currentAllMetrics.simulationMetrics);
        renderWorkerMetrics(currentAllMetrics.workerMetrics);
        renderTaskMetrics(currentAllMetrics.taskMetrics);
        renderStageMetrics(currentAllMetrics.stageMetrics);
    } catch (error) {
        console.error('Error calculating all metrics:', error);
    }
}

// Рендеринг метрик
function renderMetrics(metrics) {
    const metricsGrid = document.getElementById('metricsGrid');
    if (!metricsGrid || !metrics) return;

    metricsGrid.innerHTML = `
        ${renderLeadTimeCard(metrics.leadTime)}
        ${renderThroughputCard(metrics.throughput)}
        ${renderFlowEfficiencyCard(metrics.flowEfficiency)}
        ${renderFrequencyCard(metrics.frequency)}
    `;
}

// Карточка Lead Time
function renderLeadTimeCard(leadTime) {
    return `
        <div class="metric-card lead-time">
            <div class="metric-title">
                <i class="bi bi-clock"></i>
                <span>Lead Time</span>
            </div>
            <div class="metric-value">${leadTime.p50.toFixed(1)} д</div>
            <div class="metric-subvalue">P50 (медиана)</div>
            <div class="metric-subvalue">P85: ${leadTime.p85.toFixed(1)} д</div>
            <div class="metric-subvalue">Задач: ${leadTime.taskCount}</div>
        </div>
    `;
}

// Карточка Throughput
function renderThroughputCard(throughput) {
    return `
        <div class="metric-card throughput">
            <div class="metric-title">
                <i class="bi bi-speedometer2"></i>
                <span>Throughput</span>
            </div>
            <div class="metric-value">${throughput.overall.toFixed(2)}</div>
            <div class="metric-subvalue">задач/день (среднее)</div>
            <div class="metric-subvalue">Всего дней: ${throughput.dailyHistory?.length || 0}</div>
        </div>
    `;
}

// Карточка Flow Efficiency
function renderFlowEfficiencyCard(flowEfficiency) {
    return `
        <div class="metric-card flow-efficiency">
            <div class="metric-title">
                <i class="bi bi-pie-chart"></i>
                <span>Flow Efficiency</span>
            </div>
            <div class="metric-value">${flowEfficiency.efficiencyPercent.toFixed(1)}%</div>
            <div class="metric-subvalue">Активное время: ${flowEfficiency.activeTime.toFixed(1)} д</div>
            <div class="metric-subvalue">Время ожидания: ${flowEfficiency.waitTime.toFixed(1)} д</div>
        </div>
    `;
}

// Карточка Frequency
function renderFrequencyCard(frequency) {
    const maxCount = Math.max(...Object.values(frequency.distribution), 1);
    
    return `
        <div class="metric-card frequency">
            <div class="metric-title">
                <i class="bi bi-bar-chart"></i>
                <span>Распределение по времени</span>
            </div>
            <div class="metric-subvalue">Всего задач: ${frequency.taskCount}</div>
            <div class="frequency-distribution">
                ${Object.entries(frequency.distribution)
                    .sort((a, b) => parseInt(a[0]) - parseInt(b[0]))
                    .map(([bucket, count]) => {
                        const barWidth = (count / maxCount) * 100;
                        return `
                            <div class="frequency-item">
                                <span>${bucket} д</span>
                                <div style="display: flex; align-items: center; gap: 8px;">
                                    <span>${count}</span>
                                    <div class="frequency-bar" style="width: ${barWidth}%"></div>
                                </div>
                            </div>
                        `;
                    }).join('')}
            </div>
        </div>
    `;
}

// Переключение панели метрик работников
function toggleWorkerMetricsPanel() {
    const panel = document.getElementById('workerMetricsPanel');
    if (panel) {
        panel.classList.toggle('collapsed');
    }
}

// Рендеринг метрик работников
function renderWorkerMetrics(workerMetrics) {
    const grid = document.getElementById('workerMetricsGrid');
    if (!grid || !workerMetrics) return;

    grid.innerHTML = workerMetrics.map(w => `
        <div class="worker-metric-card">
            <div class="worker-metric-header">
                <i class="bi bi-person-circle"></i>
                <span class="worker-name">${w.login}</span>
            </div>
            <div class="worker-metric-body">
                <div class="worker-metric-row">
                    <span class="worker-metric-label">Throughput:</span>
                    <span class="worker-metric-value">${w.throughput.toFixed(2)} зад/день</span>
                </div>
                <div class="worker-metric-row">
                    <span class="worker-metric-label">Lead Time:</span>
                    <span class="worker-metric-value">${w.leadTime.toFixed(1)} дн</span>
                </div>
                <div class="worker-metric-row">
                    <span class="worker-metric-label">Утилизация:</span>
                    <span class="worker-metric-value ${w.efficiencyPercent < 50 ? 'low-efficiency' : ''}">${w.efficiencyPercent.toFixed(1)}%</span>
                </div>
                <div class="worker-metric-row small-text">
                    <span class="worker-metric-label">Work:</span>
                    <span class="worker-metric-value">${w.workTimeDays.toFixed(1)} дн</span>
                </div>
                <div class="worker-metric-row small-text">
                    <span class="worker-metric-label">Buffer:</span>
                    <span class="worker-metric-value">${w.bufferTimeDays.toFixed(1)} дн</span>
                </div>
                <div class="worker-metric-row small-text">
                    <span class="worker-metric-label">Ценных задач:</span>
                    <span class="worker-metric-value">${w.valuableTasksCount}</span>
                </div>
            </div>
        </div>
    `).join('');
}

// Переключение панели метрик задач
function toggleTaskMetricsPanel() {
    const panel = document.getElementById('taskMetricsPanel');
    if (panel) {
        panel.classList.toggle('collapsed');
    }
}

// Рендеринг метрик задач
function renderTaskMetrics(taskMetrics) {
    const grid = document.getElementById('taskMetricsGrid');
    if (!grid) {
        console.error('taskMetricsGrid element not found');
        return;
    }
    if (!taskMetrics || taskMetrics.length === 0) {
        console.warn('No task metrics to render');
        grid.innerHTML = '<div class="text-muted">Нет данных для отображения</div>';
        return;
    }

    console.log('Rendering task metrics:', taskMetrics);

    grid.innerHTML = taskMetrics.map(t => `
        <div class="task-metric-card">
            <div class="task-metric-header">
                <span class="task-key">${t.taskKey}</span>
                <span class="task-status status-${t.status.toLowerCase().replace(' ', '-')}">${t.status}</span>
            </div>
            <div class="task-metric-summary">
                <div class="task-summary-row">
                    <span class="task-summary-label">Размер:</span>
                    <span class="task-summary-value">${t.shirtType || 'N/A'}</span>
                </div>
                <div class="task-summary-row">
                    <span class="task-summary-label">Lead Time:</span>
                    <span class="task-summary-value">${t.leadTimeDays.toFixed(1)} дн</span>
                </div>
                <div class="task-summary-row">
                    <span class="task-summary-label">Flow Efficiency:</span>
                    <span class="task-summary-value ${t.flowEfficiencyPercent < 50 ? 'low-efficiency' : ''}">${t.flowEfficiencyPercent.toFixed(1)}%</span>
                </div>
            </div>
            <div class="task-metric-body">
                <div class="task-metric-row">
                    <span class="task-metric-label">Active:</span>
                    <span class="task-metric-value">${t.activeTimeDays.toFixed(1)} дн</span>
                </div>
                <div class="task-metric-row">
                    <span class="task-metric-label">Wait:</span>
                    <span class="task-metric-value">${t.waitTimeDays.toFixed(1)} дн</span>
                </div>
            </div>
            <div class="task-stages">
                <div class="task-stages-header" onclick="toggleTaskStages(this)">
                    <div class="task-stages-title">
                        <i class="bi bi-layers"></i>
                        <span>Стадии</span>
                    </div>
                    <i class="bi bi-chevron-down task-stages-toggle"></i>
                </div>
                <div class="task-stages-content collapsed">
                    ${t.stages.map(s => `
                        <div class="task-stage-row">
                            <div class="task-stage-info">
                                <span class="task-stage-name">${s.stageName}</span>
                                <span class="task-stage-type ${s.stageType.toLowerCase()}">${s.stageType}</span>
                            </div>
                            <div class="task-stage-details">
                                <span class="task-stage-time">${s.timeInStageDays.toFixed(1)} дн</span>
                                ${s.workers && s.workers.length > 0 ? `
                                    <div class="task-stage-workers">
                                        ${s.workers.map(w => `<span class="worker-badge">${w}</span>`).join('')}
                                    </div>
                                ` : ''}
                            </div>
                        </div>
                    `).join('')}
                </div>
            </div>
        </div>
    `).join('');
}

// Переключение сворачивания блока стадий
function toggleTaskStages(headerElement) {
    const content = headerElement.nextElementSibling;
    const toggle = headerElement.querySelector('.task-stages-toggle');
    if (content && toggle) {
        content.classList.toggle('collapsed');
        toggle.classList.toggle('collapsed');
    }
}

// Переключение панели метрик стадий
function toggleStageMetricsPanel() {
    const panel = document.getElementById('stageMetricsPanel');
    if (panel) {
        panel.classList.toggle('collapsed');
    }
}

// Переключение панели CFD
function toggleCfdPanel() {
    const panel = document.getElementById('cfdPanel');
    if (panel) {
        panel.classList.toggle('collapsed');
    }
}

// Рендеринг метрик стадий
function renderStageMetrics(stageMetrics) {
    const grid = document.getElementById('stageMetricsGrid');
    if (!grid) {
        console.error('stageMetricsGrid element not found');
        return;
    }
    if (!stageMetrics || stageMetrics.length === 0) {
        console.warn('No stage metrics to render');
        grid.innerHTML = '<div class="text-muted">Нет данных для отображения</div>';
        return;
    }

    console.log('Rendering stage metrics:', stageMetrics);

    // Находим максимальное P85 для подсветки узких мест
    const maxP85 = Math.max(...stageMetrics.map(s => s.p85Days));

    grid.innerHTML = `
        <table class="stage-metrics-table">
            <thead>
                <tr>
                    <th>Стадия</th>
                    <th>Тип</th>
                    <th>Задач</th>
                    <th>P50</th>
                    <th>P85</th>
                    <th>P95</th>
                    <th>Среднее</th>
                    <th>Макс</th>
                </tr>
            </thead>
            <tbody>
                ${stageMetrics.map(s => `
                    <tr>
                        <td class="stage-name">${s.stageName}</td>
                        <td>
                            <span class="stage-type-badge stage-type-${s.stageType.toLowerCase()}">
                                ${s.stageType}
                            </span>
                        </td>
                        <td>${s.taskCount}</td>
                        <td class="metric-value">${s.p50Days.toFixed(1)}</td>
                        <td class="metric-value ${s.p85Days >= maxP85 * 0.8 ? 'highlight' : ''}">
                            ${s.p85Days.toFixed(1)}
                        </td>
                        <td class="metric-value">${s.p95Days.toFixed(1)}</td>
                        <td>${s.avgDays.toFixed(1)}</td>
                        <td class="metric-value">${s.maxDays.toFixed(1)}</td>
                    </tr>
                `).join('')}
            </tbody>
        </table>
    `;
}

// Сбор данных для CFD из истории симуляции
function collectCfdData() {
    if (!simulationState || !simulationState.history || simulationState.history.length === 0) {
        return null;
    }

    const allStages = simulationState.config.workflow.stages;
    const stageNames = allStages.map(s => s.name);
    
    // Цвета для стадий (красивая палитра)
    const stageColors = {
        'Todo': '#6c757d',
        'Developing': '#667eea',
        'Testing': '#17a2b8',
        'Code Review': '#ffc107',
        'Ready for Testing': '#20c997',
        'Ready to Merge': '#0dcaf0',
        'Release Preparation': '#fd7e14',
        'Done': '#28a745'
    };

    // Получаем цвета для всех стадий
    const colors = stageNames.map(name => 
        stageColors[name] || getColorForStage(name)
    );

    // Собираем данные по дням: для каждого дня считаем количество задач в каждой стадии
    const maxDay = simulationState.currentDay;
    const totalTasks = simulationState.board.tasks.length;
    
    // Инициализируем массив данных для каждого дня (от 0 до maxDay)
    const cfdData = [];
    
    // День 0: все задачи в Todo
    const initialCounts = {};
    for (const stageName of stageNames) {
        initialCounts[stageName] = stageName === 'Todo' ? totalTasks : 0;
    }
    cfdData.push({ day: 0, counts: initialCounts });
    
    // Для каждого дня строим состояние задач
    // Копируем начальные позиции всех задач (все в Todo)
    const taskPositions = {};
    for (const task of simulationState.board.tasks) {
        taskPositions[task.key] = 'Todo';
    }
    
    // Проходим по всем дням истории
    for (const dayHistory of simulationState.history) {
        const dayNumber = dayHistory.dayNumber;
        
        // Применяем все события TaskMoved за этот день
        for (const activity of dayHistory.activities) {
            if (activity.type === 'TaskMoved' && activity.taskKey) {
                taskPositions[activity.taskKey] = activity.stageName;
            }
        }
        
        // Считаем задачи в каждой стадии на конец дня
        const stageCounts = {};
        for (const stageName of stageNames) {
            stageCounts[stageName] = 0;
        }
        
        for (const taskKey of Object.keys(taskPositions)) {
            const stageName = taskPositions[taskKey];
            if (stageName && stageCounts.hasOwnProperty(stageName)) {
                stageCounts[stageName]++;
            }
        }
        
        cfdData.push({
            day: dayNumber,
            counts: stageCounts
        });
    }

    return {
        stageNames,
        colors,
        data: cfdData
    };
}

// Получить цвет для стадии (генерация по имени)
function getColorForStage(stageName) {
    const hash = stageName.split('').reduce((acc, char) => acc + char.charCodeAt(0), 0);
    const hue = hash % 360;
    return `hsl(${hue}, 70%, 50%)`;
}

// Рендеринг CFD графика с интерактивной подсветкой
function renderCfdChart() {
    const chartContainer = document.getElementById('cfdChart');
    if (!chartContainer) {
        console.error('cfdChart element not found');
        return;
    }

    const cfdData = collectCfdData();
    if (!cfdData || cfdData.data.length === 0) {
        chartContainer.innerHTML = '<div class="text-muted text-center py-5">Нет данных для отображения CFD. Запустите симуляцию.</div>';
        return;
    }

    const { stageNames, colors, data } = cfdData;

    // Параметры графика
    const width = 650;
    const height = 350;
    const padding = { top: 20, right: 20, bottom: 50, left: 50 };
    const chartWidth = width - padding.left - padding.right;
    const chartHeight = height - padding.top - padding.bottom;

    // Находим максимальное количество задач (для масштаба Y)
    const maxTasks = data.length > 0 ? Math.max(...data.map(d => Object.values(d.counts).reduce((a, b) => a + b, 0))) : 1;
    const maxDay = Math.max(...data.map(d => d.day), 1);

    // Функции масштабирования
    const xScale = (day) => padding.left + (day / maxDay) * chartWidth;
    const yScale = (count) => padding.top + chartHeight - (count / maxTasks) * chartHeight;

    // Рисуем STACKED areas (слоями) для каждой стадии
    const reversedStageNames = [...stageNames].reverse();
    const reversedColors = [...colors].reverse();

    let cumulativeBottom = data.map(d => 0);
    const stageAreas = []; // Сохраняем информацию об областях для интерактивности

    for (let i = 0; i < reversedStageNames.length; i++) {
        const stageName = reversedStageNames[i];
        const color = reversedColors[i];

        // Считаем кумулятивную сумму для верхней границы этой области
        const cumulativeTop = data.map((d, idx) => {
            const count = d.counts[stageName] || 0;
            return (cumulativeBottom[idx] || 0) + count;
        });

        // Пропускаем стадии, где никогда не было задач
        const hasTasks = cumulativeTop.some(val => val > 0);
        if (!hasTasks) {
            cumulativeBottom = cumulativeTop;
            continue;
        }

        // Строим путь для области
        let areaPath = `M ${xScale(data[0].day)} ${yScale(cumulativeBottom[0])}`;

        for (let j = 0; j < data.length; j++) {
            areaPath += ` L ${xScale(data[j].day)} ${yScale(cumulativeTop[j])}`;
        }

        for (let j = data.length - 1; j >= 0; j--) {
            areaPath += ` L ${xScale(data[j].day)} ${yScale(cumulativeBottom[j])}`;
        }

        areaPath += ' Z';

        // Сохраняем данные о стадии для использования в обработчиках
        stageAreas.push({
            stageName,
            color,
            path: areaPath,
            cumulativeTop,
            cumulativeBottom: [...cumulativeBottom]
        });

        cumulativeBottom = cumulativeTop;
    }

    // Генерируем SVG с интерактивными элементами
    let svg = `<svg class="cfd-svg" viewBox="0 0 ${width} ${height}" preserveAspectRatio="xMidYMid meet">`;

    // Сетка
    const yGridLines = 5;
    for (let i = 0; i <= yGridLines; i++) {
        const y = padding.top + (i / yGridLines) * chartHeight;
        const value = Math.round(maxTasks - (i / yGridLines) * maxTasks);
        svg += `<line class="cfd-grid-line" x1="${padding.left}" y1="${y}" x2="${width - padding.right}" y2="${y}" />`;
        svg += `<text class="cfd-axis-label" x="${padding.left - 10}" y="${y + 4}" text-anchor="end">${value}</text>`;
    }

    // Ось X
    const xSteps = Math.min(maxDay + 1, 10);
    for (let i = 0; i <= xSteps; i++) {
        const day = Math.round((i / xSteps) * maxDay);
        const x = xScale(day);
        svg += `<line class="cfd-grid-line" x1="${x}" y1="${padding.top}" x2="${x}" y2="${height - padding.bottom}" />`;
        svg += `<text class="cfd-axis-label" x="${x}" y="${height - padding.bottom + 15}" text-anchor="middle">${day}</text>`;
    }

    // Рисуем области с data-атрибутами для интерактивности
    stageAreas.forEach((area, index) => {
        svg += `<path class="cfd-area" 
                    d="${area.path}" 
                    fill="${area.color}" 
                    data-stage-name="${area.stageName}"
                    data-stage-index="${index}"
                    style="pointer-events: all;"/>`;
    });

    // Верхняя граница
    let topLine = `M ${xScale(data[0].day)} ${yScale(cumulativeBottom[0])}`;
    for (let j = 1; j < data.length; j++) {
        topLine += ` L ${xScale(data[j].day)} ${yScale(cumulativeBottom[j])}`;
    }
    svg += `<path class="cfd-line" d="${topLine}" stroke="#333" stroke-width="2" fill="none" style="pointer-events: none;"/>`;

    svg += '</svg>';

    // Легенда с интерактивными элементами
    const numColumns = reversedStageNames.length > 6 ? 3 : (reversedStageNames.length > 3 ? 2 : 1);
    const itemsPerColumn = Math.ceil(reversedStageNames.length / numColumns);
    let legendColumns = '';

    for (let col = 0; col < numColumns; col++) {
        const start = col * itemsPerColumn;
        const end = Math.min(start + itemsPerColumn, reversedStageNames.length);
        if (start >= reversedStageNames.length) break;

        let columnHtml = '<div class="cfd-legend-column">';
        for (let i = start; i < end; i++) {
            columnHtml += `
                <div class="cfd-legend-item" 
                     data-stage-name="${reversedStageNames[i]}"
                     data-stage-index="${i}">
                    <div class="cfd-legend-color" style="background: ${reversedColors[i]}"></div>
                    <span>${reversedStageNames[i]}</span>
                </div>
            `;
        }
        columnHtml += '</div>';
        legendColumns += columnHtml;
    }

    // Добавляем тултип
    const tooltipHtml = '<div class="cfd-tooltip" id="cfdTooltip"></div>';

    chartContainer.innerHTML = `
        <div class="cfd-chart-wrapper">
            <div class="cfd-svg-container" style="height: ${height + 40}px;">
                ${svg}
            </div>
            <div class="cfd-legend-container">
                ${legendColumns}
            </div>
        </div>
        ${tooltipHtml}
    `;

    // Навешиваем обработчики событий после рендеринга
    initCfdInteractivity(chartContainer, stageAreas, data);
}

// Инициализация интерактивности CFD
function initCfdInteractivity(chartContainer, stageAreas, data) {
    const tooltip = document.getElementById('cfdTooltip');
    if (!tooltip) return;

    const areaElements = chartContainer.querySelectorAll('.cfd-area');
    const legendItems = chartContainer.querySelectorAll('.cfd-legend-item');

    // Создаём маппинг stageName -> элементы
    const stageMap = new Map();
    
    areaElements.forEach(area => {
        const stageName = area.dataset.stageName;
        stageMap.set(stageName, { ...stageMap.get(stageName), area });
    });

    legendItems.forEach(item => {
        const stageName = item.dataset.stageName;
        const existing = stageMap.get(stageName) || {};
        stageMap.set(stageName, { ...existing, legend: item });
    });

    // Обработчики для областей графика
    areaElements.forEach(area => {
        area.addEventListener('mouseenter', (e) => handleCfdHover(e, area.dataset.stageName, stageMap, data, tooltip, true));
        area.addEventListener('mouseleave', () => handleCfdLeave(stageMap, tooltip));
        area.addEventListener('mousemove', (e) => handleCfdMove(e, tooltip));
    });

    // Обработчики для элементов легенды
    legendItems.forEach(item => {
        item.addEventListener('mouseenter', (e) => handleCfdHover(e, item.dataset.stageName, stageMap, data, tooltip, false));
        item.addEventListener('mouseleave', () => handleCfdLeave(stageMap, tooltip));
        item.addEventListener('mousemove', (e) => handleCfdMove(e, tooltip));
    });
}

// Обработчик наведения на область/легенду
function handleCfdHover(event, stageName, stageMap, data, tooltip, isArea) {
    const stageData = stageMap.get(stageName);
    if (!stageData) return;

    // Подсветка активной области
    if (stageData.area) {
        stageData.area.classList.remove('dimmed');
        stageData.area.style.opacity = '1';
        stageData.area.style.filter = 'brightness(1.1)';
    }

    // Подсветка активной легенды
    if (stageData.legend) {
        stageData.legend.classList.add('active');
        stageData.legend.classList.remove('dimmed');
    }

    // Затемнение остальных элементов
    stageMap.forEach((value, key) => {
        if (key !== stageName) {
            if (value.area) {
                value.area.classList.add('dimmed');
            }
            if (value.legend) {
                value.legend.classList.add('dimmed');
                value.legend.classList.remove('active');
            }
        }
    });

    // Показываем тултип с данными
    showTooltip(stageName, data, tooltip);
}

// Обработчик ухода с области/легенды
function handleCfdLeave(stageMap, tooltip) {
    // Сброс всех стилей
    stageMap.forEach((value) => {
        if (value.area) {
            value.area.classList.remove('dimmed');
            value.area.style.opacity = '';
            value.area.style.filter = '';
        }
        if (value.legend) {
            value.legend.classList.remove('active', 'dimmed');
        }
    });

    // Скрываем тултип
    tooltip.classList.remove('show');
}

// Обработчик движения мыши (для позиционирования тултипа)
function handleCfdMove(event, tooltip) {
    const container = document.getElementById('cfdChart');
    if (!container) return;

    const rect = container.getBoundingClientRect();
    const x = event.clientX - rect.left + 15;
    const y = event.clientY - rect.top + 15;

    // Проверка выхода за границы
    const tooltipWidth = 250;
    const tooltipHeight = 80;

    let finalX = x;
    let finalY = y;

    if (x + tooltipWidth > rect.width) {
        finalX = x - tooltipWidth - 10;
    }

    if (y + tooltipHeight > rect.height) {
        finalY = y - tooltipHeight - 10;
    }

    tooltip.style.left = `${finalX}px`;
    tooltip.style.top = `${finalY}px`;
}

// Показ тултипа с данными о стадии
function showTooltip(stageName, data, tooltip) {
    // Находим последнее значение для этой стадии
    const latestData = data[data.length - 1];
    const count = latestData?.counts[stageName] || 0;
    const day = latestData?.day || 0;

    tooltip.innerHTML = `
        <div class="cfd-tooltip-title">${stageName}</div>
        <div class="cfd-tooltip-value">
            <span>Задач:</span>
            <span class="cfd-tooltip-count">${count}</span>
        </div>
        <div class="cfd-tooltip-value" style="margin-top: 4px; font-size: 0.75rem; opacity: 0.8;">
            <span>День ${day}</span>
        </div>
    `;

    tooltip.classList.add('show');
}

// Интеграция рендеринга CFD в calculateAllMetrics
async function calculateAllMetrics() {
    if (!simulationState) {
        return;
    }

    try {
        const response = await fetch('/api/simulation/all-metrics', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(simulationState)
        });

        if (!response.ok) {
            console.error('Error calculating all metrics:', response.status);
            return;
        }

        currentAllMetrics = await response.json();

        // Рендерим все секции метрик
        renderMetrics(currentAllMetrics.simulationMetrics);
        renderWorkerMetrics(currentAllMetrics.workerMetrics);
        renderTaskMetrics(currentAllMetrics.taskMetrics);
        renderStageMetrics(currentAllMetrics.stageMetrics);
        
        // Рендерим CFD график
        renderCfdChart();
    } catch (error) {
        console.error('Error calculating all metrics:', error);
    }
}
