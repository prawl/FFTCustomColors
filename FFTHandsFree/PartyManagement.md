# Party Management Guide

## Overview

The Party Menu lets you view and manage your units between battles. Access it from the world map.

## Accessing the Party Menu

1. On the world map, press Tab or navigate to the menu
2. Screen detection: `PartyMenu` (party=1)

## Unit Stats

Each unit has:
- **Level / EXP** — Level 1-99, EXP 0-99 per level
- **HP / MP** — Health and magic points
- **PA / MA** — Physical and Magick Attack power
- **Speed** — Determines turn order (higher = more turns)
- **Brave** — Affects physical abilities and Reaction trigger rate (0-100)
- **Faith** — Affects magick damage dealt AND received (0-100)
- **Move / Jump** — Movement range and height clearance
- **CT** — Charge Time accumulator (100 = ready to act)

## Jobs

- Each unit can change between unlocked jobs
- Jobs unlock by earning JP (Job Points) in prerequisite jobs
- Each job has a primary action ability set (e.g., Knight = Arts of War)
- Units can equip one secondary action ability from any learned set
- Reaction, Support, and Movement abilities can each equip one

## Equipment

- 5 equipment slots: Right Hand, Left Hand, Head, Body, Accessory
- Available equipment depends on the unit's current job
- Equipment affects stats (PA, MA, Speed, HP, MP, Move, Jump)
- Weapons determine Attack range and damage type

## Abilities

- **Action Abilities** — Active skills used in battle (Attack, spells, etc.)
- **Reaction Abilities** — Auto-trigger on specific events (Counter, Parry)
- **Support Abilities** — Passive bonuses (Dual Wield, Magick Defense Boost)
- **Movement Abilities** — Movement enhancements (Move +1, Teleport, Fly)

## Bridge Commands

```bash
# Navigate to party menu from world map
execute_action PartyMenu

# Party management is mostly manual for now
# Unit stats are read from the roster at 0x1411A18D0 (stride 0x258)
```

## Roster Memory Layout

Roster base `0x1411A18D0`, stride `0x258`, 50 slots max. See UNIT_DATA_STRUCTURE.md for the full field map. Equipment summary:

| Offset | Field | Size | Notes |
|--------|-------|------|-------|
| +0x07 | Secondary ability index | 1 byte | |
| +0x08 | Reaction ability ID | 1 byte | |
| +0x0A | Support ability ID | 1 byte | |
| +0x0C | Movement ability ID | 1 byte | |
| +0x0E | Helm item ID | u16 LE | FFTPatcher-canonical; 0xFF = empty |
| +0x10 | Body armor item ID | u16 LE | |
| +0x12 | Accessory item ID | u16 LE | |
| +0x14 | Right-hand weapon ID | u16 LE | |
| +0x16 | Left-hand weapon ID | u16 LE | dual-wield; 0xFF when normal loadout |
| +0x18 | Reserved | u16 LE | always 0xFF observed |
| +0x1A | Left-hand shield ID | u16 LE | |
| +0x1C | EXP | 1 byte | |
| +0x1D | Level | 1 byte | **level == 0 = empty slot** |
| +0x1E | Brave | 1 byte | |
| +0x1F | Faith | 1 byte | |
| +0x230 | Name ID | 2 bytes | |

**Item IDs use the FFTPatcher canonical 0-315 encoding.** Directly pluck into `ItemData.GetItem(id)` — no translation table needed. Verified 2026-04-14 via Ramza and Kenrick live dumps; see UNIT_DATA_STRUCTURE.md "Equipment Decoding" section for the proof table.

**HP / MP are NOT in the roster** — scanned Ramza's full 0x258 bytes for his displayed HP=719 and MP=138 and found zero matches. They must be runtime-computed from job base + equipment bonuses, or stored in a separate per-unit live stats table not yet located. Until that's found, the party grid surfaces equipment but not HP/MP.

## Active Party Filter

The game's party-menu header shows a `current/max` count like `16/50`. To reproduce that count from memory, iterate slots 0..49 and keep those where:

```
unitIndex != 0xFF  AND  level > 0
```

Either condition alone gives wrong results:
- `unitIndex == 0xFF` alone: some active story characters (Rapha, extra Construct 8 clones) carry 0xFF but are real party members.
- `level == 0` alone: some dismissed-but-still-allocated template slots have level > 0.

Both checks together match the in-game count exactly. See `RosterReader.IsEmptySlot` in `ColorMod/GameBridge/RosterReader.cs`.
