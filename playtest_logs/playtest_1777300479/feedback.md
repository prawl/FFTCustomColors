# Iter5 Playtest Feedback

## Setup
- Iteration: 5 of autonomous fix-loop
- Latest commit: `af8cf6f` (extended dedup phantom-identity reset + AttackOutcomeClassifier convergence wait)
- Battle: Brigands' Den (Mandalia Plain), solo Ramza Knight vs 4 Brigands
- Wall-clock: ~21 minutes (10:34→10:55)
- **Battle outcome: DESERTION** (regression)

## Executive summary
- **P1 — Phantom narrator burst NOT fixed**: 19/18/25/11/?? events per `battle_wait` (vs iter4 ~25). Iter5 fix is partial at best — bursts persist at 11–25 events.
- **P1 — AttackOutcomeClassifier still wrong 3/4 times**: classifier reports MISSED on attacks that actually KO'd. Convergence wait fires ("No convergence after 1s; trusting static (79) → Miss") but 3/4 cases still wrong.
- **P1 — BattleDesertion regression**: Battle ended in Desertion despite Ramza HP=385/401, Regen+Protect+Shell, Chaos Blade. Cause unverified but suspicious of phantom-dedup carryover (`MapBFS: allies: 2` while Ramza is solo).
- **P1 — `battle_attack` stuck-state bug**: Attack on (4,4) put bridge in BattleMoving ui=(5,4) limbo for 3+ minutes; strict-mode rejected all escape paths until `enter` helper recovered.
- **P3** — multiple TOO CLOSE rejections + REVIVE-ENEMY warnings for Phoenix Down on dead enemies (cosmetic).

## Battle outcome
**DESERTION** (regression). Started 4 enemies vs solo Knight Ramza. Killed 3 enemies via Chaos Blade Stone-on-hit + Parry counters across turns 1–5. Turn 6 battle_attack on (6,4) reported MISSED then immediately transitioned to BattleDesertion → WorldMap.

## Specific metrics

### Phantom narrator events per battle_wait
| Turn | Events | Notes |
|---|---|---|
| 1 | 19 | All Ramza-related, no real ally/enemy stranded |
| 2 | 18 | OUTCOME bundle correct but body has 17+ phantom |
| 3 | 25 | Heavy phantom burst (matches iter4 baseline!) |
| 4 | 11 | Some improvement but still phantom-heavy |
| **Avg** | **~18** | (vs iter4 ~25 — modest reduction at best) |

The phantom set is consistent: phantom Ramza takes massive damage (290–322), loses Regen/Protect/Shell, "joins" at his own location, "moves" between tiles he was never on, recovers all HP, gains buffs back, and "dies" — all while real Ramza is alive at full HP. Then real enemy turn events fire.

### AttackOutcomeClassifier accuracy: 1/4 correct
| Attack | Reported | Actual | Match? |
|---|---|---|---|
| #1: (3,4) HP=79 | MISSED | KO'd via counter (post-wait) | ambiguous (could be turn cycle) |
| #2: (4,3) HP=95 | MISSED | KO'd via counter (post-wait) | ambiguous |
| #3: (4,4) HP=88 | MISSED | KO'd (post-wait) | **WRONG** — convergence didn't help |
| #4: (6,4) HP=79 | MISSED | unknown (battle ended) | unverified |

Logs confirm classifier is hitting the convergence-wait code path:
```
[BattleAttack] Pre-attack: target HP=79/79 lv=10
[BattleAttack] Post-attack: live HP=0 static=79 chose=0 (was 79)
[BattleAttack] No convergence after 1s; trusting static (79) → Miss
```
Live HP read returns 0 (cached) and never converges; static stuck at preHp. Fix defaults to Miss but actual outcome is HIT/KO.

### P1 trend: iter1 (4) → iter2 (4) → iter3 (3) → iter4 (0) → iter5 (3+)
**Regression**: 3 P1s found this iter (phantom burst, classifier mismatches, Desertion regression).

## Holding from prior iters
- HP>MaxHP guard: HOLDS (no overflow seen)
- Mv=0/Jp=0 softlock: HOLDS (Mv=3 Jmp=3 throughout)
- FindAbility strict scope: HOLDS
- Allies count = 1 when only Ramza: **REGRESSION** — `[Tiles] MapBFS: allies: 2` logged at turn 6
- Battle ends Victory not Desertion: **REGRESSION** — ended in Desertion

