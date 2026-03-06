# Individual Unit Color Remapping Research

**Status:** Research / Theoretical — **Major new leads discovered March 2026**
**Goal:** Enable two units of the same job class (e.g., two female squires) to have different colors simultaneously

---

## The Problem

Currently, FFTColorCustomizer uses **file-based sprite swapping**:
- When you select "dark_knight" theme for Knight, ALL knights use that sprite
- The `charclut.nxd` system used for Ramza is keyed by **character/chapter**, not by **unit instance**
- No mechanism exists to differentiate "Squire #1" from "Squire #2" at the palette level

---

## Current Architecture Limitations

### Sprite File Approach
```
User selects theme → Sprite file copied → ALL units of that job use same sprite
```
- Works great for per-job theming
- Cannot differentiate individual units

### charclut.nxd Approach (Ramza)
```
Key Structure:
  Key  = Character/Chapter ID (1=Ch1, 2=Ch2/3, 3=Ch4)
  Key2 = Palette variant (0=base, 1-3=alternates)
```
- Works for story characters with unique IDs
- Generic jobs share the same palette entries
- No per-instance differentiation

### Key Fact: Game Bypasses BIN for Ramza
The game has built-in logic that skips reading Ramza's BIN palette files and uses the NXD CLUT system instead. Generic units go through BIN palettes directly. This is why charclut.nxd only affects Ramza.

---

## NEW DISCOVERY: The CharShape → CharCLUT Pipeline (March 2026)

### The Full NXD Rendering Chain

Analysis of the NXD layout files revealed a **complete palette selection pipeline** that was not previously documented:

```
Unit Instance
  └→ Spriteset (byte field, patchable via OverrideEntryData)
       └→ CharShape (charshape.nxd, SingleKeyed)
            ├→ charclut+Id ────────→ CharCLUT (charclut.nxd, DoubleKeyed)
            │                          ├→ CLUTData (48 bytes = 16 colors × 3 RGB)
            │                          ├→ CharaColorSkinId → CharaColorSkin
            │                          │                       └→ UserSituationId (conditional)
            │                          └→ UnkBool14 (enable/disable flag)
            ├→ charshapelutparam+Id1-4 → CharShapeLUTParam
            │                              ├→ Unknown8 (float)
            │                              ├→ UnknownC (float)
            │                              ├→ Unknown10 (short)
            │                              └→ Unknown12 (short)
            ├→ charshapeshadowsize+Id1-2 → CharShapeShadowSize
            ├→ Height (byte)
            └→ Unknown fields (9-17)
```

### What This Means

**CharShape** is the bridge between a unit's sprite and its palette. Each CharShape entry has a `charclut+Id` field that directly references which CharCLUT palette entry to use. This is how the game knows Ramza should use CLUT palettes instead of BIN palettes.

### Layout File Sources

| File | Table | Key Type | Purpose |
|------|-------|----------|---------|
| `CharShape.layout` | CharShape | SingleKeyed | Maps sprites to CLUT entries |
| `CharCLUT.layout` | CharCLUT | DoubleKeyed | Stores 16-color palettes |
| `CharaColorSkin.layout` | CharaColorSkin | DoubleKeyed | Conditional palette selection |
| `CharShapeLUTParam.layout` | CharShapeLUTParam | SingleKeyed | Rendering parameters (floats) |
| `CharShapeShadowSize.layout` | CharShapeShadowSize | SingleKeyed | Shadow rendering size |
| `OverrideEntryData.layout` | OverrideEntryData | DoubleKeyed | Per-unit field overrides |

---

## NEW DISCOVERY: CharCLUT Keys 254 & 255 Are Generic Palettes

### Analysis of charclut.sqlite

Keys 254 and 255 are **not mystery entries** — they contain real palette data with `CharaColorSkinId = 0` (not tied to any named character like Ramza):

