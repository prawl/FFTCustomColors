# WotL Characters Spawner - Prototype in FFTColorCustomizer

## Status: PAUSED — Planning standalone mod

The spawner prototype works (sprite, job, name all correct) but is currently embedded in FFTColorCustomizer. Next step is to extract this into a standalone Reloaded-II mod that replaces the WotL Characters mod entirely, bundling both the NXD character data and the auto-spawner.

### Known issue: generic detection
Story characters reclassed to generic jobs (e.g., a story char reclassed to Summoner, job=82) are indistinguishable from actual generic recruits — both have spriteSet=128 and a job in range 74-93. The spawner may sacrifice reclassed story characters instead of true generics. Needs a better heuristic or user control before shipping.

---

## Background
- WotL Characters mod provides NXD data (names, jobs, abilities, face portraits) but characters have DLCFlags=1 and no recruitment trigger
- FFT_Egg_Control mod (by dicene) has reverse-engineered the unit data array: 55 slots x 0x258 bytes each
- WotL Character Repair mod (by Dana Crysalis) documents the Cheat Engine process: "Setting their Character IDs, Character Name, and Current job to their respective spots (162 and 163) will change what character you're modifying into the person you're adding."

## Unit Data Array (from FFT_Egg_Control)
- AoB: `48 8D 05 ?? ?? ?? ?? 48 03 C8 74 ?? 8B 43 ?? F2 0F 10 43 ?? F2 0F 11 41 ?? 89 41 ?? 0F B7 43 ?? 66 89 41`
- Resolution: RIP-relative — read Int32 at pattern+3, address = pattern+7+offset
- 55 slots, 0x258 (600) bytes each
- Known offsets: +0x00=spriteSet, +0x01=unitIndex (0xFF=empty), +0x02=job, +0x230=nameId (uint16 LE)

## Unit Data Structure (reverse-engineered via hex dumps)

### Slot patterns observed
- **Ramza (slot 0):** spriteSet=3, unitIndex=0, job=160
- **Story characters (slots 1-3):** spriteSet=128, unitIndex=sequential, job=unique (77/87/82)
- **Generic recruits:** spriteSet=128 (male) or 129 (female), job in range 74-93 (Squire-Mime)
- **Guest/enemy units:** spriteSet==job (e.g., sprSet=33, job=33)
- **Meliadoul (slot 22):** spriteSet=130, job=145 (special story char)

### What does NOT work (crashed the game)
- Creating new units in empty slots (unitIndex=0xFF) — crashes on party menu regardless of approach:
  - Copying full 0x258 from a generic template + overwriting spriteSet/unitIndex/job
  - Copying from a story character template + overwriting fields
  - Zero-filling slot + setting only spriteSet/unitIndex/job
  - The game cannot handle ANY data written to empty slots

### What DOES work (no crash)
- **In-place conversion** of existing generic units: only modify spriteSet, job, and nameId on an already-valid unit
- This matches the Cheat Engine manual process described by the WotL Character Repair mod

## Character Data
| Character | Job ID | Job Name     | spriteSet | CharaName Key |
|-----------|--------|-------------|-----------|---------------|
| Balthier  | 162    | Sky Pirate  | 162       | 162           |
| Luso      | 163    | Game Hunter | 163       | 163           |

## Completed Work

- [x] AoB scan + RIP-relative address resolution
- [x] Background polling loop (every 2s), continuous across save reloads
- [x] In-place conversion of generic units (spriteSet + job + nameId)
- [x] Name field discovered: +0x230 (uint16 LE) indexes CharaName-en NXD table
- [x] Generic detection: spriteSet=128/129 + job 74-93
- [x] Safety check: won't convert unless enough generics available for all missing characters
- [x] Cleaned up diagnostic hex dump logging

## Remaining Work

- [ ] **Extract into standalone Reloaded-II mod** (replace WotL Characters mod)
- [ ] Solve generic vs reclassed-story-character detection
- [ ] Consider: user config for which units to sacrifice, or explicit trigger
- [ ] Bundle NXD character data (need to verify redistribution rights)
- [ ] Add tests

## References
- [WotL Character Repair mod (Nexus)](https://www.nexusmods.com/finalfantasytacticstheivalicechronicles/mods/33)
- [FearLess Cheat Engine thread](https://fearlessrevolution.com/viewtopic.php?f=4&t=36719)
- [FFHacktics Wiki — World Stats](https://ffhacktics.com/wiki/World_Stats)
- [docs/UNIT_DATA_STRUCTURE.md](docs/UNIT_DATA_STRUCTURE.md) — Full reverse-engineering documentation
- Secondary job field addresses confirm 0x258 stride (from FearLess forum)
- FF16Tools.CLI — NXD to SQLite conversion (`nxd-to-sqlite -g fft`)
