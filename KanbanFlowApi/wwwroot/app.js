// KanbanFlow Simulation - Client Logic

let simulationState = null;
let autoPlayInterval = null;
let isAutoPlaying = false;
let isLoading = false;

// Инициализация при загрузке страницы
document.addEventListener('DOMContentLoaded', () => {
    loadDefaultConfig();
    updateLoadingIndicator();
});

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
    const btnLoad = document.getElementById('btnLoadConfig');
    
    if (btnSimulate) btnSimulate.disabled = isLoading || !simulationState;
    if (btnAutoPlay) btnAutoPlay.disabled = isLoading;
    if (btnLoad) btnLoad.disabled = isLoading;
}

// Загрузка конфигурации по умолчанию
async function loadDefaultConfig() {
    isLoading = true;
    updateLoadingIndicator();

    try {
        // Получаем выбранную конфигурацию из селекта
        const configSelector = document.getElementById('configSelector');
        const configName = configSelector?.value || 'default';
        
        // Получаем состояние переключателя вариативности
        const variabilityToggle = document.getElementById('variabilityToggle');
        const useVariability = variabilityToggle?.checked ?? true;

        const response = await fetch(`/api/simulation/default-config?configName=${configName}&useVariability=${useVariability}`);
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        simulationState = await response.json();
        renderBoard();
        renderWorkers();
        renderHistory();
        updateControls();
        showToast(`Конфигурация "${configName}" загружена`, 'success');
    } catch (error) {
        console.error('Error loading config:', error);
        showToast('Ошибка загрузки конфигурации: ' + error.message, 'danger');
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
        // Получаем состояние переключателя вариативности
        const variabilityToggle = document.getElementById('variabilityToggle');
        const useVariability = variabilityToggle?.checked ?? true;

        const response = await fetch(`/api/simulation/simulate-day?useVariability=${useVariability}`, {
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
    const currentTick = document.getElementById('currentTick');

    if (btnSimulateDay) {
        btnSimulateDay.disabled = !simulationState;
    }

    if (currentDay) {
        currentDay.textContent = simulationState?.currentDay || 0;
    }

    if (currentTick) {
        currentTick.textContent = simulationState?.currentTick || 0;
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
