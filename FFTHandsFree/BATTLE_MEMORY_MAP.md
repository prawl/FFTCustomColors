<!-- This file should not be longer than 200 lines, if so prune me. -->
# Battle Memory Map (IC Remaster)

## Formation Screen
- **Space bar** opens "Commence Battle?" confirmation
- **Right arrow** moves unit placement cursor, **Enter** places a unit
- Battle stat arrays are NOT populated until battle starts

## 1. Roster Array (0x1411A18D0, stride 0x258)
Readable during AND outside battle. 50 slots. Key fields: spriteSet +0x00, job +0x02, secondary +0x07, reaction +0x08, support +0x0A, movement +0x0C, equipment u16 LE slots +0x0E..+0x1A (helm / body / acc / R-hand / L-hand / reserved / shield — FFTPatcher-canonical IDs, 0xFF = empty), exp +0x1C, level +0x1D, brave +0x1E, faith +0x1F, nameId +0x230. Empty slot = `unitIndex != 0xFF AND level > 0`. HP/MP are NOT here (runtime-computed, see §19). See UNIT_DATA_STRUCTURE.md and §18 for equipment details.

## 2. Condensed Battle Struct (0x14077D2A0, variable stride)
Rolling turn-order queue. Slot 0 = current unit. Updates in real-time (HP changes visible). Each entry followed by FFFF-terminated ability list (stride varies per unit).

| Offset | Field | Notes |
|--------|-------|-------|
| +0x00 | level | uint16 |
| +0x02 | team | 0=friendly, 1=enemy |
| +0x04 | nameId | uint16 |
| +0x08 | exp | uint16 |
| +0x0A | CT? | uint16 |
| +0x0C | HP | uint16 |
| +0x10 | maxHP | uint16 |
| +0x12 | MP | uint16 |
| +0x16 | maxMP | uint16 |
| +0x18 | PA? | uint16 |
| +0x1A | MA? | uint16 |
| +0x28+ | ability list | uint16[], FFFF-terminated |

## 3. Battle HP/MP Arrays
| Array | Address | Type |
|-------|---------|------|
| Current HP | 0x141024EC8 | uint32 x 21 |
| Current MP | 0x14102223C | uint32 x ~21 |
| Unit Existence | 0x14077CA30 | uint32 x 10 (0xFF=exists, 0xFFFFFFFF=terminator) |

## 4. Turn Region (0x14077CA60)
| Offset | Field |
|--------|-------|
| +0x60 | CT of acting unit |
| +0x6C | Job of acting unit |
| +0x8C | Action Taken flag (0→1) |
| +0x9C | Movement Taken flag (0→1) |

## 5. Action Menu & Navigation
- **Menu cursor**: `0x1407FC620` (byte, 0-4): Move=0, Abilities=1, Wait=2, Status=3, AutoBattle=4
- **Turn detection**: Team=0 AND Act=0 AND Mov=0 → friendly turn, action menu open
- **Wait sequence**: Down to cursor=2, Enter (select), Enter (confirm facing)

## 6. Movement Tile List (0x140C66315, 7 bytes/entry)
| Offset | Field |
|--------|-------|
| +0x00 | X coordinate |
| +0x01 | Y coordinate |
| +0x02 | Elevation (raw/2 = display height) |
| +0x03 | Flag (1=valid, 0=terminator) |

Entry[0] = active unit's current position. Cursor index at `0x140C64E7C`.

## 7. UI Buffer (0x1407AC7C0)
| Offset | Field |
|--------|-------|
| +0x00 | Level |
| +0x04 | NameId |
| +0x08 | Exp |
| +0x0A | CT |
| +0x0C | HP (stale) |
| +0x10 | MaxHP |
| +0x12 | MP |
| +0x16 | MaxMP |
| +0x18 | PA? |
| +0x24 | Move |
| +0x26 | Jump |
| +0x2A | Job |
| +0x2C | Brave |
| +0x2E | Faith |

## 8. Unit Position & Stats (Static Battle Array)

**Static Battle Array (BEST METHOD — no input needed):**
Per-unit slots at stride 0x200. Player units at `BattleArrayBase + n*0x200` (n≥1), enemies at negative offsets (up to -20 slots). Base: `0x140893C00`. Filter active units: `+0x12 == 1`.

