# Baseline snapshot — 2026-04-26_0905

Sixth playtest of this iteration. After playtest #5 surfaced 3 regressions in earlier fixes, all 3 have been investigated and re-fixed in this round. Tests still 4687.

## Bridge log offsets
- live_log.txt — 117028 bytes / 1209 lines

## Battle: The Siedge Weald (loc 26) — Lloyd as last man, wounded

Lloyd active at (9,8) HP=105/432 (badly wounded). 3 live enemies. Lloyd may not survive long; agent should focus on reading the fix surface, not winning.

## Regression fixes shipped between playtest #5 and this playtest

The agent shouldn't be told these — fresh-eyes test.

1. **Wrapper-level BattleVictory flicker resolve** — `ProcessCommand` now does its own 3×500ms recheck if `response.Screen` (after the `??=` fallback) is terminal. AND `BattleAttack` / `BattleAbility` now PIN their resolved screen on `response.Screen` so the wrapper's `??=` doesn't re-read the flicker post-handler. Two layers of defense for the same agent friction.
2. **Move-rejection check uses scan-canonical position, not cursor** — `MoveGrid` post-confirm now runs `CollectUnitPositionsFull()` and reads the active unit's authoritative `GridX/Y`, not the cursor. Catches the case where cursor reaches the target but the unit doesn't commit. Agent saw `execute_turn 8 8` report success while Lloyd actually stayed at (9,8).
3. **Position-preserve guard uses fresh DetectScreen, not lagged `_lastClassifiedScreen`** — `CacheLearnedAbilities` checks the screen state RIGHT NOW. The lagged `_lastClassifiedScreen` was tripping after battle_wait completed (still BattleMoving classification leftover), causing the post-wait scan to skip caching the active unit's position. Result: bare `[BattleMyTurn] ui=Move` header with no active-unit name.

## Current state

```
[BattleMyTurn] ui=Move Lloyd(Orator) (9,8) HP=105/432 MP=73/73
  primary=Speechcraft secondary=Geomancy

Abilities (verified rendering):
  Attack R:8 → (6,10)<Archer !blocked> (5,11)<Archer !blocked> (+67 empty tiles)
  Sinkhole R:5 AoE:2 → (9,8)<Lloyd SELF> (9,9)<Time Mage >rear> (6,10)<Archer> (+37 empty tiles)
    best: (9,9) e:Time Mage  (10,9) e:Time Mage  (6,10) e:Archer
```
