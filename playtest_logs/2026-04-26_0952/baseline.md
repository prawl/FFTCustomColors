# Baseline snapshot — 2026-04-26_0952

Eighth playtest. Playtest #7's three top friction items shipped:
1. execute_turn now aggregates per-step Info via ExecuteTurnInfoAggregator
   and surfaces the turn-interrupt reason on terminal screens. Previously
   `[GameOver] t=20s` returned with NO Info — the abort message got
   overwritten by a join that lost it.
2. Render split: `[Treasure]`/`[Crystal]`/`[Dead]` no longer appear
   inside the alive-status block; lifeState surfaces as a separate ` DEAD`
   / ` TREASURE` / ` CRYSTAL` / ` STONE` suffix. (Server-side
   StatusDecoder.DecodeAliveStatuses + LifeState read from full status
   bits not just HP.)
3. scan_units removed from BattleTurns.md / Commands.md / strict-mode
   error message; `screen` is the documented canonical battle scan.

Also +1 incidental fix from this playtest setup: bash heredoc broke when
the JS comment I added to fft.sh:3805 contained backtick-quoted words —
fixed by stripping the backticks (matches feedback_bash_heredoc_quote_breakage.md
pattern, second instance now seen).

## Bridge log offsets
- live_log.txt — 122490 bytes / 1266 lines

## Battle: The Siedge Weald (loc 26) — Lloyd as last man, wounded

Same starting position as playtest #7 — restart bounced back to the
auto-loaded last-stand state. Lloyd active at (9,8) HP=105/432, 3 live
enemies (Time Mage adj at (9,9), 2 Archers at (5,11)/(6,10)), 3 KO'd
allies (Ramza dead on counter, Kenrick crystal-treasure, Wilham dead),
1 enemy crystal-treasure (Summoner), 1 enemy dead (Knight). Difficult
position; agent should focus on reading the fix surface, not winning.

This is intentional — running playtest #8 against the SAME scenario
that produced playtest #7's friction lets us A/B the fixes. If Lloyd
dies again on the same first action, we'll see whether `[GameOver]`
now carries useful Info instead of being silent.

## Fixes shipped between playtest #7 and this playtest

The agent shouldn't be told these — fresh-eyes test.

1. **execute_turn Info aggregation** — `ExecuteTurnInfoAggregator.Aggregate`
   prefixes each step's Info with `> [action]` so fft.sh's narrator
   surfaces it; appends a `> [turn-interrupt]` line carrying the
   abort reason on terminal screens (GameOver/Victory/Desertion).
   Aggregation moved from inline `string.Join` (which silently
   overwrote the interrupt message) to the pure helper. +10 tests.
2. **lifeState render split** — `StatusDecoder.DecodeAliveStatuses()`
   filters Crystal/Dead/Treasure/Petrify out of the rendered
   alive-status block; `BuildUnitResponses.LifeState` now reads
   the full life-state from status bits (was HP=0-only, missed
   crystal/treasure). fft.sh renders ` DEAD`/` CRYSTAL`/` TREASURE`
   /` STONE` as a distinct suffix. +7 tests.
3. **scan_units doc/impl reconcile** — removed scan_units from
   BattleTurns.md (Available Commands), Commands.md (State table),
   and CommandWatcher.cs strict-mode error message. `scan_move`
   stays canonical (and `screen` calls it under the hood).

Total: +17 tests (4685 → 4701 passing, 4 skipped).

## Current state

```
[BattleMyTurn] Lloyd(Orator) (9,8) HP=105/432 [Blaze Gun] ui=Move
  primary=Speechcraft secondary=Geomancy
  (Lloyd has no learned Speechcraft abilities — generic recruit; Geomancy + Attack only.)

  [ENEMY] Knight (6,9) f=S HP=0/531 d=4 R:Speed Surge S:Tame DEAD
  [ENEMY] Summoner (6,6) f=N HP=0/318 d=5 R:Speed Surge TREASURE
  [PLAYER] Ramza(Gallant Knight) (6,8) HP=0/719 [Regen,Protect,Shell] DEAD
  [PLAYER] Kenrick(Thief) (7,4) HP=0/467 TREASURE
  [PLAYER] Wilham(Samurai) (7,9) HP=0/528 DEAD
```

Note the new render: alive-statuses in `[Regen,Protect,Shell]` are
clearly buffs; the ` DEAD` / ` TREASURE` are clearly lifeState suffixes.
Previously these collided in one bracket as `[Dead,Regen,Protect,Shell]`
/ `[Treasure]`. Playtest #7 specifically called this out.
