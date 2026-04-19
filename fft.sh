#!/bin/bash
# =============================================================================
# FFT Game Bridge Helper
# =============================================================================
# Source this before using:  source ./fft.sh
#
# HOW IT WORKS:
#   Claude writes command.json → C# mod picks it up → executes → writes response.json
#   Every response includes: screen name, battle data, and validPaths (next actions)
#
# QUICK START:
#   source ./fft.sh      # Load all helpers
#   screen               # Check current screen & state
#   path Move            # Execute a validPath action by name
#   enter                # Send Enter key
#   rv "0x14077D208"     # Read 1 byte from memory address
#   rv "0x14077D208" 2   # Read 2 bytes (uint16 LE)
#   block "0x140C66315" 70  # Read 70 bytes as hex string
#
# AVAILABLE COMMANDS (most common):
#   Actions:       execute_action <name>  (execute a validPath action)
#   Battle:        battle_move <x> <y>, battle_attack <x> <y>, battle_wait
#                  battle_flee, battle_retry, battle_retry_formation
#   Navigation:    world_travel_to <id>, advance_dialogue
#   Management:    save, load, buy <item> <qty>, sell <item> <qty>, change_job <id> <job>
#   Memory:        rv <addr> [size], block <addr> <size>, batch '<json>'
#   State:         screen, state, scan_units, scan_move, logs
#   System:        running, restart, boot, strict 1/0
#
# RESPONSE FORMAT:
#   Most commands print: [ScreenName] loc=X hover=Y menu=M status=ST
#   Use fft_full to get the raw JSON response with all fields
#   Use path to see validPaths (available next actions) in the response
#
# VALIDPATHS:
#   Every response includes validPaths — a map of action names to commands.
#   These are context-sensitive: different screens show different paths.
#   Use: path <name>  to execute one.  Example: path Flee, path Wait, path Move
#   After executing, the response shows NEW validPaths for the resulting screen.
#   ALWAYS read validPaths to know what actions are available — don't guess.
#
# MEMORY READS:
#   rv <addr>           Read 1 byte, returns just the number
#   rv <addr> 2         Read 2 bytes (uint16 LE)
#   rv <addr> 4         Read 4 bytes (uint32 LE)
#   block <addr> <n>    Read n bytes, returns hex string (pairs of hex chars)
#                       Parse with bash: hex="$(block ...)"; byte=${hex:offset*2:2}
#                       Convert: echo $((16#$byte))
#   batch '<json>'      Read multiple addresses in one round-trip
#                       Example: batch '{"addr":"14077D208","size":1,"label":"loc"}'
#
# PARSING HEX BLOCKS IN BASH (no python needed):
#   hex=$(block "0x140C66315" 70)
#   # Each byte = 2 hex chars. Byte 0 = ${hex:0:2}, Byte 1 = ${hex:2:2}, etc.
#   # Byte at offset N = ${hex:$((N*2)):2}
#   # Convert hex to decimal: echo $((16#${hex:0:2}))
#   # Parse 7-byte tile entries:
#   for i in $(seq 0 7 69); do
#     x=$((16#${hex:$((i*2)):2}))
#     y=$((16#${hex:$((i*2+2)):2}))
#     elev=$((16#${hex:$((i*2+4)):2}))
#     flag=$((16#${hex:$((i*2+6)):2}))
#     echo "X=$x Y=$y elev=$elev flag=$flag"
#   done
#
# BATTLE TILE LIST (at 0x140C66315, 7 bytes per entry):
#   Byte 0: X coordinate
#   Byte 1: Y coordinate
#   Byte 2: Elevation
#   Byte 3: Flag (1=valid, 0=terminator)
#   Bytes 4-6: Always 0
#   Tile[0] = active unit's current position
#   Cursor index at 0x140C64E7C = which tile the cursor is on
#
# TIMEOUTS:
#   FFT_MAX controls total script timeout (default 30s). Override before sourcing:
#     FFT_MAX=60 source ./fft.sh
#   Individual commands timeout after 5s if no response.json appears.
#   Always pass timeout to Bash tool calls (10000ms is safe for most commands).
#
# =============================================================================

B="/c/program files (x86)/steam/steamapps/common/FINAL FANTASY TACTICS - The Ivalice Chronicles/Reloaded/Mods/FFTColorCustomizer/claude_bridge"

# Session 27 pivot: chain guard REMOVED.
#
# History: five prior attempts to block chained shell calls all produced
# collateral false-positives (piped helpers mis-killed, debugging
# sequences blocked, composite helpers needing complex reset logic)
# without actually stopping the class of bug they targeted. Live-tested
# across sessions: chained Bash calls like `right && sleep 0.3 && screen`
# work reliably — the single-threaded bridge sequences game-affecting
# commands, and the bridge-side auto-delay (C# side) handles the narrow
# case where two key-sending commands arrive faster than the game can
# render.
#
# What stayed:
#   - `_track_key_call` + `_FFT_KEY_CALLS` counter for telemetry. A
#     second key-sending call emits [CHAIN INFO] on stderr so the
#     SessionCommandLog sees when chains happen.
#   - `_is_key_sending` classifier — used by telemetry and retained in
#     case future code needs to distinguish info vs action calls.
#
# What got neutered (kept as no-op named functions so existing callers
# still compile):
#   - `_fft_guard` — now a no-op; no more `[NO] kill -9 $$`.
#   - `_fft_reset_guard` — now a no-op; composites no longer need it.
#
# Expected behavior: any chain (reads, keys, mixes) runs to completion.
# The bridge enforces its own per-command sequencing.
_FFT_DONE=0
_FFT_KEY_CALLS=0
_FFT_CHAIN_WARNED=0
# Kept for [CHAIN WARN] telemetry only — cleared on source so each fresh
# bash invocation starts with counter=0. The old hard-exit disk flag is
# gone (session 27); just clean up any stale files left from prior runs.
_FFT_CHAIN_COUNTER="$B/fft_key_calls.count"
rm -f "$B/fft_done.flag" "$_FFT_CHAIN_COUNTER" 2>/dev/null

_is_key_sending() {
  # $1 = raw JSON payload sent to the bridge. Returns 0 if the command
  # fires game keys (via non-empty "keys":[...] array OR a known
  # game-action verb), 1 otherwise. An empty "keys":[] array is an
  # observational no-op (used by _current_screen to refresh state).
  case "$1" in
    *'"keys":[]'*) return 1 ;;
    *'"keys":['*) return 0 ;;
    *'"action":"execute_action"'*) return 0 ;;
    *'"action":"world_travel_to"'*) return 0 ;;
    *'"action":"battle_'*) return 0 ;;
    *'"action":"open_'*) return 0 ;;
    *'"action":"advance_dialogue"'*) return 0 ;;
    *'"action":"navigate"'*) return 0 ;;
    *'"action":"move_to"'*) return 0 ;;
    *'"action":"confirm_attack"'*) return 0 ;;
    *'"action":"auto_place_units"'*) return 0 ;;
    *'"action":"buy"'*|*'"action":"sell"'*) return 0 ;;
    *'"action":"change_job"'*) return 0 ;;
    *'"action":"hold_key"'*) return 0 ;;
    *'"action":"save"'*|*'"action":"load"'*) return 0 ;;
    *'"action":"remove_equipment_at_cursor"'*) return 0 ;;
    *'"action":"resolve_eqa_row"'*) return 0 ;;
    *'"action":"resolve_picker_cursor"'*) return 0 ;;
    *'"action":"resolve_equip_picker_cursor"'*) return 0 ;;
    *'"action":"resolve_job_cursor"'*) return 0 ;;
    *) return 1 ;;
  esac
}

_fft_guard() {
  # Session 27 pivot: no-op. Five prior attempts to block chained shell
  # calls caused collateral false-positives without stopping real races.
  # The single-threaded bridge already sequences game-affecting commands,
  # and the bridge-side auto-delay (`[CHAIN WARNING]` path) handles the
  # narrow case where two key-sending commands arrive too fast. The
  # hard-exit kept catching legitimate flows (piped helpers, debugging
  # sequences). Left as a named no-op so existing callers stay valid.
  _FFT_DONE=1  # kept for any downstream code that inspects it
  return 0
}

# _fft_reset_guard: Session 27 no-op. Composite helpers still call this
# between their sequential fft calls — kept as a named function so
# those call sites compile; the actual block is gone.
_fft_reset_guard() {
  _FFT_DONE=0
  return 0
}

# Called by fft()/fft_full() AFTER _fft_guard, once we know the JSON
# payload. Increments the key-call counter; warns loudly if a second
# key-sending call fires in this invocation without _FFT_ALLOW_CHAIN=1.
# Mirrors the counter to a disk file so pipe subshells can't reset it.
_track_key_call() {
  local payload="$1"
  if ! _is_key_sending "$payload"; then return 0; fi
  local disk_count=0
  [ -f "$_FFT_CHAIN_COUNTER" ] && disk_count=$(cat "$_FFT_CHAIN_COUNTER" 2>/dev/null || echo 0)
  _FFT_KEY_CALLS=$((disk_count + 1))
  echo "$_FFT_KEY_CALLS" > "$_FFT_CHAIN_COUNTER"
  if [ "$_FFT_KEY_CALLS" -gt 1 ] && [ "${_FFT_ALLOW_CHAIN:-0}" -ne 1 ] && [ "$_FFT_CHAIN_WARNED" -eq 0 ]; then
    # Session 27: this is now pure telemetry — no longer blocks. The
    # bridge-side auto-delay sequences game-affecting commands if they
    # arrive too fast. Keep the warning so the SessionCommandLog and
    # terminal see when chains happen, in case a real race ever surfaces.
    echo "[CHAIN INFO] ${_FFT_KEY_CALLS} key-sending fft calls in one invocation. Bridge auto-delays if needed." >&2
    _FFT_CHAIN_WARNED=1
  fi
}

# --- Timeout tracking ---
# FFT_MAX = max seconds before all commands bail out (prevents runaway scripts)
FFT_START=${SECONDS}
FFT_MAX=${FFT_MAX:-30}

_check_total() {
  local elapsed=$(( ${SECONDS:-0} - ${FFT_START:-0} ))
  if [ "$elapsed" -ge "${FFT_MAX:-30}" ] 2>/dev/null; then
    echo "[TOTAL_TIMEOUT] Script exceeded ${FFT_MAX}s"
    return 1
  fi
  return 0
}

# --- Core command sender ---
# fft: Send raw command JSON, wait for response, print one-line screen summary.
# Returns: [ScreenName] loc=X hover=Y menu=M status=ST
# Use fft_full instead if you need the raw JSON response.
fft() {
  _check_total || return 1
  _fft_guard
  _track_key_call "$1"
  # Shell-side timing: on by default in dev. Prints a short `t=Nms(bridge/render)`
  # line to stderr after each command, colored green under FFT_SLOW_MS (default
  # 800), yellow up to 2×, red beyond. FFT_TIME=0 silences. Pairs with the mod's
  # server-side latencyMs in session_*.jsonl. Cheap — EPOCHREALTIME is a bash
  # builtin (no subprocess).
  local _t0=$EPOCHREALTIME
  rm -f "$B/response.json"
  echo "$1" > "$B/command.json"
  local tries=0
  # $2 = per-command timeout in seconds (default 5). Poll at 20ms intervals.
  local max_tries=$(( ${2:-5} * 50 ))
  until [ -f "$B/response.json" ]; do
    sleep 0.02
    tries=$((tries + 1))
    if [ $tries -ge $max_tries ]; then
      echo "[TIMEOUT] No response after $(( max_tries / 50 ))s"
      running
      return 1
    fi
  done
  local _t1=$EPOCHREALTIME
  # Render compact summary via the shared helper — see _fmt_screen_compact above.
  # Single source of truth; screen() uses the same function. Pass timing so the
  # renderer appends a `t=Nms[action]` suffix to the main [Screen] line.
  _FFT_TIMING_SUFFIX=$(_fmt_timing "$_t0" "$_t1" "$1")
  _fmt_screen_compact "$B/response.json"
  unset _FFT_TIMING_SUFFIX

  # Parse the status + chain-warning out of the response ourselves so we can
  # still surface the bridge's auto-delay notice to the terminal.
  local R=$(cat "$B/response.json" | tr -d '\r\n ')
  local ST=$(echo "$R" | grep -o '"status":"[^"]*"' | head -1 | cut -d'"' -f4)

  # Surface bridge's chain-warning to the terminal. The bridge auto-delays
  # a game command that arrives too fast after the previous one (prevents
  # key drops during menu-open animations). If this prints, the caller
  # chained two game-affecting commands with && or similar. Fix: batch
  # via _fire_keys / one fft call with keys:[...] and delayBetweenMs.
  # Parse from the raw file (not $R, which had whitespace stripped) so the
  # warning message keeps its spaces, then unescape \u0026 → &.
  # Surface errors from failed commands.
  if [ "$ST" = "failed" ]; then
    local ERR=$(grep -o '"error": *"[^"]*"' "$B/response.json" | head -1 | sed -E 's/^"error": *"//; s/"$//; s/\\u0027/'"'"'/g')
    echo "[FAILED] $ERR"
    return 1
  fi

  # Surface rejection reasons. Rejected commands are valid but refused (e.g.
  # already at destination, locked location). The error field carries the
  # reason; print it so Claude doesn't waste a round-trip figuring out why.
  if [ "$ST" = "rejected" ]; then
    local ERR=$(grep -o '"error": *"[^"]*"' "$B/response.json" | head -1 | sed -E 's/^"error": *"//; s/"$//; s/\\u0027/'"'"'/g')
    [ -n "$ERR" ] && echo "[REJECTED] $ERR"
    return 1
  fi

  local CW=$(grep -o '"chainWarning": *"[^"]*"' "$B/response.json" | head -1 | sed -E 's/^"chainWarning": *"//; s/"$//; s/\\u0026/\&/g')
  [ -n "$CW" ] && echo "[CHAIN WARNING] $CW" >&2
  return 0
}

# _fmt_gil: Render a gil amount with thousands separators.
#   2605569 -> 2,605,569
# Called by both fft() and screen() when appending gil=<n> to the output line.
_fmt_gil() {
  LC_ALL=en_US.UTF-8 printf "%'d" "$1" 2>/dev/null || echo "$1"
}

# --- Terminal colors ---
# Honor NO_COLOR=1 and disable when stdout isn't a terminal (piped output stays clean).
if [ -t 1 ] && [ -z "$NO_COLOR" ]; then
  _C_RESET='\033[0m'
  _C_SCR='\033[1;36m'      # bright cyan  — screen name [PartyMenuUnits]
  _C_UI='\033[1;33m'       # bright yellow — ui= values (the decision surface)
  _C_UNIT='\033[1;32m'     # bright green  — viewedUnit / active unit names
  _C_EQUIP='\033[0;36m'    # cyan          — equippedItem= / pickerTab=
  _C_LOC='\033[0;90m'      # grey          — loc=, objective= (low-signal metadata)
  _C_OK='\033[0;32m'       # green         — status=completed
  _C_WARN='\033[0;33m'     # yellow        — status=partial, chain-warn, chain-delay
  _C_ERR='\033[0;31m'      # red           — status=failed, timeout, error
  _C_MARK='\033[1;35m'     # bright magenta — cursor->, hover=, row markers
else
  _C_RESET=''; _C_SCR=''; _C_UI=''; _C_UNIT=''; _C_EQUIP=''
  _C_LOC=''; _C_OK=''; _C_WARN=''; _C_ERR=''; _C_MARK=''
fi

# _col <color> <text>  → wraps text with ANSI color, safe when colors disabled.
_col() { printf '%b%s%b' "$1" "$2" "$_C_RESET"; }

# _is_party_tree_screen <screenName> → 0 if on a PartyMenuUnits-family screen.
# PartyMenuUnits tree screens don't benefit from loc=/objective=/gil= in the
# compact line — the player isn't navigating the world there, so those
# fields are pure carry-over noise that pushes meaningful signals
# (viewedUnit, equippedItem, pickerTab, ui) further away from Claude's
# eye. Per TODO §"What Goes In Compact vs Verbose vs Nowhere": if a
# field doesn't change a decision on *this* screen, drop it.
_is_party_tree_screen() {
  case "$1" in
    PartyMenuUnits|PartyMenuInventory|PartyMenuChronicle|PartyMenuOptions|\
    CharacterStatus|CharacterDialog|DismissUnit|\
    EquipmentAndAbilities|CombatSets|\
    JobSelection|JobActionMenu|JobChangeConfirmation|\
    SecondaryAbilities|ReactionAbilities|SupportAbilities|MovementAbilities|\
    EquippableWeapons|EquippableShields|EquippableHeadware|EquippableCombatGarb|EquippableAccessories|\
    ChronicleEncyclopedia|ChronicleStateOfRealm|ChronicleEvents|ChronicleAuracite|ChronicleReadingMaterials|ChronicleCollection|ChronicleErrands|ChronicleStratagems|ChronicleLessons|ChronicleAkademicReport|\
    OptionsSettings)
      return 0 ;;
    *)
      return 1 ;;
  esac
}

# _status_col <status> → returns the color ANSI for a given status string.
_status_col() {
  case "$1" in
    completed) printf '%b' "$_C_OK" ;;
    partial)   printf '%b' "$_C_WARN" ;;
    failed)    printf '%b' "$_C_ERR" ;;
    *)         printf '%b' "$_C_WARN" ;;
  esac
}

# Per-tag slow-warn thresholds (ms). Picked from session-32 live baselines:
#   keys ~215ms, screen ~180ms, scan_move ~450ms, save/travel ~4-10s.
# A tag's threshold = warn level; 2× = red ("!!"). Unmatched tags use FFT_SLOW_MS
# (default 800). Override the whole table via FFT_SLOW_MS globally or set one
# FFT_SLOW_MS_<TAG> env var per tag (upper-case, non-alphanumerics → _).
_slow_threshold_for_tag() {
  local _tag="$1"
  # User-supplied override wins. `key:Up` → `FFT_SLOW_MS_KEY_UP`.
  local _upper
  _upper=$(printf '%s' "$_tag" | tr '[:lower:]:-' '[:upper:]__')
  local _override
  eval "_override=\${FFT_SLOW_MS_${_upper}:-}"
  if [ -n "$_override" ]; then
    printf '%s' "$_override"
    return
  fi
  case "$_tag" in
    screen|snapshot|heap_snapshot|diff) printf '300' ;;
    key:*)                              printf '400' ;;
    scan_move|scan_units|scan_tavern)   printf '700' ;;
    save|load|world_travel_to|world_travel|enter_tavern)
                                        printf '8000' ;;
    snap*|search_bytes|heap_diff)       printf '2000' ;;
    *)                                  printf '%s' "${FFT_SLOW_MS:-800}" ;;
  esac
}

# _fmt_timing <t0> <t1> <commandJson>
# Returns (to stdout) a colored `t=Nms[tag]` timing suffix. No newline — caller
# owns the line. Respects FFT_TIME (default on; set to 0 to silence) and
# per-tag thresholds from `_slow_threshold_for_tag` (FFT_SLOW_MS global fallback,
# default 800). Threshold coloring: green ≤ warn, yellow to 2×, red beyond.
_fmt_timing() {
  [ "${FFT_TIME:-1}" = "0" ] && return 0
  local _t0="$1" _t1="$2" _cmd="$3"
  # Action tag. "action":"foo" → [foo]. Key commands with "keys":[{"name":"Up"}]
  # → [key:Up] (multi-key: [key:Up+Down]). Empty keys list (screen ping) → [screen].
  local _tag
  _tag=$(printf '%s' "$_cmd" | grep -o '"action":"[^"]*"' | head -1 | cut -d'"' -f4)
  if [ -z "$_tag" ]; then
    local _keys
    _keys=$(printf '%s' "$_cmd" | grep -o '"name":"[^"]*"' | cut -d'"' -f4 | tr '\n' '+' | sed 's/+$//')
    if [ -n "$_keys" ]; then
      _tag="key:$_keys"
    elif printf '%s' "$_cmd" | grep -q '"keys":\[\]'; then
      _tag="screen"
    fi
  fi
  [ -z "$_tag" ] && _tag="?"
  local _warn
  _warn=$(_slow_threshold_for_tag "$_tag")
  local _raw
  _raw=$(awk -v t0="$_t0" -v t1="$_t1" -v warn="$_warn" -v tag="$_tag" \
    'BEGIN{
       br=(t1-t0)*1000;
       s=""; if(br>=warn*2){s="!!"} else if(br>=warn){s="!"};
       printf "t=%.0fms[%s]%s",br,tag,s
     }')
  case "$_raw" in
    *"!!") _col "$_C_ERR"  "$_raw" ;;
    *"!")  _col "$_C_WARN" "$_raw" ;;
    *)     _col "$_C_OK"   "$_raw" ;;
  esac
}