| Key | Key2 | CharaColorSkinId | UnkBool14 | Description |
|-----|------|------------------|-----------|-------------|
| 1 | 0-3 | **1** (Ramza) | 1 | Chapter 1 palettes |
| 2 | 0-3 | **1** (Ramza) | 1 | Chapter 2/3 palettes |
| 3 | 0-3 | **1** (Ramza) | 1 | Chapter 4 palettes |
| **254** | 0 | **0** (generic) | 1 | Blue-toned soldier palette |
| **254** | 1 | **0** (generic) | 0 | Blue-toned (disabled?) |
| **255** | 0 | **0** (generic) | 1 | Purple/magenta palette |
| **255** | 1 | **0** (generic) | 0 | All zeros (null/disabled) |

### Key 254 Palette Colors (Blue Soldier)
```
Index 3: RGB( 48,  48,  72)  - Dark blue armor
Index 4: RGB( 64,  64, 120)  - Medium blue armor
Index 5: RGB( 80,  88, 160)  - Bright blue armor
Index 6: RGB(120, 112, 184)  - Very bright blue armor
Hair/skin: Warm browns (indices 13-15)
```

### Key 255 Palette Colors (Purple/Debug)
```
Index 3: RGB( 24,  24,  32)  - Dark purple
Index 4: RGB( 40,   8, 104)  - Medium purple
Index 5: RGB( 40,   0, 152)  - Bright purple
Index 6: RGB( 40,  32,  56)  - Slightly lighter
Hair: Bright magenta (indices 13-15) — clearly non-standard
```

### Significance

`CharaColorSkinId = 0` means these palettes are **not character-specific**. They could be:
- Generic unit CLUT fallback palettes
- System-level palette entries for non-Ramza characters
- Proof that the CLUT system was designed to handle more than just Ramza

---

## NEW DISCOVERY: OverrideEntryData Can Patch Spriteset Per-Unit

### The Per-Unit Color Theory

From `OverrideEntryData.layout`:
```
add_column|Spriteset|uint  // 08 - if not zero, it's cast to byte and patches that respective field
```

This means OverrideEntryData can **change a specific unit's Spriteset value** at runtime. If Spriteset maps to CharShape, and CharShape maps to CharCLUT, then:

```
OverrideEntryData (unit #1) → Spriteset=50 → CharShape #50 → charclut+Id=10 → Custom Red palette
OverrideEntryData (unit #2) → Spriteset=51 → CharShape #51 → charclut+Id=11 → Custom Blue palette
```

**Two units of the same job class with different colors** — controlled entirely through NXD files, no memory hooks needed.

### OverrideEntryData Full Column Reference

Key columns relevant to per-unit customization:

| Offset | Column | Type | Patch Condition |
|--------|--------|------|-----------------|
| 0x08 | **Spriteset** | uint→byte | If not zero |
| 0x10 | MainJob | uint→byte | If not zero |
| 0x14 | JobUnlock | uint→byte | If not 20 |
| 0x2C | Head | int→byte | If >= 0 |
| 0x30 | Body | int→byte | If >= 0 |
| 0x34 | Accessory | int→byte | If >= 0 |
| 0x44 | UnitId+characontrolid+Id | int | If not zero, looks up CharaControlId table |
| 0x7C | Level | short→byte | If > 0 |
| 0x82 | Bravery | short→byte | If >= 0 |
| 0x84 | Faith | short→byte | If >= 0 |
| 0x88 | PositionX | short→byte | If >= 0 |
| 0x8A | PositionY | short→byte | If >= 0 |

---

## The Critical Unknown: Does CharShape Apply to Generics?

### What We Know
- Ramza goes through CharShape → CharCLUT → palette colors (confirmed, working)
- Generic units use BIN palette 0 for players, palettes 1-4 for enemies (confirmed)
- The game has "built-in mumbo-jumbo" that routes Ramza through CLUT instead of BIN

