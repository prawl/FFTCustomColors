# Playtest transcript — 1777255007

## T0: Boot

`screen` → EncounterDialog at Brigands' Den (post-victory pre-battle dialogue). Valid paths: Fight, Flee.

The battle after Brigands' Den is the next scripted encounter — likely Sand Rat Cellar (Famfrit / Algus's brigands?) or Lenalia Plateau. Going to advance dialogue carefully, one Enter at a time.

## T+1: Engage

`fight` → TravelList briefly → BattleFormation at Mandalia Plain (MAP085). This is a wild encounter, NOT a story battle. Surprising — I expected a scripted next event.

`auto_place_units` → BattleMyTurn 5.6s. Save-edited Ramza: Knight, lvl 8, HP=393/393, Chaos Blade + Maximillian + Sortilege + Escutcheon (clearly post-game gear at chapter 1 location). Mv=3, Jmp=3. Only Ramza on player side; no other units shown. 4 enemies on opposite corner of map.

## T+2: First wait — phantom burst #1

`battle_wait` → BattleEnemiesTurn → second `battle_wait` to BattleAlliesTurn returned `[OUTCOME yours] Ramza -326 HP -Regen,Protect,Shell MA +4`. That's 326 HP loss + 3 status drops + MA +4 boost SIMULTANEOUSLY. Ramza was untouched at the spawn corner. Pure phantom — flagged P2.

Third wait → BattleMyTurn. 9 lines of phantom narrator: Ramza taking 311 damage, recovering 311 HP, dying, "(unit@10,7) (ENEMY) joined at (10,7)". Reality: Ramza intact at (1,6). Flagged P1.

## T+3: Verify FindAbility scope

Test #1: `battle_ability "Holy Sword" 8 8` → CLEAN error: "Ability 'Holy Sword' not found in available skillsets: Attack, Arts of War, Items, Reequip". Beautiful.

Test #2: `battle_ability "Mettle" 8 8` → SAME clean error. No cryptic "Skillset 'X' not in submenu" leak. **FindAbility scope fix HOLDS.**

## T+4: Movement and combat

Several Move/Wait cycles approaching enemies. Each `battle_wait` produced 10-35 lines of phantom narrator output. Real events buried but surfacable via `[OUTCOME ...]` prefix lines.

Notable: phantom `(0,0) HP=8192/288 [Confuse,Regen,Slow,Stop]` enemy reappeared mid-battle. **HP>MaxHP guard FAILED to filter it.** Then it disappeared on a later scan — transient phantom, not stable.

## T+5: Combat verifications

`battle_attack 8 8` from (9,8) → KO'd! (82→0/82). Real attack, real KO. AttackOutcomeClassifier correct. Static unit list lagged briefly (still showed 82/82) until next wait, which logged `[OUTCOME enemies] (unit@8,8) KO'd` confirming the kill.

`battle_attack 8 11` from (8,10) on HP=9 critical enemy → KO'd! Final enemy down. Expected Victory.

## T+6: Battle ends

Screen jumped to BattleDesertion. WTF. Logs show:
```
[ScreenTransitionValidator] Rejected BattleDesertion → BattleVictory (impossible transition); reverting to BattleDesertion
```
The game's Victory transition was OVERRIDDEN by the Desertion classification.

Root cause hint: scan_move logs show `allies=` going 0→0→1→2→1 over the battle, when only Ramza was ever on field. Phantom "allies" went up then down. The drop from 2 to 1 likely tipped the desertion-detection heuristic.

`execute_action Dismiss` cleared back to WorldMap.

## End

Total wall-clock: ~14 min. Outcome (game-side): Victory likely. Outcome (mod-side): Desertion mis-classified.

