<!-- This file should not be longer than 200 lines, if so prune me. -->
# Battle Memory Map (IC Remaster)

## Formation Screen
- **Space bar** opens "Commence Battle?" confirmation
- **Right arrow** moves unit placement cursor, **Enter** places a unit
- Battle stat arrays are NOT populated until battle starts

## 1. Roster Array (0x1411A18D0, stride 0x258)
Readable during battle. Key fields: spriteSet +0x00, job +0x02, secondary +0x07, reaction +0x08, support +0x0A, movement +0x0C, exp +0x1C, level +0x1D, brave +0x1E, faith +0x1F, nameId +0x230.

## 2. Condensed Battle Struct (0x14077D2A0, variable stride)
Rolling turn-order queue. Slot 0 = current unit. Updates in real-time (HP changes visible). Each entry followed by FFFF-terminated ability list (stride varies per unit).

| Offset | Field | Notes |
|--------|-------|-------|
| +0x00 | level | uint16 |
| +0x02 | team | 0=friendly, 1=enemy |
| +0x04 | nameId | uint16 |
| +0x08 | exp | uint16 |
| +0x0A | CT? | uint16 |
| +0x0C | HP | uint16 |
| +0x10 | maxHP | uint16 |
| +0x12 | MP | uint16 |
| +0x16 | maxMP | uint16 |
| +0x18 | PA? | uint16 |
| +0x1A | MA? | uint16 |
| +0x28+ | ability list | uint16[], FFFF-terminated |

## 3. Battle HP/MP Arrays
| Array | Address | Type |
|-------|---------|------|
| Current HP | 0x141024EC8 | uint32 x 21 |
| Current MP | 0x14102223C | uint32 x ~21 |
| Unit Existence | 0x14077CA30 | uint32 x 10 (0xFF=exists, 0xFFFFFFFF=terminator) |

## 4. Turn Region (0x14077CA60)
| Offset | Field |
|--------|-------|
| +0x60 | CT of acting unit |
| +0x6C | Job of acting unit |
| +0x8C | Action Taken flag (0→1) |
| +0x9C | Movement Taken flag (0→1) |

## 5. Action Menu & Navigation
- **Menu cursor**: `0x1407FC620` (byte, 0-4): Move=0, Abilities=1, Wait=2, Status=3, AutoBattle=4
- **Turn detection**: Team=0 AND Act=0 AND Mov=0 → friendly turn, action menu open
- **Wait sequence**: Down to cursor=2, Enter (select), Enter (confirm facing)

## 6. Movement Tile List (0x140C66315, 7 bytes/entry)
| Offset | Field |
|--------|-------|
| +0x00 | X coordinate |
| +0x01 | Y coordinate |
| +0x02 | Elevation (raw/2 = display height) |
| +0x03 | Flag (1=valid, 0=terminator) |

Entry[0] = active unit's current position. Cursor index at `0x140C64E7C`.

## 7. UI Buffer (0x1407AC7C0)
| Offset | Field |
|--------|-------|
| +0x00 | Level |
| +0x04 | NameId |
| +0x08 | Exp |
| +0x0A | CT |
| +0x0C | HP (stale) |
| +0x10 | MaxHP |
| +0x12 | MP |
| +0x16 | MaxMP |
| +0x18 | PA? |
| +0x24 | Move |
| +0x26 | Jump |
| +0x2A | Job |
| +0x2C | Brave |
| +0x2E | Faith |

## 8. Unit Position

**Live Position via C+Up Cycling (BEST METHOD):**
Hold C + press Up repeatedly — cursor snaps to each unit in turn order. Read grid pos (0x140C64A54 X, 0x140C6496C Y), team (0x14077D2A2), world pos (0x14077D360 X, 0x14077D362 Y).

**Condensed Turn Queue Position:**
X at 0x14077D360, Y at 0x14077D362 — updates per turn rotation. Background polling at 100ms catches all units.

**Starting Position via Heap Struct:**
Found via `search_bytes`. Starting X at +0x1A, Y at +0x23 from stat base. Does NOT update after movement.

## 9. Movement Tile Validity (Map-Based BFS)

