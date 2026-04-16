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

# Block chained commands: only one fft/fft_full call allowed per session.
_FFT_DONE=0
_fft_guard() {
  if [ "$_FFT_DONE" -eq 1 ]; then
    echo "[NO] Only call one command at a time. Do not chain commands."
    kill -9 $$ 2>/dev/null
    exit 1
  fi
  _FFT_DONE=1
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
  # Render compact summary via the shared helper — see _fmt_screen_compact above.
  # Single source of truth; screen() uses the same function.
  _fmt_screen_compact "$B/response.json"

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
  _C_SCR='\033[1;36m'      # bright cyan  — screen name [PartyMenu]
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

# _is_party_tree_screen <screenName> → 0 if on a PartyMenu-family screen.
# PartyMenu tree screens don't benefit from loc=/objective=/gil= in the
# compact line — the player isn't navigating the world there, so those
# fields are pure carry-over noise that pushes meaningful signals
# (viewedUnit, equippedItem, pickerTab, ui) further away from Claude's
# eye. Per TODO §"What Goes In Compact vs Verbose vs Nowhere": if a
# field doesn't change a decision on *this* screen, drop it.
_is_party_tree_screen() {
  case "$1" in
    PartyMenu|PartyMenuInventory|PartyMenuChronicle|PartyMenuOptions|\
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

# _fmt_screen_compact: Render the one-line screen summary from a response file.
# Single source of truth for the compact render — called by both fft() and screen()
# so every entry point renders identically.
# Arg: $1 = path to response.json (so we can use `node` for space-preserving field reads)
# Consults: $SCR (screen name, already parsed by caller).
_fmt_screen_compact() {
  local RESP="$1"
  local R=$(cat "$RESP" | tr -d '\r\n ')

  # Simple regex extracts (no spaces to worry about).
  local SCR=$(echo "$R" | grep -o '"name":"[^"]*"' | head -1 | cut -d'"' -f4)
  local LOC=$(echo "$R" | grep -o '"location":[0-9]*' | head -1 | cut -d: -f2)
  local LOCNAME=$(echo "$R" | grep -o '"locationName":"[^"]*"' | head -1 | cut -d'"' -f4)
  local HOV=$(echo "$R" | grep -o '"hover":[0-9]*' | head -1 | cut -d: -f2)
  local ST=$(echo "$R" | grep -o '"status":"[^"]*"' | head -1 | cut -d'"' -f4)
  local OBJ=$(echo "$R" | grep -o '"storyObjective":[0-9]*' | head -1 | cut -d: -f2)
  local OBJNAME=$(echo "$R" | grep -o '"storyObjectiveName":"[^"]*"' | head -1 | cut -d'"' -f4)
  local ANAME=$(echo "$R" | grep -o '"activeUnitName":"[^"]*"' | head -1 | cut -d'"' -f4)
  local AJOB=$(echo "$R" | grep -o '"activeUnitJob":"[^"]*"' | head -1 | cut -d'"' -f4)
  local GIL=$(echo "$R" | grep -o '"gil":[0-9]*' | head -1 | cut -d: -f2)
  local SLCI=$(echo "$R" | grep -o '"shopListCursorIndex":[0-9]*' | head -1 | cut -d: -f2)

  # Space-bearing string fields need the JSON parser so "Equipment & Abilities"
  # / "Magick Defense Boost" / "All Weapons & Shields" survive intact.
  local UI=$(cat "$RESP" | node -e "let d='';process.stdin.on('data',c=>d+=c);process.stdin.on('end',()=>{try{const j=JSON.parse(d);process.stdout.write(j.screen?.ui||'');}catch(e){}});" 2>/dev/null)
  # viewedUnit + job: combine into "Name(Job)" when both are available.
  local VUNIT=$(cat "$RESP" | node -e "let d='';process.stdin.on('data',c=>d+=c);process.stdin.on('end',()=>{try{const j=JSON.parse(d);const vu=j.screen?.viewedUnit||'';if(!vu){process.stdout.write('');return;}const r=(j.screen?.roster?.units||[]);const u=r.find(x=>x.name===vu)||{};const job=u.job||'';process.stdout.write(job?vu+'('+job+')':vu);}catch(e){}});" 2>/dev/null)
  local EQITEM=$(cat "$RESP" | node -e "let d='';process.stdin.on('data',c=>d+=c);process.stdin.on('end',()=>{try{const j=JSON.parse(d);process.stdout.write(j.screen?.equippedItem||'');}catch(e){}});" 2>/dev/null)
  local PTAB=$(cat "$RESP" | node -e "let d='';process.stdin.on('data',c=>d+=c);process.stdin.on('end',()=>{try{const j=JSON.parse(d);process.stdout.write(j.screen?.pickerTab||'');}catch(e){}});" 2>/dev/null)

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

  if [[ "$SCR" == Battle_* ]]; then
    # Battle screens: active unit banner, then ui=.
    if [ -n "$ANAME" ] && [ -n "$AJOB" ]; then
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
  fi

  [ -n "$SLCI" ] && LINE="$LINE row=$(_col "$_C_MARK" "$SLCI")"

  # World-side context — appended to same line. Suppressed on
  # PartyMenu-tree screens where they don't change per-action decisions.
  if ! _is_party_tree_screen "$SCR"; then
    [ -n "$LOCNAME" ] && LINE="$LINE $(_col "$_C_LOC" "loc=$LOCNAME")"
    [ "$SCR" = "TravelList" ] && [ -n "$HOV" ] && LINE="$LINE hover=$(_col "$_C_MARK" "$HOV")"
    [ -n "$OBJNAME" ] && LINE="$LINE $(_col "$_C_LOC" "obj=$OBJNAME")"
    [ -n "$GIL" ] && LINE="$LINE gil=$(_fmt_gil "$GIL")"
  fi

  printf '%b\n' "$LINE"

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
    save_and_travel <id>                  Save then travel to location
    enter_shop                            Enter the Outfitter at current location"
      ;;
    PartyMenu)
      helpers="    open_eqa [unit]                       Jump to Equipment & Abilities
    open_job_selection [unit]             Jump to Job Selection
    open_character_status [unit]          Jump to Character Status
    party_summary                         Show all units at a glance
    check_unit <name>                     Quick stat dump for one unit"
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
    swap_unit <name>                     Cycle Q/E to named unit"
      ;;
    CharacterStatus)
      helpers="    open_eqa [unit]                       Jump to Equipment & Abilities
    open_job_selection [unit]             Jump to Job Selection
    dismiss_unit                          Hold B to open dismiss confirmation
    swap_unit <name>                      Cycle Q/E to named unit
    check_unit <name>                     Quick stat dump for one unit"
      ;;
    JobSelection)
      helpers="    change_job_to <class>                 Change to named job class
    swap_unit <name>                      Cycle Q/E to named unit"
      ;;
    GameOver)
      helpers="    load                                  Load most recent save"
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
id() { echo "c$(date +%s%N | tail -c 8)$RANDOM"; }

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

