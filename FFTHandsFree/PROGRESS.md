# Claude Plays FFT — Progress Log

## Session: 2026-04-02

### Ability System — Fully Mapped
- **Discovered ability offsets** in unit data: secondary (+0x07), reaction (+0x08), support (+0x0A), movement (+0x0C), with equipped flags at +0x09, +0x0B, +0x0D
- **Mapped all 78 ability IDs** by sweeping through every ability in-game:
  - 28 Reaction abilities (0xA7–0xC5)
  - 30 Support abilities (0xC6–0xE5)
  - 20 Movement abilities (0xE6–0xFD)
- **JP costs documented** for every ability across all jobs (docs/ABILITY_COSTS.md)
- **JP per job** readable from unit data at +0x80 (uint16 LE, 2 bytes per job)
- **Job levels** at +0x74 (nibble-packed, 4 bits per job)
- **Secondary ability indices** confirmed as global (same index = same ability regardless of character)
- **AbilityData.cs** — static lookup tables with IDs, names, JP costs, and job associations

### Ability Management — End-to-End Working
- **Changing equipped abilities**: navigate to Equipment & Abilities screen, open ability list, sweep to find target by reading memory after each selection
- **Changing jobs**: navigate job grid, select job, Change Job, dismiss confirmation
- **Learning abilities**: navigate to Learn Abilities screen, select ability, confirm purchase, verify JP decreased
- **Key discovery**: Enter selects ability but list stays open; Escape closes the list; 2000ms wait needed after Escape before grid navigation

### Game Knowledge — Strategy Reference
- **docs/JOB_CLASSES.md** — job unlock tree, stats, equipment, essential abilities, damage formulas, recommended builds
- **docs/classes.txt** — full GameFAQs guide text for reference
- **Dream team planned and partially executed**:
  1. Ramza: Gallant Knight + Martial Arts / Counter / Concentration / Move+3 ✓
  2. Kenrick: Black Mage + Arithmeticks / Mana Shield / Magick Boost / Manafont ✓
  3. Lloyd: Ninja + Martial Arts / Shirahadori / Attack Boost / Move+2 (job changed, abilities pending)
  4. William: Samurai + Items / Shirahadori / Doublehand / Teleport (pending)

### Bridge Infrastructure Improvements
- **read_address action** — read 1/2/4 bytes at any memory address, returns value in response
- **read_block action** — read up to 4096 bytes, returns hex string
- **Sequence step reads** — readAddress field on sequence steps for inline memory reads
- **Faster polling** — command watcher reduced from 1000ms to 250ms polling, 100ms to 50ms debounce
- **Fast bridge pattern** — `brg()` bash function with poll loop replaces sleep commands (10x faster)
- **Unique command IDs** — `nextid()` function prevents duplicate ID skipping

### Memory Discoveries
- **`0x143D13BB0`** — available jobs buffer (uint16 LE, populated when ability list opens, clears on close)
- **`0x140C0EB20`** — cursor counter (global, increments on every list cursor move)
- **Learned abilities bitfield** at unit offsets 0x30–0x70 (structure identified but bit-to-ability mapping not decoded)
- **Dark Knight/Onion Knight** mods disabled — not available as job options

### E2E Test Suite (e2e_tests.sh)
8 tests covering the full automation pipeline:
1. **test1** — Launch game, verify Start Game screen (activeUnitCount=55 + screenshot)
2. **test2** — Start Game → Continue → World Map (activeUnitCount=18 + screenshot)
3. **test4** — Escape → Party Menu (cursor=0)
4. **test5** — Navigate Right → character (0,1) (cursor=1)
5. **test6** — Change job Summoner → Black Mage → restore (verify job byte changed)
6. **test7** — Change secondary Bardsong → Iaido → restore (verify secondary index)
7. **test8** — Unequip and re-equip accessory (screenshot verification)

Key test infrastructure:
- `brg()` — send command and poll for response with unique IDs
- `nextid()` — timestamp+random unique ID generator
- `assert_eq()` — equality assertion with pass/fail reporting
- Precondition checks at test start
- Sequential execution, stops on failure

