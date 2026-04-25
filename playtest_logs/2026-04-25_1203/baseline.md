# Baseline snapshot — 2026-04-25_1203

## Bridge log offsets
- live_log.txt — 310151 bytes / 3638 lines
- session_20260425_145059_358.jsonl — 6158 bytes / 31 lines

## Screen state at spawn

```
[BattleMyTurn] ui=Move [ACTED] Ramza(Gallant Knight) (8,10) HP=719/719 MP=138/138 curLoc=The Siedge Weald t=358ms[scan_move]

Abilities: (already used this turn — Wait/Status/AutoBattle remain)

Move tiles: (7,10 h=3) (8,11 h=5) (8,9 h=5) (6,10 h=2) (7,11 h=4) (7,9 h=2) (9,11 h=5) (9,9 h=5) (8,8 h=5) (5,10 h=2) (6,11 h=3) (6,9 h=2) (10,11 h=5) (10,9 h=5) (9,8 h=5) (10,10 h=5) (7,8) (8,7) (4,10 h=2) (5,11 h=2) (10,8 h=5) (9,7 h=3)  — 22 tiles from (8,10) Mv=4 Jmp=5 enemies=4
Recommend Wait: Face West — 3 front, 1 side, 0 back

Units:  [ENEMY] Black Goblin (1,5) f=E HP=426/426 d=12
        [ENEMY] Dryad (0,11) f=E HP=689/689 d=9
        [ENEMY] Dryad (1,10) f=E HP=671/671 d=7
        [ENEMY] Wisenkin (1,0) f=E HP=657/657 d=17
        [PLAYER] Ramza(Gallant Knight) (8,10) f=W HP=719/719 [Chaos Blade onHit:chance to add Stone] [Regen,Protect,Shell] *
```

## Bridge process

```
[running] FFT_enhanced.exe: YES
```

## Re-baseline (just before agent spawn — fresh turn) — 12:05:22

## Bridge log offsets
- live_log.txt — 358593 bytes / 4165 lines
- session_20260425_145059_358.jsonl — 7640 bytes / 38 lines

## Screen state at spawn

```
[BattleMyTurn] ui=Move Ramza(Gallant Knight) (8,10) HP=719/719 MP=138/138 curLoc=The Siedge Weald t=349ms[scan_move]

Abilities:
  Attack → (no targets in range)
  Focus → (8,10)<Ramza SELF>
  Throw Stone → (4,10)<Dryad>
  Salve {Removes: Poison, Blindness, Silence} → (8,10)<Ramza SELF>
  Tailwind → (8,10)<Ramza SELF>
  Chant → (8,10)<Ramza SELF>
  Steel → (8,10)<Ramza SELF>
  Shout → (8,10)<Ramza SELF>
  Ultima ct=20 → (4,10)<Dryad> (8,10)<Ramza SELF>
  Potion {Restores 30 HP} [x4] → (4,10)<Dryad> (8,10)<Ramza SELF>
  Hi-Potion {Restores 70 HP} [x1] → (4,10)<Dryad> (8,10)<Ramza SELF>
  X-Potion {Restores 150 HP} [x94] → (4,10)<Dryad> (8,10)<Ramza SELF>
  Ether {Restores 20 MP} [x99] → (4,10)<Dryad> (8,10)<Ramza SELF>
  Antidote {Removes Poison} [x97] → (4,10)<Dryad> (8,10)<Ramza SELF>
  Echo Herbs {Removes Silence} [x99] → (4,10)<Dryad> (8,10)<Ramza SELF>
  Gold Needle {Removes Stone} [x99] → (4,10)<Dryad> (8,10)<Ramza SELF>
  Holy Water {Removes Undead, Vampire} [x99] → (4,10)<Dryad> (8,10)<Ramza SELF>
  Remedy {Removes Poison, Blindness, Silence, Toad, Stone, Confusion, Oil, Sleep} [x99] → (4,10)<Dryad> (8,10)<Ramza SELF>
  Phoenix Down {Removes KO} [x99] → (no targets in range)

Move tiles: (7,10 h=3) (8,11 h=5) (8,9 h=5) (6,10 h=2) (7,11 h=4) (7,9 h=2) (9,11 h=5) (9,9 h=5) (8,8 h=5) (5,10 h=2) (6,11 h=3) (6,9 h=2) (10,11 h=5) (10,9 h=5) (9,8 h=5) (10,10 h=5) (7,8) (8,7) (5,11 h=2) (10,8 h=5) (9,7 h=3)  — 21 tiles from (8,10) Mv=4 Jmp=5 enemies=4
Recommend Wait: Face West — 3 front, 1 side, 0 back

Units:  [ENEMY] Black Goblin (1,5) f=E HP=426/426 d=12
        [ENEMY] Dryad (3,11) f=E HP=689/689 d=6
        [ENEMY] Dryad (4,10) f=E HP=671/671 d=4
        [ENEMY] Wisenkin (2,2) f=N HP=657/657 d=14
```
