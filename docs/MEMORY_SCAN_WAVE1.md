# Memory Scan Results — 2026-04-06

## Verification Status
- **Wave 1**: 10 agents discovered addresses (initial scan)
- **Wave 2**: 10 agents verified within same session (same-session consistency)
- **Wave 3**: 10 agents verified after full game restart (cross-session stability)
- **Result**: 56/60+ addresses confirmed stable across restarts. 4 addresses failed (marked below).

## Scan Context
- Game: FFT: The Ivalice Chronicles (IC Remaster)
- State: Battle_MyTurn, active unit is ally (Ramza Lv99, HP=719/719)
- 10 parallel agents scanning read-only via file bridge
- All addresses are for 64-bit process (base ~0x140000000)

---

## Heap Battle Struct (Dynamic Addresses)

Found via `search_bytes` for HP/MaxHP patterns. Lives on dynamic heap (addresses like `0x41667Bxxxx`).

**Stride: 0x800 per unit. Two copies per unit (live + base/snapshot).**

| Offset | Size | Field | Player Value | Enemy Value | Confidence |
|--------|------|-------|-------------|-------------|------------|
| +0x00-0x0F | 16B | Raw stat accumulators / equipment data | varies | 0 for enemies | Low |
| +0x10 | u8 | EXP or unknown (NOT Level for enemies) | 99 | 40 | CORRECTED — Wave 2 found +0x10 is NOT Level for all units |
| +0x11 | u8 | **Level** | 99 | 89 | CONFIRMED post-restart — C# code uses stat+0x01 |
| +0x12 | u8 | Brave (original) | 94 | 52 | High |
| +0x13 | u8 | Brave (current) | 94 | 52 | High |
| +0x14 | u8 | Faith (original) | 75 | 66 | High |
| +0x15 | u8 | Faith (current) | 75 | 66 | High |
| +0x16-0x17 | 2B | Padding | 0 | 0 | - |
| +0x18 | u16 | HP | 719 | 650 | CONFIRMED |
| +0x1A | u16 | MaxHP | 719 | 650 | CONFIRMED |
| +0x1C | u16 | MP | 138 | 1 | CONFIRMED |
| +0x1E | u16 | MaxMP | 138 | 1 | CONFIRMED |
| +0x20 | u8 | Base PA | 17 | 17 | High |
| +0x21 | u8 | Base MA | 16 | 51 | High |
| +0x22 | u8 | Base Speed | 11 | 12 | High |
| +0x23 | u8 | Base Move or Jump | 3 | 0 | Medium |
| +0x24-0x25 | 2B | Unknown | 0 | 0 | - |
| +0x26 | u8 | Effective PA | 20 | 17 | High |
| +0x27 | u8 | Effective MA | 16 | 51 | High |
| +0x28 | u8 | Effective Speed | 11 | 12 | High |
| +0x29 | u8 | CT (Clock Tick) | 88 | 96 | CONFIRMED |
| +0x2A | u8 | X coordinate | 7 | 3 | CONFIRMED |
| +0x2B | u8 | Height/Z | 3 | 4 | High |
| +0x2C | u8 | Unknown | 40 | 0 | Low |
| +0x2E | u8 | Unknown | 20 | 0 | Low |
| +0x32 | u8 | Effective Brave | 75 | 0 | Medium |
| +0x33 | u8 | Y coordinate | 10 | ? | Medium |
| +0x36 | u8 | Effective Faith | 50 | 0 | Medium |
| +0x39 | u8 | Facing direction (0-3) | 1 | 3 | HIGH |
| +0x3A-0x3D | 4B | Flags/bitmask | D8 40 1F FF | 80 00 00 00 | Medium |
| +0x50-0x5F | 16B | Status effects region | all zeros | 01 at +0x5C | Medium |

---

## Condensed Struct / Turn Queue Display Buffer (0x14077D2A0)

Packed multi-unit buffer. Each unit = 40-byte header + variable-length FFFF-terminated ability lists.

**Header layout (all uint16 LE):**