### What We Don't Know
1. **Do generic units have CharShape entries?** If they do, their `charclut+Id` is probably 0 (no CLUT, fall back to BIN palette)
2. **Does setting `charclut+Id` to non-zero for a generic CharShape activate CLUT mode?** The game might check "is this Ramza?" first, regardless of CharShape data
3. **Does Spriteset actually map to CharShape keys?** It might be a sprite body type selector independent of CharShape

### How to Find Out
Extract and inspect `charshape.nxd` from the base game (see Validation Test 4 below).

---

## Potential Solution: NEX API Runtime Manipulation

### Community Insight

A modder suggested:
> "You don't need memory hooks to edit his palette, assuming it's parsed every frame - use the nex api"

### The Theory

If FFT:TIC's renderer reads `charclut.nxd` palette entries **per-frame** (not just at load time), we could:

1. **Hook into the frame render loop** via Reloaded-II
2. **Detect which unit is being rendered** (by unit ID or screen position)
3. **Dynamically patch the relevant CLUT entry** before each draw call
4. **Restore/cycle the palette** for the next unit

### Pseudocode
```csharp
// Theoretical per-frame palette switching
void OnBeforeUnitRender(int unitId, int jobClass)
{
    var unitConfig = GetUnitColorConfig(unitId);
    if (unitConfig != null)
    {
        // Patch charclut.nxd entry for this job class
        PatchClutEntry(jobClass, unitConfig.Palette);
    }
}

void OnAfterUnitRender(int unitId, int jobClass)
{
    // Restore default palette for next unit
    RestoreClutEntry(jobClass);
}
```

---

## Alternative: overrideentrydata.nxd Binary Payload

### What We Know

The **Black Boco mod** uses `overrideentrydata.nxd` for per-unit customization:

| Column | Type | Description |
|--------|------|-------------|
| Key | INTEGER | Unit ID (per-instance identifier) |
| Key2 | INTEGER | Variant/instance |
| Spriteset | INTEGER | Sprite set reference |
| MainJob | INTEGER | Primary job ID |
| ... | ... | 54 total columns |
| **Binary Payload** | BLOB | **Unknown format** - may contain color data |

### The Blocker

- Contains an **undocumented binary payload** beyond the database structure
- FF16Tools preserves but doesn't expose this data
- Cannot be safely modified without reverse-engineering
- The binary payload **might** encode per-unit color overrides
- Black Boco's actual color changes appear to live in this payload, NOT in the database columns

### Research Path

1. Extract `overrideentrydata.nxd` from Black Boco mod
2. Compare binary payloads between different unit entries
3. Look for patterns that correlate with visual color differences
4. Document the binary format if discovered

---

## Validation Tests

### Test 1: Runtime Palette Refresh
```
1. Start a battle with generic units
2. While battle is running, externally modify charclut.nxd
3. Observe if sprite colors change without reload
```
- If YES: Per-frame parsing confirmed, runtime approach viable
- If NO: Palettes are cached at load time, need different approach

### Test 2: overrideentrydata.nxd Analysis
```
1. Create two versions of overrideentrydata.nxd with different unit colors
2. Compare binary diff of the files
3. Identify which bytes control color data
```

### Test 3: Reloaded-II Hook Capabilities
```
1. Check if fftivc.utility.modloader exposes render hooks
2. Look for unit ID access during render phase
3. Determine if NXD entries can be patched mid-frame
```

