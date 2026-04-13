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

| Offset | Field | Size |
|--------|-------|------|
| +0x07 | Secondary ability index | 1 byte |
| +0x08 | Reaction ability ID | 1 byte |
| +0x0A | Support ability ID | 1 byte |
| +0x0C | Movement ability ID | 1 byte |
| +0x0E | Equipment IDs (7 slots) | 14 bytes |
| +0x1C | EXP | 1 byte |
| +0x1D | Level | 1 byte |
| +0x1E | Brave | 1 byte |
| +0x1F | Faith | 1 byte |
| +0x230 | Name ID | 2 bytes |