# _fmt_screen_compact: Render the one-line screen summary from a response file.
# Single source of truth for the compact render — called by both fft() and screen()
# so every entry point renders identically.
# Arg: $1 = path to response.json (so we can use `node` for space-preserving field reads)
# Consults: $SCR (screen name, already parsed by caller).
_fmt_screen_compact() {
  local RESP="$1"

  # One node pass reads the file, extracts every field, emits tab-separated.
  # Replaces what used to be ~7 node spawns + ~14 grep/cut pipelines per render.
  # Windows node cold-start is ~60-100ms; the old code cost ~500-700ms per call.
  local FIELDS
  FIELDS=$(node -e "
try{
  const j=JSON.parse(require('fs').readFileSync(process.argv[1],'utf8'));
  const s=j.screen||{};
  const vu=s.viewedUnit||'';
  let vunit='';
  if(vu){
    const r=(s.roster&&s.roster.units)||[];
    const u=r.find(x=>x.name===vu)||{};
    vunit=u.job?vu+'('+u.job+')':vu;
  }
  const out=[
    s.name||'',                     // 0  SCR
    s.location??'',                 // 1  LOC
    s.locationName||'',             // 2  LOCNAME
    s.hover??'',                    // 3  HOV
    s.status||'',                   // 4  ST
    s.storyObjective??'',           // 5  OBJ
    s.storyObjectiveName||'',       // 6  OBJNAME
    s.activeUnitName||'',           // 7  ANAME
    s.activeUnitJob||'',            // 8  AJOB
    s.activeUnitSummary||'',        // 9  ASUM
    s.gil??'',                      // 10 GIL
    s.shopListCursorIndex??'',      // 11 SLCI
    s.cursorRow??'',                // 12 CROW
    s.cursorCol??'',                // 13 CCOL
    s.jobCellState||'',             // 14 JCSTATE
    s.eventId??'',                  // 15 EVID
    s.jobUnlockRequirements||'',    // 16 JUNLOCK
    s.ui||'',                       // 17 UI
    s.bfsMismatchWarning||'',       // 18 BFSMISMATCH
    vunit,                          // 19 VUNIT
    s.equippedItem||'',             // 20 EQITEM
    s.pickerTab||'',                // 21 PTAB
    (s.currentDialogueLine&&s.currentDialogueLine.speaker)||'',  // 22 DLG_SPK
    (s.currentDialogueLine&&s.currentDialogueLine.text)||'',     // 23 DLG_TXT
    s.currentDialogueLine?(s.currentDialogueLine.boxIndex+'/'+s.currentDialogueLine.boxCount):'', // 24 DLG_POS
  ];
  // Sanitize: strip delimiter/newlines from each field. Use non-whitespace
  // delimiter (\x01) because bash 'IFS=\$\\\\t read' collapses consecutive
  // tabs (tab is treated as whitespace), dropping empty fields and shifting
  // everything after. \\x01 is not whitespace so empty fields survive.
  process.stdout.write(out.map(x=>String(x).replace(/[\x01\n\r]/g,' ')).join('\x01'));
}catch(e){}" "$RESP" 2>/dev/null)

  local SCR LOC LOCNAME HOV ST OBJ OBJNAME ANAME AJOB ASUM GIL SLCI CROW CCOL JCSTATE EVID JUNLOCK UI BFSMISMATCH VUNIT EQITEM PTAB DLG_SPK DLG_TXT DLG_POS
  IFS=$'\x01' read -r SCR LOC LOCNAME HOV ST OBJ OBJNAME ANAME AJOB ASUM GIL SLCI CROW CCOL JCSTATE EVID JUNLOCK UI BFSMISMATCH VUNIT EQITEM PTAB DLG_SPK DLG_TXT DLG_POS <<<"$FIELDS"

  local LOCSTR="$LOC"; [ -n "$LOCNAME" ] && LOCSTR="$LOC($LOCNAME)"
  local OBJSTR=""
  if [ -n "$OBJ" ]; then
    OBJSTR="objective=$OBJ"
    [ -n "$OBJNAME" ] && OBJSTR="objective=$OBJ($OBJNAME)"
  fi

  # Header line — [Screen] + decision-surface fields + status.
  # Keeps the essential "what / where am I / what next" anchor within
  # a narrow terminal width. Subordinate fields (loc, objective, gil)
  # go on their own indented lines below so Claude can see everything
  # without wrapping at smaller widths.
  local LINE="$(_col "$_C_SCR" "[$SCR]")"

  if [[ "$SCR" == Battle* ]]; then
    # Battle screens: active unit banner (with position + HP when available), then ui=.
    if [ -n "$ASUM" ]; then
      LINE="$LINE $(_col "$_C_UNIT" "$ASUM")"
    elif [ -n "$ANAME" ] && [ -n "$AJOB" ]; then
      LINE="$LINE $(_col "$_C_UNIT" "${ANAME}")(${AJOB})"
    elif [ -n "$AJOB" ]; then
      LINE="$LINE ($AJOB)"
    fi
    [ -n "$UI" ] && LINE="$LINE ui=$(_col "$_C_UI" "$UI")"
  else
    # Non-battle: ui= first (most decision-relevant), then viewedUnit, equippedItem, pickerTab.
    [ -n "$UI" ] && LINE="$LINE ui=$(_col "$_C_UI" "$UI")"
    [ -n "$VUNIT" ] && LINE="$LINE viewedUnit=$(_col "$_C_UNIT" "$VUNIT")"
    [ -n "$EQITEM" ] && LINE="$LINE equippedItem=$(_col "$_C_EQUIP" "$EQITEM")"
    [ -n "$PTAB" ] && LINE="$LINE pickerTab=$(_col "$_C_EQUIP" "$PTAB")"
    # JobSelection: cell state + cursor position + unlock requirements
    [ -n "$JCSTATE" ] && LINE="$LINE state=$(_col "$_C_MARK" "$JCSTATE")"
    [ -n "$CROW" ] && [ -n "$CCOL" ] && LINE="$LINE cursor=($(_col "$_C_MARK" "r${CROW},c${CCOL}"))"
    [ -n "$JUNLOCK" ] && LINE="$LINE requires=$(_col "$_C_LOC" "$JUNLOCK")"
    # Cutscene: event ID
    [ -n "$EVID" ] && LINE="$LINE eventId=$(_col "$_C_MARK" "$EVID")"
  fi

  [ -n "$SLCI" ] && LINE="$LINE row=$(_col "$_C_MARK" "$SLCI")"

  # World-side context — appended to same line. Suppressed on
  # PartyMenuUnits-tree screens where they don't change per-action decisions.
  if ! _is_party_tree_screen "$SCR"; then
    [ -n "$LOCNAME" ] && LINE="$LINE $(_col "$_C_LOC" "curLoc=$LOCNAME")"
    [ -n "$OBJNAME" ] && LINE="$LINE $(_col "$_C_LOC" "obj=$OBJNAME")"
    # Gil only on shop-adjacent screens — no decision value on WorldMap/TravelList/battle.
    if [[ "$SCR" == LocationMenu || "$SCR" == Outfitter* || "$SCR" == Tavern || "$SCR" == WarriorsGuild || "$SCR" == PoachersDen || "$SCR" == SaveGame* || "$SCR" == ShopConfirmDialog ]]; then
      [ -n "$GIL" ] && LINE="$LINE gil=$(_fmt_gil "$GIL")"
    fi
  fi

  # Append timing suffix when caller pre-computed it (fft() / screen()).
  [ -n "$_FFT_TIMING_SUFFIX" ] && LINE="$LINE $_FFT_TIMING_SUFFIX"
  printf '%b\n' "$LINE"

  # Current dialogue box — the exact text on screen right now. Rendered
  # under the header for BattleDialogue / Cutscene / BattleChoice so the
  # user can pace the scene without a screenshot. Explicitly state-gated
  # so we never surface text on a non-dialogue screen even if the field
  # were mistakenly populated.
  if [ -n "$DLG_TXT" ] && [[ "$SCR" == "BattleDialogue" || "$SCR" == "BattleChoice" || "$SCR" == "Cutscene" ]]; then
    if [ -n "$DLG_SPK" ]; then
      printf '  %b\n' "$(_col "$_C_UNIT" "$DLG_SPK")$(_col "$_C_LOC" " [$DLG_POS]"): $DLG_TXT"
    else
      printf '  %b\n' "$(_col "$_C_LOC" "[narrator $DLG_POS]"): $DLG_TXT"
    fi
  fi

  # BFS mismatch warning — surfaced loudly under the main screen line so
  # Claude sees it without a `logs grep` round trip. Only appears when the
  # BFS-computed tile count disagrees with the game's own count.
  if [ -n "$BFSMISMATCH" ]; then
    printf '  %b\n' "$(_col '\033[1;31m' "⚠ $BFSMISMATCH")"
  fi

  # EqA compact summary: two lines showing equipment and abilities at a glance.
  # Only on EquipmentAndAbilities — enough to decide which slot to open.
  if [ "$SCR" = "EquipmentAndAbilities" ]; then
    cat "$RESP" | node -e "
try{
  const j=JSON.parse(require('fs').readFileSync(0,'utf8'));
  const l=j.screen&&j.screen.loadout;
  const a=j.screen&&j.screen.abilities;
  if(!l&&!a)process.exit(0);
  const eq=[];
  if(l){
    if(l.weapon)eq.push(l.weapon); else eq.push('(none)');
    if(l.leftHand)eq.push(l.leftHand);
    if(l.shield)eq.push(l.shield); else if(!l.leftHand)eq.push('(none)');
    if(l.helm)eq.push(l.helm); else eq.push('(none)');
    if(l.body)eq.push(l.body); else eq.push('(none)');
    if(l.accessory)eq.push(l.accessory); else eq.push('(none)');
  }
  const ab=[];
  if(a){
    ab.push(a.primary||'(none)');
    ab.push(a.secondary||'(none)');
    ab.push(a.reaction||'(none)');
    ab.push(a.support||'(none)');
    ab.push(a.movement||'(none)');
  }
  if(eq.length)console.log('  Equip: '+eq.join(' / '));
  if(ab.length)console.log('  Abilities: '+ab.join(' / '));
}catch(e){}
" 2>/dev/null
  fi
}

# _show_helpers: Print available shell helper commands for the current screen.
# Shown after ValidPaths so Claude knows the high-level shortcuts.
_show_helpers() {
  local scr="$1"
  local helpers=""
  case "$scr" in
    WorldMap)
      helpers="    open_eqa [unit]                       Jump to Equipment & Abilities
    open_job_selection [unit]             Jump to Job Selection
    open_character_status [unit]          Jump to Character Status
    party_summary                         Show all units at a glance
    check_unit <name>                     Quick stat dump for one unit
    view_unit <name>                      Read-only unit data (no nav)
    save_and_travel <id>                  Save then travel to location
    travel_safe <id>                      Travel with auto-flee on encounters
    enter_shop                            Enter the Outfitter at current location
    scan_inventory                        Open inventory tab + dump full list
    start_encounter                       Trigger random encounter (battlegrounds only)"
      ;;
    PartyMenuUnits)
      helpers="    open_eqa [unit]                       Jump to Equipment & Abilities
    open_job_selection [unit]             Jump to Job Selection
    open_character_status [unit]          Jump to Character Status
    party_summary                         Show all units at a glance
    check_unit <name>                     Quick stat dump for one unit
    view_unit <name>                      Read-only unit data (no nav)
    scan_inventory                        Open inventory tab + dump full list
    return_to_world_map                   Universal escape back to WorldMap
    fft_resync                            Full state-reset without game restart (~5s vs ~45s)"
      ;;
    EquipmentAndAbilities)
      helpers="    change_secondary_ability_to <name>   Set secondary skillset
    change_reaction_ability_to <name>    Set reaction ability
    change_support_ability_to <name>     Set support ability
    change_movement_ability_to <name>    Set movement ability
    remove_ability <name>                Unequip an ability by name
    list_secondary_abilities             Show learned secondary skillsets
    list_reaction_abilities              Show learned reaction abilities
    list_support_abilities               Show learned support abilities
    list_movement_abilities              Show learned movement abilities
    remove_equipment                     Unequip item at cursor
    unequip_all <unit>                   Strip all equipment from a unit
    swap_unit <name>                     Cycle Q/E to named unit
    open_picker <unit> <slot>            Open equipment picker (weapon/shield/helm/garb/accessory)
    return_to_world_map                  Universal escape back to WorldMap
    fft_resync                           Full state-reset without game restart (~5s vs ~45s)"
      ;;
    CharacterStatus)
      helpers="    open_eqa [unit]                       Jump to Equipment & Abilities
    open_job_selection [unit]             Jump to Job Selection
    dismiss_unit                          Hold B to open dismiss confirmation
    swap_unit <name>                      Cycle Q/E to named unit
    check_unit <name>                     Quick stat dump for one unit
    open_picker <unit> <slot>             Open equipment picker (weapon/shield/helm/garb/accessory)
    return_to_world_map                   Universal escape back to WorldMap
    fft_resync                            Full state-reset without game restart (~5s vs ~45s)"
      ;;
    JobSelection)
      helpers="    change_job_to <class>                 Change to named job class
    swap_unit <name>                      Cycle Q/E to named unit
    return_to_world_map                   Universal escape back to WorldMap
    fft_resync                            Full state-reset without game restart (~5s vs ~45s)"
      ;;
    BattleFormation)
      helpers="    auto_place_units                      Accept default placement and start battle
    party_summary                         Show all units at a glance"
      ;;
    EncounterDialog)
      helpers="    auto_place_units                      Accept fight + place units + start battle"
      ;;
    GameOver)
      helpers="    load                                  Load most recent save"
      ;;
    BattleMyTurn)
      helpers="    battle_move <x> <y>                   Move active unit to tile
    battle_ability \"<name>\" [x y]          Use ability (coords optional for self-target)
    battle_attack <x> <y>                 Shortcut for battle_ability \"Attack\" x y
    battle_wait                           End turn + auto-face + wait for next turn
    battle_flee                           Quit battle, return to world map"
      ;;
    BattleActing)
      helpers="    battle_ability \"<name>\" [x y]          Use ability (if you haven't acted yet)
    battle_wait                           End turn + auto-face + wait for next turn"
      ;;
    BattlePaused)
      helpers="    battle_flee                           Quit battle, return to world map
    battle_retry                          Retry battle from start
    battle_retry_formation                Retry with formation screen"
      ;;
    TravelList)
      helpers="    world_travel_to <id>                  Travel to location by ID"
      ;;
    LocationMenu)
      helpers="    enter_shop                            Enter the Outfitter at current location"
      ;;
    Cutscene)
      helpers="    advance_dialogue                      Advance one text box"
      ;;
    BattleDialogue)
      helpers="    advance_dialogue                      Advance one text box"
      ;;
    CharacterDialog)
      helpers="    advance_dialogue                      Advance flavor text"
      ;;
    BattleMoving|BattleAttacking|BattleCasting|BattleAbilities)
      helpers="    battle_move <x> <y>                   Move active unit to tile
    battle_ability \"<name>\" [x y]          Use ability (coords optional for self-target)
    battle_attack <x> <y>                 Shortcut for battle_ability \"Attack\" x y
    battle_wait                           End turn + auto-face + wait for next turn"
      ;;
    BattleAlliesTurn|BattleEnemiesTurn|BattleWaiting|Battle)
      helpers="    screen                                Poll screen state until BattleMyTurn returns
    battle_flee                           Quit battle, return to world map"
      ;;
    Outfitter|WarriorsGuild|PoachersDen|SaveGame)
      helpers="    party_summary                         Show all units at a glance
    check_unit <name>                     Quick stat dump for one unit"
      ;;
    Tavern)
      helpers="    read_rumor [idx]                      Open Rumors (scroll to idx if given)
    read_errand [idx]                     Open Errands (scroll to idx if given)
    party_summary                         Show all units at a glance"
      ;;
    TavernRumors)
      helpers="    read_rumor <idx>                      Scroll to rumor #idx (0-based)
    scan_tavern                           Count available rumors"
      ;;
    TavernErrands)
      helpers="    read_errand <idx>                     Scroll to errand #idx (0-based)
    scan_tavern                           Count available errands"
      ;;
    PartyMenuInventory|PartyMenuChronicle|PartyMenuOptions)
      helpers="    open_eqa [unit]                       Jump to Equipment & Abilities
    open_job_selection [unit]             Jump to Job Selection
    open_character_status [unit]          Jump to Character Status
    party_summary                         Show all units at a glance
    check_unit <name>                     Quick stat dump for one unit"
      ;;
  esac
  if [ -n "$helpers" ]; then
    printf '  Helpers:\n%s\n' "$helpers"
  fi
  return 0
}

# fft_full: Send raw command JSON, wait for response, return entire JSON.
# Use this when you need battle data, validPaths, tile lists, etc.
fft_full() {
  _check_total || return 1
  _fft_guard
  _track_key_call "$1"
  rm -f "$B/response.json"
  echo "$1" > "$B/command.json"
  local tries=0
  # $2 = per-command timeout in seconds (default 5). Poll at 20ms intervals.
  local max_tries=$(( ${2:-5} * 50 ))
  until [ -f "$B/response.json" ]; do
    sleep 0.02
    tries=$((tries + 1))
    if [ $tries -ge $max_tries ]; then
      echo "[TIMEOUT] No response after $(( max_tries / 50 ))s"
      running
      return 1
    fi
  done
  cat "$B/response.json"
}

# id: Generate unique command ID. Every command needs a unique ID or the watcher skips it.
# Pure-bash (no subprocesses). EPOCHREALTIME is bash-5-native; with the dot stripped
# it yields microsecond precision. $RANDOM appended for uniqueness across fast bursts.
id() { echo "c${EPOCHREALTIME//.}${RANDOM}"; }

# =============================================================================
# KEY PRESS HELPERS
# =============================================================================
# Send a single key press. No client-side sleep needed — the mod handles settling.
# For waiting until a specific screen appears, use key_wait / enter_wait.
# VK codes: Enter=13, Escape=27, Up=38, Down=40, Left=37, Right=39,
#           Space=32, Tab=9, T=84, E=69, Q=81, F=70

key()  { fft "{\"id\":\"$(id)\",\"keys\":[{\"vk\":$1,\"name\":\"$2\"}],\"delayBetweenMs\":150}"; }
enter() { key 13 Enter; }
esc()   { key 27 Escape; }
up()    { key 38 Up; }
down()  { key 40 Down; }
left()  { key 37 Left; }
right() { key 39 Right; }
space() { key 32 Space; }
tab()   { key 9 Tab; }
tkey()  { key 84 T; }
ekey()  { key 69 E; }

# key_wait: Send key and block until screen matches expected name (or timeout).
# Usage: key_wait <vk> <name> <screenName> [timeoutMs]
key_wait() {
  local vk=$1 name=$2 screen=$3 timeout=${4:-2000}
  fft "{\"id\":\"$(id)\",\"keys\":[{\"vk\":$vk,\"name\":\"$name\"}],\"delayBetweenMs\":150,\"waitForScreen\":\"$screen\",\"waitTimeoutMs\":$timeout}"
}
enter_wait() { key_wait 13 Enter "$1" "${2:-2000}"; }
esc_wait()   { key_wait 27 Escape "$1" "${2:-2000}"; }

# key_leave: Send key and block until screen changes AWAY from the given name.
# Usage: key_leave <vk> <name> <currentScreenName> [timeoutMs]
key_leave() {
  local vk=$1 name=$2 screen=$3 timeout=${4:-2000}
  fft "{\"id\":\"$(id)\",\"keys\":[{\"vk\":$vk,\"name\":\"$name\"}],\"delayBetweenMs\":150,\"waitUntilScreenNot\":\"$screen\",\"waitTimeoutMs\":$timeout}"
}

# key_changed: Send key and block until a memory address changes value.
# Useful for detecting cursor moves, state transitions, etc.
# Usage: key_changed <vk> <name> <hexAddr> [timeoutMs]
key_changed() {
  local vk=$1 name=$2 addr=$3 timeout=${4:-2000}
  fft "{\"id\":\"$(id)\",\"keys\":[{\"vk\":$vk,\"name\":\"$name\"}],\"delayBetweenMs\":150,\"waitForChange\":[\"$addr\"],\"waitTimeoutMs\":$timeout}"
}

# =============================================================================
# MEMORY READ HELPERS
# =============================================================================
# These read live game memory. Addresses are hex strings like "0x14077D208".

# rv: Read a value at an address. Returns just the decimal number.
# Usage: rv <addr> [size]   (size: 1=byte, 2=uint16 LE, 4=uint32 LE; default 1)
# Example: rv "0x14077D208"     → 24
#          rv "0x14077D208" 2   → 1234
rv() {
  _check_total || return 1
  rm -f "$B/response.json"
  echo "{\"id\":\"$(id)\",\"action\":\"read_address\",\"address\":\"$1\",\"readSize\":${2:-1}}" > "$B/command.json"
  local tries=0
  until [ -f "$B/response.json" ]; do
    sleep 0.02
    tries=$((tries + 1))
    if [ $tries -ge 250 ]; then echo "TIMEOUT"; return 1; fi
  done
  tr -d '\r\n ' < "$B/response.json" | grep -o '"value":[0-9]*' | head -1 | cut -d: -f2
}

# block: Read a block of bytes as a hex string. Each byte = 2 hex chars.
# Usage: block <addr> <numBytes>
# Example: hex=$(block "0x140C66315" 70)
#          # Byte at offset 0: echo $((16#${hex:0:2}))
#          # Byte at offset 5: echo $((16#${hex:10:2}))
block() {
  _check_total || return 1
  rm -f "$B/response.json"
  echo "{\"id\":\"$(id)\",\"action\":\"read_block\",\"address\":\"$1\",\"blockSize\":$2}" > "$B/command.json"
  local tries=0
  until [ -f "$B/response.json" ]; do
    sleep 0.02
    tries=$((tries + 1))
    if [ $tries -ge 250 ]; then echo "TIMEOUT"; return 1; fi
  done
  tr -d '\r\n ' < "$B/response.json" | grep -o '"blockData":"[^"]*"' | head -1 | cut -d'"' -f4
}

# batch: Read multiple addresses in one round-trip (faster than multiple rv calls).
# Usage: batch '{"addr":"14077D208","size":1,"label":"loc"},{"addr":"140787A22","size":1,"label":"hover"}'
# Returns full JSON response — parse with grep or node.
batch() {
  fft_full "{\"id\":\"$(id)\",\"action\":\"batch_read\",\"addresses\":[$1]}"
}

# get_flag <name>: Read a mod state flag. Returns value or '(unset)'.
# Disk-backed via claude_bridge/mod_state.json — survives mod reloads but
# not full game restarts (bridge dir may be wiped by rebuild; flags are
# re-loaded if the file is still there).
get_flag() {
  local name="$1"
  if [ -z "$name" ]; then echo "[get_flag] usage: get_flag <name>"; return 1; fi
  fft "{\"id\":\"$(id)\",\"action\":\"get_flag\",\"searchLabel\":\"$name\"}"
}

# set_flag <name> <value>: Set a mod state flag to an integer.
# Used for sticky UI caches / diagnostic toggles / session counters.
# Not a save-file replacement — see ModStateFlags docs for scope.
set_flag() {
  local name="$1" val="$2"
  if [ -z "$name" ] || [ -z "$val" ]; then echo "[set_flag] usage: set_flag <name> <int>"; return 1; fi
  fft "{\"id\":\"$(id)\",\"action\":\"set_flag\",\"searchLabel\":\"$name\",\"searchValue\":$val}"
}

# list_flags: Dump all currently-set mod state flags.
list_flags() {
  fft "{\"id\":\"$(id)\",\"action\":\"list_flags\"}"
}

# =============================================================================
# HIGH-LEVEL NAVIGATION ACTIONS
# =============================================================================

# execute_action: Execute a validPath by name. Shows screen + available next paths.
# This is the PRIMARY way to interact with the game. Every response tells you
# what actions are available — pick one and call execute_action again.
# Usage: execute_action Flee, execute_action Move, execute_action Wait
execute_action() {
  _check_total || return 1
  _fft_guard
  _track_key_call '"action":"execute_action"'
  rm -f "$B/response.json"
  echo "{\"id\":\"$(id)\",\"action\":\"execute_action\",\"to\":\"$1\"}" > "$B/command.json"
  local tries=0
  until [ -f "$B/response.json" ]; do
    sleep 0.02
    tries=$((tries + 1))
    if [ $tries -ge 3000 ]; then echo "[TIMEOUT]"; return 1; fi
  done
  local R=$(cat "$B/response.json" | tr -d '\r\n ')
  local SCR=$(echo "$R" | grep -o '"name":"[^"]*"' | head -1 | cut -d'"' -f4)

  # If Wait completed and we're at Battle_MyTurn, auto-show screen
  if [ "$1" = "Wait" ] && [[ "$SCR" == "BattleMyTurn" ]]; then
    screen
    return
  fi

  # Compact one-liner with full context (ui=, viewedUnit=, EqA summary).
  _fmt_screen_compact "$B/response.json"

  # INFO/ERROR/ValidPaths from the bridge response.
  node -e "
const r=JSON.parse(require('fs').readFileSync(process.argv[1],'utf8'));
if(r.info)console.log('  INFO:',r.info);
if(r.error&&r.status!=='completed')console.log('  ERROR:',r.error);
const vp=r.validPaths||{};
const keys=Object.keys(vp);
if(keys.length){console.log('  ValidPaths:');keys.forEach(k=>console.log('    '+k+': '+vp[k].desc));}
" "$B/response.json"

  # Show available shell helpers for this screen.
  _show_helpers "$SCR"
}

# battle_wait: End turn. Handles menu navigation → Wait → confirm facing → polls until next friendly turn.
# battle_wait [direction]: End turn, choose facing, wait for next friendly turn.
# direction (optional): N/S/E/W or North/South/East/West (case-insensitive).
# With no arg, the code auto-picks the optimal facing via arc scoring.
battle_wait() {
  local dir="${1:-}"
  if [ -n "$dir" ]; then
    fft "{\"id\":\"$(id)\",\"action\":\"battle_wait\",\"pattern\":\"$dir\"}" 60
  else
    fft "{\"id\":\"$(id)\",\"action\":\"battle_wait\"}" 60
  fi
}