### Test 4: CharShape → CharCLUT Pipeline for Generics (NEW - HIGHEST PRIORITY)
```
Prerequisites:
  - FF16Tools CLI (https://github.com/Nenkai/FF16Tools/releases)
  - Access to base game NXD files

Steps:
  1. Extract charshape.nxd from the base game:
     FF16Tools.CLI nxd-to-sqlite -i <game_nxd_dir> -o charshape.db -g fft

  2. Inspect ALL CharShape entries in SQLite:
     SELECT * FROM CharShape ORDER BY Key;

  3. Look for generic job entries and check their charclut+Id values:
     - If charclut+Id = 0 for generics: CLUT system is Ramza-only by default
     - If charclut+Id != 0: Generics already use CLUT (huge finding!)

  4. If charclut+Id = 0 for a generic job, try modifying it:
     UPDATE CharShape SET "charclut+Id" = 254 WHERE Key = <knight_shape_id>;

  5. Convert back and deploy:
     FF16Tools.CLI sqlite-to-nxd -i charshape.db -o <output_dir> -g fft
     Copy charshape.nxd to [Mod]/FFTIVC/data/enhanced/nxd/

  6. Start a battle with that generic job and observe:
     - If the unit turns blue (key 254 palette): CharShape controls generic palettes!
     - If no change: Game bypasses CharShape for generics entirely
```

**If Test 4 succeeds, per-unit coloring becomes:**
```
For each unit you want custom colors:
  1. Create a new CharCLUT entry (new Key) with custom palette
  2. Create a new CharShape entry pointing to that CharCLUT
  3. Use OverrideEntryData to assign that Spriteset to the specific unit
```

### Test 5: Modify Key 254 Palette and Observe (NEW)
```
Even without charshape.nxd analysis, we can test if keys 254/255 affect anything:

  1. Modify charclut.nxd key 254 to use bright red colors
  2. Deploy to mod directory
  3. Start a new game and observe all units
  4. If ANY unit turns red, we know key 254 is actively used
```

---

## Implementation Approaches (Updated)

### Approach D: CharShape → CharCLUT NXD Pipeline (NEW - Most Promising)

**Theory:** Create per-unit palette control through the existing NXD system:
1. Create custom CharCLUT entries with desired palettes
2. Create CharShape entries that reference those CharCLUT entries
3. Use OverrideEntryData to assign different Spritesets per unit

**Pros:**
- Uses existing, proven NXD infrastructure
- No memory hooks or runtime patching needed
- Static configuration — set once, works every battle
- FF16Tools can create/edit all required NXD files
- Could support many palette variants per job

**Cons:**
- Requires charshape.nxd to affect generic units (unverified)
- May need understanding of Spriteset → CharShape key mapping
- Changing Spriteset might affect sprite body shape, not just palette
- Requires Test 4 validation before implementation

### Approach A: Frame-Based CLUT Patching (If Test 1 Passes)

**Pros:**
- No binary reverse-engineering needed
- Uses existing NXD infrastructure
- Could support unlimited color variants

**Cons:**
- Performance overhead (patching every frame)
- Race conditions with renderer
- Requires precise hook timing

### Approach B: overrideentrydata.nxd Extension (If Test 2 Succeeds)

**Pros:**
- Per-unit configuration at load time
- No runtime overhead
- Integrates with existing mod system

**Cons:**
- Requires reverse-engineering binary format
- May have limited color options
- Could break with game updates

### Approach C: Memory Hooks (Fallback)

**Pros:**
- Full control over palette data
- Can intercept at exact render moment

**Cons:**
- Complex implementation
- Game version dependent
- Potential stability issues
- Original approach that was deemed "too complex"

---

## BIN Palette Architecture (Reference)

### Palette Slot Usage in BIN Files
Each `.bin` sprite file has 16 palette slots (512 bytes total):

| Slot | Size | Purpose |
|------|------|---------|
| Palette 0 | 32 bytes | Player unit colors (themed) |
| Palette 1 | 32 bytes | Enemy variant 1 |
| Palette 2 | 32 bytes | Enemy variant 2 |
| Palette 3 | 32 bytes | Enemy variant 3 |
| Palette 4 | 32 bytes | Enemy variant 4 |
| Palettes 5-7 | 32 bytes each | Generic theme enemy variants |
| Palettes 8-15 | 32 bytes each | Unused (zeros) |

### Color Format: BGR555
```
ushort bgr555 = low_byte | (high_byte << 8);
int r5 = bgr555 & 0x1F;           // Bits 0-4
int g5 = (bgr555 >> 5) & 0x1F;    // Bits 5-9
int b5 = (bgr555 >> 10) & 0x1F;   // Bits 10-14
```