### Files Created/Modified
- `ColorMod/GameBridge/AbilityData.cs` — ability ID/name/cost/job lookup tables
- `ColorMod/GameBridge/GameStateReporter.cs` — ability names in state.json output
- `ColorMod/GameBridge/GameMemoryScanner.cs` — ability field offsets
- `ColorMod/GameBridge/GameStateModels.cs` — ability fields on UnitState
- `ColorMod/GameBridge/MemoryExplorer.cs` — ReadBlock method
- `ColorMod/Utilities/CommandWatcher.cs` — read_address, read_block actions, faster polling
- `ColorMod/Utilities/CommandBridgeModels.cs` — ReadResult, BlockData, address fields
- `docs/CLAUDE_GAME_BRIDGE.md` — ability offsets, full ID tables, cursor address
- `docs/UNIT_DATA_STRUCTURE.md` — ability field offsets
- `docs/ABILITY_COSTS.md` — JP costs for every ability by job
- `docs/JOB_CLASSES.md` — job strategy reference
- `docs/classes.txt` — GameFAQs guide source
- `e2e_tests.sh` — automated E2E test suite
- `bridge.sh` — bridge helper script

### World Map Navigation — Fully Mapped
- **Current location address: `0x14077D208`** — byte storing location ID where party is standing
- **Hover cursor address: `0x140787A22`** — byte storing location ID cursor is hovering over (0xFF when idle)
- **40 locations mapped** across 3 travel list tabs (Settlements, Battlegrounds, Miscellaneous)
- **Travel List**: T opens, Q/E switch tabs, Up/Down scroll (circular), Enter moves cursor (doesn't travel), Enter again on map to travel
- IDs 10 and 20 not yet observed (possibly Eagrose Castle + one other)
- **Encounter dialog detection**: `0x140900824 != 0x140900828` means Fight/Flee dialog is active
- **Auto-flee**: wait 2s after detection, send Down, wait 1s, send Enter (cursor defaults to Fight, must move to Flee first)
- **Story objective address: `0x1411A0FB6`** — byte storing location ID of yellow diamond marker (next story destination)
- Discovery method: differential memory scanning with noise filtering (idle snapshots to exclude timer/animation addresses)

### Settlement & Outfitter — Working
- **Gil address: `0x140D39CD0`** — uint32 LE, verified by buying/selling (Oak Staff 120 Gil, Antidote 50 Gil, X-Potion 700 Gil)
- **Item inventory**: byte array near `0x1411A17FB` (close to roster base). Each byte = count of that item (max 99)
- **Settlement menu flow**: Enter on settlement → Outfitter/Tavern/Warriors' Guild/Poachers' Den
- **Outfitter flow**: Buy/Sell/Fitting → category tabs (A/D or Left/Right) → item list (Up/Down) → Enter → quantity (Down wraps to max) → Enter → Buy/Cancel → Enter
- **Sell flow**: identical to Buy but shows owned inventory
- **Exit flow**: Escape from item list → Escape from Buy/Sell/Fitting → merchant goodbye dialog (dismisses automatically) → Escape from settlement menu
- **Static item mapping**: full late-game inventory documented in SHOP_ITEMS.md (6 category tabs, ~160 items at Trade City of Sal Ghidos). Other settlements may have fewer items.
- **Tip**: Down from quantity 1 wraps to max purchasable (99 minus held count)

### Battle Memory Mapping — Major Progress (Session 2026-04-02)

**Screen State Machine (CommandWatcher.cs)**
Built memory-based screen detection into every bridge response. Every command returns `{"screen": {"name": "...", ...}}` with the current screen and key values.
- **TitleScreen**: location=255, not in battle
- **Battle_MyTurn / Battle**: unitSlot0==255 AND unitSlot9==0xFFFFFFFF
- **PartyMenu**: partyFlag(0x140D3A41E)==1
- **TravelList**: uiFlag(0x140D4A264)==1, not party, not battle
- **WorldMap**: both flags 0
- **EncounterDialog**: encA(0x140900824) != encB(0x140900828)

**Battle Data Structures Confirmed**
- **Turn Order Queue** (0x14077D2A0): Rolling queue of upcoming units with Level, NameId, Team, Exp, HP, MaxHP, MP, MaxMP. Updates in real-time (HP drops visible immediately).
- **Team Allegiance**: Condensed struct +0x02 (0=friendly, 1=enemy)
- **Movement Tile List** (0x140C66315): 7 bytes per tile [X][Y][elevation][flag][0][0][0], terminated by flag=0. Available when in Move mode.
- **Cursor Tile Index** (0x140C64E7C): byte index into tile list for hover position
- **Action Menu Cursor** (0x1407FC620): 0=Move, 1=Abilities, 2=Wait, 3=Status, 4=AutoBattle
- **Unit Existence Slots** (0x14077CA30): uint32 per unit, 0xFF=exists, 0xFFFFFFFF=terminator. 9 units in Mt. Germinas battle.
- **Act/Move Taken Flags**: 0x14077CA8C (acted), 0x14077CA9C (moved)

**Battle Navigation Confirmed**
- **Formation screen**: Enter places unit, Space→Enter starts battle
- **Action menu**: Move(0), Abilities(1), Wait(2), Status(3), AutoBattle(4)
- **Attack sequence**: Abilities→Enter→Enter(Attack)→direction→Enter→Enter(confirm)
- **Wait sequence**: Navigate to menu=2, Enter, Enter (confirm facing)
- **Pause menu**: Tab key opens it; has Units/Retry/Load/Settings/Return to World Map/Return to Title

**Combat Achievements**
- Successfully automated 5 consecutive Wait turns in Mt. Germinas
- Killed an enemy unit via Attack targeting
- Executed full Attack+Wait turn sequences
- Navigated to Siedge Weald and Mandalia Plains via travel list
- Auto-fled encounters during travel

**PSX Battle Stats Reference**
- Full PSX field layout documented in docs/playtime/BATTLE_STATS_PSX_REFERENCE.md
- 0x1C0 (448 bytes) per unit on PSX
- Includes all fields: Identity, Job, Team, Equipment, Stats, Position (X/Y), CT, Statuses, Abilities, etc.

**Helper Script (/tmp/fft.sh)**
Bash helper with shortcuts: `enter`, `esc`, `up`, `down`, `left`, `right`, `space`, `tab`, `tkey`, `ekey`, `state`. Every command prints `[ScreenName] loc=X hover=Y ui=Z menu=W`.

### State Machine Improvements (Session 2, 2026-04-02)

**Screen Detection Updates**
- Added `Battle_Paused` state (0x140C64A5C == 1)
- Added `Battle_Acting` state (team=0, act=1 or mov=1) for submenus/targeting
- Fixed battle detection: now requires `location >= 255` to avoid false positives on world map
- `fft.sh` helper script with timeouts, delays, and `block()` for efficient memory reads
- Every bridge response includes screen state — no more guessing

**Confirmed Battle States**
- `Battle_MyTurn` — action menu open (team=0, act=0, mov=0)
- `Battle_Acting` — in submenu/targeting (team=0, act=1 or mov=1)
- `Battle_Paused` — Tab pause menu (0x140C64A5C=1)
- `Battle` — enemy turn or other

**Action Menu Cursor Verified**
- 0x1407FC620 works correctly: 0=Move, 1=Abilities, 2=Wait, 3=Status, 4=AutoBattle
- After acting, Abilities is grayed but cursor still passes through it (not skipped)

**Combat Execution Pattern (Proven)**
```
1. Abilities(down→enter) → Attack(enter) → direction(left/right/up/down) → select(enter) → confirm(enter)
2. Wait(down×2→enter) → facing(enter)
```

**Remaining Unsolved**
- Enemy/unit positions: X=12,Y=10 for Ramza not found in memory scans (searched 0x14077CA, 0x14077D, 0x141020 regions)
- Turn detection false positive: Battle_MyTurn shows during enemy turns (fixed partially with Battle_Acting)
- Game over detection: still reads as [Battle]
- Travel list tab identification: no reliable memory flag, use hover ID range

### Critical Blockers for Battle Automation (NEXT SESSION)

**1. Turn Detection is Broken**
- Current check: team=0 AND act=0 AND mov=0 → "Battle_MyTurn"
- Problem: These values read the SAME during enemy turns as during player turns
- Need: Find an address that's truly different when it's our turn vs not
- Approach: Take snapshots during confirmed "my turn" (action menu visible) vs confirmed "enemy turn" (enemies moving), diff to find the distinguishing flag

**2. Game Over Not Detected**
- Game over screen reads as [Battle] — same as active battle
- Need: Find a flag that changes on game over (Ramza HP=0? Death counter? A specific game state byte?)
- Approach: Read Ramza's HP from condensed struct. If HP<=0 and we're in "battle", it might be game over. Or diff the game over screen vs active battle.

**3. Enemy Positions Unknown**
- Can't target attacks without knowing where enemies are
- Attacking empty tiles wastes turns and leads to death
- Need: Read all unit X/Y positions simultaneously during battle
- Approach options:
  a. Find the full PSX-style battle struct in the remaster (position at PSX +0x47/+0x48)
  b. Use the movement tile list (only available during Move mode) to infer nearby units
  c. Cycle through attack target tiles and check if each has a unit on it
  d. Search for unit X/Y parallel arrays (earlier found candidates at 0x141025xxx)

### Still TODO (from before)
- Decode learned abilities bitfield (0x30–0x70) for instant ability list reading
- Finish equipping Lloyd and William (dream team)
- Equipment item ID mapping (same approach as abilities)
- World map adjacency graph (which nodes connect to which)
- Item ID memory mapping (currently using static position mapping from SHOP_ITEMS.md)
- Map other settlement inventories (may differ from late-game Sal Ghidos)
