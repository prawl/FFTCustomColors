# Session Progress — 2026-04-06

## What We Did

### 1. Memory Scan (30 agents across 3 waves)
- **Wave 1**: 10 agents discovered ~120 memory fields across 8 regions
- **Wave 2**: 10 agents verified within same session (56/60+ confirmed)
- **Wave 3**: 10 agents verified after full game restart (all static addresses survived)
- Full results in [docs/MEMORY_SCAN_WAVE1.md](docs/MEMORY_SCAN_WAVE1.md)

### 2. Battle State Reporting (ActiveUnit)
Added to every battle response:
- `nameId`, `ct`, `jobId` — from confirmed static addresses
- `brave`, `faith` — from roster lookup (permanent values, player units only)
- `reactionAbilityName`, `supportAbilityName`, `movementAbilityName` — with human-readable names
- `equipment` — 7 equipment slot IDs from roster
- `hoveredAction` — resolved menu item name ("Move", "Abilities", "Wait", "Status", "AutoBattle")

Removed after proving wrong:
- `speed` (condensed +0x06 = 6, real speed = 11 — NOT speed)
- `pa`, `ma` (condensed +0x18 = 100, real PA = 20 — NOT PA, likely CT gauge)
- `move`, `jump`, `moveModifier`, `jumpModifier` (from UI buffer = cursor-selected unit, NOT active unit)

### 3. Screen Detection Fixes
- **Battle_Moving/Targeting**: Now uses `moveMode == 255 || battleMode == 2` (was only battleMode==2, missed battleMode==1 state)
- **Location during battle**: Falls back to saved `_lastWorldMapLocation` when location reads 255
- **`hoveredAction`** replaces `menuCursor` — shows "Move" instead of 0

### 4. Character & Item Data Files
- `CharacterData.cs` — ~40 story characters + ~100 job/monster names, `GetDisplayName(nameId, jobId)`
- `ItemData.cs` — 293 items from FFTPatcher (IDs don't match IC remaster, need remapping)

### 5. Map Auto-Detection
- Added fallback: when `_lastWorldMapLocation` is unknown, uses `MapLoader.DetectMap()` to cross-reference unit positions against all 122 MAP files
- Fires on first battle screen detection (any battle screen, not just Battle_Moving)
- Movement ability bonus (Move+1/+2/+3) applied to base Move for BFS tile computation

### 6. Text Encoding Research
- Cracked FFT PSX encoding from FFTPatcher source: 0x0A-0x23 = A-Z, 0x24-0x3D = a-z, 0x00-0x09 = 0-9
- Location/item/character names stored in `battle_bin.en.bin` (compressed in 0002.en.pac), custom encoding
- 20 agents searched 0x140000000-0x141400000 + heap ranges — text NOT in static module memory
- Game text lives in PAGE_READONLY heap memory loaded from compressed game files

## Pitfalls Found During Battle Play

1. **battleMode=1 not recognized** — game was in Move mode but we detected Battle_MyTurn. Fixed with moveMode fallback.
2. **acted/moved flags reset** between turn phases — after acting, if you enter move-after-act, acted reads 0 again
3. **MoveToEnemy silently fails** without a loaded MAP file
4. **Keys sent during unrecognized state** get processed by game — accidentally toggled AutoBattle
5. **UI buffer shows cursor-selected unit** not active unit — all UI buffer stats were wrong
6. **Condensed nameId is sequential** (1,2,3...) not a character identifier — "Ramza" name showed for Kenrick
7. **Condensed header values misidentified** — +0x18 is NOT PA (it's 100 = CT gauge), +0x06 is NOT Speed
8. **Item IDs don't match FFTPatcher** — IC remaster uses different numbering, no formula found
9. **Map auto-detection picked wrong map** (MAP007 instead of MAP074) without location hint
10. **Tile count too low** — BFS used base Move=4 instead of effective Move=7 (Movement+3 not applied)

## Still TODO

### Critical (blocks gameplay)
- [ ] Fix location persistence — `last_location.txt` never created when going save→battle
- [ ] Effective stats (Speed, PA, MA, Move, Jump) — only in heap struct, needs reliable scanning
- [ ] Roster-to-battle matching — condensedNameId is useless, need equipment-based matching

### Important
- [ ] Generic character names — can't read from memory (custom encoding, compressed)
- [ ] Item ID remapping — need more data points or extract from game files
- [ ] Verify map detection picks correct MAP for each location
- [ ] Game over flag verification — 0x140D3A10C candidate, untested

### Nice to Have  
- [ ] Turn order prediction from CT table at 0x14077D868
- [ ] Status effect reading from heap struct +0x50-0x5F
- [ ] Facing direction from heap struct +0x39
