# Baseline snapshot — 2026-04-25_2245

Second playtest of the night, after committing the multi-unit hand-off banner + 12 follow-up fixes (`cd98030`).

## Bridge log offsets
- live_log.txt — 514877 bytes / 5808 lines
- session_20260426_021134_840.jsonl — 17430 bytes (active)

## Battle: The Siedge Weald (loc 26) — multi-unit party (fresh restart)

```
[BattleMyTurn] ui=Move Kenrick(Thief) (9,9) HP=467/467 MP=53/53 curLoc=The Siedge Weald

Move tiles: (6,9 h=2) (7,8) (8,7) (9,6) (7,9 h=2) (8,8 h=5) (9,7 h=3) (10,6 h=2) (6,10 h=2) (8,9 h=5) (9,8 h=5) (10,7 h=3) (7,10 h=3) (10,8 h=5) (7,11 h=4) (8,11 h=5) (9,11 h=5) (10,11 h=5)  — 18 tiles from (9,9) Mv=4 Jmp=4 enemies=5
Attack tiles: Up→(10,9) ally (Orator) HP=432
Recommend Wait: Face West — 5 front, 0 side, 0 back
Timeline: E(E,ct=91) → E(E,ct=91) → E(E,ct=77) → E(E,ct=84) → E(E,ct=91)
Heights: caster h=5 vs enemies h=2-3

Units:  [ENEMY] Archer (0,3) f=E HP=484/484 d=15 S:Evasive Stance
        [ENEMY] Archer (0,4) f=E HP=447/447 d=14 R:Sticky Fingers S:Evasive Stance
        [ENEMY] Knight (1,3) f=E HP=531/531 d=14 S:Equip Heavy Armor
        [ENEMY] Summoner (0,5) f=E HP=318/318 d=13 S:Evasive Stance
        [ENEMY] Time Mage (1,2) f=E HP=353/353 d=15
        [PLAYER] Ramza(Gallant Knight) (8,10) f=W HP=719/719 d=2 [Chaos Blade onHit:chance to add Stone] R:Counter S:Evasive Stance M:Jump +2 [Regen,Protect,Shell]
        [PLAYER] Kenrick(Thief) (9,9) f=W HP=467/467 [Gastrophetes] [Thief's Cap, Black Garb, Featherweave Cloak] R:Shirahadori S:Equip Crossbows M:Movement +2 *
        [PLAYER] Lloyd(Orator) (10,9) f=W HP=432/432 d=1 [Blaze Gun] R:Parry S:Attack Boost M:Movement +2
        [PLAYER] Wilham(Samurai) (10,10) f=W HP=528/528 d=2 [Chirijiraden] R:Speed Surge S:Attack Boost M:Movement +3
```

## Things that should be different vs prior playtest

Same battle, same units, but these changes shipped between runs (commit `cd98030`):

1. **Move tiles list sorted by min Manhattan distance to nearest enemy** — `(6,9)` first instead of BFS-order `(8,9)`.
2. **`>BACK` attack-arc renamed to `>rear`** — lowercase, matches `>side` pattern.
3. **TURN HANDOFF banner deduped** — execute_turn won't emit two copies.
4. **Move-only `execute_turn` no longer ends the turn** — `execute_turn 6 5` (just 2 args) leaves Act/Wait available.
5. **Stand-still `execute_turn` accepts caster's current tile** — `execute_turn 7 9 "Phoenix Down" 6 9` from (7,9) won't reject as "not in valid move range".
6. **BattleVictory-flicker recovery** — wait nav settles + rechecks before failing on transient terminal states.
7. **`battle_ability "Shout"` self-target uses scan-canonical caster pos** — won't auto-fill another unit's tile when cursor sits on them.
8. **`execute_turn` / `battle_wait` response carries `[OUTCOME]` recap** — HP delta, status, KO, stat changes (Speed Surge +1 etc) prepended to `response.Info`, surfaces above the `> ` narrator events.
9. **`fft.sh` id-matched polling** — long execute_turn responses won't be lost to follow-up shell calls.
10. **Stat-change events** — Speed/PA/MA changes show in OUTCOME and narrator (Speed Surge +1, Tailwind PA +2, etc).

The agent shouldn't be told this list — fresh-eyes test. If anything feels improved (or broken), that's the friction signal.