| Offset | Size | Field | Verified |
|--------|------|-------|----------|
| +0x0C | byte | Exp | ✓ |
| +0x0D | byte | Level | ✓ |
| +0x0E | byte | origBrave | ✓ |
| +0x0F | byte | brave (in-battle) | ✓ |
| +0x10 | byte | origFaith | ✓ |
| +0x11 | byte | faith (in-battle) | ✓ |
| +0x12 | u16 | inBattleFlag (1=active, 0=stale) | ✓ |
| +0x14 | u16 | HP | ✓ |
| +0x16 | u16 | MaxHP | ✓ |
| +0x18 | u16 | MP | ✓ |
| +0x1A | u16 | MaxMP | ✓ |
| +0x22 | byte | PA (total, with equipment) | ✓ |
| +0x23 | byte | MA (total, with equipment) | ✓ |
| +0x24 | byte | Speed | ✓ |
| +0x25 | byte | CT | ✓ |
| +0x26 | byte | PA (raw, without equipment) | ✓ |
| +0x27 | byte | MA (raw, without equipment) | ✓ |
| +0x28 | byte | WP (right hand weapon power) | ✓ |
| +0x2E | byte | C-EV % (class evasion) | ✓ |
| +0x32 | byte | S-EV % (shield evasion) | ✓ |
| +0x33 | byte | Grid X | ✓ 10/10 |
| +0x34 | byte | Grid Y | ✓ 10/10 |
| +0x45 | 5 bytes | Status effects bitfield | ✓ |

Move/Jump NOT in this array — only available from UI buffer for active unit.

**Condensed Struct (active unit only):**
X at 0x14077D360, Y at 0x14077D362 — world coords, not grid coords.

## 9. Movement Tile Validity (Map-Based BFS)

**Map BFS (primary, 100% accurate):**
- Load map via `set_map <number>`, files in `claude_bridge/maps/MAP###.json`
- Grid coords = map tile coords (identity mapping)
- Height: `display = height + slope_height / 2`, jump check: `|heightA - heightB| <= jump`
- `no_walk` = impassable, enemy tiles block movement
- **Verified 6/6 tile counts match in-game exactly**

**`scan_move` action:** Scans units via static array, computes valid tiles via BFS, returns `ValidMoveTiles`.
Usage: `scan_move <move> <jump>`

**Known issue:** Move/Jump only available from UI buffer for active unit (base values, no equipment modifiers).

**Fallback BFS (no map loaded):** Terrain grid at 0x140C65000, approximate heights, ~12 false positives.

## 10. Tile Height
| Address | What | Encoding |
|---|---|---|
| 0x140C6492C | Cursor tile height × 10 | uint32, 25 = display 2.5 |
| 0x14077CA5C | Move mode flag | 0xFF = Move mode |

## 11. Map File Data (fft-map-json)
Source: `c:/Users/ptyRa/Dev/fft-map-json/data/MAP###.json`, deploy to `claude_bridge/maps/`. 122 maps available.
Tile fields: x, y, height, slope_height, depth, no_walk, no_cursor, surface_type, slope_type.
Maps have `lower`/`upper` levels (lower is primary) and `starting_locations` for team spawns.
Story battles and random encounters at same location use DIFFERENT maps. Terrain fingerprinting uniquely identifies all 122.

## 12. Screen Detection

### Current implementation (has known bugs — see detection_audit.md)
See `ColorMod/GameBridge/ScreenDetectionLogic.cs`. Current rules:
```
inBattle = (slot0==255 AND slot9==0xFFFFFFFF) OR (slot9==0xFFFFFFFF AND battleMode in {2,3,4})
Battle_MyTurn = inBattle AND team==0 AND act==0 AND mov==0
Battle_Moving = inBattle AND battleMode==2
Battle_Attacking = inBattle AND battleMode==4
Battle_Abilities = inBattle AND submenuFlag==1 AND battleMode==3 AND team==0 AND (act==1 OR mov==1)
Battle_Paused = inBattle AND paused==1
GameOver = inBattle AND paused==1 AND battleMode==0 AND gameOverFlag==1
EncounterDialog = encA(0x140900824) != encB(0x140900828)
PartyMenu = partyFlag(0x140D3A41E)==1
TravelList = uiFlag(0x140D4A264)==1
TitleScreen = location(0x14077D208)==255
```

