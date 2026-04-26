# Baseline snapshot — 2026-04-26_0157

Fifth playtest of the session. After playtest #4 surfaced 5 friction items, all 6 (5 from #4 + 1 missing-doc) were addressed. Tests still 4687.

## Bridge log offsets
- live_log.txt — 117610 bytes / 1216 lines

## Battle: The Siedge Weald (loc 26) — Lloyd as last man, mid-fight

Game restarted, battle preserved. Lloyd active at (9,8) HP=315/432. Other player units already KO'd in prior runs. 3 live enemies (Archer, Archer, Time Mage). Difficult position — Lloyd may not survive long.

## Fixes shipped between playtest #4 and this playtest

The agent shouldn't be told these — fresh-eyes test.

1. **`battle_attack` BattleVictory-flicker tolerance** — terminal-state classifications now go through 3×500ms recheck before being trusted. Agent saw `[BattleVictory]` mid-battle on a normal attack with 2 enemies still alive.
2. **Cursor-miss action-commit guard** — `battle_ability` cursor-miss path now sends up to 3 Escapes spaced 300ms, polls for BattleMyTurn after each, and reports "aborted cleanly" or "could NOT abort, action may have committed." Agent's Tanglevine cursor-miss had silently consumed the turn.
3. **`best:` line rendering for radius-AoE abilities** — Geomancy / Meteor / Ultima now show top 3 ranked centers with `e:enemy_names a:ally_names` annotations. Docs in BattleTurns.md promised this output but it wasn't rendered.
4. **`battle_wait` cursor-reset after sub-menu Escape** — when battle_wait runs from BattleAbilities, after the Escape-to-BattleMyTurn the cursor byte is force-reset to slot 0 (Move). Defeats the phantom-Move side-effect agent observed.
5. **`!blocked` tag documented** in BattleTurns.md — explained as a position-dependent LoS hint that's "conservative — try anyway if you really want to attack." Plus all the other per-tile annotations (`>rear`, affinity sigils, `[REVIVE]` etc) listed.
6. **`return_to_my_turn` helper** — universal escape hatch. Sends up to 5 Escapes from any in-battle sub-menu back to BattleMyTurn. Listed in Commands.md.

## Current state

```
[BattleMyTurn] ui=Move Lloyd(Orator) (9,8) HP=315/432 MP=73/73
  primary=Speechcraft secondary=Geomancy

(Geomancy abilities now show best: lines. Example:
  Sinkhole R:5 AoE:2 ... → (9,8)<Lloyd SELF> (5,9)<Time Mage> (+38 empty tiles)
    best: (5,7) e:Archer  (5,8) e:Time Mage  (5,9) e:Time Mage)

3 live enemies. Lloyd's last stand.
```