# battle_flee: Quit battle and return to world map (Tab → Down x4 → Enter → Enter).
# 20s timeout — the full pause menu nav + world map transition takes ~12s.
battle_flee() { fft "{\"id\":\"$(id)\",\"action\":\"battle_flee\"}" 20; }

# world_travel_to: Navigate to a world map location by ID. Opens travel list, selects, confirms.
# Usage: world_travel_to 26   (travel to Siedge Weald)
world_travel_to() {
  _require_state world_travel_to "WorldMap|TravelList" || return 1
  fft "{\"id\":\"$(id)\",\"action\":\"world_travel_to\",\"locationId\":$1}"
}

# advance_dialogue: Advance cutscene dialogue by one text box (presses Enter).
advance_dialogue() { fft "{\"id\":\"$(id)\",\"action\":\"advance_dialogue\"}"; }

# hold_key: Hold a key down for a duration, then release. Used for game
# mechanics that need a real held press (e.g. hold-B-3s → DismissUnit).
# Usage: hold_key <vk> [durationMs]   (default 3500ms)
hold_key() {
  local vk="$1"; local ms="${2:-3500}"
  fft "{\"id\":\"$(id)\",\"action\":\"hold_key\",\"searchValue\":$vk,\"readSize\":$ms}" $((ms / 1000 + 5))
}

# dismiss_unit: Trigger the hold-B DismissUnit confirmation on CharacterStatus.
dismiss_unit() { hold_key 66 3500; }  # 0x42 = VK_B

# =============================================================================
# Compound navigation helpers
# =============================================================================
# Navigate from anywhere (WorldMap or party-tree) to a specific sub-screen
# for a specific unit, in one command. Handles the full path internally:
# escape to WorldMap → PartyMenuUnits → cursor to unit → CharacterStatus → target.

# _nav_to_party_unit <unit_name>
# Internal: navigate to PartyMenuUnits with cursor on the named unit.
# Returns 0 on success, 1 on failure. Leaves state on PartyMenuUnits.
_nav_to_party_unit() {
  local _FFT_ALLOW_CHAIN=1
  local target="$1"

  # Compound navigators need multiple fft calls. Reset the guard between steps.
  # This is safe because each step waits for its response before the next fires.
  _fft_reset_guard

  # Step 1: get to WorldMap or PartyMenuUnits
  _current_screen >/dev/null
  local curScr=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen.name)" < "$B/response.json" 2>/dev/null)
  if [ "$curScr" != "WorldMap" ] && [ "$curScr" != "PartyMenuUnits" ]; then
    fft "{\"id\":\"$(id)\",\"action\":\"execute_action\",\"to\":\"ReturnToWorldMap\"}" >/dev/null
    _fft_reset_guard
    _current_screen >/dev/null
    curScr=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen.name)" < "$B/response.json" 2>/dev/null)
  fi

  # Step 2: open PartyMenuUnits if on WorldMap
  if [ "$curScr" = "WorldMap" ]; then
    fft "{\"id\":\"$(id)\",\"action\":\"execute_action\",\"to\":\"PartyMenuUnits\"}" >/dev/null
    _fft_reset_guard
    _current_screen >/dev/null
    curScr=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen.name)" < "$B/response.json" 2>/dev/null)
  fi

  if [ "$curScr" != "PartyMenuUnits" ]; then
    echo "[_nav_to_party_unit] ERROR: could not reach PartyMenuUnits (on $curScr)"
    return 1
  fi

  # Step 3: find unit's grid position from roster
  local gridInfo=$(cat "$B/response.json" | node -e "
    let d='';process.stdin.on('data',c=>d+=c);process.stdin.on('end',()=>{
      const j=JSON.parse(d);
      const u=(j.screen?.roster?.units||[]).find(x=>x.name?.toLowerCase()==='$(echo "$target" | tr 'A-Z' 'a-z')');
      if(!u){process.stdout.write('NOT_FOUND');return;}
      const idx=u.displayOrder||0;
      process.stdout.write(Math.floor(idx/5)+','+idx%5);
    });" 2>/dev/null)

  if [ "$gridInfo" = "NOT_FOUND" ]; then
    echo "[_nav_to_party_unit] ERROR: unit '$target' not found in roster"
    return 1
  fi

  local targetRow="${gridInfo%%,*}"
  local targetCol="${gridInfo##*,}"

  # Step 4: navigate cursor to the target unit in one key batch.
  # Cursor may NOT be at (0,0) — PartyMenuUnits preserves the last position.
  # First reset to (0,0) by wrapping: Up enough to hit row 0, Left enough
  # to hit col 0. With wrapping, pressing Up from row 0 goes to last row,
  # so we need to know the grid size. Use roster count to compute rows.
  local rosterCount=$(cat "$B/response.json" | node -e "let d='';process.stdin.on('data',c=>d+=c);process.stdin.on('end',()=>{const j=JSON.parse(d);process.stdout.write(String((j.screen?.roster?.units||[]).length))});" 2>/dev/null)
  local gridRows=$(( (rosterCount + 4) / 5 ))

  local keysJson=""
  local first=1
  # Reset to (0,0): Up × gridRows + Left × 5 guarantees wrap to origin
  for i in $(seq 1 "$gridRows"); do
    [ "$first" -eq 0 ] && keysJson+=","
    keysJson+="{\"vk\":38,\"name\":\"Up\"}"
    first=0
  done
  for i in $(seq 1 5); do
    [ "$first" -eq 0 ] && keysJson+=","
    keysJson+="{\"vk\":37,\"name\":\"Left\"}"
    first=0
  done
  # Now navigate from (0,0) to target
  for i in $(seq 1 "$targetRow"); do
    [ "$first" -eq 0 ] && keysJson+=","
    keysJson+="{\"vk\":40,\"name\":\"Down\"}"
    first=0
  done
  for i in $(seq 1 "$targetCol"); do
    [ "$first" -eq 0 ] && keysJson+=","
    keysJson+="{\"vk\":39,\"name\":\"Right\"}"
    first=0
  done

  if [ -n "$keysJson" ]; then
    fft "{\"id\":\"$(id)\",\"keys\":[$keysJson],\"delayBetweenMs\":150}" >/dev/null
    _fft_reset_guard
  fi
  return 0
}

# Valid source states for party-tree compound nav helpers (open_eqa, etc.)
# These states can reliably escape to WorldMap then open PartyMenuUnits.
# Shop screens, battle states, cutscenes, etc. are blocked — they need
# to exit to WorldMap first before the helper will work safely.
_PARTY_NAV_VALID_STATES="WorldMap|PartyMenuUnits|PartyMenuInventory|PartyMenuChronicle|PartyMenuOptions|CharacterStatus|EquipmentAndAbilities|JobSelection|JobActionMenu|JobChangeConfirmation|CombatSets|EquippableWeapons|EquippableShields|EquippableHeadware|EquippableCombatGarb|EquippableAccessories|SecondaryAbilities|ReactionAbilities|SupportAbilities|MovementAbilities|CharacterDialog|DismissUnit"

# open_character_status [unit_name]
# Navigate to CharacterStatus for the named unit (default: Ramza).
# Append `dry-run` to preview the key sequence without firing.
# Single bridge action — C# handles all navigation internally.
# Internal: after an open_* nav action, verify the viewed unit on screen
# matches the requested unit. Returns 0 if OK (or unable to read), 1 on
# verified mismatch. Emits a visible WARN line on mismatch so the caller
# sees the silent-drift case instead of silently proceeding. Case-insensitive
# and tolerates story-character name variants.
_verify_open_viewed_unit() {
  local requested="$1"
  local helperName="$2"
  # Skip verify when request was defaulted to Ramza (single-arg default)
  # — that's a passive "whoever is current" intent, not a precise target.
  [ -z "$requested" ] && return 0
  local actual=$(node -e "
let d='';process.stdin.on('data',c=>d+=c);process.stdin.on('end',()=>{try{
const j=JSON.parse(d);process.stdout.write((j.screen?.viewedUnit||'').toString());
}catch(e){}});" < "$B/response.json" 2>/dev/null)
  # If bridge gave no viewedUnit, nothing to verify — skip (not a failure).
  [ -z "$actual" ] && return 0
  # Case-insensitive compare; also tolerate when the helper resolved an
  # ambiguous name to a specific roster entry.
  local reqLower=$(echo "$requested" | tr 'A-Z' 'a-z')
  local actLower=$(echo "$actual"    | tr 'A-Z' 'a-z')
  if [ "$reqLower" != "$actLower" ]; then
    echo "[$helperName] WARN: requested viewedUnit='$requested' but landed on viewedUnit='$actual'. Helper silently drifted — downstream commands will act on the wrong unit. See TODO §0 'C# bridge action viewedUnit lag on chain calls'."
    return 1
  fi
  return 0
}

open_character_status() {
  _require_state open_character_status "$_PARTY_NAV_VALID_STATES" || return 1
  local dryRun=0
  local args=("$@")
  local n=${#args[@]}
  if [ $n -gt 0 ] && { [ "${args[$((n-1))]}" = "dry-run" ] || [ "${args[$((n-1))]}" = "--dry-run" ]; }; then
    dryRun=1
    unset 'args[n-1]'
  fi
  local unitArg="${args[*]:-}"
  local unit="${unitArg:-Ramza}"
  if [ "$dryRun" = "1" ]; then
    fft "{\"id\":\"$(id)\",\"action\":\"dry_run_nav\",\"to\":\"$unit\"}"
    # dry_run_nav puts the plan in response.info; surface it so the caller
    # sees the planned key sequence without needing to grep response.json.
    local INFO=$(node -e "const j=JSON.parse(require('fs').readFileSync(process.argv[1],'utf8'));if(j.info)console.log(j.info);" "$B/response.json" 2>/dev/null)
    [ -n "$INFO" ] && echo "$INFO"
    return 0
  else
    fft "{\"id\":\"$(id)\",\"action\":\"open_character_status\",\"to\":\"$unit\"}"
    # Only verify when the caller explicitly named a unit (not default).
    [ -n "$unitArg" ] && _verify_open_viewed_unit "$unit" "open_character_status"
  fi
}

# open_eqa [unit_name] [dry-run]
# Navigate from WorldMap/PartyMenuUnits/party-tree to EquipmentAndAbilities.
# Append `dry-run` to preview the key sequence without firing.
# Single bridge action — C# handles all navigation internally.
open_eqa() {
  _require_state open_eqa "$_PARTY_NAV_VALID_STATES" || return 1
  local dryRun=0
  local args=("$@")
  local n=${#args[@]}
  if [ $n -gt 0 ] && { [ "${args[$((n-1))]}" = "dry-run" ] || [ "${args[$((n-1))]}" = "--dry-run" ]; }; then
    dryRun=1
    unset 'args[n-1]'
  fi
  local unitArg="${args[*]:-}"
  local unit="${unitArg:-Ramza}"
  if [ "$dryRun" = "1" ]; then
    fft "{\"id\":\"$(id)\",\"action\":\"dry_run_nav\",\"to\":\"$unit\"}"
    # dry_run_nav puts the plan in response.info; surface it so the caller
    # sees the planned key sequence without needing to grep response.json.
    local INFO=$(node -e "const j=JSON.parse(require('fs').readFileSync(process.argv[1],'utf8'));if(j.info)console.log(j.info);" "$B/response.json" 2>/dev/null)
    [ -n "$INFO" ] && echo "$INFO"
    return 0
  else
    fft "{\"id\":\"$(id)\",\"action\":\"open_eqa\",\"to\":\"$unit\"}"
    [ -n "$unitArg" ] && _verify_open_viewed_unit "$unit" "open_eqa"
  fi
}

# open_job_selection [unit_name] [dry-run]
# Navigate from WorldMap/PartyMenuUnits/party-tree to JobSelection.
# Append `dry-run` to preview the key sequence without firing.
# Single bridge action — C# handles all navigation internally.
open_job_selection() {
  _require_state open_job_selection "$_PARTY_NAV_VALID_STATES" || return 1
  local dryRun=0
  local args=("$@")
  local n=${#args[@]}
  if [ $n -gt 0 ] && { [ "${args[$((n-1))]}" = "dry-run" ] || [ "${args[$((n-1))]}" = "--dry-run" ]; }; then
    dryRun=1
    unset 'args[n-1]'
  fi
  local unitArg="${args[*]:-}"
  local unit="${unitArg:-Ramza}"
  if [ "$dryRun" = "1" ]; then
    fft "{\"id\":\"$(id)\",\"action\":\"dry_run_nav\",\"to\":\"$unit\"}"
    # dry_run_nav puts the plan in response.info; surface it so the caller
    # sees the planned key sequence without needing to grep response.json.
    local INFO=$(node -e "const j=JSON.parse(require('fs').readFileSync(process.argv[1],'utf8'));if(j.info)console.log(j.info);" "$B/response.json" 2>/dev/null)
    [ -n "$INFO" ] && echo "$INFO"
    return 0
  else
    fft "{\"id\":\"$(id)\",\"action\":\"open_job_selection\",\"to\":\"$unit\"}"
    [ -n "$unitArg" ] && _verify_open_viewed_unit "$unit" "open_job_selection"
  fi
}

# =============================================================================
# Quick helpers (aliases + one-liners)
# =============================================================================

# party_summary: Formatted one-line-per-unit roster overview.
# Works from any screen — reads the last response's roster data, or
# navigates to PartyMenuUnits if roster isn't available.
party_summary() {
  _fft_reset_guard
  # Get a fresh PartyMenuUnits read to ensure roster is populated
  _current_screen >/dev/null
  local curScr=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen?.name||'')" < "$B/response.json" 2>/dev/null)
  local hasRoster=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen?.roster?.units?.length>0?'yes':'no')" < "$B/response.json" 2>/dev/null)

  if [ "$hasRoster" != "yes" ]; then
    # Navigate to PartyMenuUnits to get roster
    if [ "$curScr" != "WorldMap" ] && [ "$curScr" != "PartyMenuUnits" ]; then
      fft "{\"id\":\"$(id)\",\"action\":\"execute_action\",\"to\":\"ReturnToWorldMap\"}" >/dev/null
      _fft_reset_guard
    fi
    fft "{\"id\":\"$(id)\",\"action\":\"execute_action\",\"to\":\"PartyMenuUnits\"}" >/dev/null
    _fft_reset_guard
  fi

  node -e "
const j=JSON.parse(require('fs').readFileSync(process.argv[1],'utf8'));
const units=j.screen?.roster?.units||[];
if(!units.length){console.log('  (no roster data)');process.exit(0);}
console.log('  Party ('+units.length+' units):');
units.forEach(u=>{
  const eq=u.equipment||{};
  const gear=[eq.weapon,eq.leftHand,eq.shield,eq.helm,eq.body,eq.accessory].filter(Boolean);
  const gearStr=gear.length?gear.join(', '):'(unequipped)';
  const z=u.zodiac?' '+u.zodiac:'';
  console.log('    '+u.name.padEnd(14)+u.job.padEnd(16)+'Lv'+String(u.level).padStart(3)+'  Br'+String(u.brave??'--').padStart(3)+' Fa'+String(u.faith??'--').padStart(3)+z);
  console.log('      '+gearStr);
});
" "$B/response.json"
}

# check_unit <name>: Quick stat dump for a single unit from roster data.
check_unit() {
  local target="$*"
  if [ -z "$target" ]; then echo "[check_unit] usage: check_unit <name>"; return 1; fi
  _fft_reset_guard
  _current_screen >/dev/null
  local hasRoster=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen?.roster?.units?.length>0?'yes':'no')" < "$B/response.json" 2>/dev/null)
  if [ "$hasRoster" != "yes" ]; then
    fft "{\"id\":\"$(id)\",\"action\":\"execute_action\",\"to\":\"PartyMenuUnits\"}" >/dev/null
    _fft_reset_guard
  fi
  local lowerTarget=$(echo "$target" | tr 'A-Z' 'a-z')
  node -e "
const j=JSON.parse(require('fs').readFileSync(process.argv[1],'utf8'));
const units=j.screen?.roster?.units||[];
const u=units.find(x=>(x.name||'').toLowerCase()==='$lowerTarget');
if(!u){console.log('[check_unit] unit not found: $target');process.exit(1);}
const eq=u.equipment||{};
const gear=[eq.weapon,eq.leftHand,eq.shield,eq.helm,eq.body,eq.accessory].filter(Boolean);
const parts=[u.name,u.job,'Lv '+u.level,'JP '+(u.jp??'--'),'Brave '+(u.brave??'--'),'Faith '+(u.faith??'--')];
if(u.zodiac)parts.push('Zodiac: '+u.zodiac);
console.log('  '+parts.join('  '));
console.log('  HP --/--  MP --/--  PA --  MA --  Speed --  Move --  Jump --');
console.log('  Equip: '+(gear.length?gear.join(' / '):'(none)'));
" "$B/response.json"
}

# flee: Flee from an encounter. Alias for execute_action Flee.
flee() { execute_action Flee; }

# fight: Accept an encounter. Alias for execute_action Fight.
fight() { execute_action Fight; }

# save_and_travel <id>: Save the game then travel to a location.
# Must be on WorldMap. Validates before acting.
save_and_travel() {
  local _FFT_ALLOW_CHAIN=1
  local dest="$1"
  if [ -z "$dest" ]; then echo "[save_and_travel] usage: save_and_travel <location_id>"; return 1; fi
  _fft_reset_guard
  _current_screen >/dev/null
  local curScr=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen?.name||'')" < "$B/response.json" 2>/dev/null)
  if [ "$curScr" != "WorldMap" ]; then
    echo "[save_and_travel] ERROR: must be on WorldMap (current: $curScr)"
    return 1
  fi
  save
  _fft_reset_guard
  world_travel_to "$dest"
}

# enter_shop: From WorldMap at a settlement (IDs 0-14), navigate into the Outfitter.
# Validates you're at a settlement before pressing anything.
enter_shop() {
  local _FFT_ALLOW_CHAIN=1
  _fft_reset_guard
  _current_screen >/dev/null
  local curScr=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen?.name||'')" < "$B/response.json" 2>/dev/null)
  if [ "$curScr" != "WorldMap" ]; then
    echo "[enter_shop] ERROR: must be on WorldMap (current: $curScr)"
    return 1
  fi
  local locId=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen?.location??-1)" < "$B/response.json" 2>/dev/null)
  local locName=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen?.locationName||'unknown')" < "$B/response.json" 2>/dev/null)
  if [ "$locId" -lt 0 ] || [ "$locId" -gt 14 ]; then
    echo "[enter_shop] ERROR: not at a settlement (loc=$locId $locName). Settlements are IDs 0-14."
    return 1
  fi
  execute_action EnterLocation >/dev/null
  _fft_reset_guard
  execute_action EnterShop >/dev/null
  _fft_reset_guard
  screen
}

# enter_tavern: From WorldMap at a settlement, navigate into the Tavern.
# Flow: WorldMap → LocationMenu (default cursor on Outfitter) → CursorDown to
# Tavern → EnterShop → Tavern root screen. Validates settlement ID (0-14).
#
# Tavern sub-actions (Rumors / Errands) are opened via `read_rumor` / `read_errand`
# which assume you're already on the Tavern root.
enter_tavern() {
  local _FFT_ALLOW_CHAIN=1
  _fft_reset_guard
  _current_screen >/dev/null
  local curScr=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen?.name||'')" < "$B/response.json" 2>/dev/null)
  if [ "$curScr" != "WorldMap" ]; then
    echo "[enter_tavern] ERROR: must be on WorldMap (current: $curScr)"
    return 1
  fi
  local locId=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen?.location??-1)" < "$B/response.json" 2>/dev/null)
  local locName=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen?.locationName||'unknown')" < "$B/response.json" 2>/dev/null)
  if [ "$locId" -lt 0 ] || [ "$locId" -gt 14 ]; then
    echo "[enter_tavern] ERROR: not at a settlement (loc=$locId $locName). Settlements are IDs 0-14."
    return 1
  fi
  execute_action EnterLocation >/dev/null
  _fft_reset_guard
  # LocationMenu opens with cursor on Outfitter; Tavern is one position down.
  execute_action CursorDown >/dev/null
  _fft_reset_guard
  # Verify cursor landed on Tavern before entering.
  local landedUI=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen?.ui||'')" < "$B/response.json" 2>/dev/null)
  if [ "$landedUI" != "Tavern" ]; then
    echo "[enter_tavern] WARN: expected ui=Tavern, got ui=$landedUI — this settlement may not have a Tavern"
    return 1
  fi
  execute_action EnterShop >/dev/null
  _fft_reset_guard
  screen
}

# read_rumor [index]: On TavernRumors, scroll to rumor #index (0-based) and
# render the screen AND the decoded body text for that row.
#
# Body text is read from the hardcoded 26-entry RumorCorpus decoded from
# world_wldmes.bin (see FFTHandsFree/TavernRumorTitleMap.md). The corpus is
# a flat list shared across all taverns; each city shows a per-story-progress
# subset. Resolution tiers in the bridge: (1) exact title, (2) body substring,
# (3) {locationId, row} via CityRumors.cs, (4) raw corpus index.
#
# The integer-index mode is a best-effort match (pass-through to corpus[N]);
# use read_rumor "<title>" or "<phrase>" for accurate lookups, or the
# locationId/unitIndex path from code when the cursor row is known.
#
# Falls back to `screen` only (no body) if the mod isn't running the new
# get_rumor action.
read_rumor() {
  # Usage:
  #   read_rumor                     — just render current screen
  #   read_rumor <idx>               — scroll to row N, render, return corpus[N] body
  #   read_rumor "<title fragment>"  — look up body by body/title substring
  #                                    (does NOT scroll the UI)
  local arg="$1"
  _fft_reset_guard
  _current_screen >/dev/null
  local curScr=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen?.name||'')" < "$B/response.json" 2>/dev/null)

  # String-argument mode: skip nav, just do the title lookup.
  if [ -n "$arg" ] && ! [[ "$arg" =~ ^[0-9]+$ ]]; then
    local q=$(printf '%s' "$arg" | node -e "process.stdout.write(JSON.stringify(require('fs').readFileSync(0,'utf8')))")
    fft "{\"id\":\"$(id)\",\"action\":\"get_rumor\",\"searchLabel\":$q}" >/dev/null
    node -e "const r=JSON.parse(require('fs').readFileSync(0,'utf8')); if(r.status==='completed') process.stdout.write(r.dialogue||''); else process.stdout.write('[lookup failed: '+(r.error||'unknown')+']')" < "$B/response.json"
    echo ""
    return 0
  fi

  # Numeric / no-arg: nav-then-body flow
  if [ "$curScr" = "Tavern" ]; then
    execute_action Select >/dev/null
    _fft_reset_guard
  elif [ "$curScr" != "TavernRumors" ]; then
    echo "[read_rumor] ERROR: must be on Tavern or TavernRumors (current: $curScr). Try: enter_tavern"
    return 1
  fi
  if [ -n "$arg" ]; then
    local n="$arg"
    [ "$n" -lt 0 ] && n=0
    for i in $(seq 1 "$n"); do
      execute_action ScrollDown >/dev/null
    done
  fi
  screen
  if [ -n "$arg" ]; then
    fft "{\"id\":\"$(id)\",\"action\":\"get_rumor\",\"unitIndex\":$arg}" >/dev/null
    local body=$(node -e "const r=JSON.parse(require('fs').readFileSync(0,'utf8')); if(r.status==='completed') process.stdout.write(r.dialogue||''); else process.stdout.write('[no body: '+(r.error||'unknown')+']')" < "$B/response.json" 2>/dev/null)
    echo ""
    echo "$body"
  fi
}