# =============================================================================
# HIGH-LEVEL NAVIGATION ACTIONS
# =============================================================================

# execute_action: Execute a validPath by name. Shows screen + available next paths.
# This is the PRIMARY way to interact with the game. Every response tells you
# what actions are available — pick one and call execute_action again.
# Usage: execute_action Flee, execute_action Move, execute_action Wait
execute_action() {
  _check_total || return 1
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
battle_wait() { fft "{\"id\":\"$(id)\",\"action\":\"battle_wait\"}" 60; }

# battle_flee: Quit battle and return to world map (Tab → Down x4 → Enter → Enter).
battle_flee() { fft "{\"id\":\"$(id)\",\"action\":\"battle_flee\"}"; }

# world_travel_to: Navigate to a world map location by ID. Opens travel list, selects, confirms.
# Usage: world_travel_to 26   (travel to Siedge Weald)
world_travel_to() { fft "{\"id\":\"$(id)\",\"action\":\"world_travel_to\",\"locationId\":$1}"; }

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
# escape to WorldMap → PartyMenu → cursor to unit → CharacterStatus → target.

# _nav_to_party_unit <unit_name>
# Internal: navigate to PartyMenu with cursor on the named unit.
# Returns 0 on success, 1 on failure. Leaves state on PartyMenu.
_nav_to_party_unit() {
  local target="$1"

  # Compound navigators need multiple fft calls. Reset the guard between steps.
  # This is safe because each step waits for its response before the next fires.
  _FFT_DONE=0

  # Step 1: get to WorldMap or PartyMenu
  _current_screen >/dev/null
  local curScr=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen.name)" < "$B/response.json" 2>/dev/null)
  if [ "$curScr" != "WorldMap" ] && [ "$curScr" != "PartyMenu" ]; then
    fft "{\"id\":\"$(id)\",\"action\":\"execute_action\",\"to\":\"ReturnToWorldMap\"}" >/dev/null
    _FFT_DONE=0
    _current_screen >/dev/null
    curScr=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen.name)" < "$B/response.json" 2>/dev/null)
  fi

  # Step 2: open PartyMenu if on WorldMap
  if [ "$curScr" = "WorldMap" ]; then
    fft "{\"id\":\"$(id)\",\"action\":\"execute_action\",\"to\":\"PartyMenu\"}" >/dev/null
    _FFT_DONE=0
    _current_screen >/dev/null
    curScr=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen.name)" < "$B/response.json" 2>/dev/null)
  fi

  if [ "$curScr" != "PartyMenu" ]; then
    echo "[_nav_to_party_unit] ERROR: could not reach PartyMenu (on $curScr)"
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
  # Cursor may NOT be at (0,0) — PartyMenu preserves the last position.
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
    _FFT_DONE=0
  fi
  return 0
}