### Enemy Palette Selection
The game engine selects palette 0 for players and palettes 1-4 for enemies. This selection mechanism is **hardcoded in the game engine** — not controlled by NXD data. The mod's `fix_enemy_palettes.py` ensures palettes 1-4 are always copied from original sprites to prevent black enemies.

---

## Related Files in Codebase

| File | Purpose |
|------|---------|
| `RamzaNxdService.cs` | Current per-chapter NXD patching |
| `NxdPatcher.cs` | Low-level CLUT byte manipulation |
| `RamzaThemeSaver.cs` | Converts palettes to NXD format |
| `RamzaBinToNxdBridge.cs` | BGR555 ↔ RGB color conversion |
| `BinSpriteExtractor.cs` | Reads BIN palettes (16 slots) |
| `UserThemeApplicator.cs` | Applies themes (replaces first 512 bytes) |
| `PaletteModifier.cs` | Modifies palette 0 for theme editing |
| `EnemyPaletteTests.cs` | Documents palette slot contracts |
| `docs/NXD_FILE_FORMAT.md` | NXD format documentation |

### NXD Layout Files (tools/Nex/Layouts/ffto/)

| File | Purpose |
|------|---------|
| `CharShape.layout` | **Key file** — maps sprites to CLUT via `charclut+Id` |
| `CharCLUT.layout` | Palette storage schema (48-byte CLUTData) |
| `CharaColorSkin.layout` | Conditional palette selection (UserSituationId) |
| `CharShapeLUTParam.layout` | Rendering LUT parameters |
| `CharShapeShadowSize.layout` | Shadow rendering |
| `OverrideEntryData.layout` | Per-unit field patching (54 columns) |
| `CharaControlId.layout` | Character control mapping |

---

## Resources

- [FF16Tools GitHub](https://github.com/Nenkai/FF16Tools) - NXD editing tools (download CLI from Releases)
- [NXD Format Documentation](https://nenkai.github.io/ffxvi-modding/resources/formats/nxd/)
- [FFT:TIC Mod Loader](https://github.com/Nenkai/fftivc.utility.modloader)
- [Reloaded-II Documentation](https://reloaded-project.github.io/Reloaded-II/)

---

## Next Steps (Priority Order)

1. [ ] **Test 4: Extract and inspect charshape.nxd** — Determine if generic jobs have CharShape entries and what their `charclut+Id` values are
2. [ ] **Test 5: Modify key 254 palette** — Quick test to see if any units use the generic CLUT entries
3. [ ] Validate Test 1: Does modifying charclut.nxd mid-battle affect sprites?
4. [ ] Analyze Black Boco mod's overrideentrydata.nxd binary payload
5. [ ] Contact community modders about NEX API render hooks
6. [ ] If Test 4 succeeds: Build proof-of-concept with custom CharShape + CharCLUT for one generic job

---

## Conclusion

Per-unit color customization has a **new and promising path** via the CharShape → CharCLUT NXD pipeline:

1. **CharShape.layout** has a `charclut+Id` field linking sprites to palette data
2. **CharCLUT keys 254/255** prove the CLUT system handles non-character palettes (`CharaColorSkinId = 0`)
3. **OverrideEntryData** can patch Spriteset per-unit, potentially routing units through different CharShape entries
4. **The entire chain is NXD-based** — editable with FF16Tools, no memory hooks required

The critical validation is **Test 4**: extracting `charshape.nxd` and checking whether generic jobs have CharShape entries with `charclut+Id` references. If they do (even if set to 0), modifying those references to point at custom CharCLUT entries could unlock per-unit palette control for all characters.

---

*Research initiated: January 2026*
*Major update: March 2026 — CharShape pipeline discovery, keys 254/255 analysis*
*Status: Awaiting Test 4 validation (charshape.nxd extraction)*