# list_rumors: Dump the full decoded rumor corpus (~26 entries) with previews.
# Use when you want to see every rumor text the decoder extracted, regardless
# of which tavern/city the player is currently at.
list_rumors() {
  fft "{\"id\":\"$(id)\",\"action\":\"list_rumors\"}" >/dev/null
  node -e "const r=JSON.parse(require('fs').readFileSync(0,'utf8')); process.stdout.write(r.dialogue||('[list_rumors failed: '+(r.error||'unknown')+']'))" < "$B/response.json"
  echo ""
}

# read_errand [index]: On TavernErrands, scroll to errand #index (0-based).
# Same flow + same body-text limitation as read_rumor.
read_errand() {
  local idx="$1"
  _fft_reset_guard
  _current_screen >/dev/null
  local curScr=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen?.name||'')" < "$B/response.json" 2>/dev/null)
  if [ "$curScr" = "Tavern" ]; then
    # On Tavern root, cursor defaults to Rumors. Move right to Errands, then Select.
    execute_action CursorDown >/dev/null 2>&1 || execute_action Right >/dev/null 2>&1
    _fft_reset_guard
    execute_action Select >/dev/null
    _fft_reset_guard
  elif [ "$curScr" != "TavernErrands" ]; then
    echo "[read_errand] ERROR: must be on Tavern or TavernErrands (current: $curScr). Try: enter_tavern"
    return 1
  fi
  if [ -n "$idx" ]; then
    local n="$idx"
    [ "$n" -lt 0 ] && n=0
    for i in $(seq 1 "$n"); do
      execute_action ScrollDown >/dev/null
    done
  fi
  screen
}

# scan_tavern: On TavernRumors or TavernErrands, scroll through every entry
# (wrapping back to the start) and report how many distinct rows were visited.
# Useful for Claude to know "how many rumors are available to read" before
# deciding whether to engage.
#
# Caveat: counts by detecting when ScrollDown wraps back to the starting row.
# Relies on the row-index signal being present; if it isn't (because the index
# byte isn't surfaced yet on TavernRumors), falls back to an empirical 20-max
# scan with deduplication by decoded title once that's wired up.
scan_tavern() {
  _fft_reset_guard
  _current_screen >/dev/null
  local curScr=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen?.name||'')" < "$B/response.json" 2>/dev/null)
  case "$curScr" in
    TavernRumors|TavernErrands) ;;
    *) echo "[scan_tavern] ERROR: must be on TavernRumors or TavernErrands (current: $curScr)"; return 1 ;;
  esac
  # Get starting cursor row if available.
  local startRow=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen?.cursorRow??'')" < "$B/response.json" 2>/dev/null)
  local count=0
  local maxScan=30
  for i in $(seq 1 "$maxScan"); do
    execute_action ScrollDown >/dev/null
    _fft_reset_guard
    count=$((count+1))
    local nowRow=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen?.cursorRow??'')" < "$B/response.json" 2>/dev/null)
    # Wrap detection: row returned to start → we've seen everything.
    if [ -n "$startRow" ] && [ -n "$nowRow" ] && [ "$nowRow" = "$startRow" ] && [ "$i" -gt 1 ]; then
      echo "[scan_tavern] $count entries on $curScr (wrapped back to row=$startRow)"
      return 0
    fi
  done
  echo "[scan_tavern] reached max-scan ($maxScan) without a clean wrap — cursorRow may not be populated; treat as ≥$maxScan entries"
}

# god_ramza: Dev-mode helper that writes endgame gear + Brave/Faith 95 to
# Ramza's roster slot. Used during state-collection playthroughs so battles
# finish quickly without the user having to grind. Level/EXP NOT changed —
# leveling enemies to match caused random encounters to spawn with Lv 99
# enemies that killed the party; keep Ramza at whatever level the story
# gives him and rely on endgame gear for the speed boost.
#
# Writes (roster slot 0 at 0x1411A18D0):
#   +0x0E Helm      = 156 (Grand Helm, +150 HP)
#   +0x10 Body      = 185 (Maximillian, +200 HP)
#   +0x12 Accessory = 218 (Bracer)
#   +0x14 Weapon    = 36  (Ragnarok, WP 24, range 1, evade 20)
#   +0x1A Shield    = 141 (Kaiser Shield, PhysEv 46, MagEv 20)
#   +0x16 LeftHand  = 0xFF (empty — clear any prior dual-wield)
#   +0x18 Reserved  = 0xFF (clear)
#   +0x1E Brave     = 95
#   +0x1F Faith     = 95
#
# Safe to call repeatedly. Re-run after any session restart. To write these
# values to OTHER units edit their slot base (+N*0x258 from 0x1411A18D0).
god_ramza() {
  local RB=0x1411A18D0
  local base_dec=$((RB))

  _write_pair() {
    local off=$1
    local val=$2
    local addr_lo=$(printf "0x%X" $((base_dec + off)))
    local addr_hi=$(printf "0x%X" $((base_dec + off + 1)))
    fft "{\"id\":\"$(id)\",\"action\":\"write_address\",\"address\":\"$addr_lo\",\"readSize\":$val}" > /dev/null 2>&1
    fft "{\"id\":\"$(id)\",\"action\":\"write_address\",\"address\":\"$addr_hi\",\"readSize\":0}" > /dev/null 2>&1
  }

  _write_byte() {
    local off=$1
    local val=$2
    local addr=$(printf "0x%X" $((base_dec + off)))
    fft "{\"id\":\"$(id)\",\"action\":\"write_address\",\"address\":\"$addr\",\"readSize\":$val}" > /dev/null 2>&1
  }

  echo "[god_ramza] writing endgame gear + Brave/Faith 95 to Ramza's roster slot..."
  # Equipment (u16 each)
  _write_pair 0x0E 156   # Grand Helm
  _write_pair 0x10 185   # Maximillian
  _write_pair 0x12 218   # Bracer
  _write_pair 0x14 36    # Ragnarok
  _write_pair 0x16 255   # Clear left-hand
  _write_pair 0x18 255   # Clear reserved
  _write_pair 0x1A 141   # Kaiser Shield
  # Stats (u8 each) — Level/EXP intentionally left alone
  _write_byte 0x1E 95    # Brave
  _write_byte 0x1F 95    # Faith

  echo "[god_ramza] done. In-battle HP/PA recompute on next battle entry."
}

# swap_unit <name>: Cycle Q/E to the named unit on any unit-scoped screen
# (CharacterStatus, EquipmentAndAbilities, JobSelection). Reads the roster
# to compute how many E presses are needed.
swap_unit() {
  local target="$*"
  if [ -z "$target" ]; then echo "[swap_unit] usage: swap_unit <name>"; return 1; fi
  _fft_reset_guard
  _current_screen >/dev/null
  local curScr=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen?.name||'')" < "$B/response.json" 2>/dev/null)

  # Validate we're on a unit-scoped screen
  case "$curScr" in
    CharacterStatus|EquipmentAndAbilities|JobSelection) ;;
    *) echo "[swap_unit] ERROR: must be on CharacterStatus, EquipmentAndAbilities, or JobSelection (current: $curScr)"; return 1 ;;
  esac

  # Find current and target positions in roster
  local lowerTarget=$(echo "$target" | tr 'A-Z' 'a-z')
  local navInfo=$(node -e "
const j=JSON.parse(require('fs').readFileSync(process.argv[1],'utf8'));
const vu=j.screen?.viewedUnit||'';
const units=j.screen?.roster?.units||[];
if(!units.length){process.stdout.write('NO_ROSTER');process.exit(0);}
const curIdx=units.findIndex(x=>(x.name||'').toLowerCase()===vu.toLowerCase());
const tgtIdx=units.findIndex(x=>(x.name||'').toLowerCase()==='$lowerTarget');
if(tgtIdx<0){process.stdout.write('NOT_FOUND');process.exit(0);}
if(curIdx===tgtIdx){process.stdout.write('ALREADY');process.exit(0);}
// Forward (E) distance vs backward (Q) distance
const n=units.length;
const fwd=(tgtIdx-curIdx+n)%n;
const bwd=(curIdx-tgtIdx+n)%n;
if(fwd<=bwd)process.stdout.write('E,'+fwd);
else process.stdout.write('Q,'+bwd);
" "$B/response.json" 2>/dev/null)

  case "$navInfo" in
    NO_ROSTER) echo "[swap_unit] ERROR: no roster data available"; return 1 ;;
    NOT_FOUND) echo "[swap_unit] ERROR: unit '$target' not found in roster"; return 1 ;;
    ALREADY) echo "[swap_unit] already viewing $target"; return 0 ;;
  esac

  local dir="${navInfo%%,*}"
  local count="${navInfo##*,}"
  local vk name
  if [ "$dir" = "E" ]; then vk=69; name=E; else vk=81; name=Q; fi

  # Build key batch
  local keysJson=""
  local first=1
  for i in $(seq 1 "$count"); do
    [ "$first" -eq 0 ] && keysJson+=","
    keysJson+="{\"vk\":$vk,\"name\":\"$name\"}"
    first=0
  done

  if [ -n "$keysJson" ]; then
    fft "{\"id\":\"$(id)\",\"keys\":[$keysJson],\"delayBetweenMs\":300}" >/dev/null
    _fft_reset_guard
  fi
  screen
}

# auto_place_units: Accept default unit placement on BattleFormation and start battle.
# start_encounter: Trigger random encounter at current battleground location.
# Validates WorldMap + battleground (IDs 24-42), recenters cursor (C),
# enters location (Enter), accepts Fight (Enter). Lands on BattleFormation.
# Use auto_place_units after this to commence battle.
start_encounter() {
  _fft_reset_guard
  _current_screen >/dev/null
  local curScr=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen?.name||'')" < "$B/response.json" 2>/dev/null)
  local curLoc=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen?.location??-1)" < "$B/response.json" 2>/dev/null)

  if [ "$curScr" != "WorldMap" ]; then
    echo "[start_encounter] ERROR: must be on WorldMap (current: $curScr)"
    return 1
  fi
  if [ "$curLoc" -lt 24 ] || [ "$curLoc" -gt 42 ]; then
    echo "[start_encounter] ERROR: not a battleground (curLoc=$curLoc). Battlegrounds are IDs 24-42."
    return 1
  fi

  # Step 1: C (recenter) → Enter (trigger encounter). Wait for EncounterDialog.
  fft "{\"id\":\"$(id)\",\"keys\":[{\"vk\":67,\"name\":\"C\"},{\"vk\":13,\"name\":\"Enter\"}],\"delayBetweenMs\":500,\"waitForScreen\":\"EncounterDialog\",\"waitTimeoutMs\":8000}" >/dev/null
  _fft_reset_guard

  # EncounterDialog animation needs ~2s before it accepts input.
  sleep 2

  # Step 2: Enter (accept Fight — cursor defaults there).
  fft "{\"id\":\"$(id)\",\"keys\":[{\"vk\":13,\"name\":\"Enter\"}],\"waitUntilScreenNot\":\"EncounterDialog\",\"waitTimeoutMs\":10000}"
}

# Single bridge action — C# places 4 units (Enter×2 each), commences (Space+Enter),
# then polls until a battle state appears.
auto_place_units() {
  _require_state auto_place_units "BattleFormation" || return 1
  fft "{\"id\":\"$(id)\",\"action\":\"auto_place_units\"}" 60
}

# open_picker <unit> <slot>: Navigate to a specific equipment picker.
# Slot: weapon, shield, helm, garb, accessory
# Opens the picker but can't navigate to a specific item (picker cursor
# tracking not yet implemented). Use ScrollDown/ScrollUp + Select manually.
open_picker() {
  if [ $# -lt 2 ]; then echo "[open_picker] usage: open_picker <unit> <slot>"; return 1; fi
  _require_state open_picker "$_PARTY_NAV_VALID_STATES" || return 1
  local slot="${!#}"    # last arg is slot
  local unit="${*%$slot}" # everything before last arg is unit name
  unit="${unit% }"       # trim trailing space

  # Map slot name to EqA row index (column 0)
  local row
  case "$slot" in
    weapon|right_hand|right)   row=0 ;;
    shield|left_hand|left)     row=1 ;;
    helm|headware|head)        row=2 ;;
    garb|armor|body|chest)     row=3 ;;
    accessory|access)          row=4 ;;
    *) echo "[open_picker] ERROR: unknown slot '$slot'. Use: weapon, shield, helm, garb, accessory"; return 1 ;;
  esac

  # Navigate to the unit's EqA
  open_eqa "$unit" >/dev/null
  _fft_reset_guard

  # Navigate to the target row in column 0 (equipment column).
  # EqA opens with cursor at (0,0) = weapon slot. Move Down to target row.
  local keysJson=""
  local first=1
  for i in $(seq 1 "$row"); do
    [ "$first" -eq 0 ] && keysJson+=","
    keysJson+="{\"vk\":40,\"name\":\"Down\"}"
    first=0
  done
  # Add Enter to open the picker
  [ "$first" -eq 0 ] && keysJson+=","
  keysJson+="{\"vk\":13,\"name\":\"Enter\"}"

  fft "{\"id\":\"$(id)\",\"keys\":[$keysJson],\"delayBetweenMs\":250}" >/dev/null
  _fft_reset_guard
  screen
}

# return_to_world_map: Universal escape hatch — get back to WorldMap from
# any party-tree screen. Iterates Escape with detection until WorldMap.
# Stops at 8 attempts to avoid infinite loops. Useful when Claude is
# stuck or unsure how to back out of a nested state.
#
# REFUSES from battle states:
#   - BattleMyTurn / BattleMoving / BattleAttacking / BattleCasting /
#     BattleAbilities / BattleActing / BattleWaiting: pressing Escape
#     cancels the current action menu or exits move mode, NOT the
#     battle. Use `battle_flee` instead (which navigates the pause menu
#     → Return to World Map flow correctly).
#   - BattlePaused: Escape closes the pause menu and resumes battle.
#     Use `execute_action ReturnToWorldMap` on that screen (dedicated
#     navigation that handles the confirmation dialog).
#   - BattleFormation / BattleSequence / EncounterDialog / Cutscene /
#     GameOver: Escape either toggles menus or is a no-op. Resolve
#     those dialogs explicitly.
# BattleVictory and BattleDesertion are NOT blocked — those are post-
# battle result screens where Escape/Enter actually do advance toward
# WorldMap (verified by the game's own flow). Live-verification of
# those two paths tracked in TODO §0.
return_to_world_map() {
  local cur=$(_current_screen)
  case "$cur" in
    BattleMyTurn|BattleMoving|BattleAttacking|BattleCasting|BattleAbilities|BattleActing|BattleWaiting|BattlePaused|BattleFormation|BattleSequence|BattleEnemiesTurn|BattleAlliesTurn|EncounterDialog|Cutscene|GameOver)
      echo "[return_to_world_map] ERROR: cannot run from $cur. Use \`battle_flee\` for active battles, \`execute_action ReturnToWorldMap\` on BattlePaused, or resolve the $cur screen explicitly."
      return 1
      ;;
  esac
  local _FFT_ALLOW_CHAIN=1
  for i in $(seq 1 8); do
    cur=$(_current_screen)
    if [ "$cur" = "WorldMap" ]; then
      screen
      return 0
    fi
    fft "{\"id\":\"$(id)\",\"keys\":[{\"vk\":27,\"name\":\"Escape\"}],\"delayBetweenMs\":0}" >/dev/null
    _fft_reset_guard
    sleep 0.5
  done
  echo "[return_to_world_map] ERROR: stuck at $(_current_screen) after 8 escapes"
  return 1
}

# fft_resync: Full stuck-state recovery WITHOUT restarting the game.
# Much faster than `restart` (~5s vs ~45s) and preserves mod memory
# (resolved heap addresses, state flags, caches).
#
# Steps:
#   1. Escape up to WorldMap with 2-consecutive-confirm detection
#      (defends against false WorldMap reads during escape storm).
#   2. Hard-reset the C# ScreenStateMachine to WorldMap.
#   3. Clear every auto-resolve latch (EqA row, picker cursor, job
#      cursor, party-menu cursor, equip-picker cursor).
#   4. Final `screen` read to confirm recovery.
#
# Use when: the shell reports a state but screenshots show a different
# game state, or a compound nav helper lands in an unexpected screen,
# or after any suspected desync. Safe from any non-battle state.
#
# REFUSES from forbidden states: any Battle*, EncounterDialog, Cutscene,
# BattleSequence, BattleFormation, GameOver. The 10 escapes + state
# reset would otherwise open the pause menu / fight the encounter
# prompt / skip cutscenes and rearrange state unpredictably. In those
# cases resolve the encounter/battle/cutscene first (accept, flee,
# advance, or `restart`).
fft_resync() {
  local cur=$(_current_screen)
  if [ -z "$cur" ]; then
    echo "[fft_resync] ERROR: could not detect current screen. Try \`screen\` first, or \`restart\` if the bridge is unresponsive."
    return 1
  fi
  # Forbidden-from states: battle, encounter, cutscene, game-over.
  # Cheaper than listing every safe state — new non-battle screens
  # are automatically allowed as they get added.
  case "$cur" in
    Battle*|EncounterDialog|Cutscene|BattleSequence|BattleFormation|GameOver)
      echo "[fft_resync] ERROR: cannot run from $cur — the escape storm would open the pause menu / skip a cutscene / mis-handle the encounter. Resolve the $cur first (fight/flee/advance/wait it out), or use \`restart\` if truly stuck."
      return 1
      ;;
  esac
  local _FFT_ALLOW_CHAIN=1
  local consecutive=0
  local max_attempts=10
  echo "[fft_resync] escaping to WorldMap (max $max_attempts attempts)..."
  for i in $(seq 1 $max_attempts); do
    local cur=$(_current_screen)
    if [ "$cur" = "WorldMap" ]; then
      consecutive=$((consecutive + 1))
      if [ $consecutive -ge 2 ]; then
        echo "[fft_resync] confirmed WorldMap (2 consecutive reads)"
        break
      fi
      sleep 0.2
      continue
    fi
    consecutive=0
    fft "{\"id\":\"$(id)\",\"keys\":[{\"vk\":27,\"name\":\"Escape\"}],\"delayBetweenMs\":0}" >/dev/null
    _fft_reset_guard
    sleep 0.4
  done
  if [ $consecutive -lt 2 ]; then
    echo "[fft_resync] ERROR: could not confirm WorldMap after $max_attempts attempts — detection may be broken. Try \`restart\`."
    return 1
  fi
  # Reset C# state machine + all auto-resolve latches.
  fft "{\"id\":\"$(id)\",\"action\":\"reset_state_machine\"}" 2>&1 | tail -2
  _fft_reset_guard
  # Confirm via fresh screen read.
  screen
}

# view_unit <name>: Read-only data dump for a unit. Combines roster
# stats with EqA-derived ability assignments and JP-next info.
# No navigation, no key presses — just reads roster from memory and
# formats. Works from anywhere a roster is readable (PartyMenuUnits tree
# or WorldMap). For a richer in-menu view, use open_eqa <unit>.
view_unit() {
  local target="$*"
  if [ -z "$target" ]; then echo "[view_unit] usage: view_unit <name>"; return 1; fi
  _current_screen >/dev/null
  local lowerTarget=$(echo "$target" | tr 'A-Z' 'a-z')
  node -e "
const r=JSON.parse(require('fs').readFileSync(process.argv[1],'utf8'));
const units=(r.screen?.roster?.units||[]);
if(!units.length){console.error('[view_unit] no roster data on this screen');process.exit(1);}
const u=units.find(x=>(x.name||'').toLowerCase()==='$lowerTarget');
if(!u){console.error('[view_unit] unit \"$target\" not found in roster');process.exit(1);}
const eq=u.equipment||{};
const slots=[eq.weapon,eq.leftHand,eq.shield,eq.helm,eq.body,eq.accessory].filter(Boolean);
console.log('  '+u.name+'  '+u.job+'  Lv '+u.level+'  JP '+(u.jp??'--')+'  Brave '+(u.brave??'--')+'  Faith '+(u.faith??'--')+(u.zodiac?'  Zodiac: '+u.zodiac:''));
if(slots.length)console.log('  Equip: '+slots.join(' / '));
else console.log('  Equip: (none)');
" "$B/response.json" 2>&1
}

# unequip_all <unit>: Strip all 5 equipment slots from a named unit.
# Navigates to unit's EqA, then for each slot (R Hand, Shield, Helm,
# Garb, Accessory): Down-walk to slot row → Enter (open picker) → Enter
# again on currently-equipped item to unequip → wait for return to EqA.
# Skips slots that are already empty.
#
# RUNTIME: ~5s per populated slot (open picker, unequip, return) plus
# the initial open_eqa nav (~4s). Fully-equipped unit → ~25-30s total.
# Callers MUST set Bash timeout ≥35s (45s recommended). Per-slot progress
# is printed as each slot completes so a caller watching stdout can see
# it hasn't hung.
unequip_all() {
  local _FFT_ALLOW_CHAIN=1
  local target="$*"
  if [ -z "$target" ]; then echo "[unequip_all] usage: unequip_all <name>"; return 1; fi
  _require_state unequip_all "$_PARTY_NAV_VALID_STATES" || return 1

  open_eqa "$target" >/dev/null
  _fft_reset_guard
  sleep 0.3

  # Verify we landed on EqA for the right unit
  _current_screen >/dev/null
  local landedScr=$(node -e "console.log(JSON.parse(require('fs').readFileSync(process.argv[1],'utf8')).screen?.name||'')" "$B/response.json" 2>/dev/null)
  local landedUnit=$(node -e "console.log(JSON.parse(require('fs').readFileSync(process.argv[1],'utf8')).screen?.viewedUnit||'')" "$B/response.json" 2>/dev/null)
  if [ "$landedScr" != "EquipmentAndAbilities" ]; then
    echo "[unequip_all] ERROR: expected EquipmentAndAbilities, landed on $landedScr"
    return 1
  fi
  if [ "$(echo "$landedUnit" | tr 'A-Z' 'a-z')" != "$(echo "$target" | tr 'A-Z' 'a-z')" ]; then
    echo "[unequip_all] WARN: requested $target but viewedUnit=$landedUnit; aborting"
    return 1
  fi

  echo "[unequip_all] $target: stripping 5 slots (ETA ~25s)"

  # Per-slot progress: read current loadout, pretty-print item name,
  # unequip if populated, else skip. Surface each slot as it completes
  # so the caller can see forward progress.
  local removed=0 skipped=0 idx=0
  local labels=("R Hand" "Shield" "Helm" "Garb" "Accessory")
  for slot in weapon shield helm garb accessory; do
    idx=$((idx+1))
    _current_screen >/dev/null
    local slotItem=$(node -e "
const r=JSON.parse(require('fs').readFileSync(process.argv[1],'utf8'));
const l=r.screen?.loadout||{};
const map={weapon:'weapon',shield:'shield',helm:'helm',garb:'body',accessory:'accessory'};
const v=l[map['$slot']];
console.log(v||'');
" "$B/response.json" 2>/dev/null)
    local label="${labels[$((idx-1))]}"
    if [ -z "$slotItem" ]; then
      echo "  $idx/5 $label: (empty) — skip"
      skipped=$((skipped+1))
      continue
    fi
    echo "  $idx/5 $label: $slotItem → removing..."
    remove_equipment >/dev/null 2>&1 || true
    _fft_reset_guard
    removed=$((removed+1))
    sleep 0.2
    # Move cursor to next slot row (Down on column 0 cycles slots)
    fft "{\"id\":\"$(id)\",\"keys\":[{\"vk\":40,\"name\":\"Down\"}],\"delayBetweenMs\":0}" >/dev/null 2>&1
    _fft_reset_guard
    sleep 0.2
  done
  echo "[unequip_all] $target: done — removed $removed, skipped $skipped (already empty)"
  screen
}

# travel_safe <id>: world_travel_to with auto-flee on encounters.
# Useful for non-grinding traversal. Polls screen state after travel;
# if EncounterDialog appears, fires Flee and re-polls until WorldMap
# or until we've fled 5 times (suggests we're stuck on a forced battle).
travel_safe() {
  local _FFT_ALLOW_CHAIN=1
  local dest="$1"
  if [ -z "$dest" ]; then echo "[travel_safe] usage: travel_safe <location_id>"; return 1; fi
  _require_state travel_safe "WorldMap" || return 1

  world_travel_to "$dest" >/dev/null
  _fft_reset_guard

  local fled=0
  for i in $(seq 1 10); do
    sleep 1
    local cur=$(_current_screen)
    case "$cur" in
      WorldMap)
        echo "[travel_safe] arrived at $(node -e "console.log(JSON.parse(require('fs').readFileSync(process.argv[1],'utf8')).screen?.locationName||'?')" "$B/response.json")"
        screen
        return 0
        ;;
      EncounterDialog)
        if [ $fled -ge 5 ]; then
          echo "[travel_safe] ERROR: fled 5+ times, stuck on forced battle"
          return 1
        fi
        execute_action Flee >/dev/null 2>&1
        _fft_reset_guard
        fled=$((fled+1))
        ;;
      *)
        # Transitional state — keep polling
        ;;
    esac
  done
  echo "[travel_safe] TIMEOUT: didn't arrive at WorldMap after 10s. Current: $(_current_screen)"
  return 1
}

