# FFT Hands-Free — Claude Plays Final Fantasy Tactics

## Project Goal

Make FFT fully hands-free by giving Claude Code a platform to play the game as if it were a human player. Claude sends commands to the game through a file-based bridge, reads game state from memory, and makes intelligent decisions during gameplay.

**Core principles:**
- **Speed** — Claude's interactions with the game should be as fast as possible. Every round-trip matters. Batch operations, embed state in responses, minimize tool calls.
- **Intelligence** — Claude should make smart tactical decisions in battle, manage party builds, navigate the world map, and plan ahead like an experienced player.
- **Engagement** — This should be fun to watch. Claude experiences the story as a new player — reading dialogue, reacting to plot twists, commenting on characters, sharing facts and observations as it learns. It should feel like watching a friend play for the first time.
- **Autonomy** — Claude should be able to play extended sessions with minimal human intervention. Scan the battlefield, pick a strategy, execute moves, handle unexpected situations, and recover from mistakes.

The ultimate vision: you say "play FFT" and Claude boots the game, loads a save, navigates the world, enters battles, makes tactical decisions, enjoys the story, and keeps you entertained along the way.

---

## Status Key
- [x] Done
- [ ] Not started
- [~] Partially done

---

## 1. ValidPaths + High-Level Actions (Navigation System)

Every response includes `validPaths` — a map of action names to exact commands. Claude picks one, sends it, zero interpretation needed.

### ValidPaths Per Screen
- [x] TitleScreen: Advance
- [x] WorldMap: PartyMenu, TravelList, EnterLocation
- [x] TravelList: PrevTab, NextTab, ScrollUp/Down, SelectLocation, Close
- [x] PartyMenu: WorldMap, PrevTab, NextTab, CursorUp/Down/Left/Right, SelectUnit
- [x] CharacterStatus: Equipment, Jobs, SidebarUp/Down, Back
- [x] EquipmentScreen: SelectSlot, CursorUp/Down/Left/Right, Back
- [x] JobScreen: SelectJob, CursorUp/Down/Left/Right, Back
- [x] JobActionMenu: LearnAbilities, ChangeJob, Back
- [x] EncounterDialog: Fight, Flee
- [x] Battle_MyTurn: Move, Abilities, Wait, Status, AutoBattle, Pause, MoveToEnemy
- [x] Battle_Moving: ConfirmMove, Cancel, CursorUp/Down/Left/Right
- [x] Battle_Targeting: Confirm, Cancel, CursorUp/Down/Left/Right
- [x] Battle_Paused: Resume, ReturnToWorldMap
- [ ] Settlement: Outfitter, Tavern, Guild, PoachDen (detect via entering settlement location)
- [ ] Outfitter Buy/Sell screens

### High-Level Actions
- [x] `battle_wait` — Navigate to Wait, confirm, confirm facing, poll for terminal state
- [x] `navigate` — Multi-step screen navigation (e.g., anywhere to PartyMenu)
- [x] `travel` — Open travel list, find location by ID, select
- [x] `confirm_attack` — Double-F confirm, poll through attack animation
- [x] `move_to` — Scan units, find nearest enemy, move adjacent, confirm
- [x] `scan_units` — C+Up cycle through all units, return rich stats per unit
- [x] `move_grid` — Navigate cursor to target grid (x,y), confirm with F
- [x] `get_arrows` — Compute arrow key sequence from current pos to target
- [x] `test_c_hold` — Diagnostic for DirectInput C+Up simulation
- [x] `write_byte` — Write a byte to a memory address (for testing)
- [ ] `battle_attack` — Auto-target enemy at given grid position, confirm attack
- [ ] `buy_item` — Navigate outfitter, select category, find item, buy quantity
- [ ] `change_job` — Navigate to job screen, select job, confirm
- [ ] `equip_ability` — Navigate to equipment screen, select slot, find ability

---

## 2. Differential Memory Scanner

Cheat Engine-style snapshot/diff for discovering UI state addresses.

- [x] `snapshot` action — save memory state with label
- [x] `diff` action — compare two snapshots, write changed addresses to file
- [x] `read_address` / `read_block` / `batch_read` — arbitrary memory reads
- [x] `search_bytes` — AoB scan across writable memory
- [x] Target main module writable sections only (skip code, skip >10MB regions)
- [x] Diff output: address, old/new value, byte/uint interpretation

---

## 3. Speed Optimizations (Game Bridge)

### Phase 1: Embed State in Response
- [x] GameState included in every CommandResponse (no separate state.json read needed)
- [x] Screen detection included in every response (`screen` field)
- [x] Battle data included when in battle (`battle` field with units, active unit, etc.)

### Phase 2: Sequence Commands with Assertions
- [x] `SequenceStep` model: keys, waitMs, assert, description
- [x] `SequenceAssert` model: screen, cursorIndex, tab, sidebarIndex
- [x] `ExecuteSequence` in CommandWatcher: runs steps, halts on assertion failure
- [x] Step-level read_address support for inline memory reads
- [x] `waitForScreen` / `waitUntilScreenNot` / `waitForChange` per-step waiting