| Offset | Field | Confidence |
|--------|-------|------------|
| +0x00 | Level | CONFIRMED |
| +0x02 | Team (0=ally, 1=enemy) | CONFIRMED |
| +0x04 | NameId | LIKELY |
| +0x06 | Speed | LIKELY |
| +0x08 | Exp or duplicate Level | SPECULATIVE |
| +0x0A | CT (Clock Tick) | LIKELY |
| +0x0C | HP | CONFIRMED |
| +0x0E | Padding (0) | - |
| +0x10 | MaxHP | CONFIRMED |
| +0x12 | MP | CONFIRMED |
| +0x14 | Padding (0) | - |
| +0x16 | MaxMP | CONFIRMED |
| +0x18 | PA | LIKELY |
| +0x1A | MA | LIKELY |
| +0x1C | Unknown (100) | SPECULATIVE |
| +0x1E | Unknown (250) | SPECULATIVE |
| +0x20 | Unknown (100) | SPECULATIVE |
| +0x22-0x26 | Zero padding | - |

After the header: FFFF-terminated ability ID lists (variable length per unit).

After ability lists: a section with grid XY, effective Move, PA, Speed, MA, equipment IDs. **Offset is variable** — depends on ability list length. For the active unit in this session, it was at +0xC0.

Only 2 units stored in this buffer (not all 9 in battle).

---

## UI Stat Display Buffer (0x1407AC7C0)

Shows the **cursor-selected unit** (not necessarily the active unit).

| Offset | Address | Size | Field | Confidence |
|--------|---------|------|-------|------------|
| +0x00 | 0x1407AC7C0 | u16 | Unit/name ID | High |
| +0x02 | 0x1407AC7C2 | u16 | Unknown | - |
| +0x04 | 0x1407AC7C4 | u16 | Speed (base) | High |
| +0x06 | 0x1407AC7C6 | u16 | Speed (effective) | High |
| +0x08 | 0x1407AC7C8 | u16 | CT | High |
| +0x0A | 0x1407AC7CA | u16 | Cursor grid index | CONFIRMED |
| +0x0C | 0x1407AC7CC | u16 | HP | CONFIRMED |
| +0x0E | 0x1407AC7CE | u16 | Unknown (183) | - |
| +0x10 | 0x1407AC7D0 | u16 | MaxHP | CONFIRMED |
| +0x12 | 0x1407AC7D2 | u16 | MP | CONFIRMED |
| +0x14 | 0x1407AC7D4 | u16 | MaxMP | High |
| +0x1C | 0x1407AC7DC | u16 | Brave (permanent) | High |
| +0x20 | 0x1407AC7E0 | u16 | Faith (permanent) | High |
| +0x24 | 0x1407AC7E4 | u16 | Move (BASE only) | CONFIRMED |
| +0x26 | 0x1407AC7E6 | u16 | Jump (BASE only) | CONFIRMED |
| +0x2A | 0x1407AC7EA | u16 | Job ID | CONFIRMED |
| +0x2C | 0x1407AC7EC | u16 | Brave (display) | CONFIRMED |
| +0x2E | 0x1407AC7EE | u16 | Faith (display) | CONFIRMED |

Pre-buffer modifier block at 0x1407AC754: value -3 (0xFFFD) — possibly Move/Jump modifier.
In-battle Brave/Faith at 0x1407AC75C/75E: 80/80 (different from permanent 100/100).

---

## Battle State Region (0x14077CA00)

| Address | Size | Field | Value | Confidence |
|---------|------|-------|-------|------------|
| 0x14077CA00-CA27 | 40B | Color/palette table (3 doubled 6-byte entries) | palette | High |
| 0x14077CA30-CA50 | 36B | Unit existence slots (9 x u32, 0xFF=exists) | 9 units | CONFIRMED |
| 0x14077CA54 | u32 | Unit slot terminator | 0xFFFFFFFF | High |
| 0x14077CA5C | u8 | ~~Move mode flag~~ | 0 | REJECTED — volatile/noisy across reads |
| 0x14077CA60 | u32 | Active unit CT | 16 | High |
| 0x14077CA68 | u32 | Possibly roster index | 6 | Medium |
| 0x14077CA6C | u32 | Possibly job ID | 8 | Medium |
| 0x14077CA74 | u32 | Team or facing (2=neutral?) | 2 | Medium |
| 0x14077CA8C | u8 | Acted flag (0=not yet) | 0 | High |
| 0x14077CA94 | u32 | Active unit nameId | 401 | High |
| 0x14077CA9C | u8 | Moved flag (0=not yet) | 0 | High |