# scan_inventory: Open PartyMenuInventory in verbose mode and dump
# the full inventory grouped by category. Saves Claude a navigation
# round-trip when planning purchases or equipment changes.
scan_inventory() {
  local _FFT_ALLOW_CHAIN=1
  _require_state scan_inventory "$_PARTY_NAV_VALID_STATES" || return 1
  # Get to PartyMenuUnits first (Escape until WorldMap → Escape to PartyMenuUnits)
  return_to_world_map >/dev/null
  _fft_reset_guard
  fft "{\"id\":\"$(id)\",\"keys\":[{\"vk\":27,\"name\":\"Escape\"}],\"delayBetweenMs\":0,\"waitForScreen\":\"PartyMenuUnits\",\"waitTimeoutMs\":3000}" >/dev/null
  _fft_reset_guard
  # Switch to Inventory tab via E
  fft "{\"id\":\"$(id)\",\"keys\":[{\"vk\":69,\"name\":\"E\"}],\"delayBetweenMs\":0,\"waitForScreen\":\"PartyMenuInventory\",\"waitTimeoutMs\":3000}" >/dev/null
  _fft_reset_guard
  # Dump verbose inventory
  screen -v
}

# =============================================================================
# EquipmentAndAbilities helpers
# =============================================================================
# Locked to the EquipmentAndAbilities state — all helpers error if you're
# anywhere else. Idempotent: re-equipping an ability that's already in the slot
# no-ops. Validation: rejects abilities the viewed unit hasn't learned (per
# screen.availableAbilities in the picker).

# Internal: current screen name. Sends a no-op key-press command to refresh
# response.json with the current DetectedScreen, then reads screen.name.
_current_screen() {
  rm -f "$B/response.json"
  echo "{\"id\":\"$(id)\",\"keys\":[],\"delayBetweenMs\":0}" > "$B/command.json"
  local t=0
  until [ -f "$B/response.json" ] || [ $t -ge 150 ]; do
    sleep 0.02
    t=$((t + 1))
  done
  if [ -f "$B/response.json" ]; then
    tr -d '\r\n ' < "$B/response.json" | grep -o '"name":"[^"]*"' | head -1 | cut -d'"' -f4
  fi
}

# _require_state <helper_name> <allowed_states_regex>
# Validates that the current screen matches one of the allowed states.
# Returns 0 if valid, prints error and returns 1 if not.
# Usage: _require_state open_eqa "WorldMap|PartyMenuUnits|CharacterStatus|EquipmentAndAbilities|JobSelection|CombatSets" || return 1
_require_state() {
  local helper="$1"
  local allowed="$2"
  local cur=$(_current_screen)
  if [ -z "$cur" ]; then
    echo "[$helper] ERROR: could not detect current screen"
    return 1
  fi
  if ! echo "$cur" | grep -qE "^($allowed)$"; then
    echo "[$helper] ERROR: cannot run from $cur. Allowed states: $(echo "$allowed" | tr '|' ',' | sed 's/,/, /g')"
    return 1
  fi
  return 0
}

# Internal: read the current equipped ability name for a slot type
# (secondary / reaction / support / movement) from screen.abilities.
_current_equipped() {
  local field="$1"
  node -e "
const r=JSON.parse(require('fs').readFileSync(process.argv[1],'utf8'));
const a=(r.screen&&r.screen.abilities)||{};
console.log(a['$field']||'');
" "$B/response.json" 2>/dev/null
}

# Internal: read availableAbilities from the currently-open picker.
# Emits TSV: <index>\t<name>\t<isEquipped 0|1>
_picker_list_tsv() {
  node -e "
const r=JSON.parse(require('fs').readFileSync(process.argv[1],'utf8'));
const list=(r.screen&&r.screen.availableAbilities)||[];
list.forEach((a,i)=>console.log(i+'\t'+a.name+'\t'+(a.isEquipped?1:0)));
" "$B/response.json" 2>/dev/null
}

# _change_ability <slotType> <targetName>
# slotType: secondary|reaction|support|movement
# Packs the entire navigation as ONE fft_full multi-key batch with a
# generous delayBetweenMs, which keeps the state machine in lockstep
# with the game (no inter-command races). Works from either
# EquipmentAndAbilities OR CharacterStatus with sidebar on Equipment &
# Abilities (prepends an Enter to open the panel).
#
# Returns 0 on success, 1 on error (prints a human-readable message).
_change_ability() {
  local _FFT_ALLOW_CHAIN=1
  local slotType="$1"
  local target="$2"
  if [ -z "$slotType" ] || [ -z "$target" ]; then
    echo "[_change_ability] usage: _change_ability <slotType> <targetName>"
    return 1
  fi
  _require_state "change_${slotType}_ability_to" "EquipmentAndAbilities" || return 1

  # Guard: some units have ability slots pinned to their defaults and
  # cannot re-equip. Construct 8 is the known case. The picker still
  # opens but Enter on alternatives is a no-op — firing the nav keys
  # would just waste round-trips. See JobGridLayout.LockedAbilityUnits.
  local _vunit=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen?.viewedUnit||'')" < "$B/response.json" 2>/dev/null)
  case "$_vunit" in
    "Construct 8")
      echo "[change_${slotType}_ability_to] ERROR: $_vunit has locked ability slots — defaults cannot be changed."
      return 1
      ;;
  esac

  local currentField pickerScreen targetRow
  case "$slotType" in
    secondary) currentField=secondary; pickerScreen=SecondaryAbilities; targetRow=1 ;;
    reaction)  currentField=reaction;  pickerScreen=ReactionAbilities;  targetRow=2 ;;
    support)   currentField=support;   pickerScreen=SupportAbilities;   targetRow=3 ;;
    movement)  currentField=movement;  pickerScreen=MovementAbilities;  targetRow=4 ;;
    *) echo "[_change_ability] unknown slot: $slotType"; return 1 ;;
  esac

  # One fresh state read to prep everything we need: current screen,
  # sidebar position (for CharacterStatus entry), current equipped in
  # this slot, and the unit's learned list for this slot (used to
  # compute target index in the game's canonical picker order).
  _current_screen >/dev/null

  # Parse needed values in ONE node invocation so we don't round-trip
  # the bridge for each.
  local stateFile="$B/__state.txt"
  node -e "
const r=JSON.parse(require('fs').readFileSync(process.argv[1],'utf8'));
const s=r.screen||{};
const ab=s.abilities||{};
const learned=ab['learned${currentField^}']||[];
const cur=ab['$currentField']||'';
const row=(typeof s.cursorRow==='number')?s.cursorRow:0;
const col=(typeof s.cursorCol==='number')?s.cursorCol:0;
console.log(JSON.stringify({name:s.name||'',sidebarUi:s.ui||'',cur:cur,learned:learned,row:row,col:col}));
" "$B/response.json" 2>/dev/null > "$stateFile"
  # Ugly-but-works: shell out to node once per field we need to extract
  # from the single state snapshot.
  local curScreen=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).name)" < "$stateFile")
  local curUi=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).sidebarUi)" < "$stateFile")
  local curEquipped=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).cur)" < "$stateFile")
  local curRow=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).row)" < "$stateFile")
  local curCol=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).col)" < "$stateFile")

  # Build the prefix: what extra keys to send to land on EquipmentAndAbilities.
  local -a prefix=()
  if [ "$curScreen" = "EquipmentAndAbilities" ]; then
    :  # already there
  elif [ "$curScreen" = "CharacterStatus" ] && [ "$curUi" = "Equipment & Abilities" ]; then
    # One Enter opens the panel. After the Enter the state machine's
    # cursor will be (0, 0) on EquipmentAndAbilities by default — we
    # treat curRow/curCol as (0, 0) from this point.
    prefix+=("Enter")
    curRow=0
    curCol=0
  else
    echo "[change_${slotType}_ability_to] ERROR: locked to EquipmentAndAbilities state (or CharacterStatus with sidebar on Equipment & Abilities). Current: $curScreen ui=$curUi"
    rm -f "$stateFile"
    return 1
  fi

  # Idempotence check (only meaningful when we can already read abilities,
  # i.e. we were on EquipmentAndAbilities to begin with). If we're entering
  # via the prefix Enter, the `curEquipped` we read was the CharacterStatus
  # sidebar cursor label — not the ability. Skip the idempotence short-
  # circuit in that case and verify at the end instead.
  if [ "${#prefix[@]}" -eq 0 ] && [ "$curEquipped" = "$target" ]; then
    echo "[change_${slotType}_ability_to] already equipped: $target — no-op"
    rm -f "$stateFile"
    return 0
  fi

  # Validate the target is in the learned list (for EquipmentAndAbilities
  # entry path; for CharacterStatus entry path we skip validation — the
  # roster read on CharacterStatus doesn't include learned* yet).
  if [ "${#prefix[@]}" -eq 0 ]; then
    local targetIdx=$(node -e "
const s=JSON.parse(require('fs').readFileSync(0,'utf8'));
const i=s.learned.indexOf('$target');
console.log(i);" < "$stateFile")
    if [ "$targetIdx" -lt 0 ]; then
      echo "[change_${slotType}_ability_to] ERROR: '$target' is not in the unit's learned ${slotType}s."
      echo "  Available (game's picker order):"
      node -e "
const s=JSON.parse(require('fs').readFileSync(0,'utf8'));
s.learned.forEach(n=>console.log('    - '+n+(n===s.cur?' [equipped]':'')));" < "$stateFile"
      rm -f "$stateFile"
      return 1
    fi
    local equippedIdx=$(node -e "
const s=JSON.parse(require('fs').readFileSync(0,'utf8'));
console.log(s.cur?s.learned.indexOf(s.cur):-1);" < "$stateFile")
  fi
  rm -f "$stateFile"

  # Build the key array. Sequence:
  #   [prefix Enter if from CharacterStatus]
  #   nav to Abilities col (CursorRight if curCol==0)
  #   nav to target row (CursorUp/Down delta)
  #   Enter to open picker
  #   [ScrollUp/Down delta in picker]   — only if we have targetIdx/equippedIdx
  #   Select (Enter) to equip
  #   Escape to close picker
  local -a keys=()
  for k in "${prefix[@]}"; do keys+=("$k"); done

  # Equipment→Abilities column (col 0 → col 1)
  if [ "$curCol" -lt 1 ]; then keys+=("CursorRight"); fi

  # Walk to target row (row 0..4)
  local r="$curRow"
  while [ "$r" -gt "$targetRow" ]; do keys+=("CursorUp"); r=$((r - 1)); done
  while [ "$r" -lt "$targetRow" ]; do keys+=("CursorDown"); r=$((r + 1)); done

  # Open the picker
  keys+=("Enter")

  # Walk picker cursor from equipped to target (only when we pre-computed
  # indices). When entering via CharacterStatus we don't have indices —
  # the picker will still open on the equipped entry, but we can't
  # determine the target offset. In that case, fall back to closing the
  # picker with Escape and failing the helper.
  if [ "${#prefix[@]}" -eq 0 ]; then
    local fromIdx=${equippedIdx:--1}
    if [ "$fromIdx" -lt 0 ]; then fromIdx=0; fi
    local delta=$((targetIdx - fromIdx))
    while [ "$delta" -gt 0 ]; do keys+=("Down"); delta=$((delta - 1)); done
    while [ "$delta" -lt 0 ]; do keys+=("Up"); delta=$((delta + 1)); done
    # Select + close
    keys+=("Enter" "Escape")
  else
    # CharacterStatus entry — close the picker we just opened and bail.
    keys+=("Escape")
    echo "[change_${slotType}_ability_to] ERROR: entering from CharacterStatus is not yet fully supported (learned list not available until on EquipmentAndAbilities). Press Enter on 'Equipment & Abilities' sidebar first, then rerun."
    _fire_keys "${keys[@]}" >/dev/null 2>&1
    return 1
  fi

  # Fire the entire batch as one command with a generous per-key delay
  # so the game has time to render between presses.
  _fire_keys "${keys[@]}" >/dev/null 2>&1

  # Verify post-state.
  _current_screen >/dev/null
  local newScr=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen.name)" < "$B/response.json" 2>/dev/null)
  local newEquipped=$(_current_equipped "$currentField")
  if [ "$newScr" = "EquipmentAndAbilities" ] && [ "$newEquipped" = "$target" ]; then
    echo "[change_${slotType}_ability_to] ${curEquipped:-(none)} -> $target"
    return 0
  fi
  echo "[change_${slotType}_ability_to] ERROR: equip verification failed. Expected EquipmentAndAbilities + $target; got $newScr + ${newEquipped:-(none)}."
  return 1
}

# _fire_keys <key1> <key2> ...
# Sends a batch of named keys (Enter/Escape/CursorUp/Down/Left/Right/
# ScrollUp/Down/Up/Down/Select) as a SINGLE fft command. Uses a
# 220ms delay between keys — tuned so the game renders each step
# before the next key fires, preventing state-machine/game desync.
_fire_keys() {
  local keysJson=""
  local first=1
  for k in "$@"; do
    local vk name
    case "$k" in
      Up|CursorUp|ScrollUp)       vk=38; name=Up ;;
      Down|CursorDown|ScrollDown) vk=40; name=Down ;;
      Left|CursorLeft)            vk=37; name=Left ;;
      Right|CursorRight)          vk=39; name=Right ;;
      Enter|Select)               vk=13; name=Enter ;;
      Escape|Cancel)              vk=27; name=Escape ;;
      Space)                      vk=32; name=Space ;;
      Tab)                        vk=9;  name=Tab ;;
      *) echo "[_fire_keys] unknown key: $k" >&2; return 1 ;;
    esac
    if [ "$first" -eq 1 ]; then
      keysJson="{\"vk\":$vk,\"name\":\"$name\"}"
      first=0
    else
      keysJson+=",{\"vk\":$vk,\"name\":\"$name\"}"
    fi
  done
  fft "{\"id\":\"$(id)\",\"keys\":[$keysJson],\"delayBetweenMs\":220}"
}

# Public helpers.
change_reaction_ability_to() { _change_ability reaction "$*"; }
change_support_ability_to()  { _change_ability support  "$*"; }
change_movement_ability_to() { _change_ability movement "$*"; }
change_secondary_ability_to() { _change_ability secondary "$*"; }

# Internal: coordinates (row, col) for each class on the Ramza JobSelection
# grid. Layout verified live 2026-04-15 — see project_job_grid_cursor.md.
# Returns "<row>,<col>" or empty if the class is unknown. Generic unit
# layouts are assumed identical but with Squire at (0,0) instead of Gallant
# Knight and Bard/Dancer gender-specific at row 2 — verify when first used.
_job_grid_coord() {
  local className="$1"
  local isRamza="${2:-1}"  # 1 if Ramza, 0 for generics
  case "$className" in
    # Row 0 (6 cells)
    "Gallant Knight") [ "$isRamza" = 1 ] && echo "0,0" ;;
    "Squire")         [ "$isRamza" = 0 ] && echo "0,0" ;;
    "Chemist")    echo "0,1" ;;
    "Knight")     echo "0,2" ;;
    "Archer")     echo "0,3" ;;
    "Monk")       echo "0,4" ;;
    "White Mage") echo "0,5" ;;
    # Row 1 (7 cells)
    "Black Mage") echo "1,0" ;;
    "Time Mage")  echo "1,1" ;;
    "Summoner")   echo "1,2" ;;
    "Thief")      echo "1,3" ;;
    "Orator")     echo "1,4" ;;
    "Mystic")     echo "1,5" ;;
    "Geomancer")  echo "1,6" ;;
    # Row 2 (6 cells)
    "Dragoon")       echo "2,0" ;;
    "Samurai")       echo "2,1" ;;
    "Ninja")         echo "2,2" ;;
    "Arithmetician") echo "2,3" ;;
    "Bard")          echo "2,4" ;;  # male-only; for females use Dancer
    "Dancer")        echo "2,4" ;;  # female-only; for males use Bard
    "Mime")          echo "2,5" ;;
  esac
}

# Internal: per-row width on the JobSelection grid. Ramza: 6/7/6.
_job_row_width() {
  local row="$1"
  case "$row" in
    1) echo 7 ;;
    *) echo 6 ;;
  esac
}

# change_job_to <ClassName>
# Switch the viewed unit's job via JobSelection → JobActionMenu
# (Change Job) → JobChangeConfirmation → Confirm flow. Locked to
# JobSelection state (or CharacterStatus with sidebar on Job —
# prepends the Enter to open the grid). Idempotent: if already on
# the target job, no-op with "already equipped".
#
# Usage:
#   change_job_to Chemist
#   change_job_to "Time Mage"
#   change_job_to "Gallant Knight"   # Ramza only
#
# Returns 0 on success, 1 on error.
change_job_to() {
  local target="$*"
  if [ -z "$target" ]; then
    echo "[change_job_to] usage: change_job_to <ClassName>"
    echo "  e.g. change_job_to Knight, change_job_to \"Time Mage\""
    return 1
  fi
  _require_state change_job_to "JobSelection" || return 1

  _current_screen >/dev/null
  local stateFile="$B/__jobstate.txt"
  node -e "
const r=JSON.parse(require('fs').readFileSync(process.argv[1],'utf8'));
const s=r.screen||{};
console.log(JSON.stringify({
  name: s.name||'',
  sidebarUi: s.ui||'',
  row: (typeof s.cursorRow==='number')?s.cursorRow:0,
  col: (typeof s.cursorCol==='number')?s.cursorCol:0,
}));" "$B/response.json" 2>/dev/null > "$stateFile"

  local curScreen=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).name)" < "$stateFile")
  local curUi=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).sidebarUi)" < "$stateFile")
  local curRow=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).row)" < "$stateFile")
  local curCol=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).col)" < "$stateFile")
  rm -f "$stateFile"

  # Build the entry prefix. JobSelection is one Enter past CharacterStatus
  # when the sidebar is on "Job".
  local -a prefix=()
  if [ "$curScreen" = "JobSelection" ]; then
    :  # already there
  elif [ "$curScreen" = "CharacterStatus" ] && [ "$curUi" = "Job" ]; then
    prefix+=("Enter")
    # After Enter the state machine lands on JobSelection (0, 0) — the
    # resolver will kick in on the next screen call. We can't know the
    # real starting cursor here (it's the unit's current job), so we
    # set curRow/curCol=0 and walk from (0, 0). Worst case: the walk
    # is longer than needed by a few keys.
    curRow=0
    curCol=0
  else
    echo "[change_job_to] ERROR: locked to JobSelection (or CharacterStatus with sidebar on Job). Current: $curScreen ui=$curUi"
    return 1
  fi

  # Determine character kind: Ramza is always the first display-order
  # unit in the party grid. Since the command is invoked from a nested
  # screen where the viewed unit is frozen, we infer from the presence
  # of "Gallant Knight" in the sidebar ui when we entered, or just
  # default to Ramza=1 for now. Generic detection needs per-unit gender
  # which isn't surfaced yet. Safe default: assume Ramza (the only
  # character with Gallant Knight); generics share the rest of the grid.
  local isRamza=1

  # Look up target coordinates.
  local targetCoord=$(_job_grid_coord "$target" "$isRamza")
  if [ -z "$targetCoord" ]; then
    echo "[change_job_to] ERROR: unknown class '$target'. Valid classes:"
    echo "  Row 0: Gallant Knight (Ramza) / Squire (generics), Chemist, Knight, Archer, Monk, White Mage"
    echo "  Row 1: Black Mage, Time Mage, Summoner, Thief, Orator, Mystic, Geomancer"
    echo "  Row 2: Dragoon, Samurai, Ninja, Arithmetician, Bard (M) / Dancer (F), Mime"
    return 1
  fi
  local targetRow="${targetCoord%,*}"
  local targetCol="${targetCoord#*,}"

  # Build the key sequence. Walk vertically first (rows are uniform
  # indexable), then horizontally within the target row.
  local -a keys=()
  for k in "${prefix[@]}"; do keys+=("$k"); done

  # Vertical walk — no wrap, grid has 3 rows for Ramza (Mime is row 2
  # for the default grid; row 3 would be extra-unlocks if we add them).
  local r="$curRow"
  while [ "$r" -gt "$targetRow" ]; do keys+=("CursorUp"); r=$((r - 1)); done
  while [ "$r" -lt "$targetRow" ]; do keys+=("CursorDown"); r=$((r + 1)); done

  # When crossing rows, the previous column may be wider than the new
  # row. The state machine clamps col to the new row's last cell, but
  # we walked using curCol directly — fix it here. For the simple walk,
  # we track the effective col AFTER clamp:
  local rowWidth=$(_job_row_width "$targetRow")
  local effectiveCol="$curCol"
  if [ "$effectiveCol" -ge "$rowWidth" ]; then
    effectiveCol=$((rowWidth - 1))
  fi

  # Horizontal walk within the target row.
  local c="$effectiveCol"
  while [ "$c" -gt "$targetCol" ]; do keys+=("CursorLeft"); c=$((c - 1)); done
  while [ "$c" -lt "$targetCol" ]; do keys+=("CursorRight"); c=$((c + 1)); done

  # Walk ONLY to the target cell first — don't open JobActionMenu yet.
  # This gives us a chance to read the cell state and bail out cleanly
  # if the target is Locked (no party member has it) or Visible (we
  # lack prereqs). Attempting a change on those states leaves the game
  # in a weird state (beeps, no-op, or unexpected dialog).
  _fire_keys "${keys[@]}" >/dev/null 2>&1

  # Read the cell state the resolver just populated. screen.jobCellState
  # is "Locked" / "Visible" / "Unlocked".
  _current_screen >/dev/null
  local cellState=$(node -e "
const r=JSON.parse(require('fs').readFileSync(process.argv[1],'utf8'));
console.log((r.screen&&r.screen.jobCellState)||'');" "$B/response.json" 2>/dev/null)

  case "$cellState" in
    "Unlocked")
      : # OK, continue below
      ;;
    "Locked")
      echo "[change_job_to] ERROR: '$target' is Locked — no party member has unlocked this class yet."
      return 1
      ;;
    "Visible")
      echo "[change_job_to] ERROR: '$target' is Visible but this unit hasn't met the unlock prerequisites. Change refused."
      return 1
      ;;
    "")
      # Resolver failed or cell state didn't populate — proceed optimistically.
      # The game will refuse on its own if the cell isn't selectable.
      echo "[change_job_to] WARNING: cell state unknown (resolver may have failed). Proceeding optimistically."
      ;;
    *)
      echo "[change_job_to] ERROR: unexpected cellState '$cellState'. Aborting."
      return 1
      ;;
  esac

  # Capture the viewed unit's current job BEFORE commit so we can verify
  # the change actually took effect. If the commit sequence mis-aligns
  # with the menu flow (e.g. Change Job button absent because target == current
  # job, or a key drops mid-sequence), landing on EqA/CS alone doesn't prove
  # the change — session 31 observed the helper claim success while the job
  # was unchanged.
  local preJobState=$(cat "$B/response.json" | node -e "
let d='';process.stdin.on('data',c=>d+=c);process.stdin.on('end',()=>{try{
const j=JSON.parse(d);
const vu=j.screen?.viewedUnit||'';
const r=(j.screen?.roster?.units||[]);
const u=r.find(x=>x.name===vu);
process.stdout.write(u?u.job:'');
}catch(e){}});" 2>/dev/null)

  # Commit the change: Enter → JobActionMenu → CursorRight → Enter →
  # JobChangeConfirmation → CursorRight → Enter → "Job changed!" dialog →
  # Enter → CharacterStatus (or EquipmentAndAbilities if gear dropped).
  local -a commit=("Enter" "CursorRight" "Enter" "CursorRight" "Enter" "Enter")
  _fire_keys "${commit[@]}" >/dev/null 2>&1

  _current_screen >/dev/null
  local newScr=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen.name)" < "$B/response.json" 2>/dev/null)

  # Verify the job actually changed by reading the viewed unit's job from
  # the post-commit roster. Roster data lags slightly on some screens, so
  # bounce through PartyMenu if we're not on CS/EqA yet.
  local newJob=$(cat "$B/response.json" | node -e "
let d='';process.stdin.on('data',c=>d+=c);process.stdin.on('end',()=>{try{
const j=JSON.parse(d);
const vu=j.screen?.viewedUnit||'';
const r=(j.screen?.roster?.units||[]);
const u=r.find(x=>x.name===vu);
process.stdout.write(u?u.job:'');
}catch(e){}});" 2>/dev/null)

  if [ "$newScr" = "EquipmentAndAbilities" ] || [ "$newScr" = "CharacterStatus" ]; then
    # Idempotent path: if we were already on the target class, the commit
    # sequence may land us on EqA without a job change — that's also OK.
    if [ -n "$newJob" ] && [ "$newJob" != "$target" ] && [ "$newJob" = "$preJobState" ]; then
      echo "[change_job_to] ERROR: commit sequence landed on $newScr but job UNCHANGED (still $newJob). Target '$target' not applied. Likely cause: JobActionMenu commit keys mis-aligned (e.g. 'Change Job' button absent if target == current job, or an animation swallowed a key)."
      return 1
    fi
    echo "[change_job_to] -> $target (landed on $newScr, job=$newJob)"
    return 0
  fi
  echo "[change_job_to] WARNING: expected EquipmentAndAbilities or CharacterStatus; got $newScr. Verify manually."
  return 1
}

