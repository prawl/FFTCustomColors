<!-- This file should not be longer than 200 lines, if so prune me. -->
# Class Fingerprint System

The FFT IC remaster keeps live battle units in heap-allocated structs. Each struct has 11 bytes at offset **+0x69** that identify the unit's class deterministically. **Bytes 1-10** are the stable class signature (byte 0 varies per unit/team).

This is our primary enemy class identifier. Player units still prefer the roster lookup since it's authoritative, but fingerprints fill in everything else.

## Why this is needed
- **UI buffer** at `0x1407AC7EA + 0x2A` returns `Job=1` (Chemist) for all enemies during C+Up cycling. Unreliable.
- **Condensed struct** at `0x14077D2A0` has HP/level/PA/MA but no jobId byte.
- **Static battle array** at `0x140893C00` only contains player units.
- **Heap unit structs** have the fingerprint at +0x69 — reliable across sessions.

## Locating a unit struct
1. Read `unit.Hp` and `unit.MaxHp` from the condensed struct during scan.
2. Build 4-byte pattern `HP_lo HP_hi MaxHP_lo MaxHP_hi`.
3. Call `SearchBytesInAllMemory(pattern, maxResults=16, minAddr: 0x4000000000L, maxAddr: 0x4200000000L)`.
4. Struct base = match address - 0x10 (HP lives at struct+0x10).
5. Read 11 bytes at `struct base + 0x69`.
6. Lookup key is bytes 1-10 as hex string: `BitConverter.ToString(fp, 1, 10)`.

## Heap unit struct layout (partial)
| Offset | Size | Field |
|---|---|---|
| +0x00..+0x07 | 8 | Header bytes |
| +0x08 | 1 | exp |
| +0x09 | 1 | level |
| +0x0A..+0x0D | 4 | origBrave, brave, origFaith, faith |
| +0x10..+0x11 | 2 | HP (u16 LE) — used as the search anchor |
| +0x12..+0x13 | 2 | MaxHP |
| +0x14..+0x17 | 4 | MP, MaxMP |
| +0x68 | 1 | per-unit variation (skip) |
| **+0x69..+0x73** | **11** | **class fingerprint** |

## Known pitfalls (all handled in code)

### 1. Heap address drift
Unit structs don't always live in the same range. Known-good saves put them around `0x4160..0x4180`, some put them at `0x430x`. The widened range `0x4000000000..0x4200000000` catches most cases. Mod-forced battles (Grogh Heights, Dugeura Pass, Tchigolith Fenlands, Mandalia Plain story battles) can put structs entirely outside searchable ranges — fingerprint lookup just fails and enemies show as `(?)`. **Skip those battles**.

### 2. Graphics buffer false positives
Common HP values (116, 344, etc.) appear as float data in graphics buffers around `0x428Cxxxx`. The range filter keeps us out of that region.

### 3. Zero fingerprints from dead slots
Some heap matches land on dead/reserved unit slots where +0x69 is all zeros. The code tries successive heap matches until it finds a non-zero fingerprint.

### 4. Byte 0 variation (per-team)
A player Knight and enemy Knight had fingerprints differing only at byte 0 (`02-0A-78-...` vs `03-0A-78-...`). Drop byte 0 from the key. Use bytes 1-10.

### 5. Same-fingerprint collisions
**Arithmetician (player) and Ahriman (enemy monster)** have identical bytes 1-10. Disambiguated by team via `FingerprintByTeam` dict: `team==0` → Arithmetician, `team==1` → Ahriman.

### 6. Ramza's fingerprint varies per save
Ramza has had 5+ different fingerprints across saves (his job/equipment changes over time). Story chars like Ramza are unreliable via fingerprint — always prefer the roster lookup for them.

### 7. Story char roster job field is actually nameId
For Mustadio/Marach/Beowulf/etc., roster +0x02 equals nameId (22/26/31/...) instead of a generic job ID. Reading as job mislabels them (e.g. Marach nameId=26 becomes "Dragoon" via PSX 0x1A=Dragoon). Fix: `CharacterData.StoryCharacterJob` dict keyed by nameId, `GetStoryJob(nameId)` returns canonical job.

## Fingerprint family patterns
Byte 1 correlates with family:
- `0x05` — skeletons, behemoths
- `0x06` — goblins, panthers, eyeballs, chocobos, bulls, dragons
- `0x07` — ghosts, bombs, birds, some chocobos
- `0x08` — malboros
- `0x09..0x14` — generic humans (no 1E..55 marker)

Monster bytes 3 and 5 are usually `0x1E` and `0x55` respectively. Chocobo breaks this with `0x4B` at byte 5, Malboro uses `0x5A`.

## Monster abilities
Monsters have fixed ability loadouts per class (verified: two Goblins in the same battle with different HP/level had identical kits). `MonsterAbilities.cs` maps class name → ability list, `MonsterAbilityLookup.cs` gives each ability its full metadata (range, AoE, target, element, effect).

Data source for abilities: FFT Fandom wiki "Final Fantasy Tactics enemy abilities" A-Z table. Beastmaster-only abilities are excluded (enemies don't typically have Beastmaster configurations).

## Adding a new fingerprint
When a scan shows `(?)` for an enemy:
1. `logs grep "Unknown fingerprint"` to get the raw bytes
2. Ask the user what class it is (include HP + level + position for disambiguation)
3. Add entry to `ClassFingerprintLookup.FingerprintToJob` (bytes 1-10 only)
4. If the user also provides abilities, add to `MonsterAbilities.ClassToAbilities`
5. Rebuild and verify the label appears correctly

## Files
- `ColorMod/GameBridge/ClassFingerprintLookup.cs` — fingerprint → class name table (~50+ classes)
- `ColorMod/GameBridge/MonsterAbilities.cs` — class name → ability list (~47 classes)
- `ColorMod/GameBridge/MonsterAbilityLookup.cs` — ability name → full metadata (range, AoE, element, etc.)
- `ColorMod/GameBridge/CharacterData.cs` — `StoryCharacterJob` dict for story char job override
- `ColorMod/GameBridge/NavigationActions.cs` — `CollectUnitPositionsFull` wires everything together

## See also
- `Instructions/ClassFingerprintLabeling.md` — battle loop procedure for collecting new fingerprints
