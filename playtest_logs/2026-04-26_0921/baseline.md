# Baseline snapshot — 2026-04-26_0921

Seventh playtest. After playtest #6 surfaced 3 friction items, all 3 addressed.

## Bridge log offsets
- live_log.txt — 117608 bytes / 1216 lines

## Battle: The Siedge Weald (loc 26) — Lloyd as last man, wounded

Lloyd active at (9,8) HP=105/432. 3 live enemies (Time Mage adj at (9,9), 2 Archers at (5,11)/(6,10)). Difficult position; agent should focus on reading the fix surface, not winning.

## Fixes shipped between playtest #6 and this playtest

The agent shouldn't be told these — fresh-eyes test.

1. **`[TOO CLOSE]` tag now actually renders** — the `InRange` field on `AttackTileInfo` had `JsonIgnore(WhenWritingDefault)` so `false` was never serialized; renderer's `a.inRange === false` was unreachable. Removed the JsonIgnore. Cardinal panel now correctly shows `[TOO CLOSE]` for adjacent enemies a Blaze Gun / bow / crossbow can't reach due to MinRange.
2. **BattleVictory flicker on ability "cursor was already on target" branch** — the no-nav branch in `BattleAbility` (when `delta.x == 0 && delta.y == 0`) didn't pin `response.Screen`, so the wrapper's `??=` could re-catch a flicker. Now pins via `ResolveTerminalFlicker`.
3. **Hide-empty-skillset rule removed** — `AbilityCompactor.IsHidden` used to hide enemy-target abilities when no enemies were in range. This caused Lloyd's whole Speechcraft skillset to vanish from the dump even though Lloyd has it as primary. Now all learned abilities show with `(no targets in range)` for the empty case. (Note: if the skillset is equipped but ZERO abilities are learned, the dump won't have entries — that's an unlearned-skillset situation, documented in BattleTurns.md.)

## Current state

```
[BattleMyTurn] ui=Move Lloyd(Orator) (9,8) HP=105/432 MP=73/73
  primary=Speechcraft secondary=Geomancy

(Lloyd has no learned Speechcraft abilities — this is a generic-recruit
unlearned-primary situation. The dump shows Geomancy + Attack only.)
```
