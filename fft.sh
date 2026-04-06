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
#   Key presses:   enter, esc, up, down, left, right, space, tab, tkey, ekey
#   Navigation:    path <name>, nav <screen>, travel <locationId>
#   Battle:        battle_wait, path Move, path Abilities, path MoveToEnemy
#   Memory:        rv <addr> [size], block <addr> <size>, batch '<json>'
#   State:         screen, state, fft_full '<json>'
#   System:        restart, boot
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
  until [ -f "$B/response.json" ]; do
    sleep 0.02
    tries=$((tries + 1))
    if [ $tries -ge 250 ]; then
      echo "[TIMEOUT] No response after 5s"
      return 1
    fi
  done
  # Parse screen fields from response — read whole file, strip whitespace
  local R=$(cat "$B/response.json" | tr -d '\r\n ')
  local SCR=$(echo "$R" | grep -o '"name":"[^"]*"' | head -1 | cut -d'"' -f4)
  local LOC=$(echo "$R" | grep -o '"location":[0-9]*' | head -1 | cut -d: -f2)
  local LOCNAME=$(echo "$R" | grep -o '"locationName":"[^"]*"' | head -1 | cut -d'"' -f4)
  local HOV=$(echo "$R" | grep -o '"hover":[0-9]*' | head -1 | cut -d: -f2)
  local UI=$(echo "$R" | grep -o '"ui":"[^"]*"' | head -1 | cut -d'"' -f4)
  local ST=$(echo "$R" | grep -o '"status":"[^"]*"' | head -1 | cut -d'"' -f4)
  local LOCSTR="$LOC"; [ -n "$LOCNAME" ] && LOCSTR="$LOC($LOCNAME)"
  echo "[$SCR] loc=$LOCSTR hover=$HOV ui=$UI status=$ST"
}