# remove_ability <name>
# Unequip a passive by re-Enter'ing its already-equipped row in the picker
# (the in-game unequip idiom: the picker opens with cursor ON the equipped
# entry, and pressing Enter on an already-equipped ability removes it).
# Works from EquipmentAndAbilities; scans all three passive slots +
# secondary to find which one holds <name>. Errors if <name> isn't
# currently equipped anywhere.
remove_ability() {
  local target="$*"
  if [ -z "$target" ]; then
    echo "[remove_ability] usage: remove_ability <ability name>"
    return 1
  fi
  _require_state remove_ability "EquipmentAndAbilities" || return 1

  _current_screen >/dev/null
  local stateFile="$B/__state.txt"
  node -e "
const r=JSON.parse(require('fs').readFileSync(process.argv[1],'utf8'));
const s=r.screen||{};
const ab=s.abilities||{};
console.log(JSON.stringify({
  name: s.name||'',
  row: (typeof s.cursorRow==='number')?s.cursorRow:0,
  col: (typeof s.cursorCol==='number')?s.cursorCol:0,
  secondary: ab.secondary||'',
  reaction:  ab.reaction||'',
  support:   ab.support||'',
  movement:  ab.movement||'',
}));" "$B/response.json" 2>/dev/null > "$stateFile"

  local curScreen=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).name)" < "$stateFile")
  if [ "$curScreen" != "EquipmentAndAbilities" ]; then
    echo "[remove_ability] ERROR: locked to EquipmentAndAbilities state (current: $curScreen)."
    rm -f "$stateFile"
    return 1
  fi

  # Find which slot holds the target ability.
  local slotType=""
  for st in reaction support movement secondary; do
    local cur=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8'))['$st'])" < "$stateFile")
    if [ "$cur" = "$target" ]; then slotType="$st"; break; fi
  done
  if [ -z "$slotType" ]; then
    echo "[remove_ability] ERROR: '$target' is not currently equipped in any ability slot."
    rm -f "$stateFile"
    return 1
  fi

  local targetRow curRow curCol
  case "$slotType" in
    secondary) targetRow=1 ;;
    reaction)  targetRow=2 ;;
    support)   targetRow=3 ;;
    movement)  targetRow=4 ;;
  esac
  curRow=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).row)" < "$stateFile")
  curCol=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).col)" < "$stateFile")
  rm -f "$stateFile"

  # Build the batch: nav to slot, open picker, re-Enter to unequip, Escape.
  local -a keys=()
  if [ "$curCol" -lt 1 ]; then keys+=("CursorRight"); fi
  local r="$curRow"
  while [ "$r" -gt "$targetRow" ]; do keys+=("CursorUp"); r=$((r - 1)); done
  while [ "$r" -lt "$targetRow" ]; do keys+=("CursorDown"); r=$((r + 1)); done
  keys+=("Enter" "Enter" "Escape")

  _fire_keys "${keys[@]}" >/dev/null 2>&1

  # Verify the slot is now empty.
  _current_screen >/dev/null
  local newEquipped=$(_current_equipped "$slotType")
  local newScr=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen.name)" < "$B/response.json" 2>/dev/null)
  if [ "$newScr" = "EquipmentAndAbilities" ] && [ -z "$newEquipped" ]; then
    echo "[remove_ability] removed $target from $slotType slot"
    return 0
  fi
  echo "[remove_ability] ERROR: removal verification failed. Screen=$newScr, $slotType=${newEquipped:-(empty)}"
  return 1
}

# open_equipment_picker <slot>
# DEFERRED — do not call. Ships as a no-op that prints a notice.
#
# Intended to navigate from EquipmentAndAbilities (or CharacterStatus
# sidebar on Equipment & Abilities) to a requested equipment slot and
# open its picker. Blocked on TWO missing pieces:
#
#   1. No memory-backed cursor for the EquipmentAndAbilities equipment
#      column. The state machine tracks a row index but it drifts — on
#      live test 2026-04-15 the helper navigated Down×2 + Enter from
#      what looked like a clean CharacterStatus entry and opened the
#      Weapons picker instead of the requested Helm picker, even
#      though the state machine reported EquippableHeadware /
#      equippedItem=Grand Helm. The game-side cursor state survives
#      the CharacterStatus → EqA transition in a way we don't model.
#
#   2. No stable picker-list row→item-name mapping. See TODO §10.6
#      "EquippableItemList ui= cursor decode". Inventory storage
#      order, not item ID, drives the list.
#
# Either (1) or (2) must be solved first. Until then, use raw keys.
open_equipment_picker() {
  echo "[open_equipment_picker] DEFERRED — equipment picker navigation is"
  echo "  blocked on memory-backed cursor tracking for EquipmentAndAbilities"
  echo "  column 0. Live test 2026-04-15 showed row drift landing on the"
  echo "  wrong slot (Weapons instead of Helm). Use raw keys for now:"
  echo "    fft '{\"id\":\"...\",\"keys\":[{\"vk\":13,\"name\":\"Enter\"},{\"vk\":40,\"name\":\"Down\"},...],\"delayBetweenMs\":250}'"
  echo "  and verify with screenshot before selecting. Follow-up TODO §10.6."
  return 1
}

open_equipment_picker_impl_deferred() {
  local slot="$1"
  if [ -z "$slot" ]; then
    echo "[open_equipment_picker] usage: open_equipment_picker <weapon|shield|helm|garb|accessory>"
    return 1
  fi

  # Slot → (row index in EquipmentAndAbilities column 0, screen name after Enter).
  # Row indices match the left column order used by CommandWatcher slot
  # tracking: 0=Weapon, 1=Shield, 2=Helm, 3=CombatGarb, 4=Accessory.
  # The dual-hand / L-Hand row is only shown when Dual Wield support is
  # equipped — a non-factor on first-pass Ramza/generics, so we use the
  # standard 5-row layout.
  local row expectedScr
  case "$slot" in
    weapon)    row=0; expectedScr=EquippableWeapons ;;
    shield)    row=1; expectedScr=EquippableShields ;;
    helm)      row=2; expectedScr=EquippableHeadware ;;
    garb)      row=3; expectedScr=EquippableCombatGarb ;;
    accessory) row=4; expectedScr=EquippableAccessories ;;
    *) echo "[open_equipment_picker] unknown slot '$slot' (expected weapon|shield|helm|garb|accessory)"; return 1 ;;
  esac

  _current_screen >/dev/null
  local curScr=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen.name||'')" < "$B/response.json" 2>/dev/null)
  local curUi=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen.ui||'')" < "$B/response.json" 2>/dev/null)

  # EquipmentAndAbilities equipment column is a 5-row wrapping list:
  # 0=Weapon 1=Shield/LHand 2=Helm 3=CombatGarb 4=Accessory. Up wraps
  # from row 0 to row 4; Down wraps from row 4 to row 0. When entering
  # from CharacterStatus via Enter, the cursor lands at row 0 (Weapon).
  # From that known baseline we press Down <row> times.
  #
  # From EquipmentAndAbilities directly we don't know the current row
  # (cursor drifts and the memory-backed tracking isn't live for EqA
  # column 0 yet). Read cursorRow if present, otherwise assume row 0
  # and accept one potential misclick.
  local -a keys=()
  if [ "$curScr" = "EquipmentAndAbilities" ]; then
    local curRow=$(node -e "const s=JSON.parse(require('fs').readFileSync(0,'utf8')).screen||{};console.log(typeof s.cursorRow==='number'?s.cursorRow:0)" < "$B/response.json" 2>/dev/null)
    local delta=$((row - curRow))
    if [ "$delta" -ge 0 ]; then
      for i in $(seq 1 $delta); do keys+=("CursorDown"); done
    else
      local up=$((-delta))
      for i in $(seq 1 $up); do keys+=("CursorUp"); done
    fi
    keys+=("Enter")
  elif [ "$curScr" = "CharacterStatus" ] && [ "$curUi" = "Equipment & Abilities" ]; then
    # Enter the panel — lands cursor at row 0. Then Down×row + Enter.
    keys+=("Enter")
    for i in $(seq 1 $row); do keys+=("CursorDown"); done
    keys+=("Enter")
  else
    echo "[open_equipment_picker] ERROR: locked to EquipmentAndAbilities or CharacterStatus (sidebar on Equipment & Abilities). Current: $curScr ui=$curUi"
    return 1
  fi
  # Fix the edge case: $row=0 means 'seq 1 0' produces nothing, so the
  # only keys are Enter (+Enter for the CharacterStatus path). Both are
  # correct — Weapon row is row 0 and opens directly.

  _fire_keys "${keys[@]}" >/dev/null 2>&1
  sleep 0.4
  _current_screen >/dev/null
  local newScr=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen.name||'')" < "$B/response.json" 2>/dev/null)

  if [ "$newScr" = "$expectedScr" ]; then
    echo "[open_equipment_picker] $slot picker opened — now navigate with ScrollUp/Down/Enter to select, Escape to close."
    return 0
  fi
  echo "[open_equipment_picker] WARNING: expected $expectedScr, got $newScr. Drift — verify state manually."
  return 1
}

# list_<slotType>_abilities
# Print the viewed unit's full learned list for the given ability type.
# Reads screen.abilities.learned* (populated on EquipmentAndAbilities) so
# there's zero picker navigation — just a single state read. Marks the
# currently-equipped entry with [equipped].
_list_abilities() {
  local slotType="$1"
  _require_state "list_${slotType}_abilities" "EquipmentAndAbilities" || return 1
  local field learnField
  case "$slotType" in
    secondary) field=secondary; learnField=learnedSecondary ;;
    reaction)  field=reaction;  learnField=learnedReaction ;;
    support)   field=support;   learnField=learnedSupport ;;
    movement)  field=movement;  learnField=learnedMovement ;;
    *) echo "[_list_abilities] unknown slot: $slotType"; return 1 ;;
  esac

  local scr=$(_current_screen)
  if [ "$scr" != "EquipmentAndAbilities" ]; then
    echo "[list_${slotType}_abilities] ERROR: locked to EquipmentAndAbilities state (current: $scr)."
    return 1
  fi

  node -e "
const r=JSON.parse(require('fs').readFileSync(process.argv[1],'utf8'));
const a=(r.screen&&r.screen.abilities)||{};
const equipped=a['$field']||null;
const list=a['$learnField']||[];
if(!list.length){console.log('[list_${slotType}_abilities] No learned ${slotType}s for this unit.');process.exit(0);}
console.log('Learned ${slotType}s ('+list.length+'):');
list.forEach(n=>console.log('  - '+n+(n===equipped?'  [equipped]':'')));
" "$B/response.json"
}
list_reaction_abilities()  { _list_abilities reaction; }
list_support_abilities()   { _list_abilities support; }
list_movement_abilities()  { _list_abilities movement; }
list_secondary_abilities() { _list_abilities secondary; }

# resolve_eqa_row: Pin the EqA column-0 cursor row via the unequip-diff trick.
# Fires Enter+Enter to toggle the mirror, finds which slot transitioned, then
# Enter+Escape to restore. Works on empty slots too (inverse 0→X detection).
# Cost ~1.5s. Locks ScreenMachine.CursorRow to the resolved row.
resolve_eqa_row() { fft "{\"id\":\"$(id)\",\"action\":\"resolve_eqa_row\"}"; }

# cursor_walk: Diagnostic that walks the cursor from the unit's start tile,
# flood-fills via cursor + snapshot-diff probe (looking for 04→05 slot-flag
# transitions that indicate cursor-on-valid-tile), and compares the
# game-observed valid-tile set against our BFS output. Logs false positives
# (BFS said valid, game rejected) and false negatives (game valid, BFS missed).
# Must be called in BattleMoving with the cursor on the unit's own tile.
# Cost: ~30-60 seconds per run (slow — diagnostic, NOT for runtime).
cursor_walk() { fft "{\"id\":\"$(id)\",\"action\":\"cursor_walk\"}"; }

# remove_equipment: Unequip whatever is in the currently-hovered EqA slot.
# One atomic C# action: opens the picker, toggles, reads the mirror to learn
# which row we were on, leaves the slot empty, closes the picker. Works on
# both populated and empty slots (empty-slot case auto-equips the first
# picker item and then unequips it — net zero).
#
# Position-agnostic entry (added session 24): the underlying C# toggle
# is position-sensitive — firing it on the abilities column opens an
# ability picker instead of an equipment slot, producing a silent wrong
# action. We inspect cursor column from the state response first and
# auto-Left from column 1 to column 0 so the toggle always targets the
# intended equipment slot. If the cursor is somewhere else entirely
# (shouldn't happen on EqA), we refuse with a clear error rather than
# guessing.
remove_equipment() {
  local _FFT_ALLOW_CHAIN=1
  _require_state remove_equipment "EquipmentAndAbilities" || return 1

  # Pull the current cursor column. ScreenStateMachine tracks it;
  # compact render surfaces cursor=(r<N>,c<N>) on EqA.
  _current_screen >/dev/null
  local curCol=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen?.cursorCol??-1)" < "$B/response.json" 2>/dev/null)

  case "$curCol" in
    0)
      # Already on the equipment column — safe to toggle directly.
      ;;
    1)
      # Abilities column. Fire one Left to hop to the equipment column.
      # The toggle targets whichever row we land on (state machine
      # preserves row across a Left press).
      fft "{\"id\":\"$(id)\",\"keys\":[{\"vk\":37,\"name\":\"Left\"}],\"delayBetweenMs\":0}" >/dev/null
      _fft_reset_guard
      ;;
    *)
      echo "[remove_equipment] ERROR: cursor on unexpected column ($curCol). Expected 0 (equipment) or 1 (abilities). Refusing to fire — bring the cursor to the equipment column first."
      return 1
      ;;
  esac

  fft "{\"id\":\"$(id)\",\"action\":\"remove_equipment_at_cursor\"}"
}

# Equipment-slot change helpers — blocked on picker item-list decoding
# (availableWeapons[] / per-job equippability table, TODO §0). Cursor row
# IS now resolvable via resolve_eqa_row, but without the filtered item list
# we can't navigate the picker to a named target.
_not_implemented_equipment() {
  local helper="$1"
  echo "[$helper] Not implemented yet. Cursor row is now resolvable via resolve_eqa_row, but picker item-list decoding (per-job equippability filter) is still pending. remove_equipment DOES work — use that if you just want to unequip the hovered slot."
  return 1
}
change_right_hand_to()  { _not_implemented_equipment change_right_hand_to; }
change_left_hand_to()   { _not_implemented_equipment change_left_hand_to; }
change_helm_to()        { _not_implemented_equipment change_helm_to; }
change_garb_to()        { _not_implemented_equipment change_garb_to; }
change_accessory_to()   { _not_implemented_equipment change_accessory_to; }

# save: Save the game.
save() { fft "{\"id\":\"$(id)\",\"action\":\"save\"}"; }

# save_game [slot]: Navigate the in-game Save flow at a settlement
# (LocationMenu → Save Game → SaveGame_Menu → pick slot → confirm).
# UNIMPLEMENTED — flow needs SaveGame_Menu detection + slot picker
# tracking. Use `save` for the underlying action stub once it works.
save_game() {
  echo "[save_game] NOT IMPLEMENTED — needs SaveGame_Menu navigation flow"
  return 1
}

# load_game [slot]: Navigate the LoadGame screen (from title or pause
# menu) → pick slot → confirm load. UNIMPLEMENTED — needs LoadGame
# detection from non-GameOver paths and slot cursor tracking.
load_game() {
  echo "[load_game] NOT IMPLEMENTED — needs LoadGame navigation flow"
  return 1
}

# load: Load the most recent save.
load() { fft "{\"id\":\"$(id)\",\"action\":\"load\"}"; }

# detection_dump: Dump raw screen-detection inputs + detected screen name.
# Audit tool for verifying ScreenDetectionLogic.Detect against ground truth.
detection_dump() { fft "{\"id\":\"$(id)\",\"action\":\"dump_detection_inputs\"}"; }

# log_tail: Show the last N rows of the acted/moved sampler log.
# Usage: log_tail [N] (default 30)
log_tail() { tail -n "${1:-30}" "$B/acted_moved_log.csv"; }

# battle_retry: Retry the current battle from the pause menu.
battle_retry() { fft "{\"id\":\"$(id)\",\"action\":\"battle_retry\"}"; }

# battle_retry_formation: Retry battle with formation screen from the pause menu.
battle_retry_formation() { fft "{\"id\":\"$(id)\",\"action\":\"battle_retry_formation\"}"; }

# buy: Buy an item from a shop.
# Usage: buy <item_name> <quantity>
buy() { fft "{\"id\":\"$(id)\",\"action\":\"buy\",\"to\":\"$1\",\"locationId\":${2:-1}}"; }

# sell: Sell an item at a shop.
# Usage: sell <item_name> <quantity>
sell() { fft "{\"id\":\"$(id)\",\"action\":\"sell\",\"to\":\"$1\",\"locationId\":${2:-1}}"; }

# change_job: Change a unit's job.
# Usage: change_job <unit_id> <job_name>
change_job() { fft "{\"id\":\"$(id)\",\"action\":\"change_job\",\"locationId\":$1,\"to\":\"$2\"}"; }

# goto: Travel to a location, enter encounter, place Ramza solo, start battle.
# Handles: travel → confirm move → encounter → formation → battle.
# Flees encounters at wrong locations along the way.
# Usage: goto 26   (travel to loc 26 and fight the random encounter)
goto() {
  local _FFT_ALLOW_CHAIN=1
  local target=$1
  _check_total || return 1
  # Step 1: Travel to hover over target + confirm move
  echo "[goto] Traveling to location $target..."
  fft "{\"id\":\"$(id)\",\"action\":\"world_travel_to\",\"locationId\":$target}"
  sleep 0.5
  key 13 Enter
  sleep 2
  # Step 2: Poll for encounter or arrival
  local max=20
  for i in $(seq 1 $max); do
    rm -f "$B/response.json"
    echo "{\"id\":\"$(id)\",\"keys\":[],\"delayBetweenMs\":0}" > "$B/command.json"
    local t=0
    until [ -f "$B/response.json" ] || [ $t -ge 150 ]; do sleep 0.02; t=$((t+1)); done
    local R=$(tr -d '\r\n ' < "$B/response.json")
    local scr=$(echo "$R" | grep -o '"name":"[^"]*"' | head -1 | cut -d'"' -f4)
    local loc=$(echo "$R" | grep -o '"location":[0-9]*' | head -1 | cut -d: -f2)
    echo "[goto] poll $i — screen=$scr loc=$loc"
    # Encounter dialog
    if [ "$scr" = "EncounterDialog" ]; then
      if [ "$loc" = "$target" ]; then
        echo "[goto] Encounter at target! Accepting..."
        key 13 Enter
        sleep 3
        # Formation screen: place Ramza solo → start battle
        # Enter=place on starting tile, Space=done, Enter=Yes to commence
        echo "[goto] Formation: placing Ramza..."
        key 13 Enter
        sleep 1
        key 32 Space
        sleep 1
        key 13 Enter
        sleep 3
        echo "[goto] Battle starting..."
        return 0
      else
        echo "[goto] Encounter at loc $loc (not target $target) — fleeing..."
        key 40 Down
        sleep 0.5
        key 13 Enter
        sleep 3
        continue
      fi
    fi
    # Already in battle
    if echo "$scr" | grep -q "Battle"; then
      echo "[goto] In battle at loc $loc"
      return 0
    fi
    # On world map at target — press Enter to trigger encounter
    if [ "$scr" = "WorldMap" ] && [ "$loc" = "$target" ]; then
      echo "[goto] At target, triggering encounter..."
      key 13 Enter
      sleep 2
      continue
    fi
    # TravelList at target — press Enter to trigger encounter
    if [ "$scr" = "TravelList" ] && [ "$loc" = "$target" ]; then
      echo "[goto] TravelList at target, pressing Enter..."
      key 13 Enter
      sleep 2
      continue
    fi
    sleep 1
  done
  echo "[goto] Timeout after $max polls"
  return 1
}