### Address reliability (verified 2026-04-14, 45-sample audit)

**RELIABLE:**
- `party` — 1 in party menu / status screen, 0 otherwise
- `menuDepth` at **`0x14077CB67`** — byte, 0 on outer party-menu-tree screens (WorldMap / PartyMenu / CharacterStatus), **2** on inner panels (EquipmentAndAbilities / ability picker). Discovered 2026-04-14 session 13 via module-memory snapshot diff with oscillating UI states. Verified stable across repeated reads in every state tested (CS↔EqA↔picker). Primary use: **drift-check for the state machine** — if SM thinks we're on an inner panel but this reads 0 for N consecutive reads, snap back to CharacterStatus. Doesn't distinguish EqA from specific pickers (both read 2), nor WorldMap from PartyMenu from CharacterStatus (all three read 0); pair with `party` + `ui` for a full screen name.
- `rawLocation` — **0-42 means AT a named location (village/shop/campaign ground), 255 means "unspecified"**. NOT "in battle" as the code comment suggests.
- `paused` — 1=pause menu open
- `slot0 == 0xFFFFFFFF` — fresh process, formation, some dialogue transitions
- `slot0 == 0x000000FF` — in-battle or stale-in-battle residue
- `battleTeam` — 0=player, 1=enemy, 2=NPC/uninit

**OVERLOADED / CONTEXT-DEPENDENT:**
- `menuCursor` at 0x1407FC620 — meaning changes based on context:
  - Action menu (submenuFlag=0): 0=Move, 1=Abilities, 2=Wait, 3=Status, 4=AutoBattle
  - Abilities submenu (submenuFlag=1): skillset/ability index (can exceed 4)
  - Targeting (battleMode=1/4/5): target list index
  - Pause menu: pause item index
  - Enemy turn: enemy-side cursor — do not interpret for player UI
- `battleMode` at 0x140900650 — encodes (submode × cursor-tile-class):
  - During Move: 2=cursor on unit's tile, 1=cursor on other tile
  - During basic Attack: 4=on valid target, 1=on invalid tile
  - During Cast: 5=on caster's tile, 4=on valid target, 1=on invalid
  - Top-level menu: 3
  - Paused: 0
  - Fresh process: 255
  - Formation: 1 (different meaning entirely)
- `battleActed` / `battleMoved` at 0x14077CA8C / 0x14077CA9C — turn-state flags, BUT also flip to 1 temporarily during some pause subscreens (e.g., Status). Use with care.

**UNRELIABLE — DO NOT USE:**
- `encA / encB` at 0x140900824 / 0x140900828 — counters that drift independently and re-sync. Every rule using `encA != encB` is a coincidence-detector. Observed flipping between `2/2`, `3/3`, `5/5`, `8/8`, `10/10`, `11/11`, sometimes `6/5` briefly mid-screen.
- `gameOverFlag` (aliased with `submenuFlag` at 0x140D3A10C) — **STICKY across process lifetime once GameOver fires**. Rules requiring `gameOverFlag==0` will fail forever after the first game-over.

**UNDER-UTILIZED:**
- `eventId` at 0x14077CA94 — range-dependent:
  - `0xFFFF (65535)` = unset
  - `1-399` = real story event ID (cutscene/dialogue active)
  - `400+` = aliased active-unit nameId (not a real event)
  - Current rule filters `eventId < 200` which misses real events in 200-399 range (e.g. Orbonne pre-battle dialogue at eventId=302)
- `hover` at 0x140787A22 — read in DetectScreen but NOT passed to ScreenDetectionLogic.

### Known bugs (from 45-sample audit 2026-04-14)

See `detection_audit.md` in repo root for full sample-by-sample data. Summary:

1. **`Battle_AutoBattle` rule never fires correctly** — demands `submenuFlag==1` but real top-level AutoBattle hover has `submenuFlag=0`. Fires SPURIOUSLY in Abilities submenu when cursor lands at skillset index 4. ROOT CAUSE of "Auto-Battle instead of Wait" bug.
2. **`Battle_Casting` cannot be detected from memory** — cast-time and instant targeting produce byte-identical 18-input snapshots. Queued-vs-instant is a property of the ability, not the screen.
3. **`WorldMap` rule unreachable** — requires `party==0 && ui==0` but actual world map has `party=0, ui=1`.
4. **Multiple world-side screens byte-identical** — WorldMap, TravelList, PartyMenu, EncounterDialog, TitleScreen, LoadGame all fall into `rawLocation=255` catch-all. Rule ordering preempts them.
5. **Shop types indistinguishable** — Outfitters, Warrior Guild, Poachers' Den, Save Game, Tavern all byte-identical at `rawLocation=0-42` (split only by `ui` which is coarse).
6. **Two distinct `TitleScreen` states** — fresh-process (all uninit sentinels) vs post-GameOver (stale battle residue). Current rule catches only the first.
7. **No rule for `LoadGame` / `LocationMenu` / `Battle_ChooseLocation`** — these states fall through to wrong detections.
8. **`Battle_Dialogue` / `Cutscene` filter too tight** — `eventId < 200` misses real events like 302 (pre-battle Orbonne).
9. **`Battle_Victory` / `Battle_Desertion` rules depend on `encA vs encB`** — coincidence-dependent, unreliable.

Fix work tracked in TODO §12 "Screen Detection Rewrite".

## 13. Attack Sequence
```
1. Down to menu=1 (Abilities), Enter
2. Enter (select Attack)
3. Direction key to target tile with enemy
4. Enter (select target), Enter (confirm attack)
5. Navigate to Wait (menu=2), Enter, Enter (confirm facing)
```

## 14. Quick Reference
| Question | How |
|----------|-----|
| Enemy count | Count Team=1 in turn queue |
| Whose turn | Turn queue slot 0 at 0x14077D2A0 |
| Unit alive? | HP > 0 at condensed struct +0x0C |
| Valid move tiles | scan_move or BFS from map JSON |
| Total units | Existence slots at 0x14077CA30 |

## 15. IC Remaster Roster Job IDs
The IC remaster uses different job IDs than PSX in the roster (+0x02). Verified values:
| ID | Job | ID | Job |
|----|-----|----|-----|
| 74 | Squire | 82 | Summoner* |
| 75 | Chemist* | 83 | Thief* |
| 76 | White Mage* | 84 | Orator* |
| 77 | Archer | 85 | Mystic* |
| 78 | Monk | 86 | Geomancer* |
| 79 | Knight | 87 | Dragoon* |
| 80 | Black Mage* | 88 | Samurai* |
| 81 | Time Mage* | 89 | Ninja |
*= estimated, not yet verified in-game

## 16. Heap-Only Structures (dynamic addresses)

These tables live in heap-allocated UE4 memory and move between sessions. Find them at runtime via anchor signature search.

### Class Fingerprints (per-unit, `+0x69` from struct base)
Every battle unit has an 11-byte fingerprint in its heap unit struct at offset +0x69. **Bytes 1-10** are the stable class signature (byte 0 varies per unit). This is the ONLY reliable enemy class identifier. See `CLASS_FINGERPRINTS.md` for full details, family patterns, and known collision handling.

To find a unit struct: search for its `HP MaxHP` u16 pair, subtract 0x10. Search range: `0x4000000000..0x4200000000`.

### Roster Name Display Table (per-slot, stride 0x280)
Player character names (including generic recruits like "Kenrick" or "Lloyd") live in per-slot records separate from the roster array. Each record is 0x280 bytes; the displayed name is a null-terminated ASCII string at **+0x10 inside each record**. Anchor signature: `Ramza\0Delita\0Argath\0Zalbaag\0Dycedarg\0Larg\0Goltanna\0Ovelia\0Orland\0` (note "Orland" not "Orlandeau"). See `UNIT_DATA_STRUCTURE.md` → "Roster Name Display Table".

### Passive Ability Bitfields (per-unit, from heap struct base)
Enemy units store equipped reaction/support/movement abilities as bitfields in the heap unit struct. These are the ONLY source for enemy passive abilities — enemies don't have roster slots.

| Field | Offset | Size | Base ID | Notes |
|-------|--------|------|---------|-------|
| Reaction | +0x74 | 4 bytes | 166 | MSB-first. Parry=bit 25, Counter Tackle=bit 14, Gil Snapper=bit 17 |
| Support | +0x78 | 5 bytes | 198 | MSB-first. Equip Swords=bit 2, Evasive Stance=bit 25 |
| Movement | +0x7D | 3 bytes | TBD | MSB-first. Base not yet verified with known equipped ability |