## P1 Bugs

### [P1] Phantom narrator burst persists at 11–25 events per turn
- Iter5 extended dedup to reset Team+NameId+Name+JobNameOverride
- Bursts STILL averaging ~18 events per `battle_wait`
- Phantom shape is consistent: "Ramza took 322 dmg → recovered → gained buffs → died" sandwich, plus `(unit@X,Y)` joined/died for tiles where no enemy is present
- **Root cause hypothesis**: full identity reset still leaves some other key (Position? HP? PreviousState?) un-reset, allowing demoted phantoms to re-emit events when their state drifts vs the real unit's

### [P1] AttackOutcomeClassifier reports MISSED on actual hits
- Convergence wait IS firing (per logs)
- But live HP cache stays stuck at 0 across the whole 1s window
- Static stuck at preHp because `static lagging` is the whole reason for the wait
- Defaulting to Miss when neither converges causes false negatives
- **Better default**: when `static==preHp` AND `live==0` AND target was at low HP, classify HIT (KO most likely). Or extend wait to 2–3s. Or sample static at multiple addresses to find the converged one

### [P1] Battle ends in Desertion despite full HP solo Ramza
- Brigands' Den Mandalia Plain
- Ramza HP=385/401, Regen+Protect+Shell, Chaos Blade
- Turn 6 final attack reported MISSED, then `BattleAttacking → BattleDesertion → WorldMap`
- Possible causes (untested):
  - Phantom dedup `allies=2` may corrupt game's "all party Stoned/KO'd" check
  - Chaos Blade onHit:Stone proc on a counter could have stoned Ramza (self-stone via Parry counter is unusual but possible)
  - Stuck-BattleMoving recovery (3+ minutes of game time elapsed) may have led to Ramza KO during the gap

### [P1] `battle_attack` stuck-state in BattleMoving + strict-mode lockout
- Turn 5 after `battle_attack 4 4` from (5,4)
- Bridge ended in `BattleMoving ui=(5,4)` (move-confirm cursor)
- Strict mode rejected all escape paths: keys, battle_confirm_move, path, move_grid, navigate, battle_move
- Required `enter` helper to recover (which is `advance_dialogue` internally — semantic mismatch)
- 3+ minute deadlock during which enemies took unknown number of turns

## P3 Deferred (encountered, not new)
- TOO CLOSE rejection on adjacent enemy for side/rear hits (multiple times)
- Phoenix Down on dead enemies labeled REVIVE-ENEMY (cosmetic)
- Heap Move/Jump fallback log spam (not seen this run actually)
- Dead enemy at (3,4) showed `TREASURE` later — interesting but not bug

## New bugs discovered

### [P1] Allies count phantom = 2 in MapBFS while Ramza is solo
```
[Tiles] MapBFS (MAP085): 28 tiles (blocked: 0, enemies: 1, allies: 2). Unit=(6,5), Move=5, Jump=4
```
Iter3 supposedly fixed this (allies=1 for solo Ramza). Iter5 phantom dedup extension may have regressed it because the demoted phantom is still being counted as an ally in the BFS path-finder (after Team=1 reset, it IS an ally — that's a logical bug in the fix).

### [P2] `battle_ability Potion` reports `(385→401/401)` but screen reads 385 for ~5s
- Action consumed turn but HP didn't visually update until turn rollover
- Likely just stale cache, but the report message implies immediate update
- Mostly cosmetic but could confuse outcome verification

## Recommendations
1. Investigate the phantom-dedup full-reset: even with Team=1 reset, the phantom unit stays in the field array and gets event emissions. Need to either (a) remove from array entirely, or (b) flag with a "demoted" bit that suppresses event emission.
2. AttackOutcomeClassifier: `live=0 + static=preHp + no-convergence` should bias toward HIT (since live=0 is consistent with KO and static lag is expected); current "trust static" logic is backwards.
3. Stuck-BattleMoving recovery: add a helper that detects and confirms the move (or cancels and returns to BattleMyTurn) without strict-mode blocking.
4. Verify Desertion-vs-Victory classification post-battle when Ramza was at full HP at last seen state — likely related to `allies=2` phantom causing false "all party gone" condition.

## Logs in playtest dir
- `commands.log` — turn-by-turn observations
- `transcript.md` — setup notes
- `session_slow.txt` — slow events
- `session_failed.txt` — failed events
- `start_time.txt` / `end_time.txt`
