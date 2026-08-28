#!/usr/bin/env bash
#
# build-raspberrypi.sh — сборка и публикация KanbanFlow для Raspberry Pi (Debian /
# Raspberry Pi OS). Аналог build-macos.sh.
#
# Что делает скрипт:
#   1. Проверяет наличие .NET SDK 9. Если его нет — скачивает и ставит локально
#      в каталог репозитория (./.dotnet), система при этом не трогается.
#   2. Публикует KanbanFlowApi как self-contained приложение под архитектуру Pi
#      (linux-arm64 / linux-arm / linux-x64): нужный .NET runtime вшит внутрь,
#      отдельно ничего ставить не надо.
#   3. Кладёт готовое приложение в отдельную папку (по умолчанию
#      ~/Applications/KanbanFlow) и создаёт:
#        - run.sh            — запускалка из терминала;
#        - KanbanFlow.desktop — ярлык для двойного клика в файловом менеджере
#          и в меню приложений (аналог .command в macOS).
#   4. По флагу --service ставит systemd-сервис, чтобы приложение поднималось
#      само при загрузке Pi (удобно для «безголовой» установки).
#
# Использование:
#   ./build-raspberrypi.sh                  # публикация в ~/Applications/KanbanFlow
#   ./build-raspberrypi.sh -o /путь/куда    # своя папка назначения
#   ./build-raspberrypi.sh --install-deps   # доставить системные пакеты (apt, sudo)
#   ./build-raspberrypi.sh --service        # + systemd-сервис (автозапуск при загрузке)
#   PORT=5200 ./build-raspberrypi.sh        # порт веб-сервера (по умолчанию 5200)
#   HOST=127.0.0.1 ./build-raspberrypi.sh   # адрес привязки (по умолчанию 0.0.0.0 — виден в сети)
#
set -euo pipefail

# ─────────────────────────────────────────────────────────────────────────────
# Параметры
# ─────────────────────────────────────────────────────────────────────────────
REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$REPO_DIR/KanbanFlowApi/KanbanFlowApi.csproj"
APP_NAME="KanbanFlow"
DOTNET_CHANNEL="9.0"
PORT="${PORT:-5200}"
HOST="${HOST:-0.0.0.0}"
OUTPUT_DIR="${HOME}/Applications/${APP_NAME}"
INSTALL_DEPS=0
INSTALL_SERVICE=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    -o|--output)   OUTPUT_DIR="$2"; shift 2 ;;
    --host)        HOST="$2"; shift 2 ;;
    --install-deps) INSTALL_DEPS=1; shift ;;
    --service)     INSTALL_SERVICE=1; shift ;;
    -h|--help)
      grep '^#' "$0" | sed 's/^# \{0,1\}//'
      exit 0 ;;
    *) echo "Неизвестный аргумент: $1" >&2; exit 1 ;;
  esac
done

info()  { printf '\033[1;34m==>\033[0m %s\n' "$*"; }
warn()  { printf '\033[1;33m[!]\033[0m %s\n' "$*"; }
die()   { printf '\033[1;31m[x]\033[0m %s\n' "$*" >&2; exit 1; }

[[ "$(uname -s)" == "Linux" ]] || die "Скрипт рассчитан на Linux (Raspberry Pi OS / Debian)."
[[ -f "$PROJECT" ]] || die "Не найден проект: $PROJECT (запусти скрипт из корня репозитория)."

download() { # url dest
  if command -v curl >/dev/null 2>&1; then curl -fsSL "$1" -o "$2"
  elif command -v wget >/dev/null 2>&1; then wget -qO "$2" "$1"
  else die "Нужен curl или wget."; fi
}

# ─────────────────────────────────────────────────────────────────────────────
# Определяем архитектуру → RID
# ─────────────────────────────────────────────────────────────────────────────
case "$(uname -m)" in
  aarch64|arm64)   RID="linux-arm64"; DOTNET_ARCH="arm64" ;;
  armv7l|armv8l)   RID="linux-arm";   DOTNET_ARCH="arm" ;;
  armv6l)          die "ARMv6 (Pi 1 / Pi Zero W) не поддерживается .NET. Нужен Pi 2+ / Zero 2." ;;
  x86_64|amd64)    RID="linux-x64";   DOTNET_ARCH="x64" ;;
  *) die "Неизвестная архитектура: $(uname -m)" ;;
esac
info "Архитектура: $(uname -m) → $RID"

