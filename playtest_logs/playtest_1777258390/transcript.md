# Iter4 Playtest Transcript

## Setup
- Start time: 2026-04-26 (~22:53 local)
- Latest commit: 67085f1 (iter3 ROOT FIX — reset Team=1 on HP-match active dedup)
- Initial screen: EncounterDialog at Brigands' Den
- ValidPaths: Fight, Flee

## Mission
Verify iter3 root fix breaks the phantom-cascade:
- allies count post auto_place_units (target: matches reality)
- end-of-battle (target: BattleVictory not BattleDesertion)
- narrator events per battle_wait (target: <10, vs iter3's 26-50)
- AttackOutcomeClassifier still outstanding

## Timeline

### T+0:00 — Initial state
`screen` -> EncounterDialog at Brigands' Den, choices Fight/Flee.


### Battle Log (condensed)

**Turn 1** (Ramza @ 1,6 HP=398/398):
- Move (1,6)→(2,4) [confirmed]
- battle_wait E → 18+ phantom narrator events but Ramza HP=398/398 unchanged
- enemies=6, allies=1 (correct)

**Turn 2** (Ramza @ 2,4):
- Move (2,4)→(5,4)
- battle_wait E → similar phantom burst, OUTCOME header correct

**Turn 3** (Ramza @ 5,4 HP=398/398):
- Move (5,4)→(5,6), Attack (5,7) → KO'd 78→0
- Ramza took 15 counter dmg (HP 398→383) — REAL
- battle_wait reveals enemy at (10,5) was already KO'd (Critical→0 carryover)

**Turn 4** (Ramza @ 5,6 HP=383/398):
- Attack (5,5) — bridge says KO'd (53→0/77) but HP unchanged on next scan
- battle_wait E → OUTCOME confirms (5,5) KO'd (deferred resolution)
- Phantom burst: 16 events, none real

**Turn 5** (Ramza @ 5,6 HP=398/398):
- Attack (6,6) — bridge says KO'd (94→0/94) — same phantom-success
- battle_wait E → OUTCOME confirms (6,6) KO'd
- 14 phantom events

**Turn 6** (Ramza @ 6,5):
- Move (5,6)→(6,5), no attacks in range
- battle_wait S → routine

**Turn 7** (Ramza @ 5,7 HP=398/398):
- Move (6,5)→(5,7) (5 enemies tracked, 2 alive)
- Attack target failed: (6,7) not in range from (6,5)
- Wait → enemies still chasing

**Turn 8** (Ramza @ 5,7 HP=401/401):
- Attack (5,6) rear — KO'd 71→0
- battle_wait E → OUTCOME confirms (5,6) KO'd, (5,5) gained Crystal status
- Last enemy at (5,8)

**Turn 9** (Ramza @ 5,7):
- Move (5,7)→(6,8) flank, Attack (5,8) — KO'd 74→0
- **BattleVictory** signaled by execute_turn
- Screen briefly mis-classifies as BattleDesertion (transition flicker), then WorldMap

### Observations
- Allies count: 1 throughout (matches reality). Iter3 had 3 phantoms.
- Narrator phantom bursts: 14-50 events per battle_wait. Iter3 said 26-50, so similar magnitude.
- AttackOutcomeClassifier: KO'd phantom-success on every attack. Same as iter3.
- HP>MaxHP guard: held. No 8192 readouts.
- (0,0) phantom: present transiently w/ HP=0/288 (vs HP=8192 before guard). Disappeared mid-battle.

