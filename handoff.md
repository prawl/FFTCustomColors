# Session Handoff — 2026-04-13 (Session 6)

Delete this file after reading.

## What Happened This Session

19 commits. Massive session. Live damage detection breakthrough, extensive battle testing, many bug fixes, instruction guides written.

## Current State

**Branch:** `auto-play-with-claude`
**Tests:** 1699 passing (up from 1667 at session start)
**Game:** In battle at The Siedge Weald (loc 26). Lloyd(Dragoon) at (7,4), Ramza at (8,4) HP=84/719 Critical, Kenrick(Knight) at (6,8), Wilham(Monk) at (9,7). Two Skeletal Fiends alive at (8,2) and (8,3), one with 95 HP.

## Major Breakthroughs

### 1. Live Damage Detection (THE big win)
After extensive investigation, discovered that **readonly memory regions** (0x141xxx-0x15Bxxx) contain live HP data that updates immediately after damage — unlike the static array at `0x140893C00` which is stale mid-turn. 

**How it works:** `ReadLiveHp()` in NavigationActions.cs:
1. Before attack: read target's HP+MaxHP and level from static array
2. After attack: search readonly memory (broadSearch=true) for MaxHP pattern
3. Compare HP at each match — find the one where HP changed
4. Match by level (from static array) to confirm it's the same unit
5. Report: HIT (preHp→postHp/maxHp), KO'd, or MISSED

**Key insight:** `SearchBytesInAllMemory` with `broadSearch: true` scans all readable memory including readonly regions. The search is split into two ranges:
- Static array (0x14089xxxx) — stale reference for the "unchanged" copy
- Readonly (0x141-0x15C) — live data

**Verified:** HIT 386, HIT 319, HIT 252, KO (345→0), MISSED (3 correct detections)

### 2. Damage Preview Investigation (Unsolved)
The game displays projected damage and hit% during targeting mode. Found values in multiple locations:
- **Attacker's heap struct:** statBase-62 (hit%), statBase-96 (damage) — worked in session 5 via probe_status but offsets are session-dependent
- **Readonly memory:** Found via search_all with distinctive pattern (16 zeros + hit% + bytes + damage) but addresses shift between searches
- **Problem:** Data moves in heap between reads. Can't reliably locate it.
- **Added `read_bytes` action** for arbitrary memory reads (up to 1024 bytes) to help future investigation
- `ReadDamagePreview()` exists in code but returns (0,0) — disabled

### 3. execute_action Wait Fixed
Was completely broken — `ExecuteValidPath` bypassed the confirmation check and routed through `ExecuteNavAction` instead of the main handler. Fixed by routing battle_wait through the full handler with pre-scan and turn reset. Also removed the `needs_confirmation` check which was blocking all Waits.

### 4. Ability List Navigation Fixed
The ability list **wraps circularly** — pressing Up from index 0 goes to the bottom. The old Up×20+Down×index approach caused wrong ability selection (Aurablast→Chakra). Fixed to just Down×index from position 0 (cursor starts at 0 when entering a skillset).

### 5. Many Bug Fixes
- **Cardinal-only attack:** Reverted — FFT Attack DOES include diagonals via Manhattan distance
- **Focus self-target:** HRange=Self abilities now show caster as target
- **Post-move range validation:** Store confirmed position, validate from there
- **Raw cursor trust:** Removed EffectiveMenuCursor corrections that caused wrong menu selections
- **ui= shows Reset Move:** After moving, index 0 shows "Reset Move"
- **Neutral unit BFS:** team=2 no longer blocks movement paths
- **_movedThisTurn:** Only set after successful move (was set before, causing issues on failed moves)
- **Cutscene during targeting:** Added battleMode==0 to battleModeActive when slot9 is battle sentinel
- **Move confirmation timeout:** Increased 5s→8s for long-distance walks
- **Cursor change logging:** Added to track ui= bugs

## Key Memory Addresses

