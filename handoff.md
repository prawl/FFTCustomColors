# Session Handoff — 2026-04-14 (Session 7)

Delete this file after reading.

## What Happened This Session

BFS movement overhaul + 5 targeted bug fixes. Extensive memory investigation (3 agents) for game-native valid tiles — concluded they're not at a static address. Pivoted to fixing the BFS itself with TDD, applied two confirmed rules: ally traversal penalty and movement ability bonuses.

## Current State

**Branch:** `auto-play-with-claude`
**Tests:** 1710 passing (up from 1699)
**Game:** Mid-battle at The Siedge Weald. Wilham's turn (Monk at 10,6). Aurablast selection broken — cursor miss. Gobbledygook killed earlier by Lloyd's Jump. Two enemies alive at (9,4) and (10,4) Skeletal Fiends.

## Major Wins

### 1. BFS Movement Fix (THE big win)
Created `MovementBfs.cs` with TDD. Two critical rules added:

**Ally traversal penalty** — confirmed via game observation: walking THROUGH an ally tile costs +1 extra move point, and you can't STOP on an ally tile. Without this rule, BFS computed 13 tiles for Kenrick when the game showed 10. With the rule: perfect 10/10 match.

**Movement ability bonuses** — `ApplyMovementAbility()` parses names like "Movement +3" and "Jump +2" and adds the bonus to base Move/Jump. Before: Ramza with Movement +3 showed 18 valid tiles (using base Move=4). After: 34 tiles (using effective Move=7). User confirmed 36 is the correct count — remaining 2 tiles are likely cliff/slope edge cases.

### 2. Five Other Bug Fixes
- **Scan targeting disruption** — removed Battle_Attacking/Casting from scan-safe screens
- **execute_action missing ui= field** — always DetectScreen after key press
- **BFS terrain costs** — MapTile.MoveCost now handles Swamp/Lava (2) and depth water (1+depth)
- **Battle_Casting misdetection** — battle_move confirmation poll ignores Battle_Casting flicker
- **Ability list counter-delta verification** — retries lost keypresses via 0x140C0EB20 counter

### 3. Memory Investigation (negative result, but thorough)
3 agents scanned for game-native valid tile storage. Findings in `memory/project_movement_bitmap.md`:
- **Tile list at 0x140C66315** = perimeter outline (world coords), NOT valid tiles
- **Rendering struct at 0x140C6F400** = volatile per-frame, counts vary per read
- **Heap struct at 0x3D89D20** had tileCount=6 matching Wilham's tiles, but tile indices couldn't be decoded through the lookup table
- Attack tiles are NOT at a static address — game computes them dynamically

Conclusion: stop hunting game-native storage, keep improving the BFS.

## Files Changed This Session

| File | Changes |
|------|---------|
| ColorMod/GameBridge/MovementBfs.cs | NEW: extracted BFS from CommandWatcher, added ally penalty + ApplyMovementAbility |
| ColorMod/GameBridge/MapLoader.cs | MoveCost handles Swamp/Marsh/Poisoned marsh/Lava (cost 2), water with depth (1+depth) |
| ColorMod/GameBridge/NavigationActions.cs | Added GetAllyPositions(), removed Battle_Attacking/Casting from scan-safe screens, battle_move confirmation ignores Battle_Casting, added counter-delta retry for ability list nav |
| ColorMod/Utilities/CommandWatcher.cs | execute_action always DetectScreen, wires MovementBfs with ally positions + movement ability name |
| ColorMod/GameBridge/ScreenDetectionLogic.cs | No changes kept (tried moveMode disambiguation, reverted) |
| Tests/GameBridge/MovementBfsTests.cs | NEW: 1 ally-penalty test + 10 theory cases for ApplyMovementAbility |

## Known Bugs Still Open (in priority order)

1. **Auto-Battle triggers instead of Wait** — After battle_move confirmation, execute_action Wait sometimes navigates to AutoBattle (index 4) instead of Wait (index 2). Likely menu cursor desync with ui=Reset Move state.
2. **Submenu sticky cursor** — Abilities submenu remembers last selected skillset between visits; battle_attack assumes cursor is at Attack but it's wherever we left it.
3. **Cast-time abilities report "Used" not "Queued"** — Haste/Gravity/etc. with ct>0 are queued, not instant. Response is misleading.
4. **Jump auto-ends turn** — battle_ability should detect turn auto-ended and skip the redundant Wait prompt.
5. **Aurablast cursor miss** — "Cursor miss: at (10,6) expected (9,4)" — targeting navigation didn't move cursor to target.
6. **BFS still 2 tiles off for Ramza** — 34 found, 36 actual. Likely cliff/slope height transitions not handled per-direction.
7. **Equipment Move/Jump bonuses** — Battle Boots, Germinas Boots, Red Shoes give Move/Jump — not yet applied. Item ID mapping is inconsistent between IC Remaster and FFTPatcher so we can't lookup by ID reliably.
8. **Secondary skillset null** — RosterMatcher inconsistent; scan_move populates it, regular screen call doesn't.

## Key Memory Addresses

| Address | Field | Notes |
|---------|-------|-------|
| 0x140C66315 | Tile list perimeter outline | NOT valid tiles — just rendering path |
| 0x140C6F400 | Rendering struct base | stride 0x88, flag at +0x1D, volatile per-frame |
| 0x1407AC7E4 | Move (UI buffer) | BASE value only, no ability/equipment bonuses |
| 0x1407AC7E6 | Jump (UI buffer) | Same |
| 0x3D89D20+ | Movement calc struct | Has X, Y, Move, Jump, tileCount — tile indices undecoded |

## Next 5 Priorities

1. **Fix menu cursor desync after move** — Auto-Battle instead of Wait is a frequent blocker. Investigate how ui=Reset Move affects cursor position tracking.
2. **Submenu sticky cursor** — battle_attack should always verify/reset cursor to Attack (index 0) when entering submenu, not assume.
3. **Cast-time ability response text** — Change "Used X" to "Queued X (ct=N)" for ct>0 abilities so Claude knows to Wait.
4. **Jump auto-end detection** — battle_ability checks screen state post-confirm; if unit is no longer active, report turn ended.
5. **Equipment Move/Jump bonuses** — Need reliable item ID mapping. Consider reading item data from heap where names might be resolved.

## Battle Flow (Verified Working)

```bash
source ./fft.sh
screen              # see battlefield, ability targets with ALLY tags
battle_move 7 8     # now uses effective Move stat (with Movement +3 bonus)
battle_attack 8 6   # basic attack with HIT/MISS/KO detection
battle_ability "Gravity" 8 4  # casts with ct>0, queued in timeline
execute_action Wait # end turn (SOMETIMES triggers AutoBattle bug)
```

## Memory Notes Added

- `project_movement_bitmap.md` — game-native tile storage hunt findings
- `feedback_revive_height.md` — Revive requires same-height tiles (VRange=0 mystery)
- `feedback_submenu_sticky_cursor.md` — Abilities submenu remembers position
- `feedback_cast_time_abilities.md` — ct>0 is queued, not instant