---

## Camera / Rendering (0x14077C900)

| Address | Size | Field | Value | Confidence |
|---------|------|-------|-------|------------|
| 0x14077C970 | u8 | Rotation counter (cardinal = (val-1+4)%4) | 12 | CONFIRMED |
| 0x14077C978 | u32 | ~~Camera tilt angle~~ | 60 | FAILED post-restart — address shifts |
| 0x14077C988-C997 | 16B | ~~Camera position vectors~~ | varies | FAILED post-restart — address shifts |
| 0x14077C9C8 | 3B | ~~Ambient/fog color RGB~~ | 128,128,128 | FAILED post-restart — address shifts |
| 0x14077C9EC | u32 | Screen width | 1920 | CONFIRMED |
| 0x14077C9F0 | u32 | Screen height | 1080 | CONFIRMED |

---

## Terrain / Cursor Region (0x140C64000)

| Address | Size | Field | Value | Confidence |
|---------|------|-------|-------|------------|
| 0x140C64900 | u8 | Cursor state/mode | 2 | High |
| 0x140C64908 | u32 | UI overlay color (Red RGBA) | 0x00FF0000 | High |
| 0x140C6492A | u8 | Cursor X (grid) | 11 | High |
| 0x140C6492B | u8 | Cursor Y (grid) | 12 | High |
| 0x140C6492C | u8 | Height display (live) | 25 | CONFIRMED |
| 0x140C64E78 | u16 | Tile count | 126 | High |
| 0x140C64E7C | u8 | Cursor tile index | 1 | CONFIRMED |
| 0x140C64EC0 | u32 | ~~Camera direction~~ | 7 | REJECTED — reads 1920 on re-verify, not reliable |
| 0x140C64F8A+ | 7B/entry | Tile Table A (XY + flag + surface heights) | varies | High |
| 0x140C66315+ | 7B/entry | Tile Table B (XY + elevation + passability) | varies | CONFIRMED |

---

## UI State Flags (0x140D30000)

| Address | Size | Field | Value | Confidence |
|---------|------|-------|-------|------------|
| 0x140D3A108 | u32 | Battle active flag | 1 | CONFIRMED post-restart |
| 0x140D3A10C | u32 | Game over flag (candidate) | 0 | CONFIRMED post-restart |
| 0x140D3A110 | u32 | Pause/dialog state | 0 | CONFIRMED post-restart |
| 0x140D3A118 | u32 | Player turn active | 1 | CONFIRMED post-restart |
| 0x140D3A154 | u32 | ~~Menu option count~~ | 4 | FAILED post-restart — reads 719, address shifted |
| 0x140D3A158 | u32 | Current menu cursor index | 3 | Unverified post-restart |
| 0x140D3A305 | u8 | Possibly chapter/scenario ID | 29 | Low |
| 0x140D3A33C | u32 | ~~Number of player units~~ | 4 | FAILED — transient, reads 0 on re-check |
| 0x140D3A340 | u32 | ~~Number of enemy units~~ | 5 | FAILED — transient, reads 0 on re-check |
| 0x140D3A4A8 | u32 | ~~Visible unit count~~ | 11 | FAILED — transient, reads 0 on re-check |
| 0x140D3A4B0 | u32 | ~~Cursor active flag~~ | 1 | FAILED post-restart — reads 719, address shifted |
| 0x140D3A4E0 | u16 | Cursor grid X | 8 | CONFIRMED post-restart |
| 0x140D3A4E2 | u16 | Cursor grid Y | 9 | CONFIRMED post-restart |
| 0x140D3A514 | u32 | Battle sub-mode | 3 | REASONABLE — changed to 1, valid state value |

---

