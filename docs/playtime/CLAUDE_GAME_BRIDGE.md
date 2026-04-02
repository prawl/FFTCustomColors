# Claude Game Bridge — Automation Reference

## Overview

This mod includes a bridge that lets Claude (AI in CLI) control and read state from
FFT: The Ivalice Chronicles. Claude sends key presses via file protocol and reads
game memory to understand what's happening — no screenshots needed.

## Autonomous Loop

```
1. Kill game:     taskkill //IM FFT_enhanced.exe //F
2. Build:         dotnet build ColorMod/FFTColorCustomizer.csproj -c Release
3. Deploy:        powershell.exe -ExecutionPolicy Bypass -File ./BuildLinked.ps1
4. Launch:        "c:/program files (x86)/steam/steamapps/common/FINAL FANTASY
                   TACTICS - The Ivalice Chronicles/Reloaded/Reloaded-II.exe"
                   --launch "C:\Program Files (x86)\Steam\steamapps\common\FINAL
                   FANTASY TACTICS - The Ivalice Chronicles\FFT_enhanced.exe"
5. Wait:          Poll for state.json (~3-10s)
6. Boot:          Enter (3s delay) Enter (3s delay) Enter — loads save to world map
7. Play:          Send commands, read state, navigate game
8. Repeat from 1 when code changes needed
```

## File Bridge Protocol

Bridge directory: `{modPath}/claude_bridge/`
Mod path: `c:\program files (x86)\steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\Reloaded\Mods\FFTColorCustomizer\`

### Commands (Claude writes command.json)

```json
// Send key presses
{ "id": "x", "keys": [{"vk": 13, "name": "ENTER"}], "delayBetweenMs": 150 }

// Dump unit hex block (600 bytes)
{ "id": "x", "action": "dump_unit", "slot": 0 }

// Search memory near unit data
{ "id": "x", "action": "search_near", "searchValue": 741, "searchLabel": "hp" }

// Take memory snapshot (for differential scanning)
{ "id": "x", "action": "snapshot", "searchLabel": "before" }

// Diff two snapshots
{ "id": "x", "action": "diff", "fromLabel": "before", "toLabel": "after", "searchLabel": "result" }

// Force state report
{ "id": "x", "action": "report_state" }
```

### Responses (mod writes)

- `response.json` — command result (status, key results)
- `state.json` — auto-updated every 2s with roster data
- `hexdump_slot_N.txt` — raw hex dumps
- `search_near_*.txt` — memory search results
- `diff_*.txt` — differential scan results

## VK Key Codes

| Key | Code | FFT Action |
|-----|------|-----------|
| Enter | 13 | Confirm/Select |
| Escape | 27 | Cancel/Back |
| Up | 38 | Cursor up |
| Down | 40 | Cursor down |
| Left | 37 | Cursor left |
| Right | 39 | Cursor right |
| Q | 81 | Previous tab (party menu) |
| E | 69 | Next tab (party menu) |
| T | 84 | Job Abilities (job screen) |
| Y | 89 | Job Summary (job screen) |

---

## Game Navigation Map

### Screen Flow
```
Title Screen
  → Enter → Cutscene → Enter → Continue → Enter → World Map

World Map
  ← Escape → Party Menu (toggle)
  → Arrow keys: move between map nodes
  → Enter on node: travel / enter battle / enter town
  → T: open Travel List (location picker)

Travel List (T from world map)
  Tabs: Q=prev, E=next → [Settlements] [Battlegrounds] [Miscellaneous]
  Up/Down: scroll through locations (circular, wraps around)
  Enter: move world map cursor to selected location (does NOT travel)
  Escape: close travel list
  After Enter, press Enter again on world map to actually travel to location
  T menu remembers last-used tab between opens
  → T: open Travel List (location picker)

Travel List (T from world map)
  Tabs: Q=prev, E=next → [Settlements] [Battlegrounds] [Miscellaneous]
  Up/Down: scroll through locations (circular, wraps around)
  Enter: move world map cursor to selected location (does NOT travel)
  Escape: close travel list
  After Enter, press Enter again on world map to actually travel to the location