# open_character_status [unit_name]
# Navigate to CharacterStatus for the named unit (default: Ramza).
open_character_status() {
  local unit="${*:-Ramza}"
  _nav_to_party_unit "$unit" || return 1
  fft "{\"id\":\"$(id)\",\"keys\":[{\"vk\":13,\"name\":\"Enter\"}],\"delayBetweenMs\":300}" >/dev/null
  _FFT_DONE=0
  screen
}

# open_eqa [unit_name]
# Navigate from anywhere to EquipmentAndAbilities for the named unit.
open_eqa() {
  local unit="${*:-Ramza}"
  _nav_to_party_unit "$unit" || return 1
  # Enter (SelectUnit → CharacterStatus) + Enter (Select → EquipmentAndAbilities)
  fft "{\"id\":\"$(id)\",\"keys\":[{\"vk\":13,\"name\":\"Enter\"},{\"vk\":13,\"name\":\"Enter\"}],\"delayBetweenMs\":350}" >/dev/null
  _FFT_DONE=0
  screen
}

# open_job_selection [unit_name]
# Navigate from anywhere to JobSelection for the named unit.
open_job_selection() {
  local unit="${*:-Ramza}"
  _nav_to_party_unit "$unit" || return 1
  # Enter (SelectUnit) + Down (sidebar to Job) + Enter (Select → JobSelection)
  fft "{\"id\":\"$(id)\",\"keys\":[{\"vk\":13,\"name\":\"Enter\"},{\"vk\":40,\"name\":\"Down\"},{\"vk\":13,\"name\":\"Enter\"}],\"delayBetweenMs\":350}" >/dev/null
  _FFT_DONE=0
  screen
}

# =============================================================================
# Quick helpers (aliases + one-liners)
# =============================================================================

