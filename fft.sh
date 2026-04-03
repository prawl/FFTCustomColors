#!/bin/bash
# FFT Game Bridge Helper - source this before using: source ./fft.sh
B="/c/program files (x86)/steam/steamapps/common/FINAL FANTASY TACTICS - The Ivalice Chronicles/Reloaded/Mods/FFTColorCustomizer/claude_bridge"

# Track script start time for total timeout
FFT_START=${SECONDS}
FFT_MAX=${FFT_MAX:-30}

_check_total() {
  if [ $((SECONDS - FFT_START)) -ge $FFT_MAX ]; then
    echo "[TOTAL_TIMEOUT] Script exceeded ${FFT_MAX}s"
    return 1
  fi
  return 0
}

# Core: send command JSON, wait for response, print screen summary line
fft() {
  _check_total || return 1
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
  local HOV=$(echo "$R" | grep -o '"hover":[0-9]*' | head -1 | cut -d: -f2)
  local MC=$(echo "$R" | grep -o '"menuCursor":[0-9]*' | head -1 | cut -d: -f2)
  local ST=$(echo "$R" | grep -o '"status":"[^"]*"' | head -1 | cut -d'"' -f4)
  echo "[$SCR] loc=$LOC hover=$HOV menu=$MC status=$ST"
}

# Full response: returns entire response.json content
fft_full() {
  _check_total || return 1
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

id() { echo "c$(date +%s%N | tail -c 8)$RANDOM"; }

# --- Key helpers ---
# No client-side sleep. The mod-side waitForScreen/waitForChange handles state settling.
# For commands that need a specific screen wait, use: enter_wait, key_wait, etc.

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

# Key with waitForScreen — blocks until screen matches or timeout
key_wait() {
  local vk=$1 name=$2 screen=$3 timeout=${4:-2000}
  fft "{\"id\":\"$(id)\",\"keys\":[{\"vk\":$vk,\"name\":\"$name\"}],\"delayBetweenMs\":150,\"waitForScreen\":\"$screen\",\"waitTimeoutMs\":$timeout}"
}
enter_wait() { key_wait 13 Enter "$1" "${2:-2000}"; }
esc_wait()   { key_wait 27 Escape "$1" "${2:-2000}"; }

# Key with waitUntilScreenNot — blocks until screen changes away from current
key_leave() {
  local vk=$1 name=$2 screen=$3 timeout=${4:-2000}
  fft "{\"id\":\"$(id)\",\"keys\":[{\"vk\":$vk,\"name\":\"$name\"}],\"delayBetweenMs\":150,\"waitUntilScreenNot\":\"$screen\",\"waitTimeoutMs\":$timeout}"
}

# Key with waitForChange — blocks until specified address(es) change value
key_changed() {
  local vk=$1 name=$2 addr=$3 timeout=${4:-2000}
  fft "{\"id\":\"$(id)\",\"keys\":[{\"vk\":$vk,\"name\":\"$name\"}],\"delayBetweenMs\":150,\"waitForChange\":[\"$addr\"],\"waitTimeoutMs\":$timeout}"
}

# --- Memory read helpers ---

# Read single value at address (1/2/4 bytes), returns just the number
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

# Read a block, returns hex string
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

# Batch read: read multiple addresses in one round-trip
# Usage: batch '{"addr":"14077D208","size":1,"label":"loc"},{"addr":"140787A22","size":1,"label":"hover"}'
batch() {
  fft_full "{\"id\":\"$(id)\",\"action\":\"batch_read\",\"addresses\":[$1]}"
}

# --- High-level navigation actions ---
# path: Execute a validPath by name. Shows screen + available next paths.
# Usage: path Flee, path PartyMenu, path EnterLocation, path Wait
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

# battle_wait: End turn (navigate to Wait, confirm, confirm facing)
battle_wait() { fft "{\"id\":\"$(id)\",\"action\":\"battle_wait\"}"; }

# navigate: Go to a target screen from wherever we are
# Usage: nav PartyMenu
nav() { fft "{\"id\":\"$(id)\",\"action\":\"navigate\",\"to\":\"$1\"}"; }

# travel: Move world map cursor to a location by ID (does NOT enter it)
# Usage: travel 24
# After travel, use EnterLocation validPath to actually go there
travel() { fft "{\"id\":\"$(id)\",\"action\":\"travel\",\"locationId\":$1}"; }

# restart: kill game, build, deploy, relaunch, wait for bridge
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
  if [ -f "$B/state.json" ]; then
    echo "[restart] Bridge online. Game is on title screen."
  else
    echo "[restart] TIMEOUT waiting for bridge"
    return 1
  fi
}

# boot: Press Enter repeatedly until we leave TitleScreen
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

# --- State helpers ---
state() { fft "{\"id\":\"$(id)\",\"action\":\"report_state\"}"; }
screen() { fft "{\"id\":\"$(id)\",\"keys\":[],\"delayBetweenMs\":0}"; }
