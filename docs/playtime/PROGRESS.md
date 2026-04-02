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

### Still TODO
- Decode learned abilities bitfield (0x30–0x70) for instant ability list reading
- Finish equipping Lloyd and William (dream team)
- Equipment item ID mapping (same approach as abilities)
- Battle automation (CT tracking, action selection, targeting)
- World map adjacency graph (which nodes connect to which)
- Item ID memory mapping (currently using static position mapping from SHOP_ITEMS.md)
- Map other settlement inventories (may differ from late-game Sal Ghidos)