# =============================================================================
# SYSTEM COMMANDS
# =============================================================================

# running: Check whether FFT_enhanced.exe is currently running.
# Returns 0 if running, 1 if not. Prints one-line status.
# Fast: uses tasklist, no bridge round-trip.
running() {
  if tasklist //NH //FI "IMAGENAME eq FFT_enhanced.exe" 2>/dev/null | grep -qi "FFT_enhanced.exe"; then
    echo "[running] FFT_enhanced.exe: YES"
    return 0
  else
    echo "[running] FFT_enhanced.exe: NO"
    return 1
  fi
}

# _launch_game: Internal helper — starts Reloaded-II + FFT in the background.
# Does NOT wait for the bridge; caller is responsible for that.
_launch_game() {
  "/c/program files (x86)/steam/steamapps/common/FINAL FANTASY TACTICS - The Ivalice Chronicles/Reloaded/reloaded-ii.exe" --launch "C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\FFT_enhanced.exe" &
}

# _wait_bridge: Delete stale state.json, then poll for a fresh one.
# Returns 0 if the bridge came online within $1 seconds (default 60), 1 on timeout.
# Must delete first — stale state.json from a prior session would fool a naive check.
_wait_bridge() {
  local max_seconds=${1:-60}
  rm -f "$B/state.json"
  local deadline=$(( SECONDS + max_seconds ))
  while [ $SECONDS -lt $deadline ]; do
    if [ -f "$B/state.json" ]; then
      return 0
    fi
    sleep 0.2
  done
  return 1
}

# _raw_advance: Press Enter via the execute_action path, which is strict-mode safe.
# Used by the boot loop. Takes no args; uses the "Advance" validPath which is
# present on TitleScreen, Cutscene, and most intro/loading screens.
_raw_advance() {
  rm -f "$B/response.json"
  echo "{\"id\":\"c$(date +%s%N | tail -c 8)$RANDOM\",\"action\":\"execute_action\",\"to\":\"Advance\"}" > "$B/command.json"
  # Wait up to 3s for response; if it doesn't come, the mod may still be loading.
  local t=0
  until [ -f "$B/response.json" ] || [ $t -ge 150 ]; do
    sleep 0.02
    t=$((t + 1))
  done
}

# _raw_read_screen: Send a no-op command to read the current screen name.
# Returns the screen name via echo, or empty string on timeout.
_raw_read_screen() {
  rm -f "$B/response.json"
  echo "{\"id\":\"c$(date +%s%N | tail -c 8)$RANDOM\",\"keys\":[],\"delayBetweenMs\":0}" > "$B/command.json"
  local t=0
  until [ -f "$B/response.json" ] || [ $t -ge 150 ]; do
    sleep 0.02
    t=$((t + 1))
  done
  if [ -f "$B/response.json" ]; then
    tr -d '\r\n ' < "$B/response.json" | grep -o '"name":"[^"]*"' | head -1 | cut -d'"' -f4
  fi
}

# _advance_past_title: Press Enter (via execute_action Advance) until the screen
# is no longer TitleScreen. Shared between boot and restart. Prints per-iteration
# progress with elapsed time. Returns 0 on success, 1 on timeout after $1 attempts.
#
# Uses execute_action Advance rather than raw key presses because strict mode
# blocks raw keypresses with status=blocked, which would silently hang the loop.
_advance_past_title() {
  local max=${1:-20}
  local t0=$SECONDS
  for i in $(seq 1 $max); do
    _raw_advance
    sleep 2
    local scr=$(_raw_read_screen)
    local elapsed=$(( SECONDS - t0 ))
    echo "[boot] attempt $i/${max} (${elapsed}s) — screen: ${scr:-<no response>}"
    if [ -n "$scr" ] && [ "$scr" != "TitleScreen" ]; then
      echo "[boot] Arrived at $scr"
      return 0
    fi
  done
  echo "[boot] TIMEOUT after $max attempts"
  return 1
}

# restart: Full cycle — kill game, build mod, deploy, relaunch, wait for bridge.
# Use when you need code changes to take effect.
restart() {
  # Reset total-script budget — boot can legitimately take 60+ seconds.
  FFT_START=$SECONDS
  _fft_reset_guard
  echo "[restart] Killing game..."
  taskkill //IM FFT_enhanced.exe //F 2>/dev/null
  taskkill //IM reloaded-ii.exe //F 2>/dev/null
  sleep 2
  if running >/dev/null; then
    echo "[restart] FFT_enhanced.exe still running after taskkill — aborting"
    return 1
  fi
  echo "[restart] Building..."
  dotnet build ColorMod/FFTColorCustomizer.csproj -c Release 2>&1 | tail -3
  echo "[restart] Deploying..."
  powershell.exe -ExecutionPolicy Bypass -File ./BuildLinked.ps1 2>&1 | tail -3
  echo "[restart] Launching..."
  _launch_game
  echo "[restart] Waiting for bridge..."
  if ! _wait_bridge 60; then
    echo "[restart] TIMEOUT waiting for bridge"
    return 1
  fi
  echo "[restart] Bridge online. Advancing past title..."
  _advance_past_title 20
}

# boot: Get the game to a playable screen.
# If the game isn't running, launches it and waits for the bridge.
# Then presses Enter through title/continue screens until past TitleScreen.
boot() {
  # Reset total-script budget — boot can legitimately take 60+ seconds.
  FFT_START=$SECONDS
  _fft_reset_guard
  if ! running >/dev/null; then
    echo "[boot] Game not running — launching..."
    _launch_game
    echo "[boot] Waiting for bridge..."
    if ! _wait_bridge 60; then
      echo "[boot] TIMEOUT waiting for bridge"
      return 1
    fi
    echo "[boot] Bridge online."
  fi
  _advance_past_title 20
}

# =============================================================================
# STATE HELPERS
# =============================================================================

# screen: Universal state command. THE primary way to see game state.
# In battle: shows active unit, abilities with reachable targets, all units.
# Outside battle: shows screen name, location, status.
# Usage: screen        (compact — default)
#        screen -v     (verbose — adds PA/MA/Spd/CT/Br/Fa per unit, full ability details)
screen() {
  local verbose=false
  if [ "$1" = "-v" ]; then verbose=true; shift; fi

  # Quick screen check first
  _check_total || return 1
  local _t0=$EPOCHREALTIME
  local _cmd="{\"id\":\"$(id)\",\"keys\":[],\"delayBetweenMs\":0}"
  rm -f "$B/response.json"
  echo "$_cmd" > "$B/command.json"
  local tries=0
  until [ -f "$B/response.json" ]; do
    sleep 0.02; tries=$((tries+1))
    if [ $tries -ge 250 ]; then echo "[TIMEOUT]"; return 1; fi
  done
  local _t1=$EPOCHREALTIME
  local R=$(cat "$B/response.json" | tr -d '\r\n ')
  local SCR=$(echo "$R" | grep -o '"name":"[^"]*"' | head -1 | cut -d'"' -f4)

  # During Battle_MyTurn: run scan_move for full tactical view
  if [[ "$SCR" == "BattleMyTurn" ]]; then
    local vflag="false"; $verbose && vflag="true"
    local _t_scan0=$EPOCHREALTIME
    local raw
    local _scan_cmd="{\"id\":\"$(id)\",\"action\":\"scan_move\",\"verbose\":$vflag}"
    raw=$(fft_full "$_scan_cmd")
    local _t_scan1=$EPOCHREALTIME
    # Timing suffix: the scan_move bridge time is what matters here (dominates
    # the screen ping). _FFT_TIMING_SUFFIX is picked up by the node header below.
    export _FFT_TIMING_SUFFIX="$(_fmt_timing "$_t_scan0" "$_t_scan1" "$_scan_cmd")"
    echo "$raw" | node -e "
const j=JSON.parse(require('fs').readFileSync(0,'utf8'));
const s=j.screen||{};
const b=j.battle||{};
const au=b.activeUnit;
const us=b.units||[];
const verbose=$vflag;

// Header
const activeU=us.find(u=>u.isActive);
const aName=au?.name||activeU?.name||s.activeUnitName||'?';
const aJob=au?.jobName||activeU?.jobName||s.activeUnitJob||'';
const ax=au?.x??activeU?.x??'?';
const ay=au?.y??activeU?.y??'?';
const ahp=au?.hp??activeU?.hp??'?';
const amhp=au?.maxHp??activeU?.maxHp??'?';
const amp=au?.mp??activeU?.mp??'?';
const ammp=au?.maxMp??activeU?.maxMp??'?';
const uiTag=s.ui?' ui='+s.ui:'';
const tSuffix=process.env._FFT_TIMING_SUFFIX?' '+process.env._FFT_TIMING_SUFFIX:'';
console.log('['+s.name+']'+uiTag+' '+aName+(aJob?'('+aJob+')':'')+' ('+ax+','+ay+') HP='+ahp+'/'+amhp+' MP='+amp+'/'+ammp+tSuffix);
console.log('');

// Abilities with target tiles (filtering/collapsing done server-side by AbilityCompactor)
if(activeU&&activeU.abilities){
  console.log('Abilities:');
  activeU.abilities.forEach(a=>{
    // Element-affinity marker sigils: + absorb (healed), = null (no damage),
    // ~ half (resisted), ! weak (double damage), ^ strengthen (boosts own damage).
    // Only surfaces when ability has an element AND occupant has matching affinity.
    const affSig={absorb:'+absorb',null:'=null',half:'~half',weak:'!weak',strengthen:'^strengthen'};
    // Attack-arc sigils: back=best (backstab bonus), side=modest, front=omitted (default).
    const arcSig={back:'>BACK',side:'>side'};
    // LoS sigil — projectile ability (ranged Attack / Ninja Throw) terrain-blocked.
    // Claude should skip the tile or reposition. Null/absent = not a projectile
    // OR path is clear.
    const losSig=t=>t.losBlocked?' !blocked':'';
    // Compact rule: only render tiles that have an occupant. Empty tiles
    // dilute the line — Items 27-tile spell range becomes 25 noise tuples.
    // Trailing N-empty count preserves the range size for planning.
    const allTiles=a.validTargetTiles||[];
    const occupiedTiles=allTiles.filter(t=>t.occupant&&t.occupant!=='empty');
    const emptyCount=allTiles.length-occupiedTiles.length;
    const tiles=occupiedTiles.map(t=>{
      let s='('+t.x+','+t.y+')';
      const tag=(t.occupant==='ally'||t.occupant==='self')?' ALLY':'';
      const aff=t.affinity&&affSig[t.affinity]?' '+affSig[t.affinity]:'';
      const arc=t.arc&&arcSig[t.arc]?' '+arcSig[t.arc]:'';
      const los=losSig(t);
      if(t.unitName)s+='<'+t.unitName+tag+aff+arc+los+'>';
      else s+='<'+t.occupant+aff+arc+los+'>';
      return s;
    });
    if(emptyCount>0)tiles.push('('+emptyCount+' empty)');
    const mp=a.mpCost?' mp='+a.mpCost:'';
    const ct=a.castSpeed?' ct='+a.castSpeed:'';
    const el=a.element?' ['+a.element+']':'';
    const eff=a.addedEffect?' {'+a.addedEffect+'}':'';
    // heldCount is only set for inventory-gated skillsets (Items/Iaido/Throw).
    // Render [xN] when stocked, [OUT] when zero so Claude can pick from valid options at a glance.
    const held=(a.heldCount!=null)?(a.heldCount>0?' [x'+a.heldCount+']':' [OUT]'):'';
    console.log('  '+a.name+mp+ct+el+eff+held+' \\u2192 '+(tiles.length?tiles.join(' '):'(no targets in range)'));
  });
  console.log('');
}

// Move tiles
const vp=j.validPaths||{};
const vmt=vp.ValidMoveTiles;
if(vmt){
  // Round height to integer — half-steps (h=4.5) are slope midpoints that
  // don't change high-ground decisions. Shaves ~30% off the line length.
  const tlist=(vmt.tiles||[]).map(t=>'('+t.x+','+t.y+(t.h!=null?' h='+Math.round(t.h):'')+')');
  console.log('Move tiles: '+(tlist.length?tlist.join(' '):'(none)')+(vmt.desc?'  — '+vmt.desc:''));
}
// Attack tiles (adjacent cardinals). Only render when at least one
// cardinal has an occupant — the empty-4-cardinals case is pure noise.
const atk=vp.AttackTiles?.attackTiles||[];
const occupiedAtk=atk.filter(a=>a.occupant&&a.occupant!=='empty');
if(occupiedAtk.length){
  const arcSig2={back:' >BACK',side:' >side'};
  const lines=occupiedAtk.map(a=>{
    const occ=' '+a.occupant;
    const job=a.jobName?' ('+a.jobName+')':'';
    const hp=a.hp!=null?' HP='+a.hp:'';
    const arc=a.arc&&arcSig2[a.arc]?arcSig2[a.arc]:'';
    return a.arrow+'→('+a.x+','+a.y+')'+occ+job+hp+arc;
  });
  console.log('Attack tiles: '+lines.join('  '));
}
// Recommended facing
const rf=vp.RecommendedFacing;
if(rf?.desc)console.log('Facing: '+rf.desc);
console.log('');

// Units
console.log('Units:');
us.forEach(u=>{
  const team=u.team===0?'PLAYER':u.team===2?'ALLY':'ENEMY';
  const nm=u.name?' '+u.name:'';
  // When no name (typical for enemies), drop parens around job — reads
  // cleaner as bare job name vs parenthesized job.
  const cl=u.jobName?(u.name?'('+u.jobName+')':u.jobName):'';
  const clSep=u.jobName?(u.name?'':' '):'';
  const st=u.statuses?.length?' ['+u.statuses.join(',')+']':'';
  const life=u.lifeState==='dead'?' DEAD':'';
  const act=u.isActive?' *':'';
  const dist=u.distance!==undefined&&!u.isActive?' d='+u.distance:'';
  // Facing is decision-relevant for backstab — keep on enemies only. Ally
  // facing rarely drives the current turn (ally auto-faces on Wait).
  const face=(u.facing&&u.team===1)?' f='+u.facing[0]:'';
  let extra='';
  if(verbose){
    // Skip undefined fields — the battle scan backend doesn't always populate
    // every stat. Avoid printing literal "undefined".
    const parts=[];
    if(u.pa!=null)parts.push('PA='+u.pa);
    if(u.ma!=null)parts.push('MA='+u.ma);
    if(u.speed!=null)parts.push('Spd='+u.speed);
    if(u.ct!=null)parts.push('CT='+u.ct);
    if(u.brave!=null)parts.push('Br='+u.brave);
    if(u.faith!=null)parts.push('Fa='+u.faith);
    if(parts.length)extra=' '+parts.join(' ');
    if(u.reaction)extra+=' R:'+u.reaction;
    if(u.support)extra+=' S:'+u.support;
    if(u.movement)extra+=' M:'+u.movement;
    if(u.elementAbsorb?.length)extra+=' +abs:'+u.elementAbsorb.join(',');
    if(u.elementNull?.length)extra+=' =null:'+u.elementNull.join(',');
    if(u.elementHalf?.length)extra+=' ~half:'+u.elementHalf.join(',');
    if(u.elementWeak?.length)extra+=' !weak:'+u.elementWeak.join(',');
    if(u.elementStrengthen?.length)extra+=' ^str:'+u.elementStrengthen.join(',');
  }
  console.log('  ['+team+']'+nm+clSep+cl+' ('+u.x+','+u.y+')'+face+' HP='+u.hp+'/'+u.maxHp+dist+extra+st+life+act);
});
" 2>/dev/null
    unset _FFT_TIMING_SUFFIX
  else
    # Non-battle: render via the shared helper — same compact one-liner as fft().
    _FFT_TIMING_SUFFIX=$(_fmt_timing "$_t0" "$_t1" "$_cmd")
    _fmt_screen_compact "$B/response.json"
    unset _FFT_TIMING_SUFFIX

    # Inventory summary (verbose only) — items=N types, M total.
    if $verbose && [ -f "$B/response.json" ]; then
      local INV_SUMMARY=$(cat "$B/response.json" | node -e "let d='';process.stdin.on('data',c=>d+=c);process.stdin.on('end',()=>{try{const j=JSON.parse(d);const inv=j.screen?.inventory;if(!inv||!inv.length){return;}const name=j.screen?.name;const total=inv.reduce((a,e)=>a+(e.count||0),0);if(name==='OutfitterSell'){const gil=inv.reduce((a,e)=>a+((e.sellPrice||0)*(e.count||0)),0);process.stdout.write(inv.length+' sellable, '+total+' total, ~'+gil.toLocaleString()+' gil est');}else{process.stdout.write(inv.length+' types, '+total+' total');}}catch(e){}});" 2>/dev/null)
      [ -n "$INV_SUMMARY" ] && printf '%b\n' "  items=$INV_SUMMARY"
    fi

    # CharacterStatus verbose: unit stat sheet from roster data.
    # Shows the same info as the compact EqA summary but sourced from
    # the roster (since loadout/abilities aren't populated on CS).
    if $verbose && [ "$SCR" = "CharacterStatus" ] && [ -f "$B/response.json" ]; then
      cat "$B/response.json" | node -e "
try{
  const j=JSON.parse(require('fs').readFileSync(0,'utf8'));
  const vu=j.screen?.viewedUnit;
  const units=j.screen?.roster?.units||[];
  const u=vu?units.find(x=>x.name===vu):units[0];
  if(!u)process.exit(0);
  // Line 1: identity
  const parts=[u.name,u.job,'Lv '+u.level,'JP '+(u.jp??'--'),'Brave '+(u.brave??'--'),'Faith '+(u.faith??'--')];
  if(u.zodiac)parts.push('Zodiac: '+u.zodiac);
  console.log('  '+parts.join('  '));
  // Line 2: stats (placeholders until memory reads land)
  console.log('  HP --/--  MP --/--  PA --  MA --  Speed --  Move --  Jump --');
  // Line 3: equipment
  const eq=u.equipment||{};
  const slots=[eq.weapon,eq.leftHand,eq.shield,eq.helm,eq.body,eq.accessory].filter(Boolean);
  if(slots.length)console.log('  Equip: '+slots.join(' / '));
  else console.log('  Equip: (none)');
  // Line 4: abilities (from EqA enrichment if available, else placeholder)
  const a=j.screen?.abilities;
  if(a){
    const ab=[a.primary,a.secondary,a.reaction,a.support,a.movement].map(x=>x||'(none)');
    console.log('  Abilities: '+ab.join(' / '));
  }
}catch(e){}
" 2>/dev/null
    fi

    # JobSelection verbose: render the 3-row job grid with cursor marker.
    # Grid layout is static per-character (story chars get unique class at
    # (0,0), generics get Squire; males get Bard at (2,4), females Dancer).
    # Cell states (Unlocked/Visible/Locked) only available for hovered cell
    # today — full-grid classification is a future C# addition.
    if $verbose && [ "$SCR" = "JobSelection" ] && [ -f "$B/response.json" ]; then
      cat "$B/response.json" | node -e "
try{
  const j=JSON.parse(require('fs').readFileSync(0,'utf8'));
  const s=j.screen||{};
  const cr=s.cursorRow;
  const cc=s.cursorCol;
  // Determine grid variant from viewedUnit.
  const vu=s.viewedUnit||'';
  const storyClass={
    Ramza:'Gallant Knight',Agrias:'Holy Knight',Mustadio:'Machinist',
    Rapha:'Skyseer',Marach:'Netherseer',Beowulf:'Templar',
    'Construct 8':'Steel Giant',Orlandeau:'Thunder God',
    Meliadoul:'Divine Knight',Reis:'Dragonkin',Cloud:'Soldier',
    Luso:'Game Hunter',Balthier:'Sky Pirate'
  };
  const cell00=storyClass[vu]||'Squire';
  // Gender: roster lookup for generic units. Story chars default male (Bard).
  const roster=(s.roster&&s.roster.units)||[];
  const unit=roster.find(u=>u.name===vu)||{};
  const isFemale=unit.job&&['Dancer'].includes(unit.job); // rough heuristic
  const cell24=isFemale?'Dancer':'Bard';
  const grid=[
    [cell00,'Chemist','Knight','Archer','Monk','White Mage'],
    ['Black Mage','Time Mage','Summoner','Thief','Orator','Mystic','Geomancer'],
    ['Dragoon','Samurai','Ninja','Arithmetician',cell24,'Mime'],
  ];
  // Header line: unit identity
  const parts=[vu||'?'];
  if(unit.job)parts.push(unit.job);
  if(unit.level)parts.push('Lv '+unit.level);
  if(unit.jp!==undefined)parts.push('JP '+unit.jp);
  console.log('  '+parts.join('  ')+':');
  // Column headers
  const colW=18;
  const gutter='cursor->  ';
  const blank=' '.repeat(gutter.length);
  const maxCols=Math.max(...grid.map(r=>r.length));
  let hdr='  '+blank;
  for(let c=0;c<maxCols;c++)hdr+=('c'+c).padEnd(colW);
  console.log(hdr);
  // Grid rows
  for(let r=0;r<grid.length;r++){
    const isCursorRow=(r===cr);
    let line='  r'+r+(isCursorRow?' '+gutter:' '+blank);
    for(let c=0;c<grid[r].length;c++){
      const cls=grid[r][c];
      const isCursor=(r===cr&&c===cc);
      const cell=isCursor?'['+cls+']':cls;
      line+=cell.padEnd(colW);
    }
    console.log(line.trimEnd());
  }
}catch(e){}
" 2>/dev/null
    fi

    # Equipment loadout + abilities for EquipmentAndAbilities.
    # Full two-column grid is verbose-only (planning data). Compact gets the
    # two summary lines from _fmt_screen_compact instead.
    if $verbose && [ "$SCR" = "EquipmentAndAbilities" ] \
       && [ -f "$B/response.json" ]; then
      cat "$B/response.json" | node -e "
try{
  const j=JSON.parse(require('fs').readFileSync(0,'utf8'));
  const l=j.screen&&j.screen.loadout;
  const a=j.screen&&j.screen.abilities;
  if(!l&&!a)process.exit(0);

  // Build rows matching the in-game layout.
  //   Equipment column        Ability column
  //   R Hand: Ragnarok        Primary:   Mettle
  //   L Hand: <n/a>           Secondary: Items
  //   Shield: Escutcheon       Reaction:  Parry
  //   Helm:   Grand Helm       Support:   Magick Defense Boost
  //   Chest:  Maximillian      Movement:  Movement +3
  //   Access: Bracer
  const eqRows=[];
  if(l){
    eqRows.push(['R Hand',l.weapon]);
    if(l.leftHand)eqRows.push(['L Hand',l.leftHand]);
    eqRows.push(['Shield',l.shield]);
    eqRows.push(['Helm',l.helm]);
    eqRows.push(['Chest',l.body]);
    eqRows.push(['Access',l.accessory]);
  }
  const abRows=a?[
    ['Primary',a.primary],
    ['Secondary',a.secondary],
    ['Reaction',a.reaction],
    ['Support',a.support],
    ['Movement',a.movement],
  ]:[];

  // Header line matches the game's info bar: name, job, level, JP.
  // Find the roster entry whose name matches the loadout's unit so the
  // stats (job / level / JP) are for the CURRENTLY-VIEWED unit, not
  // blindly roster[0]. (Pre-display-order fix this was hardcoded to
  // units[0] which always showed Ramza's stats.)
  const roster=(j.screen&&j.screen.roster&&j.screen.roster.units)||[];
  const viewedName=(l&&l.unitName)||null;
  const u=(viewedName&&roster.find(x=>x.name===viewedName))||roster[0]||{};
  const headerName=viewedName||u.name;
  if(headerName){
    const parts=[headerName];
    if(u.job)parts.push(u.job);
    if(u.level)parts.push('Lv '+u.level);
    // Next: N — cheapest unlearned action ability cost in current primary skillset.
    // Matches the game's in-game info bar between Lv and JP.
    if(j.screen&&j.screen.nextJp!==undefined&&j.screen.nextJp!==null)parts.push('Next '+j.screen.nextJp);
    if(u.jp!==undefined)parts.push('JP '+u.jp);
    console.log('  '+parts.join('  ')+':');
  }
  const rows=Math.max(eqRows.length,abRows.length);
  // Compute left-column width from actual content so values never smash the right column.
  const eqLabelW=6;  // 'R Hand' 'Shield' 'Helm' 'Chest' 'Access'
  const abLabelW=10; // 'Secondary' is longest
  const eqValW=Math.max(10,...eqRows.filter(r=>r[1]).map(r=>r[1].length));
  const leftColW=eqLabelW+2+eqValW+4; // label + ': ' + value + 4 spaces gutter
  const abValW=Math.max(10,...abRows.filter(r=>r[1]).map(r=>r[1].length));
  const midColW=abLabelW+2+abValW+4;
  const fmtLeft=(lbl,val)=>{
    if(!val)return ''.padEnd(leftColW);
    return ((lbl+':').padEnd(eqLabelW+2)+val).padEnd(leftColW);
  };
  const fmtMid=(lbl,val)=>{
    if(!val)return ''.padEnd(midColW);
    return ((lbl+':').padEnd(abLabelW+2)+val).padEnd(midColW);
  };

  // Detail panel rendered as the third column. Wraps at ~50 chars so long
  // descriptions don't run off the terminal. Keep first-line flush with the
  // header so the detail lines up with the other columns.
  const det=j.screen&&j.screen.uiDetail;
  const detailLines=[];
  if(det){
    detailLines.push(det.name+(det.type?' ('+det.type+')':''));
    if(det.job)detailLines.push('  from '+det.job);
    const stats=[];
    if(det.wp)stats.push('WP '+det.wp);
    if(det.wev)stats.push('Ev '+det.wev+'%');
    if(det.range)stats.push('Range '+det.range);
    if(det.pev)stats.push('P-Ev '+det.pev+'%');
    if(det.mev)stats.push('M-Ev '+det.mev+'%');
    if(det.hpBonus)stats.push('+HP '+det.hpBonus);
    if(det.mpBonus)stats.push('+MP '+det.mpBonus);
    if(det.element)stats.push('['+det.element+']');
    if(stats.length)detailLines.push('  '+stats.join('  '));
    const wrap=(s,w)=>{const out=[];let line='';for(const word of s.split(/\s+/)){if(line.length+word.length+1>w){out.push(line.trimEnd());line=word+' ';}else line+=word+' ';}if(line)out.push(line.trimEnd());return out;};
    // Extended ItemInfo: attribute bonuses / equipment effects / attack
    // effects / weapon-type flags. Populated for top hero items; null
    // elsewhere. See ItemData.cs (TODO §0 2026-04-14).
    if(det.attributeBonuses)detailLines.push('  Bonuses: '+det.attributeBonuses);
    if(det.equipmentEffects)for(const w of wrap('Effects: '+det.equipmentEffects,40))detailLines.push('  '+w);
    if(det.attackEffects)for(const w of wrap('On hit: '+det.attackEffects,40))detailLines.push('  '+w);
    const flags=[];
    if(det.canDualWield)flags.push('Dual-Wield');
    if(det.canWieldTwoHanded)flags.push('Two-Hand');
    if(flags.length)detailLines.push('  ['+flags.join(' / ')+']');
    if(det.description)for(const w of wrap(det.description,40))detailLines.push('  '+w);
    if(det.usageCondition){
      detailLines.push('  Usage: ');
      for(const w of wrap(det.usageCondition,38))detailLines.push('    '+w);
    }
  }

  // Cursor row/col (only present on EquipmentAndAbilities). Render a
  // 'cursor -->' marker in one of two gutters so it visually points at the
  // correct column: left gutter when col=0 (Equipment), middle gutter when
  // col=1 (Abilities). Both gutters reserve space even when empty so column
  // layout stays constant.
  const cRow=j.screen.cursorRow;
  const cCol=j.screen.cursorCol;
  const gutter='cursor --> ';
  const blankGutter=' '.repeat(gutter.length);
  const hasCursor=typeof cRow==='number'&&typeof cCol==='number';
  // Header: show both gutters blank.
  console.log('    '+blankGutter+'Equipment'.padEnd(leftColW)+blankGutter+'Abilities'.padEnd(midColW)+(detailLines.length?'Detail':''));
  const printRows=Math.max(rows,detailLines.length);
  for(let i=0;i<printRows;i++){
    const left=eqRows[i]?fmtLeft(eqRows[i][0],eqRows[i][1]):''.padEnd(leftColW);
    const mid=abRows[i]?fmtMid(abRows[i][0],abRows[i][1]):''.padEnd(midColW);
    const detail=detailLines[i]||'';
    // Mark appears in the gutter that immediately precedes the cursor's column.
    const markLeft=(hasCursor&&i===cRow&&cCol===0)?gutter:blankGutter;
    const markMid=(hasCursor&&i===cRow&&cCol===1)?gutter:blankGutter;
    const line='    '+markLeft+left+markMid+mid+detail;
    console.log(line.trimEnd());
  }
}catch(e){}
" 2>/dev/null
    fi

    # Available abilities for picker screens (Secondary/Reaction/Support/Movement).
    # Picker cursor row is not yet tracked — listing shows all options + the
    # currently-equipped flag. ui=<equipped name> for now.
    if { [ "$SCR" = "SecondaryAbilities" ] \
         || [ "$SCR" = "ReactionAbilities" ] \
         || [ "$SCR" = "SupportAbilities" ] \
         || [ "$SCR" = "MovementAbilities" ]; } \
       && [ -f "$B/response.json" ]; then
      local vflag="false"; $verbose && vflag="true"
      cat "$B/response.json" | node -e "
try{
  const j=JSON.parse(require('fs').readFileSync(0,'utf8'));
  const list=j.screen&&j.screen.availableAbilities;
  if(!list||!list.length)process.exit(0);
  const verbose=$vflag;
  const label={'SecondaryAbilities':'skillsets','ReactionAbilities':'reactions','SupportAbilities':'supports','MovementAbilities':'movement'}[j.screen.name]||'options';
  console.log('  Available '+label+' ('+list.length+'):');
  // Wrap helper — used only in verbose to keep descriptions from running off.
  const wrap=(s,w)=>{const out=[];let line='';for(const word of s.split(/\\s+/)){if(line.length+word.length+1>w){out.push(line.trimEnd());line=word+' ';}else line+=word+' ';}if(line)out.push(line.trimEnd());return out;};
  list.forEach(a=>{
    const tag=a.isEquipped?'  [equipped]':'';
    // Compact: name + [equipped] only.
    // Verbose: name + [equipped] + job + wrapped description lines.
    if(verbose){
      const jobTag=a.job?'  ('+a.job+')':'';
      console.log('    - '+a.name+jobTag+tag);
      if(a.description){
        for(const w of wrap(a.description,72))console.log('        '+w);
      }
    } else {
      console.log('    - '+a.name+tag);
    }
  });
}catch(e){}
" 2>/dev/null
    fi

    # PartyMenuUnits roster:
    #   compact: render the visual 5-col grid the game shows, with a
    #            [cursor] bracket around the hovered unit. Display order
    #            is driven by roster byte +0x122 (Time Recruited sort).
    #   -v:      dump the raw roster JSON (efficient payload for tools).
    if [ "$SCR" = "PartyMenuUnits" ] && [ -f "$B/response.json" ]; then
      local vflag="false"; $verbose && vflag="true"
      cat "$B/response.json" | node -e "
try{
  const j=JSON.parse(require('fs').readFileSync(0,'utf8'));
  const r=j.screen&&j.screen.roster;
  if(!r||!r.units||!r.units.length)process.exit(0);
  const verbose=$vflag;
  if(verbose){
    // Raw JSON dump so tools / advanced users get every field. We drop
    // the full DetectedScreen wrapper — the roster is what this command
    // is for — and pretty-print at 2-space indent.
    console.log(JSON.stringify(r, null, 2));
    process.exit(0);
  }
  // Compact: 5-col grid matching the game's layout.
  const cols = r.gridCols || 5;
  const rows = r.gridRows || Math.ceil(r.units.length / cols);
  const cr = (typeof r.cursorRow === 'number') ? r.cursorRow : -1;
  const cc = (typeof r.cursorCol === 'number') ? r.cursorCol : -1;
  // units sorted by displayOrder by the backend. Build a row-major map.
  const byIdx = new Array(rows * cols).fill(null);
  r.units.forEach(u => {
    const i = (typeof u.displayOrder === 'number') ? u.displayOrder : -1;
    if (i >= 0 && i < byIdx.length) byIdx[i] = u;
  });
  console.log(' '+r.count+'/'+r.max+' units — cursor on '+(r.hoveredName||'?')+' (r'+cr+' c'+cc+'). Use \`screen -v\` for JSON.');
  // Column headers — indented to line up with the row labels below.
  const colLabels = [];
  for (let c = 0; c < cols; c++) colLabels.push(('c'+c).padEnd(15));
  // Width of the row-label gutter so headers + cells align in columns.
  const gutterPad = '              '; // 14 spaces, matches 'r0 cursor->' width
  console.log('  '+gutterPad+colLabels.join(''));
  for (let rr = 0; rr < rows; rr++) {
    const cells = [];
    for (let c = 0; c < cols; c++) {
      const idx = rr * cols + c;
      const u = byIdx[idx];
      if (!u) { cells.push(''.padEnd(15)); continue; }
      const nm = (u.name || '?').slice(0, 12);
      const isCursor = (rr === cr && c === cc);
      cells.push(isCursor ? ('['+nm+']').padEnd(15) : (nm).padEnd(15));
    }
    // Row label uses an EquipmentAndAbilities-style 'cursor->' gutter on
    // the row that holds the highlighted cell.
    const rowLabel = 'r'+rr;
    const gutter = (rr === cr) ? ' cursor->  ' : '           ';
    console.log('  '+rowLabel+gutter+cells.join(''));
  }
}catch(e){ console.error('[screen] party render failed: '+e.message); }
" 2>/dev/null
    fi

    # PartyMenuInventory:
    #   compact: just the summary line already printed above (count +
    #            total owned). We don't dump every item by default — 184
    #            entries would blow past the "compact doesn't burn context"
    #            budget. Claude consults screen -v when they actually
    #            need to look up a specific item.
    #   -v:      full inventory[] array grouped by item type, one line per
    #            entry with count. Grouping mirrors the shop / equipment
    #            picker tab layout so Claude can quickly scan "what
    #            consumables do I have" vs "what swords".
    # Verbose inventory dump — render whenever the backend populated
    # screen.inventory on PartyMenuUnits tabs, OutfitterSell, or OutfitterFitting.
    # State-machine drift can misname the tab so we check payload presence,
    # not screen name. On OutfitterSell each item line adds `sell=N gil`
    # and the footer adds a total-gil-if-sold summary.
    if [[ "$SCR" == "PartyMenuUnits" || "$SCR" == PartyMenuInventory* || "$SCR" == PartyMenuChronicle* || "$SCR" == PartyMenuOptions* || "$SCR" == "OutfitterSell" || "$SCR" == "OutfitterFitting" ]] && [ -f "$B/response.json" ]; then
      if $verbose; then
        cat "$B/response.json" | node -e "
try{
  const j=JSON.parse(require('fs').readFileSync(0,'utf8'));
  const inv=j.screen&&j.screen.inventory;
  if(!inv||!inv.length){console.log('(inventory empty)');process.exit(0);}
  const isSell=(j.screen.name==='OutfitterSell');
  // Group by type. Unknown types land in '(unmapped)' so Claude sees
  // items we haven't named yet (e.g. IC-exclusive items above id 315).
  const groups={};
  for(const e of inv){
    const t=e.type||'(unmapped)';
    (groups[t]=groups[t]||[]).push(e);
  }
  const order=['knife','ninjablade','sword','knightsword','katana','axe','rod','staff','flail','gun','crossbow','bow','instrument','book','polearm','pole','bag','cloth','throwing','bomb','fellsword','shield','helmet','hat','hairadornment','armor','clothing','robe','shoes','armguard','ring','armlet','cloak','perfume','liprouge','chemistitem','(unmapped)'];
  const seen=new Set();
  const render=(type)=>{
    const es=groups[type];
    if(!es)return;
    seen.add(type);
    const total=es.reduce((a,e)=>a+(e.count||0),0);
    const groupGil=es.reduce((a,e)=>a+((e.sellPrice||0)*(e.count||0)),0);
    console.log('');
    if(isSell){
      console.log(type+' ('+es.length+' unique, '+total+' units, ~'+groupGil.toLocaleString()+' gil):');
    }else{
      console.log(type+' ('+es.length+' unique, '+total+' total):');
    }
    for(const e of es){
      const nm=e.name||('item_'+e.id);
      const base='  '+String(e.count).padStart(3)+'  '+nm;
      const idTag='  [id='+e.id+']';
      if(isSell){
        // sell=N (verified ground-truth) vs sell~N (buy/2 estimate) vs sell=?
        // The operator tells Claude how much to trust the number.
        let sell='sell=?';
        if(e.sellPrice!=null){
          const op=e.sellPriceVerified?'=':'~';
          sell='sell'+op+e.sellPrice+' gil';
        }
        console.log(base+idTag+'  '+sell);
      }else{
        console.log(base+idTag);
      }
    }
  };
  order.forEach(render);
  // Any groups we didn't render in the ordered list (defensive).
  Object.keys(groups).forEach(t=>{ if(!seen.has(t)) render(t); });
  const total=inv.reduce((a,e)=>a+(e.count||0),0);
  console.log('');
  if(isSell){
    const totalGil=inv.reduce((a,e)=>a+((e.sellPrice||0)*(e.count||0)),0);
    console.log('Sellable: '+inv.length+' unique items, '+total+' units, ~'+totalGil.toLocaleString()+' gil if all sold');
  }else{
    console.log('Total: '+inv.length+' unique items, '+total+' owned');
  }
}catch(e){ console.error('[screen] inventory render failed: '+e.message); }
" 2>/dev/null
      fi
    fi
  fi
}

