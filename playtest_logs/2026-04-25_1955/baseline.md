# Baseline snapshot — 2026-04-25_1955

## Bridge log offsets
- live_log.txt — 121529 bytes / 1253 lines
- session_20260425_235623_265.jsonl — 24 lines (active)

## Battle: The Siedge Weald (loc 26) — multi-unit party

Parent restarted the battle just before agent spawn. All units full HP/MP, enemies far west.

```
[BattleMyTurn] ui=Move Kenrick(Thief) (9,9) HP=467/467 MP=53/53 curLoc=The Siedge Weald

Move tiles: (8,9 h=5) (9,8 h=5) (10,8 h=5) (7,9 h=2) (8,8 h=5) (9,7 h=3) (10,11 h=5) (10,7 h=3) (6,9 h=2) (7,10 h=3) (8,11 h=5) (9,11 h=5) (7,8) (8,7) (9,6) (10,6 h=2) (6,10 h=2) (7,11 h=4) (5,9) (6,8) (7,7) (9,5 h=3) (5,10 h=2) (6,11 h=3) (8,6) (10,5) (4,9) (6,7) (7,6) (8,5 h=3) (9,4 h=3) (4,10 h=2) (5,11 h=2)  — 33 tiles from (9,9) Mv=6 Jmp=4 enemies=5
Attack tiles: Down→(10,9) ally (Orator) HP=432
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

## Code shipped this session that the playtest exercises

- **Multi-unit TURN HANDOFF banner** (`TurnHandoffBannerClassifier`) — fires after `battle_wait` / `execute_turn` when active player unit changes between Ramza/Kenrick/Lloyd/Wilham. Banner format: `=== TURN HANDOFF: A(JobA) → B(JobB) (x,y) HP=h/mh ===`. Same-unit returns silent. Wired into 3 sites; live-verified Ramza→Kenrick at this very battle ~30 min before agent spawn.
- **Post-wait settle scan** (250ms) — defeats race where the static `[ACTIVE]` byte lags Ctrl release.
- **fft.sh narrator filter** widened — surfaces `===` banner lines alongside `> ` events.

The agent should NOT be told about these explicitly — fresh-eyes test. If the banner reads naturally and helps them track turns, it's working. If they ignore it or find it noisy, that's the friction signal.
