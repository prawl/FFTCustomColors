# Baseline snapshot — 2026-04-26_0021

Third playtest of the session. After committing the multi-unit hand-off banner + 12 follow-up fixes (`cd98030`) AND the 10 P2 follow-up fixes from playtest #2 (uncommitted, see below).

## Bridge log offsets
- live_log.txt — 119230 bytes / 1228 lines

## Battle: The Siedge Weald (loc 26) — Ramza already moved to (6,9)

Game restarted between playtests; battle preserved at this state. Ramza is the current active unit at (6,9) HP=719/719 MP=138/138, primary=Mettle secondary=Items. Allies: Lloyd (8,8) Orator, Wilham (9,8) Samurai. Kenrick is missing from the scan (KO'd in prior playtest). 5 enemies: 2 Archers, Knight, Summoner (Charging), Time Mage.

## Fixes shipped between playtest #2 and this playtest (P2 follow-ups, uncommitted)

The agent shouldn't be told these — fresh-eyes test. If anything feels improved, that's the friction signal.

1. **`R:N AoE:M` tags on every ability line** — agent saw `Meteor ct=10 → ...` with no R/AoE info; now sees `Meteor R:4 AoE:2 ct=10 → ...`.
2. **`(+N empty tiles)` suffix** on rendered tile lists — surfaces the cached count of off-tile targets that were stripped server-side. Agent saw only 4 ally tiles for Meteor while bridge had 27 cached.
3. **`primary=X secondary=Y` sub-line** in screen header — disambiguates job class label (e.g. Lloyd's `(Orator)`) from primary skillset (Mediation) and secondary (Geomancy).
4. **Stale-scan defense in self-target auto-fill** — if cursor disagrees with cached scan's active-unit position, force a fresh CollectUnitPositionsFull. Defeats the post-`ConfirmMove` Shout-on-wrong-tile case.
5. **BattleVictory flicker tolerance widened** — execute_turn outer loop now does 3×800ms rechecks (up from 1) before aborting on terminal-state flicker.
6. **`MoveArtifactCoalescer`** — pure helper that suppresses A→B→A phantom move pairs in the streaming narrator. 3s window, per-unit keyed.
7. **Confirm-modal escape in wait-nav** — sends Escape if `BattleActing` exit looks like a stuck modal.
8. **Static-array HP cross-check on `battle_attack`** — same dual-read pattern as X-Potion fix; defeats fabricated `HIT (X→Y)` on whiffs.
9. **Move-confirm timeout 8s→12s + skip terminal-flicker rechecks during move-confirm poll**.
10. **Heartbeat dot** to stderr every 5s during fft polling so caller sees the bridge is still working on long calls.

## Current state

```
[BattleMyTurn] ui=Move Ramza(Gallant Knight) (6,9) HP=719/719 MP=138/138 curLoc=The Siedge Weald
  primary=Mettle secondary=Items

(Abilities listed for Ramza — full Mettle suite + Items + Tailwind/Steel/Shout/Ultima/Phoenix Down)

Move tiles: (4,9) (6,7) (5,8) (5,9) (6,8) ...  — 22 tiles from (6,9) Mv=4 Jmp=5 enemies=5

Units:  [ENEMY] Archer (2,6) f=E HP=484/484 d=7 S:Evasive Stance [Defending]
        [ENEMY] Archer (3,6) f=N HP=447/447 d=6 R:Sticky Fingers S:Evasive Stance [Defending,Haste]
        [ENEMY] Knight (4,8) f=E HP=468/531 d=3 [Haste]   ← already wounded by ~63 from prior playtest
        [ENEMY] Summoner (6,6) f=N HP=274/318 d=3 [Charging]   ← also wounded, currently charging a spell
        [ENEMY] Time Mage (3,5) f=N HP=353/353 d=7
        [PLAYER] Ramza(Gallant Knight) (6,9) f=W HP=719/719 *
        [PLAYER] Lloyd(Orator) (8,8) f=W HP=432/432
        [PLAYER] Wilham(Samurai) (9,8) f=W HP=528/528
        — Kenrick KO'd in prior run, missing from active roster
```

## Known stuck-state caveat

A pre-restart `execute_turn 5 8 Attack 4 8` got NOT-CONFIRMED on move and required a manual restart. The 12s extension didn't resolve it — root cause unknown. If this happens again during the playtest, log it as a friction item; restart will recover.
