# Baseline snapshot — 2026-04-26_0124

Fourth playtest. After playtest #3 surfaced 10 friction items, all 10 were addressed (uncommitted changes; tests 4674→4687).

## Bridge log offsets
- live_log.txt — 117578 bytes / 1216 lines

## Battle: The Siedge Weald (loc 26) — mid-battle, Lloyd active

Game restarted between playtests; battle preserved. Lloyd is the current active unit at (9,8) HP=315/432 (already wounded). primary=Speechcraft secondary=Geomancy (note class label "Orator" vs primary skillset "Speechcraft" — exactly the disambiguation #9 was meant to address). Other units: Ramza, Wilham, possibly KO'd allies from prior playtest.

## Fixes shipped between playtest #3 and this playtest

The agent shouldn't be told these — fresh-eyes test.

1. **Move-confirm `NOT CONFIRMED` mitigation** — added "stale-state Enter pokes" up to 3 times, spaced 2s apart, when BattleMoving lingers. Defeats the IC-remaster "Move here? Yes/No" modal that was eating the F-confirm.
2. **BattleVictory recheck extended** to 5×800ms (4s total) from 3×800ms.
3. **Phoenix Down phantom-success guard** — when target was dead AND post-action HP still 0 AND ability is a known revive, response now reads `— REVIVE FAILED (target still 0/MaxHP)`.
4. **Cursor-vs-unit position bleed in screen header** — during BattleMoving / BattleAttacking / BattleCasting, the active-unit position cache is NOT updated (preserves pre-move pos). The game's static array can transiently report cursor pos as the unit's pos during move-preview; suppressing keeps the header accurate.
5. **TURN HANDOFF banner on EVERY response** — not just battle_wait/execute_turn. Snapshot identity at command start vs after; if changed, prepend banner. Idempotent via existing dedupe.
6. **`[OUTCOME]` split into `[OUTCOME yours] X | [OUTCOME enemies] Y`** — agent can now tell their action effects apart from ambient enemy state changes during the call.
7. **`battle_ability "Aim"` resolves to `Aim +1`** via `BattleAbilityNavigation.ResolveNumberedFamilyName` — strips `(+1 to +20)` family suffix and matches lowest-level concrete ability.
8. **Narrator move-attribution defense** — `MoveArtifactCoalescer` now suppresses moves with Manhattan distance > 8 (no FFT class can move that far in one turn).
9. **`UNIT LOST` banner** — when a player unit gains Treasure/Crystal status (crystallization), emits `=== UNIT LOST: Kenrick crystallized (permanent for this battle) ===` ahead of the OUTCOME recap.
10. **`battle_move` "Cursor already on (X,Y) — sent confirm; unit moves from its current tile to here"** — replaces the misleading "Already at (X,Y)" message that conflated cursor with unit position.

## Current state

```
[BattleMyTurn] ui=Move Lloyd(Orator) (9,8) HP=315/432 MP=73/73 curLoc=The Siedge Weald
  primary=Speechcraft secondary=Geomancy

(Abilities listed for Lloyd — Attack R:8 with Blaze Gun + full Geomancy suite)

Move tiles + 5 enemies still on field.
```