| Address | Field | Notes |
|---------|-------|-------|
| 0x140893C00 | Static battle array | Stale mid-turn for HP/position |
| 0x141xxx-0x15Bxxx | Readonly live data | Updates immediately after damage |
| 0x14077D2A0 | Condensed struct | Shows active/hovered unit |
| 0x14077D2AC | Condensed HP (u16) | Active unit during Battle_MyTurn, target during targeting |
| 0x14077D2B0 | Condensed MaxHP (u16) | Same |
| 0x1407FC620 | Menu cursor (byte) | 0=Move, 1=Abilities, 2=Wait, 3=Status, 4=AutoBattle |
| 0x140C0EB20 | Submenu counter (u16) | Tracks cursor movement in ability submenus |

## Battle Flow (Updated)

```bash
source ./fft.sh
screen              # see battlefield (ui=Move at turn start)
battle_move 7 6     # move to tile (validates against BFS)
battle_attack 8 6   # attack (validates range from post-move position)
                    # response: HIT (500→181/500) or MISSED! or KO'd!
execute_action Wait # ends turn, auto-shows next screen
```

Key commands updated:
- `battle_attack` — now reports HIT/MISS/KO with live HP detection
- `battle_ability "Aurablast" x y` — fixed ability list navigation (Down-only)
- `execute_action Wait` — fixed routing, 60s timeout
- `screen` — Focus shows self, ALLY tags, server-side filtering

## Known Bugs Still Open

- **[Cutscene] in attack response** — Screen detection catches Cutscene during attack animation (cosmetic, attack works)
- **battle_ability selects wrong ability** — Still possible if cursor doesn't start at index 0 after entering skillset
- **Scan disrupts targeting mode** — Running scan_move during targeting changes screen state
- **BFS terrain too permissive** — Height costs not matching FFT's rules
- **Static array stale mid-turn** — Positions and HP don't update until turn boundary
- **Damage preview unsolved** — Hit%/damage values found in heap but addresses shift between reads

## Files Changed This Session

| File | Changes |
|------|---------|
| NavigationActions.cs | Live damage detection (ReadLiveHp, ReadStaticArrayHpAt), ability list fix (Down-only), post-move range validation, self-target tiles, raw cursor, damage preview framework |
| CommandWatcher.cs | execute_action Wait fix, post-move position tracking, cursor logging, _movedThisTurn fix, neutral BFS |
| ScreenDetectionLogic.cs | battleMode==0 in battleModeActive for targeting flicker |
| MemoryExplorer.cs | broadSearch flag, read_bytes action |
| AbilityCompactor.cs | Server-side ability filtering (new file) |
| BattleAbilityNavigation.cs | All-skillsets fallback for FindAbility |
| BattleTracker.cs | Sticky inBattle flag, removed dead heap code, poll diagnostic |
| ItemData.cs | MinRange corrected (guns/bows=2, crossbows=3) |
| AttackDirectionLogic.cs | RightDeltaFromCameraRotation for Jump fallback |
| InputSimulator.cs | Reverted to PostMessage (SendInput failed) |
| fft.sh | ui= in header, ALLY tags, simplified ability display, 60s Wait timeout |
| UnitNameCache.cs | Cache enemy class names by position (new file) |
| TODO.md | Cleaned 20+ completed items, added new bugs |
| FormationScreen.md | New instruction guide |
| SaveLoad.md | New instruction guide |
| PartyManagement.md | New instruction guide |

## Next 5 Priorities

1. **Damage preview (hit%/damage)** — Need reliable way to read projected values during targeting. Heap search finds values but they move. Consider computing damage from FFT formula instead.
2. **BFS terrain height** — Movement validation too permissive. Need to match FFT's actual height cost rules.
3. **Scan disrupts targeting** — scan_move should be read-only during targeting mode (no key inputs).
4. **Move/Jump stat reading** — Equipment bonuses not reflected. Static array has base stats, need effective stats.
5. **battle_ability wrong ability** — Cursor might not be at index 0 when entering skillset. Need counter-delta verification.
