# Playtest 1777256496 transcript

## Initial state
EncounterDialog at Brigands' Den next battle.


## Step 1: fight → BattleFormation → auto_place_units → BattleEnemiesTurn → battle_wait → BattleMyTurn

### Phantom narrator burst (iter3 — Pass2 strict cascade hypothesis)
Output of first battle_wait:
```
> (unit@10,4) died
> (unit@10,6) died
> Ramza took 321 damage (HP 393→72)
> Ramza lost Regen, Protect, Shell
> Ramza (PLAYER) joined at (10,4)
> Ramza (PLAYER) joined at (1,6)
> Ramza moved (10,4) → (7,4)
> Ramza moved (10,4) → (7,4)
> Ramza moved (7,4) → (10,6)
> Ramza moved (7,4) → (10,6)
> Ramza moved (10,6) → (9,4)
> Ramza moved (10,6) → (9,4)
> Ramza recovered 11 HP (HP 72→83)
> Ramza recovered 321 HP (HP 72→393)
> Ramza gained Regen, Protect, Shell
> (unit@9,5) died
> Ramza died
> (unit@10,4) (ENEMY) joined at (10,4)
> (unit@10,6) (ENEMY) joined at (10,6)
> Ramza moved (9,5) → (8,4)
> (unit@10,4) moved (10,4) → (7,4)
> (unit@10,6) moved (10,6) → (9,4)
> Ramza recovered 310 HP (HP 83→393)
> Ramza gained Regen, Protect, Shell
> Ramza died
> (unit@8,4) (ENEMY) joined at (8,4)
```
~26 events. Ramza is at (1,6) HP=393/393 INTACT. Reality: 5 enemies all moved closer.

### Reality (post-wait scan)
Ramza(Knight) (1,6) HP=393/393 Regen,Protect,Shell intact.
Enemies at (4,4), (6,4), (7,4), (9,4), (8,4). All HP=full.
No deaths, no Ramza moves, no Ramza damage.

### Iter3 fix observations
- HP guard: NO (0,0) HP=8192/288 phantom — HOLDS so far.
- Pass2 strict cascade: phantom burst is just as severe as iter1/iter2. Cascade hypothesis FAILED.
- Terminal flicker: not testable yet.

## Step 2-N: full battle 1 sequence
1. battle_move 2,4 → BattleMyTurn
2. battle_wait E → enemy turn (huge phantom burst, ~50 events; reality: only 5 enemies all moved)
3. battle_attack 3,4 → "KO'd! 69→0/69" but reality (3,4) HP=69 alive
4. battle_wait E → enemy turn (more burst)
5. battle_ability Potion 2,4 → STUCK on BattleAbilities for ~4 minutes
6. battle_wait E (eventually got out) → BattleMyTurn
7. battle_move 3,3 → flank
8. battle_attack 4,3 → KO'd
9. battle_wait E
10. battle_move 4,2 → flank
11. battle_attack 3,2 → KO'd
12. battle_wait E
13. battle_move 4,4
14. battle_attack 3,4 → KO'd alive enemy at (3,4)
15. battle_wait W
16. battle_move 5,6
17. battle_attack 5,7 → KO'd last enemy
18. screen → BattleDesertion (NOT Victory) — TERMINAL FLICKER FIX FAILED

## Battle 2 sequence (started ~02:40)
1. EnterLocation → BattleFormation
2. auto_place_units (took 19s — regression)
3. battle_wait → BattleMyTurn (smaller phantom burst this round, ~9 events)
4. battle_move 2,4
5. battle_wait E → enemy turn
6. battle_attack 3,4 → "KO'd!" but post-scan still alive (MISS-vs-KO bug confirmed AGAIN)
7. battle_wait E (clean OUTCOME line emerged)
8. battle_move 4,3
9. battle_attack 4,4 → KO'd + counter (4,4) — burst happened
10. battle_wait W
11. battle_attack 5,3 → KO'd
12. battle_wait E (CLEAN narrator output — no phantom events! [OUTCOME yours] +9 HP, [OUTCOME enemies] (5,3) KO'd)
13. battle_attack 3,3 → KO'd last enemy
14. screen → BattleDesertion (TERMINAL FLICKER FAILED AGAIN)

## Key observation
Battle 2 step 12 was the cleanest narrator output of the entire session. ZERO phantom events.
The clean turn was an attack with NO counter trigger. ALL burst-prone turns had counter activations.
**Strong correlation: counter triggers cause cache desync that drives the phantom narrator burst.**