# logs: Tail the live mod log (truncated fresh on each game launch).
# Usage: logs           — last 40 lines
#        logs 100       — last 100 lines
#        logs grep foo  — grep all logs for 'foo'
logs() {
  local live="$B/live_log.txt"
  if [ ! -f "$live" ]; then
    echo "[logs] No live_log.txt yet — start the game via boot/restart first"
    return 1
  fi
  if [ "$1" = "grep" ]; then
    shift
    grep -E "$@" "$live"
  else
    tail -n "${1:-40}" "$live"
  fi
}

# session_tail [N]: Show the last N rows of the current session's command
# log — the JSONL trail written by SessionCommandLog. Useful for post-hoc
# "which command drifted?" review.
#
# Columns: time · action · source→target · status · latency · error
#
# Options:
#   session_tail             → last 20 rows
#   session_tail 50          → last 50 rows
#   session_tail failed      → only failed/partial rows
#   session_tail slow [ms]   → rows with latencyMs >= ms (default 2000)
session_tail() {
  # Find the most recent session_*.jsonl file (one per mod startup).
  local latest=$(ls -t "$B"/session_*.jsonl 2>/dev/null | head -1)
  if [ -z "$latest" ]; then
    echo "[session_tail] no session log yet — start the game first"
    return 1
  fi
  local mode="tail"
  local threshold=2000
  local limit=20
  case "$1" in
    failed)       mode="failed" ;;
    slow)         mode="slow"; [ -n "$2" ] && threshold="$2" ;;
    ''|[0-9]*)    [ -n "$1" ] && limit="$1" ;;
    *)            echo "[session_tail] usage: session_tail [N | failed | slow [ms]]"; return 1 ;;
  esac
  node -e "
const fs=require('fs');
const lines=fs.readFileSync(process.argv[1],'utf8').split('\\n').filter(Boolean);
const mode='$mode'; const thr=$threshold; const lim=$limit;
const fmt=r=>{
  const t=(r.timestamp||'').slice(11,19);
  const src=r.sourceScreen||'?'; const tgt=r.targetScreen||'?';
  const lat=(r.latencyMs!=null?r.latencyMs+'ms':'?');
  const err=r.error?' err='+r.error:'';
  return t+' '+r.action+' '+src+'→'+tgt+' '+r.status+' '+lat+err;
};
const all=lines.map(l=>{try{return JSON.parse(l);}catch(e){return null;}}).filter(Boolean);
let filtered=all;
if(mode==='failed') filtered=all.filter(r=>r.status!=='completed');
else if(mode==='slow') filtered=all.filter(r=>r.latencyMs>=thr);
const slice=mode==='tail'?filtered.slice(-lim):filtered;
slice.forEach(r=>console.log(fmt(r)));
console.log('— '+slice.length+'/'+all.length+' rows from '+process.argv[1].split(/[\\\\/]/).pop());
" "$latest"
}

# =============================================================================
# DEPRECATED COMMANDS — use screen instead
# =============================================================================
# scan_units, scan_move, state all replaced by the unified screen command.
# Kept as thin wrappers that print a reminder, in case muscle memory kicks in.
scan_units() { echo "[USE screen] scan_units is deprecated. Use: screen"; screen; }
state() { echo "[USE screen] state is deprecated. Use: screen"; screen; }
auto_move() { echo "[DISABLED] Use battle_move, battle_attack, battle_ability, battle_wait individually."; return 1; }

scan_move() { echo "[USE screen] scan_move is deprecated. Use: screen"; screen; }
scan_move_full() { echo "[USE screen -v] scan_move_full is deprecated. Use: screen -v"; screen -v; }

# _fmt_action: Shared formatter for battle action responses. Parses JSON, shows compact result.
_fmt_action() {
  local raw="$1"
  echo "$raw" | node -e "
const j=JSON.parse(require('fs').readFileSync(0,'utf8'));
const s=j.screen||{};
const ui=s.ui?' ui='+s.ui:'';
if(j.status!=='completed'){
  console.log('['+s.name+']'+ui+' '+j.status+': '+(j.error||'unknown error'));
}else{
  let msg='['+s.name+']'+ui;
  if(j.error)msg+=' '+j.error;
  else if(j.info)msg+=' '+j.info;
  if(j.postAction){
    const p=j.postAction;
    msg+=' → ('+p.x+','+p.y+') HP='+p.hp+'/'+p.maxHp;
  }
  console.log(msg);
}
" 2>/dev/null
}

# battle_move: Enter Move mode, navigate cursor to grid (x,y), confirm with F.
# Usage: battle_move <x> <y>
battle_move() { _fmt_action "$(fft_full "{\"id\":\"$(id)\",\"action\":\"battle_move\",\"locationId\":$1,\"unitIndex\":$2}")"; }

# battle_attack: Attack a target tile. Handles menu nav, rotation detection, targeting.
# Usage: battle_attack <x> <y>
battle_attack() { _fmt_action "$(fft_full "{\"id\":\"$(id)\",\"action\":\"battle_attack\",\"locationId\":$1,\"unitIndex\":$2}")"; }

# battle_ability: Use a specific ability. Self-targeting abilities need no coordinates.
# Usage: battle_ability "Shout"                    (self-target)
#        battle_ability "Throw Stone" 4 8           (targeted at tile x=4, y=8)
#        battle_ability "Cure" 10 9                 (heal ally at 10,9)
battle_ability() {
  local name="$1"
  if [ -n "$2" ] && [ -n "$3" ]; then
    _fmt_action "$(fft_full "{\"id\":\"$(id)\",\"action\":\"battle_ability\",\"description\":\"$name\",\"locationId\":$2,\"unitIndex\":$3}" 15)"
  else
    _fmt_action "$(fft_full "{\"id\":\"$(id)\",\"action\":\"battle_ability\",\"description\":\"$name\"}" 15)"
  fi
}

# get_arrows: Compute arrow keys to move Ramza next to nearest enemy. Shows the sequence.
# Usage: get_arrows          (just show arrows)
#        get_arrows execute   (show AND execute the move + confirm)
get_arrows() { fft_full "{\"id\":\"$(id)\",\"action\":\"get_arrows\",\"to\":\"${1:-plan}\"}"; }

# strict: Toggle strict mode. When ON, game actions must go through validPaths.
# Usage: strict 1   (enable)    strict 0   (disable)
strict() { fft "{\"id\":\"$(id)\",\"action\":\"set_strict\",\"locationId\":${1:-1}}"; }

# set_map: Load a MAP JSON file for exact BFS terrain data.
# Usage: set_map 74   (loads MAP074.json)
# Call before or during battle. Maps must be in claude_bridge/maps/MAP###.json
set_map() { fft "{\"id\":\"$(id)\",\"action\":\"set_map\",\"locationId\":$1}"; }

# mark_blocked: Mark a grid tile as impassable (learned from failed move attempts)
# Usage: mark_blocked <gridX> <gridY>
mark_blocked() { fft "{\"id\":\"$(id)\",\"action\":\"mark_blocked\",\"locationId\":$1,\"unitIndex\":$2}"; }

# module_snap: Snapshot FFT's main module writable regions (~0x140000000 range).
# Much cleaner signal for screen-state diffing than heap_snap — excludes UE4 heap
# animation/rendering noise. Use when hunting for game-state discriminators.
# Usage: module_snap <label>
module_snap() { fft "{\"id\":\"$(id)\",\"action\":\"snapshot\",\"searchLabel\":\"$1\"}"; }

# heap_snap: Take a heap memory snapshot (for diffing Move vs non-Move mode)
# Usage: heap_snap <label>   (e.g. "before_move", "during_move")
heap_snap() { fft "{\"id\":\"$(id)\",\"action\":\"heap_snapshot\",\"searchLabel\":\"$1\"}"; }

# heap_diff: Diff two snapshots. Results written to bridge/diff_<label>.txt
# Usage: heap_diff <from> <to> <output>
heap_diff() { fft "{\"id\":\"$(id)\",\"action\":\"diff\",\"fromLabel\":\"$1\",\"toLabel\":\"$2\",\"searchLabel\":\"$3\"}"; }

# wv: Write a byte value to a memory address. Use for testing memory flags.
# Usage: wv <addr> <value>   (value is decimal, 0-255)
# Example: wv "0x140D3A400" 1   → write 1 to cursor cycle flag
wv() {
  fft "{\"id\":\"$(id)\",\"action\":\"write_byte\",\"address\":\"$1\",\"searchValue\":$2}"
}
