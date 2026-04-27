# Iter4 Playtest Feedback — Iter3 ROOT FIX Verification

**Date:** 2026-04-26 ~22:53–23:05 local
**Commit under test:** 67085f1 (iter3 ROOT FIX — reset Team=1 on HP-match active dedup)
**Battle:** Brigands' Den — Random encounter at Mandalia Plain
**Outcome:** **BattleVictory** (vs iter3's BattleDesertion 2/2)
**Wall-clock:** ~12 min

---

## TL;DR — Did the iter3 root fix break the cascade? **YES, mostly.**

| Metric | Iter3 | Iter4 | Verdict |
|---|---|---|---|
| Allies count vs reality | `allies=3` (3 phantoms) | `enemies=6 / Ramza alone` (correct!) | **FIXED** |
| End-of-battle outcome | Desertion 2/2 | **Victory 1/1** | **FIXED** |
| Phantom narrator burst | 26-50 events/turn | 16-50 events/turn | **NOT FIXED** (mostly cosmetic) |
| AttackOutcomeClassifier | KO'd phantom-success | Same — KO'd reported pre-commit | **NOT FIXED** (still outstanding) |
| HP>MaxHP guard | held | held (no 8192/288) | held |
| (0,0) phantom unit | gone | reappeared transiently w/ HP=0/288 then disappeared | partial regression |

**Most important line:** `enemies=6` shown alongside `[PLAYER] Ramza` alone. Allies count matches reality. The Team=0 contamination is no longer poisoning roster classification.

---

## P1 Issues (game-breaking) — count: 0

(Iter1: 4 → iter2: 4 → iter3: 3 → **iter4: 0** ✅ Trending to zero!)

---

## P2 Issues (functional regressions / serious)

### [P2] Phantom narrator burst still rampant on enemy-turn battle_wait
- Each `battle_wait` returning to player turn produces 16-50 narrator events that misattribute enemy actions to Ramza.
- Sample (turn 1): `Ramza took 312 damage (HP 398→86)` / `Ramza moved (10,4) → (10,7)` / `Ramza died` — none real (Ramza HP=398/398 unchanged at (1,6)).
- Root cause likely: ENEMY array slots that share Ramza's HP value at moment of scan still get name-attributed to "Ramza" in narrator path, even though Team is now correctly 1 (so they don't inflate ally count). Narrator name-resolution is upstream of Team check.
- Cosmetic, not game-breaking — `[OUTCOME yours]` and `[OUTCOME enemies]` summary headers ARE correct (e.g. `Ramza +15 HP / (5,7) KO'd`). User-visible damage/HP is right; per-line attribution is wrong.
- **Recommended next:** narrator should consult `unit.Team` (post-dedup) to refuse name attribution for Team=1 entries.

### [P2] AttackOutcomeClassifier reports "KO'd!" pre-commit; ground truth lags
- Bridge says `[battle_ability] Attacked (5,5) from (5,6) — KO'd! (53→0/77)` then immediate `screen` shows `(5,5) HP=53/77` unchanged.
- Death only registers after the next `battle_wait` (`[OUTCOME enemies] (unit@5,5) KO'd`).
- Reproduced: 3/3 attacks this battle. Bridge always reports KO'd at attack time when target ends up dying, but HP actually drops on next state poll.
- This is the same phantom-success pattern called out in the prompt — **STILL OUTSTANDING**.
- Risk: code that trusts the immediate KO claim (e.g. AoE planners deciding "skip dead target") could double-target. Mitigation: callers wait for `[OUTCOME enemies]` line before trusting.

### [P2] Post-Victory transient screen mis-detect as BattleDesertion
- After `[BattleVictory]` was reported by `execute_turn`, next `screen` returned `[BattleDesertion]` for ~2-3s, then `[WorldMap]`.
- Already documented as a known transition flicker (`feedback_victory_gameover_both_encA255_risk` / `project_gariland_victory_fix`).
- Not caused by iter4 changes — pre-existing. But noting it because iter3 reported "BattleDesertion 2/2" — the iter3 metric may have been mis-counting this transient flicker, not actual desertions. Worth re-reading the iter3 transcript.

---

## P3 Issues (cosmetic / minor)