# fft_full: Send raw command JSON, wait for response, return entire JSON.
# Use this when you need battle data, validPaths, tile lists, etc.
fft_full() {
  _check_total || return 1
  _fft_guard
  rm -f "$B/response.json"
  echo "$1" > "$B/command.json"
  local tries=0
  until [ -f "$B/response.json" ]; do
    sleep 0.02
    tries=$((tries + 1))
    if [ $tries -ge 250 ]; then
      echo "[TIMEOUT]"
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

# path: Execute a validPath by name. Shows screen + available next paths.
# This is the PRIMARY way to interact with the game. Every response tells you
# what paths are available — pick one and call path again.
# Usage: path Flee, path PartyMenu, path Move, path Wait, path Abilities
path() {
  _check_total || return 1
  rm -f "$B/response.json"
  echo "{\"id\":\"$(id)\",\"action\":\"path\",\"to\":\"$1\"}" > "$B/command.json"
  local tries=0
  until [ -f "$B/response.json" ]; do
    sleep 0.02
    tries=$((tries + 1))
    if [ $tries -ge 250 ]; then echo "[TIMEOUT]"; return 1; fi
  done
  node -e "
const r=JSON.parse(require('fs').readFileSync(process.argv[1],'utf8'));
const s=r.screen||{};
console.log('['+s.name+'] loc='+s.location+' hover='+s.hover+' status='+r.status);
if(r.error)console.log('  ERROR:',r.error);
const vp=r.validPaths||{};
const keys=Object.keys(vp);
if(keys.length){console.log('  ValidPaths:');keys.forEach(k=>console.log('    '+k+': '+vp[k].desc));}
" "$B/response.json"
}

# battle_wait: End turn. Handles menu navigation → Wait → confirm facing automatically.
battle_wait() { fft "{\"id\":\"$(id)\",\"action\":\"battle_wait\"}"; }

# battle_flee: Quit battle and return to world map (Tab → Down x4 → Enter → Enter).
battle_flee() { fft "{\"id\":\"$(id)\",\"action\":\"battle_flee\"}"; }

# nav: Navigate to a target screen from wherever you are (multi-step).
# Usage: nav PartyMenu
nav() { fft "{\"id\":\"$(id)\",\"action\":\"navigate\",\"to\":\"$1\"}"; }

# travel: Navigate to a world map location by ID. Opens travel list, selects location, confirms move.
# Usage: travel 26   (travel to Siedge Weald)
travel() { fft "{\"id\":\"$(id)\",\"action\":\"travel_to\",\"locationId\":$1}"; }

# goto: Travel to a location, enter encounter, place Ramza solo, start battle.
# Handles: travel → confirm move → encounter → formation → battle.
# Flees encounters at wrong locations along the way.
# Usage: goto 26   (travel to loc 26 and fight the random encounter)
goto() {
  local target=$1
  _check_total || return 1
  # Step 1: Travel to hover over target + confirm move
  echo "[goto] Traveling to location $target..."
  fft "{\"id\":\"$(id)\",\"action\":\"travel\",\"locationId\":$target}"
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

# restart: Full cycle — kill game, build mod, deploy, relaunch, wait for bridge.
# Use when you need code changes to take effect.
restart() {
  echo "[restart] Killing game..."
  taskkill //IM FFT_enhanced.exe //F 2>/dev/null
  taskkill //IM reloaded-ii.exe //F 2>/dev/null
  sleep 2
  echo "[restart] Building..."
  dotnet build ColorMod/FFTColorCustomizer.csproj -c Release 2>&1 | tail -3
  echo "[restart] Deploying..."
  powershell.exe -ExecutionPolicy Bypass -File ./BuildLinked.ps1 2>&1 | tail -3
  echo "[restart] Launching..."
  "/c/program files (x86)/steam/steamapps/common/FINAL FANTASY TACTICS - The Ivalice Chronicles/Reloaded/reloaded-ii.exe" --launch "C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\FFT_enhanced.exe" &
  echo "[restart] Waiting for bridge..."
  local tries=0
  until [ -f "$B/state.json" ] || [ $tries -ge 150 ]; do
    sleep 0.2
    tries=$((tries + 1))
  done
  if [ ! -f "$B/state.json" ]; then
    echo "[restart] TIMEOUT waiting for bridge"
    return 1
  fi
  echo "[restart] Bridge online. Booting through title..."
  # Press Enter through title → main menu → Continue → save select → loading
  local max=20
  for i in $(seq 1 $max); do
    rm -f "$B/response.json"
    echo "{\"id\":\"$(id)\",\"keys\":[{\"vk\":13,\"name\":\"Enter\"}],\"delayBetweenMs\":150}" > "$B/command.json"
    local t=0
    until [ -f "$B/response.json" ] || [ $t -ge 150 ]; do sleep 0.02; t=$((t+1)); done
    sleep 2
    # Check screen
    rm -f "$B/response.json"
    echo "{\"id\":\"$(id)\",\"keys\":[],\"delayBetweenMs\":0}" > "$B/command.json"
    t=0
    until [ -f "$B/response.json" ] || [ $t -ge 150 ]; do sleep 0.02; t=$((t+1)); done
    local scr=$(tr -d '\r\n ' < "$B/response.json" | grep -o '"name":"[^"]*"' | head -1 | cut -d'"' -f4)
    echo "[restart] boot $i — screen: $scr"
    if [ "$scr" != "TitleScreen" ] && [ "$scr" != "" ]; then
      echo "[restart] Ready on $scr"
      return 0
    fi
  done
  echo "[restart] Booted (may still be loading)"
}

# boot: Press Enter through title/continue screens until world map loads.
boot() {
  local max=10
  for i in $(seq 1 $max); do
    enter
    sleep 2
    local scr=$(fft '{"id":"'$(id)'","keys":[],"delayBetweenMs":0}' | grep -o '^\[[^]]*\]' | tr -d '[]')
    echo "[boot] attempt $i — screen: $scr"
    if [ "$scr" != "TitleScreen" ]; then
      echo "[boot] Arrived at $scr"
      return 0
    fi
  done
  echo "[boot] TIMEOUT after $max attempts"
  return 1
}

# =============================================================================
# STATE HELPERS
# =============================================================================

# state: Force a full state report (roster, battle data, etc). Prints summary.
state() { fft "{\"id\":\"$(id)\",\"action\":\"report_state\"}"; }

# screen: Quick check — sends no-op command, returns current screen name & state.
screen() { fft "{\"id\":\"$(id)\",\"keys\":[],\"delayBetweenMs\":0}"; }

# scan_units: Hold C + press Up to cycle through all units, report their grid positions + teams.
# Use during battle (Battle_MyTurn) to discover where everyone is.
scan_units() { fft_full "{\"id\":\"$(id)\",\"action\":\"scan_units\"}"; }

# auto_move: Full autonomous turn — scan, compute tiles, pick safest, move, wait.
# Usage: auto_move 7 3   (move=7, jump=3)
auto_move() {
  local mv=${1:-4}; local jmp=${2:-3}
  rm -f "$B/response.json"
  echo "{\"id\":\"$(id)\",\"action\":\"auto_move\",\"locationId\":$mv,\"unitIndex\":$jmp}" > "$B/command.json"
  local tries=0
  until [ -f "$B/response.json" ]; do sleep 0.5; tries=$((tries+1)); [ $tries -ge 300 ] && echo "TIMEOUT" && return 1; done
  cat "$B/response.json"
}

# scan_move: Scan units + compute valid movement tiles from map data.
# Returns unit positions AND valid tiles. Requires set_map first.
# Usage: scan_move              (uses scanned move/jump stats)
#        scan_move 3 3          (override move=3, jump=3)
scan_move() {
  local mv=${1:-0}
  local jmp=${2:-0}
  fft_full "{\"id\":\"$(id)\",\"action\":\"scan_move\",\"locationId\":$mv,\"unitIndex\":$jmp}"
}

# move_grid: Enter Move mode, navigate cursor to grid (x,y), confirm with F.
# Usage: move_grid <x> <y>
# Example: move_grid 0 2    → move to grid position (0,2)
move_grid() { fft_full "{\"id\":\"$(id)\",\"action\":\"move_grid\",\"locationId\":$1,\"unitIndex\":$2}"; }

# battle_attack: Attack a target tile. Handles menu nav, rotation detection, targeting.
# Usage: battle_attack <x> <y>
# Example: battle_attack 2 4    → attack tile (2,4)
battle_attack() { fft_full "{\"id\":\"$(id)\",\"action\":\"battle_attack\",\"locationId\":$1,\"unitIndex\":$2}"; }

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