# party_summary: Formatted one-line-per-unit roster overview.
# Works from any screen — reads the last response's roster data, or
# navigates to PartyMenu if roster isn't available.
party_summary() {
  _FFT_DONE=0
  # Get a fresh PartyMenu read to ensure roster is populated
  _current_screen >/dev/null
  local curScr=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen?.name||'')" < "$B/response.json" 2>/dev/null)
  local hasRoster=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen?.roster?.units?.length>0?'yes':'no')" < "$B/response.json" 2>/dev/null)

  if [ "$hasRoster" != "yes" ]; then
    # Navigate to PartyMenu to get roster
    if [ "$curScr" != "WorldMap" ] && [ "$curScr" != "PartyMenu" ]; then
      fft "{\"id\":\"$(id)\",\"action\":\"execute_action\",\"to\":\"ReturnToWorldMap\"}" >/dev/null
      _FFT_DONE=0
    fi
    fft "{\"id\":\"$(id)\",\"action\":\"execute_action\",\"to\":\"PartyMenu\"}" >/dev/null
    _FFT_DONE=0
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
  _FFT_DONE=0
  _current_screen >/dev/null
  local hasRoster=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen?.roster?.units?.length>0?'yes':'no')" < "$B/response.json" 2>/dev/null)
  if [ "$hasRoster" != "yes" ]; then
    fft "{\"id\":\"$(id)\",\"action\":\"execute_action\",\"to\":\"PartyMenu\"}" >/dev/null
    _FFT_DONE=0
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
  local dest="$1"
  if [ -z "$dest" ]; then echo "[save_and_travel] usage: save_and_travel <location_id>"; return 1; fi
  _FFT_DONE=0
  _current_screen >/dev/null
  local curScr=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen?.name||'')" < "$B/response.json" 2>/dev/null)
  if [ "$curScr" != "WorldMap" ]; then
    echo "[save_and_travel] ERROR: must be on WorldMap (current: $curScr)"
    return 1
  fi
  save
  _FFT_DONE=0
  world_travel_to "$dest"
}

# enter_shop: From WorldMap at a settlement (IDs 0-14), navigate into the Outfitter.
# Validates you're at a settlement before pressing anything.
enter_shop() {
  _FFT_DONE=0
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
  _FFT_DONE=0
  execute_action EnterShop >/dev/null
  _FFT_DONE=0
  screen
}

# swap_unit <name>: Cycle Q/E to the named unit on any unit-scoped screen
# (CharacterStatus, EquipmentAndAbilities, JobSelection). Reads the roster
# to compute how many E presses are needed.
swap_unit() {
  local target="$*"
  if [ -z "$target" ]; then echo "[swap_unit] usage: swap_unit <name>"; return 1; fi
  _FFT_DONE=0
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
    _FFT_DONE=0
  fi
  screen
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
  local slotType="$1"
  local target="$2"
  if [ -z "$slotType" ] || [ -z "$target" ]; then
    echo "[_change_ability] usage: _change_ability <slotType> <targetName>"
    return 1
  fi

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

  # Commit the change: Enter → JobActionMenu → CursorRight → Enter →
  # JobChangeConfirmation → CursorRight → Enter → "Job changed!" dialog →
  # Enter → CharacterStatus (or EquipmentAndAbilities if gear dropped).
  local -a commit=("Enter" "CursorRight" "Enter" "CursorRight" "Enter" "Enter")
  _fire_keys "${commit[@]}" >/dev/null 2>&1

  _current_screen >/dev/null
  local newScr=$(node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).screen.name)" < "$B/response.json" 2>/dev/null)

  if [ "$newScr" = "EquipmentAndAbilities" ] || [ "$newScr" = "CharacterStatus" ]; then
    echo "[change_job_to] -> $target (landed on $newScr)"
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

# remove_equipment: Unequip whatever is in the currently-hovered EqA slot.
# One atomic C# action: opens the picker, toggles, reads the mirror to learn
# which row we were on, leaves the slot empty, closes the picker. Works on
# both populated and empty slots (empty-slot case auto-equips the first
# picker item and then unequips it — net zero).
remove_equipment() { fft "{\"id\":\"$(id)\",\"action\":\"remove_equipment_at_cursor\"}"; }

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
  _FFT_DONE=0
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
  _FFT_DONE=0
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
  rm -f "$B/response.json"
  echo "{\"id\":\"$(id)\",\"keys\":[],\"delayBetweenMs\":0}" > "$B/command.json"
  local tries=0
  until [ -f "$B/response.json" ]; do
    sleep 0.02; tries=$((tries+1))
    if [ $tries -ge 250 ]; then echo "[TIMEOUT]"; return 1; fi
  done
  local R=$(cat "$B/response.json" | tr -d '\r\n ')
  local SCR=$(echo "$R" | grep -o '"name":"[^"]*"' | head -1 | cut -d'"' -f4)

  # During Battle_MyTurn: run scan_move for full tactical view
  if [[ "$SCR" == "BattleMyTurn" ]]; then
    local vflag="false"; $verbose && vflag="true"
    local raw
    raw=$(fft_full "{\"id\":\"$(id)\",\"action\":\"scan_move\",\"verbose\":$vflag}")
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
console.log('['+s.name+']'+uiTag+' '+aName+(aJob?'('+aJob+')':'')+' ('+ax+','+ay+') HP='+ahp+'/'+amhp+' MP='+amp+'/'+ammp);
console.log('');