**Map BFS (primary, 100% accurate):**
- Load map via `set_map <number>`, files in `claude_bridge/maps/MAP###.json`
- Grid coords = map tile coords (identity mapping)
- Height: `display = height + slope_height / 2`, jump check: `|heightA - heightB| <= jump`
- `no_walk` = impassable, enemy tiles block movement
- **Verified 6/6 tile counts match in-game exactly**

**`scan_move` action:** Scans units via C+Up, computes valid tiles via BFS, returns `ValidMoveTiles`.
Usage: `scan_move <move> <jump>`

**Known issue:** Move/Jump stats at UI buffer show base values (no equipment modifiers).

**Fallback BFS (no map loaded):** Terrain grid at 0x140C65000, approximate heights, ~12 false positives.

## 10. Tile Height
| Address | What | Encoding |
|---|---|---|
| 0x140C6492C | Cursor tile height × 10 | uint32, 25 = display 2.5 |
| 0x14077CA5C | Move mode flag | 0xFF = Move mode |

## 11. Map File Data (fft-map-json)
Source: `c:/Users/ptyRa/Dev/fft-map-json/data/MAP###.json`, deploy to `claude_bridge/maps/`. 122 maps available.
Tile fields: x, y, height, slope_height, depth, no_walk, no_cursor, surface_type, slope_type.
Maps have `lower`/`upper` levels (lower is primary) and `starting_locations` for team spawns.
Story battles and random encounters at same location use DIFFERENT maps. Terrain fingerprinting uniquely identifies all 122.

## 12. Screen Detection
```
inBattle = (slot0==255 AND slot9==0xFFFFFFFF) OR (slot9==0xFFFFFFFF AND battleMode in {2,3,4})
Battle_MyTurn = inBattle AND team==0 AND act==0 AND mov==0
Battle_Moving = inBattle AND battleMode==2
Battle_Attacking = inBattle AND battleMode==4
Battle_Abilities = inBattle AND submenuFlag==1 AND battleMode==3 AND team==0 AND (act==1 OR mov==1)
Battle_Paused = inBattle AND paused==1
GameOver = inBattle AND paused==1 AND battleMode==0 AND gameOverFlag==1
EncounterDialog = encA(0x140900824) != encB(0x140900828)
PartyMenu = partyFlag(0x140D3A41E)==1
TravelList = uiFlag(0x140D4A264)==1
TitleScreen = location(0x14077D208)==255
```

Battle mode values: 2=move tile selection, 3=action menu/ability browsing, 4=ability targeting.
Submenu flag (0x140D3A10C): 1=submenu/mode active (Move, Abilities, targeting), 0=top-level menu.

**Known issues:** Settlement/shop not reliably detected.

## 13. Attack Sequence
```
1. Down to menu=1 (Abilities), Enter
2. Enter (select Attack)
3. Direction key to target tile with enemy
4. Enter (select target), Enter (confirm attack)
5. Navigate to Wait (menu=2), Enter, Enter (confirm facing)
```

## 14. Quick Reference
| Question | How |
|----------|-----|
| Enemy count | Count Team=1 in turn queue |
| Whose turn | Turn queue slot 0 at 0x14077D2A0 |
| Unit alive? | HP > 0 at condensed struct +0x0C |
| Valid move tiles | scan_move or BFS from map JSON |
| Total units | Existence slots at 0x14077CA30 |

## 15. IC Remaster Roster Job IDs
The IC remaster uses different job IDs than PSX in the roster (+0x02). Verified values:
| ID | Job | ID | Job |
|----|-----|----|-----|
| 74 | Squire | 82 | Summoner* |
| 75 | Chemist* | 83 | Thief* |
| 76 | White Mage* | 84 | Orator* |
| 77 | Archer | 85 | Mystic* |
| 78 | Monk | 86 | Geomancer* |
| 79 | Knight | 87 | Dragoon* |
| 80 | Black Mage* | 88 | Samurai* |
| 81 | Time Mage* | 89 | Ninja |
*= estimated, not yet verified in-game

## Still Unmapped
- Facing direction, effective Move/Jump stats (UI buffer shows base, not equipment-modified)
- IC remaster roster job IDs for jobs between Knight(79) and Ninja(89) need verification
- See BATTLE_STATS_PSX_REFERENCE.md for complete PSX field layout