### [P3] (0,0) phantom unit reappears transiently with HP=0/288 [Confuse,Regen,Slow,Stop]
- Showed up in mid-battle scan after first attack, listed as `[ENEMY] (0,0) f=S HP=0/288 d=11 DEAD`.
- Disappeared from unit list 2 turns later. Did not affect enemies count (correctly stayed at 4 alive).
- HP>MaxHP guard prevented HP=8192. Status-flag soup `[Confuse,Regen,Slow,Stop]` is the same fingerprint as iter1/iter2 — same struct, just zeroed HP now.
- Likely a memory-region leftover that gets re-classified as enemy on a single scan, then cleaned up. Harmless but indicates the underlying scan isn't bounds-checking on (0,0) sentinel.

### [P3] Heap Move/Jump read failed for active unit, fell back to Mv=0/Jp=0
- Logged: `Active unit HP=401/401: heap Move/Jump read failed, setting Mv=0 Jp=0 (was UIBuffer fallback — wrong data)`.
- Held against `feedback_hp_is_derived` advisory — the comment says fallback was changed from UIBuffer to 0/0 because UIBuffer was "wrong data". But `Mv=0 Jp=0` is also wrong (Ramza has Mv=3 Jmp=3 with Movement +1).
- Actual scan_move output showed `Mv=3 Jmp=3` correctly somewhere — there's a redundant code path here.
- Needs investigation, may be tied to the iter1-3 heap struct movement.

### [P3] "[TOO CLOSE]" annotation on adjacent enemy attack target
- scan_move on (5,7) showed `Right→(5,8) enemy HP=74 >rear [TOO CLOSE]`.
- Attempted attack failed with "Tile (5,8) is not in basic-Attack range. ValidTargetTiles (2 valid tiles)".
- Range 1 attack on a distance-1 target should be valid. The "[TOO CLOSE]" annotation seems to incorrectly mark adjacent targets as out-of-range.
- Possibly facing-related (south of attacker, but attacker faces East)? Workaround: re-position to (6,8) and attack — that worked.

---

## What HELD from earlier iterations
- **Allies count fix:** verified across 5+ scans. Always shows 1 player Ramza, never inflated.
- **HP>MaxHP guard:** no 8192/288 readouts.
- **Mv=0/Jp=0 softlock:** no actual softlock, navigation worked. Cosmetic Mv=0 in log only.
- **FindAbility strict scope:** no Aura Blast / Auralblast confusion seen this session.
- **Per-unit Move/Jump heap struct:** scan_move correctly reported `Mv=3 Jmp=3` despite log line saying read failed.

---

## What's NEW (not previously reported)
- **+Treasure -Dead state transitions** logged on KO'd enemies (auto-loot mechanic). Cosmetic; don't think this is a bug — Final Fantasy Tactics IC Crystal/Treasure post-death status is in-game. But the narrator path emits these as `gained Treasure` / `lost Dead` lines which look like phantoms at first glance. Suggest tagging them as `[CRYSTAL]` / `[TREASURE]` distinctly.
- **Counter-attack damage on attack:** Ramza took 15 dmg counter-attacking (5,7) before killing it. Single-event, real, and `[OUTCOME yours] Ramza -15 HP` correctly reported. Iter3 reported "counter-trigger turns were the worst" for phantom bursts — counter-attack handled correctly here.

---

## Session stats
- Slow ops: 2 standouts —
  - `keys EncounterDialog→TravelList completed_timeout 10049ms` (T+0:00, encounter dialog handling)
  - `battle_wait BattleMyTurn→BattleMyTurn completed 27682ms` (T+2:30, first enemy-turn — extreme phantom-burst processing time)
  - `execute_turn BattleMyTurn→BattleMyTurn completed 20476ms` (T+3:30, attack with deferred resolution)
- Failed ops: 5, all my fault (tile mis-targets while learning the level layout). No bridge bugs.
- Total commands: ~25.

---

## Cascade Status: BROKEN

The Team=1 reset on HP-match dedup successfully prevents:
- Ally count inflation (was 3, now 1).
- BattleDesertion misclassification (was 2/2, now 0/1).

The Team=1 reset does NOT prevent:
- Narrator name-attribution to "Ramza" for phantom-positioned events (still 16-50 per turn).
- AttackOutcomeClassifier pre-commit "KO'd!" claims.

These are downstream issues that should be addressed in narrator and outcome paths separately — they're not part of the same root cause.

**Recommendation:** ship the iter3 fix as-is (it solves the actual game-breaking cascade). Open separate tickets for:
1. Narrator name-resolution should respect Team=1 post-dedup.
2. AttackOutcomeClassifier should defer "KO'd" claim until next state poll confirms HP=0.
3. (0,0) phantom enemy needs sentinel filter.