Party Menu (Escape from world map)
  Tabs: Q=prev, E=next → [Units] [Inventory] [Chronicle] [Options]

  Units Tab (default):
    Character grid (5 columns × 3+ rows)
    Arrow keys navigate grid, Enter opens character status
    Info panel at bottom shows selected unit's stats

  Character Status Screen (Units > Status):
    FOCUS STARTS ON LEFT SIDEBAR (not content area)
    Left sidebar is a COLUMN (Up/Down to navigate, Enter to select):
      1. Equipment & Abilities (default) — opens equipment/ability grid
      2. Job — opens job selection grid
      3. Combat Sets — preset loadouts
    Escape: back to character grid

  Equipment & Abilities Screen (Units > Status > Equipment):
    2-column grid (5 rows):
      Left column: Equipment slots (Weapon, Shield, Helm, Armor, Accessory)
      Right column: Ability slots (Primary, Secondary, Reaction, Support, Movement)
    Arrow keys navigate grid, Enter opens selection list for that slot
    Item selection list:
      - Currently equipped item appears at top with indicator
      - Enter on equipped item = UNEQUIP (removes it, stays in list)
      - Enter on different item = EQUIP that item
      - List is circular (wraps around)
      - Escape = close list without changing
    Escape from equipment grid = back to sidebar
    
  Job Screen (Units > Status > Jobs):
    GENERIC JOB GRID (6 columns × ~3 rows):
      Row 0: Squire, Chemist, Knight, Archer, Monk, White Mage
      Row 1: Black Mage, Time Mage, Summoner, Thief, Orator, Mystic
      Row 2: Geomancer, Dragoon, Samurai, Ninja, Arithmetician, Bard, Mime

    RAMZA'S JOB GRID (has 2 extra special jobs at start):
      Row 0: Dark Knight, Golden Knight, Squire, Chemist, Knight, Archer, Monk, White Mage
      Row 1: Black Mage, Time Mage, Summoner, Thief, Orator, Mystic
      Row 2: Geomancer, Dragoon, Samurai, Ninja, Arithmetician, Bard, Mime
    
    NOTE: Other story characters may also have unique job grids with extra jobs.

    Cursor STARTS ON the character's CURRENT JOB in the grid.
    
    Enter on a job → intermediate menu (HORIZONTAL, Left/Right navigation):
      Left: "Learn Abilities" (default)
      Right: "Change Job"
      Enter confirms selection
    
    To change job: Enter (select job) → Right (Change Job) → Enter (confirm)
    
    T = Job Abilities view
    Y = Job Summary view
    Escape = back to sidebar

  Inventory Tab:
    Item list with categories
    Shows item description on right panel

  Options Tab:
    Save, Load, Achievements, Settings, Return to Title, Exit Game

Settlement Menu (Enter on a settlement node):
  1-column, 3-4 rows:
    Outfitter (default)
    Tavern
    Warriors' Guild
    Poachers' Den (some settlements only)
  Enter = select, Escape = back to world map

  Outfitter (Settlement > Outfitter):
    1-column, 3 rows:
      Buy (default)
      Sell
      Fitting
    Enter opens selected option, Escape = back + merchant dialog "I await your next visit" (Enter to dismiss)

    Buy Screen (Outfitter > Buy):
      Category tabs at top: A/D or Left/Right to switch (Weapons, Shields, Helmets, Armor, Accessories, Items)
      Item list: Up/Down to browse, shows Name, Equipped/Held count, Purchase Price
      Right panel shows selected item stats
      Gil displayed top-right, inventory count (x/50) below it
      Enter on item → Quantity popup:
        Up/Down to change quantity (default 1, max 99 per item type)
        Enter → Buy/Cancel confirmation:
          Buy (default, top)
          Cancel (bottom)
          Enter = confirm purchase
      Escape = back to Buy/Sell/Fitting menu