# ─────────────────────────────────────────────────────────────────────────────
# Системные пакеты (по флагу --install-deps)
# ─────────────────────────────────────────────────────────────────────────────
# Приложение self-contained и InvariantGlobalization=true, поэтому ICU не нужен.
# Достаточно базовых библиотек, которые на Raspberry Pi OS Bookworm уже стоят.
DEPS="curl ca-certificates libstdc++6 zlib1g libssl3 libgcc-s1"
if [[ "$INSTALL_DEPS" -eq 1 ]]; then
  info "Ставлю системные пакеты: $DEPS"
  sudo apt-get update
  for pkg in $DEPS; do
    sudo apt-get install -y "$pkg" || warn "Пакет $pkg не установлен (возможно, он не нужен в этой версии ОС)."
  done
fi

# ─────────────────────────────────────────────────────────────────────────────
# Ищем .NET SDK 9. Если нет — ставим локально в ./.dotnet
# ─────────────────────────────────────────────────────────────────────────────
LOCAL_DOTNET_DIR="$REPO_DIR/.dotnet"

have_sdk9() {
  local dotnet_bin="$1"
  command -v "$dotnet_bin" >/dev/null 2>&1 || return 1
  "$dotnet_bin" --list-sdks 2>/dev/null | grep -q "^${DOTNET_CHANNEL//./\\.}\." || return 1
}

DOTNET=""
if have_sdk9 dotnet; then
  DOTNET="$(command -v dotnet)"
  info "Найден системный .NET SDK: $("$DOTNET" --version)"
elif have_sdk9 "$LOCAL_DOTNET_DIR/dotnet"; then
  DOTNET="$LOCAL_DOTNET_DIR/dotnet"
  info "Найден локальный .NET SDK: $("$DOTNET" --version)"
else
  warn ".NET SDK $DOTNET_CHANNEL не найден — ставлю локально в $LOCAL_DOTNET_DIR"
  INSTALL_SCRIPT="$(mktemp)"
  download https://dot.net/v1/dotnet-install.sh "$INSTALL_SCRIPT"
  chmod +x "$INSTALL_SCRIPT"
  "$INSTALL_SCRIPT" --channel "$DOTNET_CHANNEL" --architecture "$DOTNET_ARCH" --install-dir "$LOCAL_DOTNET_DIR"
  rm -f "$INSTALL_SCRIPT"
  DOTNET="$LOCAL_DOTNET_DIR/dotnet"
  have_sdk9 "$DOTNET" || die "Установка .NET SDK не удалась."
  info "Установлен .NET SDK: $("$DOTNET" --version)"
fi

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

# ─────────────────────────────────────────────────────────────────────────────
# Публикация (self-contained)
# ─────────────────────────────────────────────────────────────────────────────
info "Очищаю папку назначения: $OUTPUT_DIR"
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

info "Собираю и публикую (на Pi это может занять несколько минут)…"
# self-contained: нужный .NET runtime кладётся рядом с приложением, ставить
# ничего не нужно. Single-file не используем — все файлы прячет запускалка.
"$DOTNET" publish "$PROJECT" \
  -c Release \
  -r "$RID" \
  --self-contained true \
  -p:DebugType=none \
  -p:DebugSymbols=false \
  -o "$OUTPUT_DIR"

APP_BIN="$OUTPUT_DIR/KanbanFlowApi"
[[ -f "$APP_BIN" ]] || die "После публикации не найден бинарник: $APP_BIN"
chmod +x "$APP_BIN"

# ─────────────────────────────────────────────────────────────────────────────
# Запускалка для терминала
# ─────────────────────────────────────────────────────────────────────────────
LAUNCHER="$OUTPUT_DIR/run.sh"
cat > "$LAUNCHER" <<LAUNCHER_EOF
#!/usr/bin/env bash
# Запуск KanbanFlow. Останов: Ctrl+C.
set -e
cd "\$(dirname "\$0")"

PORT="\${PORT:-$PORT}"
HOST="\${HOST:-$HOST}"

export ASPNETCORE_ENVIRONMENT="\${ASPNETCORE_ENVIRONMENT:-Development}"
export ASPNETCORE_URLS="http://\${HOST}:\${PORT}"

LOCAL_URL="http://localhost:\${PORT}"
LAN_IP="\$(hostname -I 2>/dev/null | awk '{print \$1}')"

echo "KanbanFlow запускается:"
echo "  локально:   \$LOCAL_URL"
[ -n "\$LAN_IP" ] && [ "\$HOST" != "127.0.0.1" ] && echo "  в сети:     http://\${LAN_IP}:\${PORT}   (и http://\$(hostname).local:\${PORT})"