Bit decoding (MSB-first): position = `N - B`, byte = `pos/8`, bit = `7 - (pos%8)`. These are EQUIPPED bits (1 per field). Player units use roster instead.

### Gil (static address confirmed)
`0x140D39CD0` — u32 LE. Survives restarts, updates in real-time. Read with `read_address size=4`.

## 17. Inventory — investigation paused
Not located in memory. See `memory/project_inventory_investigation.md` for full dossier. Next step: scrape Items menu while rendered (Option C).

## 18. Per-Unit Equipment (static, solved 2026-04-14)
Equipped items for every party member live in the roster array at
`0x1411A18D0 + slot*0x258 + 0x0E..+0x1B`. Seven u16 LE slots:

- +0x0E helm / +0x10 body / +0x12 accessory / +0x14 right-hand /
  +0x16 left-hand / +0x18 reserved / +0x1A shield

IDs use the **FFTPatcher canonical encoding** (0-315) — direct lookup
into `ItemData.Items` produces the displayed item name with no translation
table. Verified via Ramza (Ragnarok=36, Escutcheon=143, Grand Helm=156,
Maximillian=185, Bracer=218), Kenrick (Chaos Blade=37, Kaiser Shield=141,
Crystal Helm=154, Crystal Mail=182), and Mustadio (Mythril Gun=72, Gold
Hairpin=166, Jujitsu Gi=194, Hermes Shoes=213). `0xFF` / `0xFFFF` = empty.

Historical note: the `ItemData.cs` header previously claimed a "Game !=
FFTPatcher" ID discrepancy. That was based on a wrong slot-offset
assumption (reading the 3 non-equipment u16 bytes at +0x08..+0x0D as
item IDs). The comment has been corrected — no translation needed.

See `ColorMod/GameBridge/EquipmentReader.cs` and
`ColorMod/GameBridge/RosterReader.cs::ReadLoadout()`.

## 19. Hovered-Unit Heap Mirror (partial, heap)
A 0x200-stride heap array appears at a session-specific address (observed
`0x4166C2E400` in one session) that contains a **runtime-computed mirror**
of some units' condensed stats. Found via AoB scan for Ramza's 20-byte
equipment signature combined with a discriminator byte pair `0x90 0x03`
at struct+0x06..+0x07 (roster backup copies carry `0x90 0x06` instead).

Struct layout per entry:
- +0x14..+0x19: three non-equipment u16s (stat-max components?)
- +0x1A..+0x27: 7 u16 equipment IDs (mirror of roster +0x0E)
- +0x2A..+0x2D: `brave brave faith faith` (each byte doubled)
- +0x30..+0x33: HP / MaxHP (u16 LE each) — the **runtime-computed values
  shown on the unit card**, not stored in the roster
- +0x34..+0x37: MP / MaxMP (u16 LE each)

**What it's good for:** reading live HP/MP for the handful of units
whose struct has been populated — typically Ramza + the first few party
members and whichever unit's CharacterStatus has been opened.

**What it's NOT good for:** reading HP/MP for every party member. The
array is not a complete roster mirror — mid-roster story characters
(Mustadio, Reis, Cloud, Rapha, etc.) are absent even when they're in
the active party. Verified 2026-04-14: Mustadio's `brave brave faith
faith` signature `3C 3C 3E 3E` does not appear anywhere in the 0x200-
stride array.

**Next session options for full-party HP/MP:**
- (a) Find the widget struct that renders each unit's card (probably
  lazily populated as Claude cycles Q/E through CharacterStatus).
- (b) Recompute HP/MP from job base + equipment bonuses using FFTPatcher
  formulas + the ItemData fields we already have.
- (c) Walk the UE4 pointer chain from a stable exe-side static to the
  widget data — requires a DLL detour or Cheat Engine pointer scan.

See `ColorMod/GameBridge/HoveredUnitArray.cs` for the discovery +
read implementation.

## Still Unmapped
- Effective Move/Jump (not in static array, only UI buffer for active unit)
- Enemy display names, party item inventory (see §17), facing direction
- Live HP/MP for all party members outside battle (partial via §19)
- Dynamic shop stock arrays (see SHOP_ITEMS.md for investigation plan)
- See BATTLE_STATS_PSX_REFERENCE.md for complete PSX field layout