```

### Party Menu Character Grid Layout
```
Row 0: Ramza(0), Kenrick(1), Lloyd(2), William(3), Alicia(4)
Row 1: Lavian(5), Mustadio(6), Agrias(7), Rapha(8), Marach(9)
Row 2: ???(10), Orlandeau(11), Meliadoul(12), Reis(13), ???(14)
Row 3: Cloud(15), ???(16), ...
```
Grid index = row * 5 + col. Cursor starts at Ramza (0,0) when opening menu.
Grid order does NOT match roster slot order.

### Key Learnings / Gotchas
- When entering status screen, focus is on SIDEBAR, not content area
- Sidebar is a vertical column: Down/Up to move, Enter to select
- Equipment screen: Left/Right moves between equipment and ability columns
- Job change intermediate menu is HORIZONTAL (Left=Learn, Right=Change Job)
- Ramza has 2 extra special jobs (Dark Knight, Golden Knight) at the START of his
  job grid, shifting all standard jobs right by 2 positions
- Job grid cursor starts on the character's CURRENT job, not at (0,0)
- To safely navigate job grid: use Up+Left repeatedly to reach top-left corner,
  then count Right to target job
- Equipment item list is circular with no "Nothing" entry — unequip by selecting
  the currently equipped item

---

## Memory Layout

### Roster Array (found via AoB every launch — address is stable)
```
AoB: 48 8D 05 ?? ?? ?? ?? 48 03 C8 74 ?? 8B 43 ?? F2 0F 10 43 ??
     F2 0F 11 41 ?? 89 41 ?? 0F B7 43 ?? 66 89 41
