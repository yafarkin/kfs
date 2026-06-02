// KanbanFlow Simulation - Client Logic

let simulationState = null;
let autoPlayInterval = null;
let isAutoPlaying = false;
let isLoading = false;
let currentAllMetrics = null;

// Инициализация при загрузке страницы
document.addEventListener('DOMContentLoaded', () => {
    loadConfigPresets();
    updateLoadingIndicator();
    initSettingsPanel();
});

// Загрузка списка доступных конфигураций
async function loadConfigPresets() {
    try {
        const response = await fetch('/api/simulation/presets');
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        const presets = await response.json();

        const configSelector = document.getElementById('configSelector');
        if (configSelector) {
            configSelector.innerHTML = presets.map(preset =>
                `<option value="${preset.name}" ${preset.isDefault ? 'selected' : ''}>${preset.displayName}</option>`
            ).join('');

            // Сохраняем описания в data-атрибуты для подсказок
            configSelector.dataset.presets = JSON.stringify(presets);

            // Обновляем описание при изменении селекта
            configSelector.addEventListener('change', () => {
                updateConfigDescription(configSelector);
            });

            // Показываем описание для выбранной конфигурации
            updateConfigDescription(configSelector);
        }
    } catch (error) {
        console.error('Error loading config presets:', error);
        showToast('Ошибка загрузки списка конфигураций: ' + error.message, 'danger');
    }
}

// Обновление описания конфигурации
function updateConfigDescription(selector) {
    const descriptionEl = document.getElementById('configDescription');
    if (!descriptionEl || !selector.dataset.presets) return;

    const presets = JSON.parse(selector.dataset.presets);
    const selected = presets.find(p => p.name === selector.value);
    if (selected) {
        descriptionEl.textContent = selected.description;
    } else {
        descriptionEl.textContent = '';
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

        const response = await fetch(`/api/simulation/default-config?configName=${configName}`);
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        simulationState = await response.json();

        // Сохраняем текущее состояние переключателя вариативности в конфиге
        const variabilityToggle = document.getElementById('variabilityToggle');
        simulationState.config.useVariability = variabilityToggle?.checked ?? true;

        renderBoard();
        renderWorkers();
        renderHistory();
        updateControls();

        // Расчёт всех метрик после загрузки конфигурации
        calculateAllMetrics();

        showToast(`Конфигурация "${configName}" загружена`, 'success');
    } catch (error) {
        console.error('Error loading config:', error);
        showToast('Ошибка загрузки конфигурации: ' + error.message, 'danger');
    } finally {
        isLoading = false;
        updateLoadingIndicator();
    }
}

// Перезагрузка текущей конфигурации
async function reloadConfig() {
    if (!simulationState) {
        showToast('Сначала загрузите конфигурацию', 'warning');
        return;
    }
    await loadDefaultConfig();
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
                <div class="task-stages-title">
                    <i class="bi bi-layers"></i>
                    <span>Стадии</span>
                </div>
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
    `).join('');
}

// Переключение панели метрик стадий
function toggleStageMetricsPanel() {
    const panel = document.getElementById('stageMetricsPanel');
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
