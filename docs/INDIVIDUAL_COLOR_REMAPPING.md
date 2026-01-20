# Individual Unit Color Remapping Research

**Status:** Research / Theoretical
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

## Alternative: overrideentrydata.nxd

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

### Research Path

1. Extract `overrideentrydata.nxd` from Black Boco mod
2. Compare binary payloads between different unit entries
3. Look for patterns that correlate with visual color differences
4. Document the binary format if discovered

---

## Validation Steps

Before investing in implementation, validate these assumptions:

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

---

## Implementation Approaches

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

## Related Files in Codebase

| File | Purpose |
|------|---------|
| `RamzaNxdService.cs` | Current per-chapter NXD patching |
| `NxdPatcher.cs` | Low-level CLUT byte manipulation |
| `RamzaThemeSaver.cs` | Converts palettes to NXD format |
| `RamzaBinToNxdBridge.cs` | BGR555 ↔ RGB color conversion |
| `docs/NXD_FILE_FORMAT.md` | NXD format documentation |

---

## Resources

- [FF16Tools GitHub](https://github.com/Nenkai/FF16Tools) - NXD editing tools
- [NXD Format Documentation](https://nenkai.github.io/ffxvi-modding/resources/formats/nxd/)
- [FFT:TIC Mod Loader](https://github.com/Nenkai/fftivc.utility.modloader)
- [Reloaded-II Documentation](https://reloaded-project.github.io/Reloaded-II/)

---

## Next Steps

1. [ ] Validate Test 1: Does modifying charclut.nxd mid-battle affect sprites?
2. [ ] Analyze Black Boco mod's overrideentrydata.nxd binary payload
3. [ ] Contact community modders about NEX API render hooks
4. [ ] Document findings and update this file

---

## Conclusion

Per-unit color customization is **theoretically possible** via the NEX API if:
- Palettes are parsed per-frame (not cached at load)
- OR the `overrideentrydata.nxd` binary format can be decoded

The commenter's suggestion implies knowledge that FFT:TIC's renderer accesses palette data dynamically. This is worth investigating as it could unlock a major feature without the complexity of memory hooks.

---

*Research initiated: January 2026*
*Status: Awaiting validation tests*