Resolution: patternAddr + 7 + *(int*)(patternAddr + 3)
```
- 55 slots × 0x258 (600) bytes each
- Confirmed base: 0x1411A18D0 (consistent across launches)

**Roster field offsets:**
| Offset | Size | Field |
|--------|------|-------|
| +0x00 | byte | spriteSet (character identity) |
| +0x01 | byte | unitIndex (0xFF = empty) |
| +0x02 | byte | job ID |
| +0x07 | byte | secondary ability (index into character's unlocked ability list) |
| +0x08 | byte | reaction ability (ability ID) |
| +0x0A | byte | support ability (ability ID) |
| +0x0C | byte | movement ability (ability ID) |
| +0x1C | byte | experience (0-99) |
| +0x1D | byte | level (1-99) |
| +0x1E | byte | brave |
| +0x1F | byte | faith |
| +0x074 | nibbles | job levels (packed) |
| +0x230 | uint16 | nameId (indexes CharaName NXD table) |

Note: HP/MP are NOT in the roster. They're calculated at runtime from raw stats + equipment + job multipliers.

### UI State Buffer (found via differential scanning)

**Address: 0x1407AC7CA** (within game's main module, stable across sessions)

This buffer contains the currently displayed unit's info in the party menu.

| Offset | Size | Field | Verified |
|--------|------|-------|----------|
| +0x00 | byte | Cursor grid index | ✓ (0=Ramza, 2=Lloyd) |
| +0x02 | uint16 | Current HP | ✓ (741 Ramza, 826 Lloyd) |
| +0x06 | uint16 | Max HP | ✓ |
| +0x08 | byte | Current MP | ✓ (183 Ramza, 72 Lloyd) |
| +0x0C | byte | Max MP | ✓ |
| +0x1E | byte | Cursor index (duplicate) | ✓ |
| +0x20 | byte | Job ID | ✓ (160 Ramza, 87 Lloyd) |
| +0x22 | byte | Brave | ✓ (94 Ramza, 87 Lloyd) |
| +0x24 | byte | Faith | ✓ (75 Ramza, 41 Lloyd) |
| +0x28 | byte | Cursor index (third copy) | ✓ |

**How to verify navigation:** Read byte at 0x1407AC7CA. Value = cursor grid index.
Move Right increases by 1, Down increases by 5 (grid is 5 columns).

### World Map State

**Current Location (where you ARE): `0x14077D208`** — byte, location ID
**Hover Cursor (what cursor is on): `0x140787A22`** — byte, location ID (0xFF when no hover)

Multiple mirrors of current location also exist at: 0x140D39CE4, 0x140D42F98, 0x140D43274, 0x1411A0FA8, 0x1411A1024

**Story Objective Location: `0x1411A0FB6`** — byte, location ID of the yellow diamond marker (next story destination)

**Gil (party money): `0x140D39CD0`** — uint32 LE (verified: 2,467,769 before purchase, decreased by item price after buying)

**Item Inventory:** byte array near `0x1411A17FB` (close to roster base 0x1411A18D0). Each byte = count of that item (max 99). Oak Staff count was at 0x1411A17FB. Full item ID mapping not yet decoded.

**Encounter Dialog Detection:**
- `0x140900824` and `0x140900828` — two bytes that match on normal world map but differ during encounter
- **If `0x140900824 != 0x140900828`** → "Enemy encountered!" dialog is active (Fight/Flee)
- **If `0x140900824 == 0x140900828`** → Normal world map
- To flee: send Down (moves cursor to Flee) then Enter
- The dialog defaults cursor to Fight (top), so always send Down before Enter

**Location IDs:**

| ID | Location | Type |
|----|----------|------|
| 0 | Royal City of Lesalia | Settlement |
| 1 | Zeltennia Castle | Settlement |
| 2 | Lionel Castle | Settlement |
| 3 | Bervenia Castle | Settlement |
| 4 | Magick City of Gariland | Settlement |
| 5 | Limberry Castle | Settlement |
| 6 | Merchant City of Dorter | Settlement |
| 7 | Walled City of Yardrow | Settlement |
| 8 | Mining Town of Gollund | Settlement |
| 9 | Castled City of Zaland | Settlement |
| 11 | Clockwork City of Goug | Settlement |
| 12 | Port City of Warjilis | Settlement |
| 13 | Free City of Bervenia | Settlement |
| 14 | Trade City of Sal Ghidos | Settlement |
| 15 | Golgorand Fortress | Misc |
| 16 | Mullonde | Misc |
| 17 | Brigands' Den | Misc |
| 18 | Orbonne Monastery | Misc |
| 19 | Golgoland Gallows | Misc |
| 21 | Fort Besselat | Misc |
| 22 | Midlight's Deep | Misc |
| 23 | Nelveska Temple | Misc |
| 24 | Mandalia Plains | Battleground |
| 25 | Fovoham Windflats | Battleground |
| 26 | The Siedge Weald | Battleground |
| 27 | Mount Bervenia | Battleground |
| 28 | Gollund Desert | Battleground |
| 29 | Lenalian Plateau | Battleground |
| 30 | Tchigolith Fenlands | Battleground |
| 31 | The Paoad | Battleground |
| 32 | Araguay Woods | Battleground |
| 33 | Gough Heights | Battleground |
| 34 | Medilla Sandwilds | Battleground |
| 35 | Zeirchele Falls | Battleground |
| 36 | Dorvauldar Marsh | Battleground |
| 37 | Balias Tor | Battleground |
| 38 | Duguera Pass | Battleground |
| 39 | Balias Swale | Battleground |
| 40 | Viura's Creek | Battleground |
| 41 | Lake Poescas | Battleground |
| 42 | Mount Germinas | Battleground |

IDs 10 and 20 not yet observed — possibly Eagrose Castle and another locked location.

### Battle Stat Arrays (parallel uint32 arrays, verified stable across launches)

Offsets from roster base address:
| Array | Offset from roster base | Entry size |
|-------|------------------------|------------|
| Current MP | roster_base - 0x17F694 | uint32 |
| Current HP | roster_base - 0x17CA08 | uint32 |
| Max HP | roster_base - 0x1133E8 | uint32 |

Each array has one uint32 per battlefield unit (max ~21 units).
Entry index 1 = Ramza in tested battles.

**Still unmapped:** Max MP, CT, X/Y position, status effects, PA/MA/Speed arrays.

### Ability Offsets

**Secondary ability (+0x07)** stores an index into the character's personal unlocked ability list, NOT a universal ability ID. The list only includes abilities for jobs the character has unlocked, and story characters may have unique primary abilities that shift the indices. The fixed order for generic characters with all jobs unlocked:

| Index | Ability | Job |
|-------|---------|-----|
| 5 | Fundaments | Squire |
| 6 | Items | Chemist |
| 7 | Arts of War | Knight |
| 8 | Aim | Archer |
| 9 | Martial Arts | Monk |
| 10 | White Magicks | White Mage |
| 11 | Black Magicks | Black Mage |
| 12 | Time Magicks | Time Mage |
| 13 | Summon | Summoner |
| 14 | Steal | Thief |
| 15 | Speechcraft | Orator |
| 16 | Mystic Arts | Mystic |
| 17 | Geomancy | Geomancer |
| 18 | Jump | Dragoon |
| 19 | Iaido | Samurai |
| 20 | Throw | Ninja |
| 21 | Arithmeticks | Arithmetician |
| 22 | Bardsong | Bard (male) / Dance (female) |

Note: The character's primary ability does NOT appear in their secondary ability selection list. Story characters with unique primary abilities (e.g., Agrias's Holy Sword) shift the indices.

**Reaction ability IDs (+0x08):**
| ID | Hex | Ability |
|----|-----|---------|
| 167 | 0xA7 | Magick Surge |
| 168 | 0xA8 | Speed Surge |
| 169 | 0xA9 | Vanish |
| 170 | 0xAA | Vigilance |
| 171 | 0xAB | Dragonheart |
| 172 | 0xAC | Regenerate |
| 174 | 0xAE | Faith Surge |
| 175 | 0xAF | Critical: Recover HP |
| 176 | 0xB0 | Critical: Recover MP |
| 177 | 0xB1 | Critical: Quick |
| 178 | 0xB2 | Bonecrusher |
| 179 | 0xB3 | Magick Counter |
| 180 | 0xB4 | Counter Tackle |
| 181 | 0xB5 | Nature's Wrath |
| 182 | 0xB6 | Absorb MP |
| 183 | 0xB7 | Gil Snapper |
| 185 | 0xB9 | Auto-Potion |
| 186 | 0xBA | Counter |
| 188 | 0xBC | Cup of Life |
| 189 | 0xBD | Mana Shield |
| 190 | 0xBE | Soulbind |
| 191 | 0xBF | Parry |
| 192 | 0xC0 | Earplugs |
| 193 | 0xC1 | Reflexes |
| 194 | 0xC2 | Sticky Fingers |
| 195 | 0xC3 | Shirahadori |
| 196 | 0xC4 | Archer's Bane |
| 197 | 0xC5 | First Strike |

**Support ability IDs (+0x0A):**
| ID | Hex | Ability |
|----|-----|---------|
| 198 | 0xC6 | Equip Heavy Armor |
| 199 | 0xC7 | Equip Shields |
| 200 | 0xC8 | Equip Swords |
| 201 | 0xC9 | Equip Katana |
| 202 | 0xCA | Equip Crossbows |
| 203 | 0xCB | Equip Polearms |
| 204 | 0xCC | Equip Axes |
| 205 | 0xCD | Equip Guns |
| 206 | 0xCE | Halve MP |
| 207 | 0xCF | JP Boost |
| 208 | 0xD0 | EXP Boost |
| 209 | 0xD1 | Attack Boost |
| 210 | 0xD2 | Defense Boost |
| 211 | 0xD3 | Magick Boost |
| 212 | 0xD4 | Magick Defense Boost |
| 213 | 0xD5 | Concentration |
| 214 | 0xD6 | Tame |
| 215 | 0xD7 | Poach |
| 216 | 0xD8 | Brawler |
| 217 | 0xD9 | Beast Tongue |
| 218 | 0xDA | Throw Items |
| 219 | 0xDB | Safeguard |
| 220 | 0xDC | Doublehand |
| 221 | 0xDD | Dual Wield |
| 222 | 0xDE | Beastmaster |
| 223 | 0xDF | Evasive Stance |
| 224 | 0xE0 | Reequip |
| 226 | 0xE2 | Swiftspell |
| 228 | 0xE4 | HP Boost (mod) |
| 229 | 0xE5 | Vehemence (mod) |

**Movement ability IDs (+0x0C):**
| ID | Hex | Ability |
|----|-----|---------|
| 230 | 0xE6 | Movement +1 |
| 231 | 0xE7 | Movement +2 |
| 232 | 0xE8 | Movement +3 |
| 233 | 0xE9 | Jump +1 |
| 234 | 0xEA | Jump +2 |
| 235 | 0xEB | Jump +3 |
| 236 | 0xEC | Ignore Elevation |
| 237 | 0xED | Lifefont |
| 238 | 0xEE | Manafont |
| 239 | 0xEF | Accrue EXP |
| 240 | 0xF0 | Accrue JP |
| 242 | 0xF2 | Teleport |
| 244 | 0xF4 | Ignore Weather |
| 245 | 0xF5 | Ignore Terrain |
| 246 | 0xF6 | Waterwalking |
| 247 | 0xF7 | Swim |
| 248 | 0xF8 | Lavawalking |
| 250 | 0xFA | Levitate |
| 251 | 0xFB | Fly |
| 253 | 0xFD | Treasure Hunter |

**Bytes +0x09, +0x0B, +0x0D** are always 0x01 when the corresponding ability slot is equipped — likely "equipped" flags.

**UI ability cursor address: `0x140C0EB20`** — a global counter that increments each time the cursor moves in an ability selection list. Not an ability ID, but the delta from the initial value when the list opened gives the cursor's list position.

### Roster Slot → Grid Position Mapping

From state.json and screenshots:
| Grid Pos | Name | Roster Slot | Job | Level |
|----------|------|-------------|-----|-------|
| 0 | Ramza | 0 | 160 (Dark Knight) | 99 |
| 1 | Kenrick | 1* | 76? | 99 |
| 2 | Lloyd | 2 | 87 | 99 |
| 3 | William | 3 | 82 | 99 |
| 4 | Alicia | 4 | 33 (Bard) | 8 |
| 5 | Lavian | 5 | 57 | 8 |
| 6 | Mustadio | 11* | 22 (Oracle) | 35 |
| 7 | Agrias | 1 | 76 (Holy Knight) | 36 |
| 8 | Rapha | ? | ? | 89 |
| 9 | Marach | ? | ? | 91 |
| 10 | ??? | ? | ? | ? |
| 11 | Orlandeau | ? | ? | 88 |
| 12 | Meliadoul | ? | ? | 94 |
| 13 | Reis | ? | ? | 92 |

*Grid order does NOT match roster slot order. Needs further mapping.*

---

## Discovery Tools

### Differential Memory Scanner
Take two memory snapshots before/after an action, diff them to find changed addresses.
Used to discover UI state buffer. Can be reused to find:
- Screen state variable (diff world map → party menu)
- Battle turn indicator (diff between turns)
- Equipment slot addresses
- Any UI state

### Memory Search
Search near unit data base (+/- 2MB) for specific uint16 values.
Used to find battle HP/MP arrays.

### Hex Dump
Dump full 600-byte roster slot for a unit. Useful for finding field offsets.

---

## Known Issues / Notes

- Boot sequence (Enter x3) needs 3 second delays between presses or inputs get swallowed
- Game window doesn't need to be focused — PostMessage works in background
- All memory addresses verified on the Enhanced Edition (FFT_enhanced.exe)
- 55 "active" units show during title screen (uninitialized data) — drops to 17 after save loads
- Screenshots available via: `powershell.exe -ExecutionPolicy Bypass -File ./screenshot.ps1`
