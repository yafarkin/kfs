#!/usr/bin/env bash
#
# build-macos.sh — сборка и публикация KanbanFlow для macOS.
#
# Что делает скрипт:
#   1. Проверяет наличие .NET SDK 9. Если его нет — скачивает и ставит
#      локально в каталог репозитория (./.dotnet), система при этом не трогается.
#   2. Публикует KanbanFlowApi как self-contained приложение: нужный .NET runtime
#      уже вшит внутрь, отдельно ничего ставить не надо.
#   3. Кладёт готовое приложение в отдельную папку (по умолчанию
#      ~/Applications/KanbanFlow) и создаёт ярлык-запускалку KanbanFlow.command,
#      который можно запускать двойным кликом из Finder.
#
# Использование:
#   ./build-macos.sh                 # публикация в ~/Applications/KanbanFlow
#   ./build-macos.sh -o /путь/куда   # своя папка назначения
#   PORT=5200 ./build-macos.sh       # порт веб-сервера (по умолчанию 5200)
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
OUTPUT_DIR="${HOME}/Applications/${APP_NAME}"

while [[ $# -gt 0 ]]; do
  case "$1" in
    -o|--output) OUTPUT_DIR="$2"; shift 2 ;;
    -h|--help)
      grep '^#' "$0" | sed 's/^# \{0,1\}//'
      exit 0 ;;
    *) echo "Неизвестный аргумент: $1" >&2; exit 1 ;;
  esac
done

info()  { printf '\033[1;34m==>\033[0m %s\n' "$*"; }
warn()  { printf '\033[1;33m[!]\033[0m %s\n' "$*"; }
die()   { printf '\033[1;31m[x]\033[0m %s\n' "$*" >&2; exit 1; }

[[ "$(uname -s)" == "Darwin" ]] || die "Скрипт рассчитан на macOS."
[[ -f "$PROJECT" ]] || die "Не найден проект: $PROJECT (запусти скрипт из корня репозитория)."

# ─────────────────────────────────────────────────────────────────────────────
# Определяем архитектуру → RID
# ─────────────────────────────────────────────────────────────────────────────
case "$(uname -m)" in
  arm64)  RID="osx-arm64" ;;
  x86_64) RID="osx-x64" ;;
  *) die "Неизвестная архитектура: $(uname -m)" ;;
esac
info "Архитектура: $(uname -m) → $RID"

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
  command -v curl >/dev/null 2>&1 || die "Нужен curl для загрузки .NET."
  INSTALL_SCRIPT="$(mktemp -t dotnet-install)"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$INSTALL_SCRIPT"
  chmod +x "$INSTALL_SCRIPT"
  "$INSTALL_SCRIPT" --channel "$DOTNET_CHANNEL" --install-dir "$LOCAL_DOTNET_DIR"
  rm -f "$INSTALL_SCRIPT"
  DOTNET="$LOCAL_DOTNET_DIR/dotnet"
  have_sdk9 "$DOTNET" || die "Установка .NET SDK не удалась."
  info "Установлен .NET SDK: $("$DOTNET" --version)"
fi

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

# ─────────────────────────────────────────────────────────────────────────────
# Публикация (self-contained, один исполняемый файл)
# ─────────────────────────────────────────────────────────────────────────────
info "Очищаю папку назначения: $OUTPUT_DIR"
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

info "Собираю и публикую (это может занять пару минут)…"
# self-contained: нужный .NET runtime кладётся рядом с приложением, ставить
# ничего не нужно. Single-file намеренно не используем — на macOS arm64 у .NET 9
# он даёт сбой в Kestrel (AccessViolationException). Все файлы прячет запускалка.
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
# Ярлык-запускалка для Finder
# ─────────────────────────────────────────────────────────────────────────────
LAUNCHER="$OUTPUT_DIR/${APP_NAME}.command"
cat > "$LAUNCHER" <<LAUNCHER_EOF
#!/usr/bin/env bash
# Запуск KanbanFlow. Двойной клик в Finder открывает этот файл в Терминале.
# Закрыть приложение: Ctrl+C в окне Терминала или просто закрыть окно.
set -e
cd "\$(dirname "\$0")"

PORT="\${PORT:-$PORT}"
URL="http://localhost:\${PORT}"

export ASPNETCORE_ENVIRONMENT="\${ASPNETCORE_ENVIRONMENT:-Development}"
export ASPNETCORE_URLS="\$URL"

echo "KanbanFlow запускается на \$URL"

# В фоне ждём, пока сервер поднимется, и открываем браузер
(
  for _ in \$(seq 1 60); do
    if curl -s -o /dev/null "\$URL"; then break; fi
    sleep 0.5
  done
  open "\$URL"
) &

exec ./KanbanFlowApi
LAUNCHER_EOF
chmod +x "$LAUNCHER"

# Снимаем возможный карантин (на локально созданных файлах его обычно и нет)
xattr -dr com.apple.quarantine "$OUTPUT_DIR" 2>/dev/null || true

# ─────────────────────────────────────────────────────────────────────────────
info "Готово!"
echo
echo "  Приложение:  $OUTPUT_DIR"
echo "  Запуск:      открой в Finder «$OUTPUT_DIR» и дважды кликни «${APP_NAME}.command»"
echo "               (или из терминала:  \"$LAUNCHER\")"
echo "  Веб-интерфейс: http://localhost:${PORT}  (откроется в браузере сам)"
echo
echo "  Первый запуск .command: если macOS ругается «неопознанный разработчик» —"
echo "  правый клик по файлу → «Открыть» → «Открыть»."