# Если есть графическая сессия — откроем браузер, когда сервер поднимется
if [ -n "\${DISPLAY:-}\${WAYLAND_DISPLAY:-}" ] && command -v xdg-open >/dev/null 2>&1; then
  (
    for _ in \$(seq 1 60); do
      if curl -s -o /dev/null "\$LOCAL_URL"; then break; fi
      sleep 0.5
    done
    xdg-open "\$LOCAL_URL" >/dev/null 2>&1 || true
  ) &
fi

exec ./KanbanFlowApi
LAUNCHER_EOF
chmod +x "$LAUNCHER"

# ─────────────────────────────────────────────────────────────────────────────
# Ярлык .desktop — двойной клик в файловом менеджере и пункт в меню приложений
# ─────────────────────────────────────────────────────────────────────────────
DESKTOP_FILE="$OUTPUT_DIR/${APP_NAME}.desktop"
cat > "$DESKTOP_FILE" <<DESKTOP_EOF
[Desktop Entry]
Type=Application
Version=1.0
Name=KanbanFlow
Comment=Запустить KanbanFlow
Exec=$LAUNCHER
Path=$OUTPUT_DIR
Icon=$OUTPUT_DIR/wwwroot/favicon.ico
Terminal=true
Categories=Development;Education;
DESKTOP_EOF
chmod +x "$DESKTOP_FILE"

# Регистрируем в меню приложений
APPS_DIR="$HOME/.local/share/applications"
mkdir -p "$APPS_DIR"
cp "$DESKTOP_FILE" "$APPS_DIR/kanbanflow.desktop"
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$APPS_DIR" >/dev/null 2>&1 || true

# Кладём ярлык на рабочий стол (если он есть) и помечаем доверенным
DESKTOP_DIR="$(xdg-user-dir DESKTOP 2>/dev/null || echo "$HOME/Desktop")"
if [[ -d "$DESKTOP_DIR" ]]; then
  cp "$DESKTOP_FILE" "$DESKTOP_DIR/kanbanflow.desktop"
  chmod +x "$DESKTOP_DIR/kanbanflow.desktop"
  gio set "$DESKTOP_DIR/kanbanflow.desktop" metadata::trusted true 2>/dev/null || true
fi

# ─────────────────────────────────────────────────────────────────────────────
# systemd-сервис (по флагу --service) — автозапуск при загрузке Pi
# ─────────────────────────────────────────────────────────────────────────────
if [[ "$INSTALL_SERVICE" -eq 1 ]]; then
  UNIT_DIR="$HOME/.config/systemd/user"
  mkdir -p "$UNIT_DIR"
  cat > "$UNIT_DIR/kanbanflow.service" <<UNIT_EOF
[Unit]
Description=KanbanFlow
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
WorkingDirectory=$OUTPUT_DIR
ExecStart=$OUTPUT_DIR/KanbanFlowApi
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://$HOST:$PORT
Restart=on-failure
RestartSec=3

[Install]
WantedBy=default.target
UNIT_EOF

  systemctl --user daemon-reload
  systemctl --user enable --now kanbanflow.service
  # Чтобы сервис работал без входа пользователя в систему (headless)
  sudo loginctl enable-linger "$USER" 2>/dev/null || \
    warn "Не удалось включить linger — сервис стартует только после входа в систему. Выполни: sudo loginctl enable-linger $USER"
  info "systemd-сервис установлен и запущен (Production)."
fi

# ─────────────────────────────────────────────────────────────────────────────
LAN_IP="$(hostname -I 2>/dev/null | awk '{print $1}')"
info "Готово!"
echo
echo "  Приложение:  $OUTPUT_DIR"
echo "  Запуск:      двойной клик по «${APP_NAME}.desktop» (или пункт «KanbanFlow» в меню приложений)"
echo "               из терминала:  \"$LAUNCHER\""
echo "  Веб-интерфейс:"
echo "               локально:  http://localhost:${PORT}"
[[ -n "$LAN_IP" && "$HOST" != "127.0.0.1" ]] && \
echo "               в сети:    http://${LAN_IP}:${PORT}   (и http://$(hostname).local:${PORT})"
if [[ "$INSTALL_SERVICE" -eq 1 ]]; then
  echo
  echo "  Сервис:      systemctl --user status kanbanflow    # состояние"
  echo "               systemctl --user restart kanbanflow   # перезапуск"
  echo "               journalctl --user -u kanbanflow -f    # логи"
fi
