<!-- This file should not be longer than 200 lines, if so prune me. -->
# FFT: Ivalice Chronicles — Unit Data Array

Technical documentation for the in-memory unit/roster data structure used by FFT: The Ivalice Chronicles (Steam remaster). Reverse-engineered through hex dumps, the FFHacktics PSX World Stats wiki, the WotL Characters mod NXD files, and iterative testing.

## Locating the Unit Data Array

The unit data array is found at runtime via AoB (Array of Bytes) signature scan, originally documented by the FFT_Egg_Control mod (by dicene).

**AoB Pattern:**
```
48 8D 05 ?? ?? ?? ?? 48 03 C8 74 ?? 8B 43 ?? F2 0F 10 43 ?? F2 0F 11 41 ?? 89 41 ?? 0F B7 43 ?? 66 89 41
```

**Address Resolution:**
The pattern uses x86-64 RIP-relative addressing:
1. Find the pattern offset within the main module
2. Read the Int32 at `pattern + 3` (the RIP displacement)
3. Resolved address = `pattern + 7 + displacement`

**Array Layout:**
- 55 slots total
- 0x258 (600) bytes per slot
- Stride confirmed by secondary job field addresses (FearLess Cheat Engine forum)

## Known Field Offsets

| Offset | Size | Field | Description |
|--------|------|-------|-------------|
| +0x00 | byte | spriteSet | Character Identity — determines which sprite the game renders. Story characters use fixed values (e.g., 128 for most story chars, 3 for Ramza Ch4). Generic units have spriteSet == job. |
| +0x01 | byte | unitIndex | Party/roster index. Sequential for generic recruits. **`0xFF` does NOT reliably mean empty** — verified 2026-04-14 that several story characters (Rapha, extra Construct 8 clones) carry unitIndex=0xFF while still being real active party members, and conversely some inactive template slots have level>0. The only reliable filter is `unitIndex != 0xFF AND level > 0` combined (confirmed against the game's own 16/50 headcount). |
| +0x02 | byte | job | Current job ID. For generic units and story characters on their canonical class, this equals their nameId or a roster-range ID (74-95). Ramza uses 3 (= Gallant Knight in his case). |
| +0x07 | byte | secondaryAbility | Index into the character's personal unlocked ability list (not a universal ID). |
| +0x08 | byte | reactionAbility | Ability ID for equipped reaction ability (e.g., Counter=0xBA, Parry=0xBD). |
| +0x09 | byte | reactionEquipped | 0x01 when reaction slot has an ability equipped. |
| +0x0A | byte | supportAbility | Ability ID for equipped support ability (e.g., Attack Boost=0xD1). |
| +0x0B | byte | supportEquipped | 0x01 when support slot has an ability equipped. |
| +0x0C | byte | movementAbility | Ability ID for equipped movement ability (e.g., Move+2=0xE7). |
| +0x0D | byte | movementEquipped | 0x01 when movement slot has an ability equipped. |
| +0x0E | uint16 LE | equipHelm | Helm item ID (FFTPatcher-canonical, 0-315). 0xFF / 0xFFFF = empty. |
| +0x10 | uint16 LE | equipBody | Body armor item ID. 0xFF = empty. |
| +0x12 | uint16 LE | equipAccessory | Accessory item ID. 0xFF = empty. |
| +0x14 | uint16 LE | equipRightHand | Right-hand weapon item ID. 0xFF = empty. |
| +0x16 | uint16 LE | equipLeftHand | Left-hand weapon (dual-wield) item ID. 0xFF = empty for normal one-hand+shield layouts. |
| +0x18 | uint16 LE | equipReserved | Reserved / unknown equipment slot. Typically 0xFF on all units observed. |
| +0x1A | uint16 LE | equipShield | Left-hand shield item ID. 0xFF = empty. |
| +0x1C | byte | exp | Experience points toward next level (0-99). |
| +0x1D | byte | level | Unit level (1-99). **`level == 0` is the true empty-slot marker** on IC remaster. |
| +0x1E | byte | brave | Bravery (0-100). |
| +0x1F | byte | faith | Faith (0-100). |
| +0x230 | uint16 LE | nameId | Indexes into the `CharaName-en` NXD table. Determines the character's displayed name. |

### spriteSet Values
| Value | Meaning |
|-------|---------|
| 0x00–0x7F | Special/story character sprite |
| 0x80 (128) | Generic Male (also used as base for most story characters) |
| 0x81 (129) | Generic Female |
| 0x82 (130) | Monster |
| Job ID | Generic units (spriteSet == job) |

### nameId Format
The nameId is a uint16 that keys into the `CharaName-en` NXD database table. The NXD file is part of the game's data override system and can be inspected by converting to SQLite:
```bash
FF16Tools.CLI nxd-to-sqlite -i <nxd_directory> -o output.db -g fft
```

The CharaName-en table schema:
| Column | Type | Description |
|--------|------|-------------|
| Key | INTEGER | Name ID (matches nameId field) |
| DLCFlags | INTEGER | DLC requirement (1 = WotL DLC content) |
| Name | TEXT | Displayed character name |
| IsGeneric | INTEGER | 1 = randomly assigned generic name |

## Slot Patterns

### Ramza (Slot 0)
- spriteSet = 3 (Ch4 variant), unitIndex = 0, job = 160
- Ramza has CharaName keys 1–3 for Chapters 1, 2/3, and 4

### Story Characters (Slots 1–3 typically)
- spriteSet = 128 (story char base), unitIndex = sequential, job = unique per character
- The displayed name comes from the game's story character system, not the nameId field
- nameId still contains a value (often a generic name from recruitment) but is overridden

### Generic Units
- spriteSet == job (e.g., spriteSet=33, job=33 for a Knight)
- unitIndex = sequential (skipping empty slots)
- nameId = index of their randomly-assigned name from the CharaName table

### Empty Slots
- **True marker: `level == 0`** (verified 2026-04-14 in live PartyMenu — yields the game's own 16/50 count exactly).
- unitIndex = 0xFF is a *secondary* filter: all empty slots have 0xFF there, but some active story-character slots do too. Always AND with the level check.
- All other fields are zeroed or garbage on empty slots.
- **Writing to empty slots crashes the game** — they cannot be populated programmatically.

## Equipment Decoding

The 7 u16 equipment IDs at roster +0x0E..+0x1A use the **FFTPatcher canonical encoding** (0-315). Verified 2026-04-14 against live Ramza and Kenrick dumps — every equipped ID read directly matched `ItemData.GetItem(id)`:

| Unit | +0x0E helm | +0x10 body | +0x12 acc | +0x14 R-hand | +0x1A shield |
|------|-----------|-----------|----------|--------------|--------------|
| Ramza | 156=Grand Helm | 185=Maximillian | 218=Bracer | 36=Ragnarok | 143=Escutcheon |
| Kenrick | 154=Crystal Helm | 182=Crystal Mail | 218=Bracer | 37=Chaos Blade | 141=Kaiser Shield |
| Mustadio (custom) | 166=Gold Hairpin | 194=Jujitsu Gi | 213=Hermes Shoes | 72=Mythril Gun | 0xFF (none) |

An earlier note in `ItemData.cs` claimed a "Game=N → FFTPatcher=M" translation was needed; that was based on a wrong slot-offset assumption and has been corrected. **No translation table is required.** Read the u16, look up in `ItemData.Items`, get the name.

`0xFF` and `0xFFFF` both mean "empty slot" (e.g., no left-hand weapon, no second accessory). Filter those before lookup.

The canonical reader is `EquipmentReader.FromSlotValues(int[7])` in `ColorMod/GameBridge/EquipmentReader.cs`. `RosterReader.ReadLoadout(slotIndex)` wraps it with the memory read.

## PSX to Ivalice Chronicles Field Mapping

The IC remaster expanded the PSX's ~0xD2 byte roster entry to 0x258 bytes. The first three bytes are identical, but later fields shifted:

| PSX Offset | IC Offset | Field |
|------------|-----------|-------|
| 0x00 | 0x00 | spriteSet (Character Identity) |
| 0x01 | 0x01 | unitIndex (Party ID) |
| 0x02 | 0x02 | job (Job ID) |
| 0x03 | — | Palette |
| 0x04 | — | Gender Byte |
| 0x15 | — | Experience |
| 0x16 | — | Level |
| 0x17 | — | Brave |
| 0x18 | — | Faith |
| 0xCE | 0x230 | nameId (Unit Name ID) |

Fields marked "—" for IC offset have not been mapped in the expanded structure. The IC version likely uses wider fields (halfwords/words where PSX used bytes) and has additional fields for remaster-specific features.

## WotL Characters: Balthier & Luso

The WotL Characters mod (Nexus) provides NXD data for Balthier and Luso but sets `DLCFlags=1` with no recruitment trigger, requiring manual Cheat Engine editing to add them.

| Character | Job ID | Job Name | CharaName Key | spriteSet |
|-----------|--------|----------|---------------|-----------|
| Balthier | 162 | Sky Pirate | 162 | 162 |
| Luso | 163 | Game Hunter | 163 | 163 |

### Adding Them Programmatically

The only approach that works is **in-place conversion** of existing generic units:
1. Find a generic unit (spriteSet == job, job > 0, job < 94, unitIndex != 0xFF)
2. Set `spriteSet` = character's job ID (162 or 163)
3. Set `job` = character's job ID
4. Set `nameId` (uint16 at +0x230) = character's job ID

This gives the correct sprite, abilities, and displayed name.

### What Does NOT Work
All of these crash the game when opening the party menu:
- Writing any data to empty slots (unitIndex = 0xFF)
- Copying a template from an existing unit to an empty slot
- Zero-filling a slot and setting the three key fields
- The game's unit initialization expects data structures that cannot be replicated by simple memory writes

## Failed Name Field Attempts

During reverse engineering, the following offsets were tested for the name field and had **no effect** on the displayed name:
- +0x04 bit 7 flag
- +0x121 (contains job-1 for story characters, 0 for generics — some other purpose)
- +0x122, +0x124 (mirror unitIndex — related to roster management, not name)

## Roster Name Display Table (separate from the unit data array)

The roster array at 0x1411A18D0 does **not** contain character names as strings — `+0x230` is just a nameId integer, and for generics there's no readable name anywhere in the 0x258-byte slot data. Displayed names live in a **separate per-slot name record table** in the UE4 heap.

**Structure**: 16 roster slots, each a **0x280-byte** record. Inside each record, at offset **+0x10**, is a null-terminated UTF-8 ASCII string — the character's displayed name. After that string, there's a list of alternate name options (unused by us, likely for sprite/theme variations). The rest of the record is stats/config binary data.

**Locating it**: search for the 57-byte anchor signature:
```
"Ramza\0Delita\0Argath\0Zalbaag\0Dycedarg\0Larg\0Goltanna\0Ovelia\0Orland\0"
```
Note **"Orland"** (6 chars) not "Orlandeau" — the roster table truncates Orlandeau at a record boundary. There's a separate flat master name pool that uses the full "Orlandeau" — avoid confusing them.

The anchor pattern starts at offset +0x10 inside the first record (slot 0 = Ramza). So record base = match address - 0x10. Slot N's name is at `base + N * 0x280 + 0x10`.

**Verified layout** (empirical from live game):
```
slot 0:  Ramza    (story char, always slot 0)
slot 1:  Kenrick  (first generic recruit)
slot 2:  Lloyd
slot 3:  Wilham
slot 4:  Alicia
...
```

**Parser**: walks at 0x280 stride, reads null-terminated string at +0x10 per record, stops at first empty record (end of active recruits). See `ColorMod/GameBridge/NameTableLookup.cs` for the implementation.

**Integration**: `RosterMatcher.RosterMatchResult.SlotIndex` tells the scan which roster slot a battle unit matched to. `NavigationActions.CollectUnitPositionsFull` uses `UnitNameLookup.GetName(nameId)` first (story chars), then falls back to `NameTableLookup.GetNameBySlot(slotIndex)` for generics.

### Enemy names — NOT yet readable

Enemy units (both generic "Sithon the Bonesnatch" and monsters) have displayed names in-game when hovered, but these names are NOT findable via byte-pattern search in PAGE_READWRITE memory. Tested UTF-8 and UTF-16 LE for "Sithon", "Justitia", "Telephassa" with the enemy actively hovered — zero matches. Sanity check: "Ramza" still finds 5 matches so the search works.

Hypotheses: enemy names may be in PAGE_READONLY data sections (loaded from `battle_bin.en.bin` on demand), rendered via glyph sprite lookup without ever forming a contiguous string, or encrypted until render. 4 possible next approaches documented in the TODO and in `memory/project_unit_name_table.md`.

## References

- [FFHacktics Wiki — World Stats](https://ffhacktics.com/wiki/World_Stats) — PSX unit data structure (0xCE = Name ID)
- [FFHacktics Wiki — Miscellaneous Unit Data](https://ffhacktics.com/wiki/Miscellaneous_Unit_Data) — Battle-time sprite/animation data (not roster)
- [WotL Character Repair mod](https://www.nexusmods.com/finalfantasytacticstheivalicechronicles/mods/33) — Documents the Cheat Engine manual process
- [FearLess Cheat Engine thread](https://fearlessrevolution.com/viewtopic.php?f=4&t=36719) — Secondary job addresses confirm 0x258 stride
- [FF16Tools.CLI](https://github.com/Nenkai/FF16Tools) — NXD to SQLite conversion (`nxd-to-sqlite -g fft`)
- FFT_Egg_Control mod (by dicene) — Original AoB pattern and unit data array discovery