// Abilities with target tiles (filtering/collapsing done server-side by AbilityCompactor)
if(activeU&&activeU.abilities){
  console.log('Abilities:');
  activeU.abilities.forEach(a=>{
    const tiles=(a.validTargetTiles||[]).map(t=>{
      let s='('+t.x+','+t.y+')';
      const tag=(t.occupant==='ally'||t.occupant==='self')?' ALLY':'';
      if(t.unitName)s+='<'+t.unitName+tag+'>';
      else if(t.occupant&&t.occupant!=='empty')s+='<'+t.occupant+'>';
      return s;
    });
    const mp=a.mpCost?' mp='+a.mpCost:'';
    const ct=a.castSpeed?' ct='+a.castSpeed:'';
    const el=a.element?' ['+a.element+']':'';
    const eff=a.addedEffect?' {'+a.addedEffect+'}':'';
    console.log('  '+a.name+mp+ct+el+eff+' \\u2192 '+(tiles.length?tiles.join(' '):'(no targets in range)'));
  });
  console.log('');
}

// Move tiles
const vp=j.validPaths||{};
const vmt=vp.ValidMoveTiles;
if(vmt){
  const tlist=(vmt.tiles||[]).map(t=>'('+t.x+','+t.y+(t.h!=null?' h='+t.h:'')+')');
  console.log('Move tiles: '+(tlist.length?tlist.join(' '):'(none)')+(vmt.desc?'  — '+vmt.desc:''));
}
// Attack tiles (adjacent cardinals)
const atk=vp.AttackTiles?.attackTiles||[];
if(atk.length){
  const lines=atk.map(a=>{
    const occ=a.occupant&&a.occupant!=='empty'?' '+a.occupant:'';
    const job=a.jobName?' ('+a.jobName+')':'';
    const hp=a.hp!=null?' HP='+a.hp:'';
    return a.arrow+'→('+a.x+','+a.y+')'+occ+job+hp;
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
  const cl=u.jobName?'('+u.jobName+')':'';
  const st=u.statuses?.length?' ['+u.statuses.join(',')+']':'';
  const life=u.lifeState==='dead'?' DEAD':'';
  const act=u.isActive?' *':'';
  const dist=u.distance!==undefined&&!u.isActive?' d='+u.distance:'';
  let extra='';
  if(verbose){
    extra=' PA='+u.pa+' MA='+u.ma+' Spd='+(u.speed||'?')+' CT='+(u.ct||'?')+' Br='+u.brave+' Fa='+u.faith;
    if(u.reaction)extra+=' R:'+u.reaction;
    if(u.support)extra+=' S:'+u.support;
    if(u.movement)extra+=' M:'+u.movement;
  }
  console.log('  ['+team+']'+nm+cl+' ('+u.x+','+u.y+') HP='+u.hp+'/'+u.maxHp+dist+extra+st+life+act);
});
" 2>/dev/null
  else
    # Non-battle: render via the shared helper — same compact one-liner as fft().
    _fmt_screen_compact "$B/response.json"

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

    # PartyMenu roster:
    #   compact: render the visual 5-col grid the game shows, with a
    #            [cursor] bracket around the hovered unit. Display order
    #            is driven by roster byte +0x122 (Time Recruited sort).
    #   -v:      dump the raw roster JSON (efficient payload for tools).
    if [ "$SCR" = "PartyMenu" ] && [ -f "$B/response.json" ]; then
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
    # screen.inventory on PartyMenu tabs, OutfitterSell, or OutfitterFitting.
    # State-machine drift can misname the tab so we check payload presence,
    # not screen name. On OutfitterSell each item line adds `sell=N gil`
    # and the footer adds a total-gil-if-sold summary.
    if [[ "$SCR" == PartyMenu* || "$SCR" == "OutfitterSell" || "$SCR" == "OutfitterFitting" ]] && [ -f "$B/response.json" ]; then
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

# =============================================================================
# DEPRECATED COMMANDS — use screen instead
# =============================================================================
# scan_units, scan_move, state all replaced by the unified screen command.
# Kept as thin wrappers that print a reminder, in case muscle memory kicks in.
scan_units() { echo "[USE screen] scan_units is deprecated. Use: screen"; screen; }
state() { echo "[USE screen] state is deprecated. Use: screen"; screen; }
auto_move() { echo "[DISABLED] Use battle_move, battle_attack, battle_ability, battle_wait individually."; return 1; }
_old_scan_move() {
  local verbose=false
  if [ "$1" = "-v" ]; then verbose=true; shift; fi
  local mv=${1:-0}
  local jmp=${2:-0}
  local vflag="false"; $verbose && vflag="true"
  local R=$(fft_full "{\"id\":\"$(id)\",\"action\":\"scan_move\",\"locationId\":$mv,\"unitIndex\":$jmp,\"verbose\":$vflag}")
  # Strip whitespace for reliable grep matching (JSON may be pretty-printed)
  local RR=$(echo "$R" | tr -d '\r\n ')
  local ST=$(echo "$RR" | grep -o '"status":"[^"]*"' | head -1 | cut -d'"' -f4)
  if [ "$ST" = "blocked" ]; then
    local ERR=$(echo "$RR" | grep -o '"error":"[^"]*"' | head -1 | cut -d'"' -f4)
    echo "[BLOCKED] $ERR"
    return 1
  fi
  if [ "$ST" != "completed" ]; then
    local ERR=$(echo "$RR" | grep -o '"error":"[^"]*"' | head -1 | cut -d'"' -f4)
    echo "[FAILED] $ERR"
    return 1
  fi
  # Screen line — show active unit's name + job after the screen state
  local SCR=$(echo "$RR" | grep -o '"name":"[^"]*"' | head -1 | cut -d'"' -f4)
  local ANAME=$(echo "$RR" | grep -o '"activeUnitName":"[^"]*"' | head -1 | cut -d'"' -f4)
  local AJOB=$(echo "$RR" | grep -o '"activeUnitJob":"[^"]*"' | head -1 | cut -d'"' -f4)
  local BWON=$(echo "$RR" | grep -o '"battleWon":true')
  local ACTIVE_STR=""
  if [ -n "$ANAME" ] && [ -n "$AJOB" ]; then
    ACTIVE_STR="${ANAME} (${AJOB})"
  elif [ -n "$AJOB" ]; then
    ACTIVE_STR="(${AJOB})"
  fi
  echo "[$SCR]${ACTIVE_STR:+ ${ACTIVE_STR}} ${BWON:+*** BATTLE WON ***}"
  # Active unit
  local AX=$(echo "$RR" | grep -o '"activeUnit":{[^}]*}' | grep -o '"x":[0-9]*' | head -1 | cut -d: -f2)
  local AY=$(echo "$RR" | grep -o '"activeUnit":{[^}]*}' | grep -o '"y":[0-9]*' | head -1 | cut -d: -f2)
  local AHP=$(echo "$RR" | grep -o '"activeUnit":{[^}]*}' | grep -o '"hp":[0-9]*' | head -1 | cut -d: -f2)
  local AMHP=$(echo "$RR" | grep -o '"activeUnit":{[^}]*}' | grep -o '"maxHp":[0-9]*' | head -1 | cut -d: -f2)
  local AMV=$(echo "$RR" | grep -o '"activeUnit":{[^}]*}' | grep -o '"move":[0-9]*' | head -1 | cut -d: -f2)
  local AJP=$(echo "$RR" | grep -o '"activeUnit":{[^}]*}' | grep -o '"jump":[0-9]*' | head -1 | cut -d: -f2)
  echo "  Active: ($AX,$AY) HP=$AHP/$AMHP Mv=$AMV Jmp=$AJP"
  # Units summary — use node to parse JSON reliably
  echo "  Units:"
  # Pass the scan JSON via stdin — it's too large for argv on big scans
  # (Throw Stone + radius AoE center lists can easily exceed ~32KB).
  echo "$R" | node -e "
    var d = JSON.parse(require('fs').readFileSync(0, 'utf8'));
    var u = d.battle?.units || [];
    // One-line formatter for an ability entry. Returns an array of strings
    // (1-2 lines) so callers can console.log each. Line 1 is always the compact
    // ability header; line 2, if present, is the bestCenters summary for radius
    // abilities.
    //
    // Point-target (AoE=1): 'targets=N hits=M: (x,y) *(x,y)«Name» ...'
    //   Tiles whose occupant matches the ability's intent (ally spells star
    //   self/ally tiles; enemy spells star enemy tiles) are prefixed with '*'
    //   and suffixed with «UnitName».
    //
    // Radius (AoE>1): 'centers=N' plus a 'best:' line listing top ranked splash
    //   placements. Each best entry shows '(x,y) e:Name,Name a:Name' summarizing
    //   which units would be caught in the splash.
    function fmtAb(a) {
      var parts = [a.name];
      if (a.horizontalRange) parts.push('R:' + a.horizontalRange);
      if (a.areaOfEffect && a.areaOfEffect > 1) parts.push('AoE:' + a.areaOfEffect);
      if (a.target) parts.push('-> ' + a.target);
      if (a.element) parts.push('(' + a.element + ')');
      if (a.mp) parts.push('MP ' + a.mp);
      if (a.addedEffect) parts.push('[' + a.addedEffect + ']');

      var extraLines = [];
      // Line abilities have a bestDirections summary instead of a tile list.
      // Detect them by the presence of bestDirections OR by HR>1 with AoE=1 and
      // only 2-4 seed tiles (a radius HR=8 would return dozens). Simpler: trust
      // bestDirections presence and fall through to standard rendering when absent.
      if (a.bestDirections && a.bestDirections.length) {
        parts.push('seeds=' + (a.validTargetTiles ? a.validTargetTiles.length : 0));
        var dirRendered = a.bestDirections.map(function(bd) {
          var segs = [bd.direction + '→(' + bd.seed[0] + ',' + bd.seed[1] + ')'];
          if (bd.enemies && bd.enemies.length)
            segs.push('e:' + bd.enemies.join(','));
          if (bd.allies && bd.allies.length)
            segs.push('a:' + bd.allies.join(','));
          return segs.join(' ');
        }).join('  ');
        extraLines.push('best: ' + dirRendered);
      } else if ((a.validTargetTiles && a.validTargetTiles.length) || a.totalTargets > 0) {
        if (a.areaOfEffect && a.areaOfEffect > 1) {
          // Radius AoE: compact center count + bestCenters summary.
          parts.push('centers=' + (a.validTargetTiles ? a.validTargetTiles.length : 0));
          if (a.bestCenters && a.bestCenters.length) {
            var bestRendered = a.bestCenters.map(function(bc) {
              var segs = ['(' + bc.x + ',' + bc.y + ')'];
              if (bc.enemies && bc.enemies.length)
                segs.push('e:' + bc.enemies.join(','));
              if (bc.allies && bc.allies.length)
                segs.push('a:' + bc.allies.join(','));
              return segs.join(' ');
            }).join('  ');
            extraLines.push('best: ' + bestRendered);
          }
        } else {
          // Point-target: show occupied tiles with markers.
          // Compact mode (totalTargets > 0): only occupied tiles in the list.
          // Verbose mode: full tile list with all tiles.
          var wantsAlly = a.target && (a.target.indexOf('ally') !== -1 || a.target.indexOf('self') !== -1);
          var wantsEnemy = a.target && a.target.indexOf('enemy') !== -1;
          var tiles = a.validTargetTiles || [];
          var total = a.totalTargets || tiles.length;
          var hits = 0;
          var rendered = tiles.map(function(t) {
            var occ = t.occupant;
            var hit =
              (wantsAlly && (occ === 'self' || occ === 'ally')) ||
              (wantsEnemy && occ === 'enemy');
            if (hit) hits++;
            var marker = hit ? '*' : '';
            var suffix = t.unitName ? '«' + t.unitName + '»' : '';
            return marker + '(' + t.x + ',' + t.y + ')' + suffix;
          }).join(' ');
          var empty = total - tiles.length;
          var emptyStr = empty > 0 ? '  (' + empty + ' empty)' : '';
          parts.push('hits=' + hits + ': ' + (rendered || '(none in range)') + emptyStr);
        }
      }
      return [parts.join(' ')].concat(extraLines);
    }
    // Pre-pass: assign disambiguation suffixes to nameless units with duplicate jobs
    // within a team. Keyed by \"team|jobName\", counts how many units share that key,
    // then numbers them in scan order. Single instances get no suffix.
    var groupCounts = {};
    u.forEach(function(v) {
      if (v.name) return;  // named story chars never get #N
      var key = (v.team || 0) + '|' + (v.jobName || '?');
      groupCounts[key] = (groupCounts[key] || 0) + 1;
    });
    var groupSeen = {};
    function disambiguate(v) {
      if (v.name) return null;  // named units don't need disambiguation
      var key = (v.team || 0) + '|' + (v.jobName || '?');
      if ((groupCounts[key] || 0) < 2) return null;  // singletons don't need numbering
      groupSeen[key] = (groupSeen[key] || 0) + 1;
      return '#' + groupSeen[key];
    }
    u.forEach(function(v) {
      var t = v.team === 0 ? 'PLAYER' : v.team === 2 ? 'ALLY' : 'ENEMY';
      var nm = v.name ? v.name + ' ' : '';
      var jn = v.jobName || '?';
      var disambig = disambiguate(v);
      var jnStr = disambig ? jn + ' ' + disambig : jn;
      var ex = '';
      if (v.isActive) ex += ' *ACTIVE*';
      if (v.lifeState) {
        ex += ' [' + v.lifeState;
        if (v.lifeState === 'dead' && v.deathCounter > 0)
          ex += ' ' + v.deathCounter + '/3';
        ex += ']';
      }
      if (v.statuses && v.statuses.length) ex += ' [' + v.statuses.join(',') + ']';
      if (v.chargingAbility) ex += ' {casting ' + v.chargingAbility + ' CT=' + v.chargeCt + '}';
      if (v.elementWeak && v.elementWeak.length) ex += ' weak:' + v.elementWeak.join(',');
      if (v.elementAbsorb && v.elementAbsorb.length) ex += ' absorb:' + v.elementAbsorb.join(',');
      console.log('    [' + t + '] ' + nm + '(' + jnStr + ') (' + v.x + ',' + v.y + ') HP=' + v.hp + '/' + v.maxHp + ' dist=' + (v.distance ?? '?') + ex);
      // Show equipped passives on a second line if present
      var passives = [];
      if (v.reaction) passives.push('R:' + v.reaction);
      if (v.support) passives.push('S:' + v.support);
      if (v.movement) passives.push('M:' + v.movement);
      if (passives.length) console.log('      equip: ' + passives.join(' | '));
      // Show per-unit abilities for non-active units (active unit shown separately below).
      // Each ability may render 1-2 lines (header + optional best-centers).
      if (!v.isActive && v.abilities && v.abilities.length) {
        v.abilities.forEach(function(a) {
          var lines = fmtAb(a);
          console.log('      - ' + lines[0]);
          for (var i = 1; i < lines.length; i++) console.log('        ' + lines[i]);
        });
      }
    });
    // Tiles
    var tiles = d.validPaths?.ValidMoveTiles;
    if (tiles) console.log('  Tiles: ' + tiles.desc);
    // Attack tiles
    var atk = d.validPaths?.AttackTiles?.attackTiles || [];
    atk.forEach(function(a) {
      if (a.occupant !== 'empty')
        console.log('    Attack ' + a.arrow + ' (' + a.x + ',' + a.y + '): ' + a.occupant + (a.jobName ? ' (' + a.jobName + ')' : '') + (a.hp != null ? ' HP=' + a.hp : ''));
    });
    // Facing
    var f = d.validPaths?.RecommendedFacing;
    if (f) console.log('  ' + f.desc);
    // Abilities (active unit) — one or two lines per ability.
    var activeUnit = d.battle?.units?.find(function(x){return x.isActive});
    var ab = activeUnit?.abilities;
    if (ab && ab.length) {
      console.log('  Abilities:');
      ab.forEach(function(a) {
        var lines = fmtAb(a);
        console.log('    - ' + lines[0]);
        for (var i = 1; i < lines.length; i++) console.log('      ' + lines[i]);
      });
    }
  "
}

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
