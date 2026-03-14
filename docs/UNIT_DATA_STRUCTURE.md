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
| +0x01 | byte | unitIndex | Party/roster index. `0xFF` = empty slot. Sequential for active units. |
| +0x02 | byte | job | Current job ID. Determines abilities, stats, and job class. |
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
- unitIndex = 0xFF
- All other fields are zeroed or garbage
- **Writing to empty slots crashes the game** — they cannot be populated programmatically

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

## References

- [FFHacktics Wiki — World Stats](https://ffhacktics.com/wiki/World_Stats) — PSX unit data structure (0xCE = Name ID)
- [FFHacktics Wiki — Miscellaneous Unit Data](https://ffhacktics.com/wiki/Miscellaneous_Unit_Data) — Battle-time sprite/animation data (not roster)
- [WotL Character Repair mod](https://www.nexusmods.com/finalfantasytacticstheivalicechronicles/mods/33) — Documents the Cheat Engine manual process
- [FearLess Cheat Engine thread](https://fearlessrevolution.com/viewtopic.php?f=4&t=36719) — Secondary job addresses confirm 0x258 stride
- [FF16Tools.CLI](https://github.com/Nenkai/FF16Tools) — NXD to SQLite conversion (`nxd-to-sqlite -g fft`)
- FFT_Egg_Control mod (by dicene) — Original AoB pattern and unit data array discovery