### Phase 3: Screen State Machine
- [x] `ScreenStateMachine` — pure logic class tracking current screen
- [x] `DetectScreen` — memory-based screen detection (17 screens)
- [x] `DetectScreenSettled` — polls until screen reads same 3 consecutive times
- [x] `ScreenStateMachine.OnKeyPressed()` — updates state on each key
- [x] `set_screen` action for manual resync
- [x] Thread-safe: state machine only mutated inside command processing lock

---

## 4. Battle Automation

### Battle State Presentation

Claude needs the right information at the right time to make smart decisions — no more, no less. Too little and it guesses wrong. Too much and it wastes tokens parsing irrelevant data.

**Compact battle summary (auto-returned every turn):**
- [ ] All unit positions and teams (who's where)
- [ ] All unit HP / Max HP (who's hurt, who's a kill target)
- [ ] Distance from active unit to each enemy (who can I reach)
- [ ] Which enemies are in attack range without moving
- [ ] Active unit's Move range (how far can I go)
- [ ] Who's about to act next (CT values — should I kill them before they go?)

**Full detail (available on demand, not every turn):**
- [ ] PA/MA/Brave/Faith (for damage estimation)
- [ ] Status effects (poison, sleep, charging spells)
- [ ] Equipped abilities (reaction abilities change target priority)
- [ ] Terrain/elevation at key tiles
- [ ] Job names (flavor, helps Claude narrate)

**Goal:** A single `battle_status` response that gives Claude everything a good FFT player would glance at before choosing their action. No follow-up reads needed.

### Rework Unit Position Discovery

The current C+Up scan works but has known flaws:
- **Slow** — holds C key via DirectInput, presses Up 3-12 times with 250ms delays = 1-4 seconds
- **Fragile stop logic** — relies on duplicate position detection + expectedCount from an unverified memory address
- **Escapes the menu** — scan dismisses the action menu to do C+Up, then re-opens it, which can change camera rotation
- **Misses units** — fast units appearing twice in turn order can cause early termination
- **No continuous tracking** — must rescan every turn instead of maintaining state

**Potential improvements:**
- [ ] Find a memory structure that directly stores all unit positions (eliminate C+Up entirely)
- [ ] If C+Up is kept: do a single full scan at battle start, then track changes via background polling
- [ ] Background position tracking during enemy turns (watch cursor movement + team changes)
- [ ] Position dictionary maintained by BattleTracker, updated incrementally
- [ ] Detect deaths (HP -> 0) and remove from tracking
- [ ] Investigate whether the heap battle structs (found earlier, disabled due to crashes) can be read safely with a more targeted scan

### Speed & Accuracy Enforcement

Every interaction between Claude and the game should be as fast and reliable as possible.

**Speed:**
- [ ] Measure round-trip time per action — log it, flag anything >2s mod-side
- [ ] Battle status should be a single response with everything needed — zero follow-up reads
- [ ] Pre-compute arrow sequences in responses so Claude just says "execute"
- [ ] Minimize C+Up scan frequency — track positions continuously, only full-scan when stale

**Accuracy:**
- [ ] Self-calibrating rotation: press one arrow, measure delta, derive full rotation mapping — costs 250ms but guarantees correctness
- [ ] Build rotation verification into `move_grid` — one test press, then navigate
- [ ] Verify rotation table at all 4 rotations empirically (rot=0 and rot=2 still inferred)
- [ ] After confirming a move, verify cursor position matches target
- [ ] After attacking, verify enemy HP decreased (confirm hit landed)

### Unit Scanning (C+Up Cycling) — Current Implementation
- [x] DirectInput SCANCODE trick for simulating held C key (SetForegroundWindow + SendInput with KEYEVENTF_SCANCODE)
- [x] C+Up cycles cursor through all units in turn order
- [x] Read rich data per unit: grid position, team, level, HP/MP, PA/MA, Move/Jump, Job, Brave/Faith, CT, Exp, NameId
- [x] Read active unit before cycling (cursor starts on active unit)
- [x] Stop logic: press Up at least expectedCount times before stopping on duplicate
- [x] Unit count candidate at 0x140900650 (used as upper bound)
- [ ] Add unit name lookup (NameId -> name from CharaName NXD table)
- [ ] Include unit's equipped abilities in scan data (reaction, support, movement)

### Grid Movement
- [x] Grid cursor addresses: X at 0x140C64A54, Y at 0x140C6496C
- [x] Camera rotation at 0x14077C970 (byte, value % 4)
- [x] Arrow key -> grid delta rotation table (empirically verified at rot=1 and rot=3)
- [x] `move_grid` action: takes target (x,y), enters Move, presses arrows, confirms
- [ ] Self-calibrating rotation (test one press, derive mapping, then navigate)
- [ ] Auto-retry on invalid tile (if F doesn't confirm, try adjacent tiles)
- [ ] Read valid tile list and check target before navigating (prevent invalid moves)

### Attack Targeting
- [ ] `battle_attack` action: open Abilities -> Attack -> navigate target cursor to enemy -> confirm
- [ ] Read rotation DURING targeting mode (camera may auto-rotate between Move and Attack)
- [ ] Verify attack landed (check enemy HP decreased)

### Battle AI / Decision Making
- [ ] At turn start: auto-return battle status with all decision-relevant info
- [ ] Choose target based on HP, distance, threat assessment, CT (kill before they act)
- [ ] Choose optimal adjacent tile (avoid AoE, prefer backstab for bonus damage)
- [ ] Decide move vs attack vs wait based on distance and abilities
- [ ] Handle out-of-range enemies (move toward, wait for next turn)
- [ ] Consider reaction abilities when choosing targets (don't attack someone with Counter if low HP)

---

## 5. Known Issues / Blockers

### Coordinate System
- Grid coords (from cursor) and world coords (from battle tracker) are different systems
- Offset: gridX = worldX - offsetX (computed from known unit at both coords)
- Rotation table may need correction at rot=0 and rot=2
- Camera auto-rotates when entering Move mode or Attack targeting — always re-read rotation

### Screen Detection Edge Cases
- Battle_Paused false positives: pauseFlag=1 stale after facing confirmation
- GameOver detection: reads as Battle (need dedicated game-over flag)
- Settlement/shop screens not detected yet
- Menu cursor unreliable after animations

### Turn Detection
- `acted` and `moved` flags at 0x14077CA8C/9C are unreliable
- Entering Move mode resets a previous move (don't enter Move to "check" position)
- Active unit position from condensed struct is stale after moving

---

## 6. Shell Helpers (fft.sh)

- [x] `screen` — quick state check
- [x] `path <name>` — execute validPath
- [x] `enter/esc/up/down/left/right/space/tab/tkey/ekey` — key presses
- [x] `key_wait/key_leave/key_changed` — key + wait for condition
- [x] `rv/block/batch` — memory reads
- [x] `wv` — memory write
- [x] `scan_units` — C+Up unit scan with rich data
- [x] `move_grid <x> <y>` — navigate cursor to grid position
- [x] `get_arrows [execute]` — compute/execute arrows to nearest enemy
- [x] `battle_wait` — end turn
- [x] `nav <screen>` — navigate to screen
- [x] `travel <id>` — travel to location
- [x] `restart` — kill, build, deploy, relaunch
- [x] `boot` — press Enter through title screen

---

## 7. Key Memory Addresses

| Address | Size | Field | Notes |
|---------|------|-------|-------|
| 0x140C64A54 | byte | Grid cursor X | Absolute, doesn't change with rotation |
| 0x140C6496C | byte | Grid cursor Y | Absolute, doesn't change with rotation |
| 0x14077D2A0 | struct | Condensed battle struct | Cursor-selected unit data |
| 0x14077D2A2 | uint16 | Team (cursor unit) | 0=ally, 1+=enemy |
| 0x14077C970 | byte | Camera rotation | value % 4 = rotation 0-3 |
| 0x1407FC620 | byte | Action menu cursor | 0=Move,1=Abilities,2=Wait,3=Status,4=Auto |
| 0x140C66315 | 7*N | Movement tile list | X,Y,elev,flag per tile, flag=0 terminates |
| 0x140C64E7C | byte | Cursor tile index | Index into movement tile list |
| 0x14077CA30 | uint32*10 | Unit existence slots | 0xFF=exists, 0xFFFFFFFF=terminator |
| 0x14077CA8C | byte | Acted flag | Unreliable |
| 0x14077CA9C | byte | Moved flag | Unreliable |
| 0x14077D208 | byte | Location ID | 255=title/battle, 0-42=world map |
| 0x140787A22 | byte | Hover location | World map cursor |
| 0x140D3A41E | byte | Party menu flag | 1=in party menu |
| 0x140D4A264 | byte | UI overlay flag | 1=UI present |
| 0x140900824/828 | byte | Encounter detection | Different values=Fight/Flee dialog |
| 0x140900650 | byte | Unit count (candidate) | Matches living units on field |
| 0x1407AC7C0 | struct | UI display buffer | Level,NameId,HP,MP,PA,Move,Jump,Job,Brave,Faith |
| 0x140D39CD0 | uint32 | Gil | Party money |
| 0x1411A18D0 | 55*0x258 | Roster array | Persistent unit data |

---

## 8. DirectInput Key Simulation

FFT uses DirectInput for keyboard polling. Standard Win32 APIs (PostMessage, keybd_event, SendInput without SCANCODE) work for single key presses but NOT for held-key detection.

**Working approach for held keys:**
1. `SetForegroundWindow(gameWindow)`
2. `SendInput` with `KEYEVENTF_SCANCODE` flag, `wScan = MapVirtualKey(vk, 0)`, `wVk = 0`
3. Also send via `keybd_event` and `PostMessage` as belt-and-suspenders
4. Re-assert the held key before each action key press
5. Release via all three methods when done

**Used for:** C+Up unit cycling scan
