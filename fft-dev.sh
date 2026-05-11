# FFTColorCustomizer dev helpers
# =============================================================================
# Source this before using:  source ./fft-dev.sh
#
# Adapted from FFTHandsFree's fft.sh — minimal version that gives us:
#   running   — is FFT_enhanced.exe alive right now?
#   restart   — kill game, build+deploy, relaunch, press F1 to open the config UI
#   logs      — tail/grep the mod's live_log.txt (written by ConsoleLogger)
#   kill_fft  — just kill the game + Reloaded II processes
#
# Paths assume the standard Steam install layout.
# =============================================================================

FFT_GAME_DIR="/c/Program Files (x86)/Steam/steamapps/common/FINAL FANTASY TACTICS - The Ivalice Chronicles"
FFT_MOD_DIR="$FFT_GAME_DIR/Reloaded/Mods/FFTColorCustomizer"
FFT_LIVE_LOG="$FFT_MOD_DIR/logs/live_log.txt"
FFT_RELOADED_EXE="$FFT_GAME_DIR/Reloaded/Reloaded-II.exe"
FFT_GAME_EXE_BASENAME="FFT_enhanced.exe"

# running: 0 if FFT_enhanced.exe is alive, 1 if not. One-line status.
running() {
  if tasklist //NH //FI "IMAGENAME eq $FFT_GAME_EXE_BASENAME" 2>/dev/null | grep -qi "$FFT_GAME_EXE_BASENAME"; then
    echo "[running] $FFT_GAME_EXE_BASENAME: YES"
    return 0
  else
    echo "[running] $FFT_GAME_EXE_BASENAME: NO"
    return 1
  fi
}

# kill_fft: terminate FFT + Reloaded II if running.
kill_fft() {
  echo "[kill_fft] Stopping FFT + Reloaded-II..."
  taskkill //IM "$FFT_GAME_EXE_BASENAME" //F 2>/dev/null
  taskkill //IM "Reloaded-II.exe" //F 2>/dev/null
  sleep 1
  if running >/dev/null; then
    echo "[kill_fft] WARNING: game still running after taskkill"
    return 1
  fi
  echo "[kill_fft] done"
}

# logs: tail the mod's live_log.txt (truncated fresh on each game launch).
# Usage: logs            — last 40 lines
#        logs 100        — last 100 lines
#        logs grep foo   — grep all logs for 'foo' (extended regex)
logs() {
  if [ ! -f "$FFT_LIVE_LOG" ]; then
    echo "[logs] no live_log.txt yet at: $FFT_LIVE_LOG"
    echo "[logs] launch the game (boot/restart) to produce one"
    return 1
  fi
  if [ "$1" = "grep" ]; then
    shift
    grep -E "$@" "$FFT_LIVE_LOG"
  else
    tail -n "${1:-40}" "$FFT_LIVE_LOG"
  fi
}

# _send_f1_to_fft: Invoke scripts/send_f1.ps1 to focus FFT and SendKeys F1.
# Lives as a separate .ps1 so bash here-string quoting doesn't mangle the
# PowerShell source (an earlier inline version got eaten by bash's `@`).
_send_f1_to_fft() {
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./scripts/send_f1.ps1 -TimeoutSeconds 90
}

# restart: kill game, build, deploy, relaunch, press F1 to open the config UI.
# Use after code or asset changes to validate them end-to-end.
restart() {
  kill_fft || return 1

  echo "[restart] Building..."
  if ! dotnet build ColorMod/FFTColorCustomizer.csproj -c Release 2>&1 | tail -3; then
    echo "[restart] build failed"
    return 1
  fi

  echo "[restart] Deploying..."
  powershell.exe -ExecutionPolicy Bypass -File ./BuildLinked.ps1 2>&1 | tail -3

  if [ ! -f "$FFT_RELOADED_EXE" ]; then
    echo "[restart] Reloaded-II.exe not at: $FFT_RELOADED_EXE"
    return 1
  fi

  echo "[restart] Launching via Reloaded-II..."
  ( cd "$FFT_GAME_DIR/Reloaded" && cmd.exe //C start "" "Reloaded-II.exe" --launch "$FFT_GAME_DIR/$FFT_GAME_EXE_BASENAME" ) &
  disown 2>/dev/null

  echo "[restart] Sending F1 (waits up to 30s for the FFT window)..."
  _send_f1_to_fft
  echo "[restart] done. Use 'logs' to inspect mod output."
}