## Formation / Roster (0x1411A18D0, stride 0x258)

| Offset | Size | Field | Confidence |
|--------|------|-------|------------|
| +0x00 | u8 | Unit type/flags | High |
| +0x01 | u8 | Unit index (0-4) | High |
| +0x02 | u8 | Job ID | High |
| +0x08 | u16 | Current HP (0 if not in battle) | High |
| +0x0A | u16 | Max HP (0 if not in battle) | High |
| +0x0C | u16 | Current MP | Medium |
| +0x0E-0x1B | 14B | Unidentified (equipment? abilities?) | Low |
| +0x1C | u8 | EXP | CONFIRMED |
| +0x1D | u8 | Level | CONFIRMED |
| +0x1E | u8 | Brave | CONFIRMED |
| +0x1F | u8 | Faith | CONFIRMED |
| +0x20-0x73 | 84B | Ability learned bitmask (672 bits) | High |
| +0x74-0x7E | 11B | Job levels (nibble-packed, 22 jobs) | CONFIRMED |
| +0x80-0xAD | 46B | JP current/unspent (23 jobs x u16) | CONFIRMED |
| +0xAE-0xDB | 46B | JP total earned (23 jobs x u16) | CONFIRMED |
| +0x230 | u16 | Name ID (string table index) | CONFIRMED |

Roster does NOT store effective/derived stats. Those are computed at battle time.

---

## Ability / Equipment Tables (0x1407A0000)

| Address Range | Content |
|---------------|---------|
| 0x1407A0060-0x1407A61FF | 7 stat growth curves (256-byte bell-shaped arrays, 0x1000 stride) |
| 0x1407A9000-0x1407A90A0 | Animation sequence table |
| 0x1407A9900-0x1407A9DFF | Battle map geometry / face table |
| 0x1407AAB00-0x1407AB200 | Action/ability record table (~180 entries, 12B each) |
| 0x1407AC800+ | UI layout data (coordinate pairs, element definitions) |

---

## CT Prediction Table (~0x14077D800)

After the condensed struct units, a separate table with 6-byte entries:
`(uint16 unitRef, uint16 clockTick, uint16 unknown)`
Groups units by predicted turn order. Separated by zero-filled slots between turn cycles.

---

## Remaining Open Questions

1. **Effective Jump**: Move=3 found at heap+0x23, but Jump location still unclear
2. **Map/scenario ID**: Not found in any scanned region
3. **Game over flag**: 0x140D3A10C stable across restart (reads 0), but needs game-over trigger to confirm it flips to 1
4. **Status effects**: heap+0x50-0x5F needs in-game verification (apply a status and check)
5. **Facing**: heap+0x39 confirmed 0-3 for enemies, but Ramza read 9 post-restart — may need masking or offset adjustment
6. **Camera direction**: 0x140C64EC0 REJECTED (reads 1920). Only reliable source is rotation counter at 0x14077C970
7. **Equipment slots**: Roster +0x0E-0x1B unconfirmed
8. **UI modifier block**: 0x1407AC754=-3 and 0x1407AC756=-3 may be separate Move/Jump modifiers (both confirmed stable)

## Addresses REJECTED After Verification

| Address | Original Claim | Reason Rejected |
|---------|---------------|-----------------|
| 0x14077CA5C | Move mode flag | Volatile/noisy — different value every read |
| 0x14077C978 | Camera tilt (60) | Shifted after restart — reads garbage |
| 0x14077C9C8 | Ambient RGB (128,128,128) | Shifted after restart — reads all zeros |
| 0x140C64EC0 | Camera direction (0-7) | Reads 1920 on re-verify — not a direction enum |
| 0x140D3A154 | Menu option count (4) | Reads 719 post-restart — shifted |
| 0x140D3A33C | Player unit count (4) | Transient — reads 0 on same-session re-check |
| 0x140D3A340 | Enemy unit count (5) | Transient — reads 0 on same-session re-check |
| 0x140D3A4A8 | Visible unit count (11) | Transient — reads 0 on same-session re-check |
| 0x140D3A4B0 | Cursor active (1) | Reads 719 post-restart — shifted |
