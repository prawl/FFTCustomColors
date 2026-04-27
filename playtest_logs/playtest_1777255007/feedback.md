# Playtest 1777255007 — Iter2 fix-loop

Start: 2026-04-26 (post-Brigands' Den encounter)
Iteration: 2 (verifies commits 88d41ad + 9ddc643)

## Watch list
- RosterMatchCache poison via name cache (cache-poison gate)
- HP>MaxHP guard in StaleBattleUnitFilter
- FindAbility strict scope error message clarity
- Mv=0/Jp=0 softlock fix (88d41ad holdover)
- state.units[] residue (88d41ad holdover)

## Bugs found

### [P1] HUGE phantom narrator burst — entire enemy turn mis-attributed to Ramza (cascade hypothesis FAILED)
**Repro:** Mandalia Plain, after `battle_move 2 4` then `battle_wait E`. 19s wait through enemy turn.
**Expected:** Narrator reports enemy moves and any damage Ramza actually took.
**Actual:** ~35+ phantom events including:
```
> (unit@8,10) died
> Ramza took 350 damage (HP 393→43)
> Ramza moved (8,10) → (9,11)
> Ramza moved (9,11) → (8,11)
> Ramza recovered 350 HP (HP 43→393)
> Ramza died
> (unit@9,7) died
> Ramza took 312 damage (HP 393→81)
> Ramza moved (9,7) → (8,10)
> Ramza moved (8,10) → (8,11)
... 30+ more lines of similar
```
Reality (post-wait `screen`): Ramza at (2,4) HP=393/393 INTACT. Took zero damage. No enemy died. Enemies just moved closer.
**Logs:** see `commands.log` and the verbatim burst captured above.
**Notes:** This is the SAME cascade reported as "outstanding" in driver. The cache-poison gate (9ddc643) reduced job-name confusion but did NOT prevent enemy-position events from being mis-attributed to Ramza identity. Cascade hypothesis FAILED — fix didn't break the cascade. Hypothesis: when enemies move into / out of tiles where roster fingerprint cache previously had matches, they inherit Ramza's identity tag through the position lookup.

### [P1] Phantom narrator burst on first battle_wait — earlier instance of same bug
**Repro:** Mandalia Plain, post-Brigands' Den. `auto_place_units` → first `battle_wait` after enemy turn → second `battle_wait` to advance to Allies → third `battle_wait` returned the burst.
**Expected:** Narrator should report only what actually happened on enemy turns; Ramza is at (1,6) and was never at (10,4) or (10,7).
**Actual:** Output included:
```
> (unit@10,4) died
> Ramza took 311 damage (HP 393→82)
> Ramza lost Regen, Protect, Shell
> Ramza (PLAYER) joined at (1,6)
> Ramza moved (10,4) → (10,7)
> Ramza recovered 311 HP (HP 82→393)
> Ramza gained Regen, Protect, Shell
> Ramza died
> (unit@10,7) (ENEMY) joined at (10,7)
```
Reality: Ramza at (1,6) HP=393/393 Regen,Protect,Shell intact. Pre/post screen scans agree. He never died/moved.
**Logs:** narrator output above; subsequent `screen` shows Ramza healthy at (1,6).
**Notes:** Looks like a cache-collision: an enemy at (10,4) was KO'd, then a different enemy spawned at (10,7) — narrator mis-tagged both with Ramza identity. Cache-poison gate (9ddc643) reduced job-name confusion (Ramza is correctly tagged Knight, not Chocobo) BUT the position+identity attribution cascade still fires. The "cascade hypothesis" only PARTIALLY held — job-name is fixed; position-identity is not.

### [P1] HP>MaxHP guard FAILED — (0,0) HP=8192/288 phantom unit re-appeared
**Repro:** After multiple `battle_wait` cycles at Mandalia Plain, `screen` shows phantom enemy.
**Expected:** Per fix 9ddc643, units with HP > MaxHP should be filtered by StaleBattleUnitFilter.
**Actual:** Unit list still includes:
```
[ENEMY] (0,0) f=S HP=8192/288 d=9 [Confuse,Regen,Slow,Stop]
```
HP=8192 dramatically exceeds MaxHP=288 — exactly the case that should be filtered.
**Logs:** see `screen` output above.
**Notes:** Either the HP>MaxHP guard is checking the wrong fields, or this phantom is being injected AFTER the filter runs. The status set [Confuse,Regen,Slow,Stop] is also incoherent (stop+slow+regen+confuse simultaneously). This is the EXACT bug 9ddc643 was supposed to fix. **HP>MaxHP guard fix DID NOT HOLD.**

### [P3] Mv=0/Jp=0 heap-read failure floods logs but UI is correct
**Repro:** Any scan_move on Ramza (HP=393/393).
**Expected:** Heap Move/Jump match (or quiet fallback).
**Actual:** Every scan retries narrow→broad, both fail with 0 matches; "setting Mv=0 Jp=0" is logged. Authoritative scan_move still surfaces Mv=3 Jmp=3 to the planner so the softlock doesn't trigger — but it's noisy and the fallback path is exercised constantly.
**Logs:** ~10 lines of `MemoryExplorer SearchBytes: 20 regions, 10MB searched, 0 matches` per scan.
**Notes:** Save-edited late-game gear (Chaos Blade etc.) at Mandalia Plain may put Ramza HP=393/393 outside the heap region the scanner walks. The Mv=0 floor doesn't softlock thanks to 88d41ad fix but the heap fingerprint pattern needs broadening for boosted units.

### [P1] Battle ended in BattleDesertion instead of BattleVictory after killing all enemies
**Repro:** Mandalia Plain wild encounter (MAP085, only Ramza on field). Killed last enemy (8,11) HP=9 with Attack — KO confirmed.
**Expected:** BattleVictory (all enemies KO'd, no party member died).
**Actual:** Screen jumped to BattleDesertion. Logs show:
```
[ScreenTransitionValidator] Rejected BattleDesertion → BattleVictory (impossible transition); reverting to BattleDesertion
```
The game transitioned to BattleVictory but the validator forcibly reverted it to BattleDesertion.
**Logs:** see above.
**Notes:** Root cause likely linked to phantom-unit cascade. ScanMove logs show `allies=` count incoherent: 0→0→0→1→2→1 over the battle. Ramza is the ONLY player unit visible. The allies counter rising to 2 then falling to 1 is fictional — possibly the phantom (0,0) unit + heap collisions with dead enemies registering as "allies." When the phantom "ally count" dropped (because the heap garbage shifted), the game classified it as Desertion. Cleared via Dismiss → WorldMap.

### [P3] EqA row-drift spam: SM row=0 'Move' vs mirror 'Chaos Blade'
**Repro:** Continuous on BattleMyTurn.
**Expected:** SM and EqA mirror agree on row 0 label.
**Actual:** Log spammed with `[EqA row drift] SM row=0 label='Move' but mirror says 'Chaos Blade' at that row.` — many times per scan.
**Notes:** Doesn't affect functionality but pollutes logs. Looks like equipment-row drift specific to Chaos Blade in row 0 (which shouldn't be a row position — it's an equipped weapon).

### [P2] First battle_wait reported "[OUTCOME yours] Ramza -326 HP -Regen,Protect,Shell MA +4" but Ramza never took damage
**Repro:** First `battle_wait` after `auto_place_units` (BattleEnemiesTurn → BattleAlliesTurn).
**Expected:** No outcome line if Ramza wasn't acted on (or accurate damage line).
**Actual:** `[OUTCOME yours] Ramza -326 HP -Regen,Protect,Shell MA +4` — a 326 HP loss, status drops, and MA +4 boost simultaneously.
**Notes:** Reality: Ramza was in spawn corner unreachable from enemies. The MA +4 boost is bizarre — that's Mighty Sword / Speed Save / etc. behavior. Looks like a stale BattleArray read picked up some other unit's stats and labeled them as Ramza's outcome. Possibly tied to the phantom unit cascade.

## Summary

**Total bugs:** 6 logged (4× P1, 1× P2, 1× P3 — duplicates collapsed).

**Severity histogram:**
- P1 = 4 (phantom narrator burst, HP>MaxHP guard regression, Desertion mis-classification, repeated cascade)
- P2 = 1 (false [OUTCOME yours])
- P3 = 1 (EqA row drift spam) + Mv=0 heap-search log noise (functional fallback works)

**Top 3 friction:**
1. **Phantom narrator burst** — every `battle_wait` produces 10–35 lines of fictional events. Cache-poison gate didn't kill the cascade. Position-identity attribution still confuses enemy events with Ramza identity.
2. **Phantom (0,0) HP=8192/288 unit** — HP>MaxHP guard from 9ddc643 did NOT filter it. Same exact symptom as iteration 1 reported.
3. **Battle outcome misclassification** — clear Victory routed through Desertion classifier because the phantom-unit "allies" counter went 0→2→1 mid-battle. ScreenTransitionValidator then locked in Desertion and rejected the real Victory.

## Iter1 fix verdict

| Fix | Status | Notes |
|---|---|---|
| RosterMatchCache poison gate (cache-poison) | **PARTIAL** | Job-name doesn't get clobbered (Ramza stays Knight, not Chocobo). BUT enemy positional events are still mis-attributed to Ramza identity. Job-tag fix worked; identity-tag fix failed. |
| HP>MaxHP guard in StaleBattleUnitFilter | **FAILED** | `(0,0) HP=8192/288 [Confuse,Regen,Slow,Stop]` reappeared in unit list. Filter is checking the wrong fields, or the phantom is injected after the filter. |
| FindAbility strict scope error | **HOLDS** | Both `Holy Sword` (non-equipped ability) and `Mettle` (WotL submenu name) produce clean "not found in available skillsets: Attack, Arts of War, Items, Reequip" errors. No cryptic "submenu" leak. |

## Cascade hypothesis

**FAILED.** The cache-poison gate addressed name-cache fallback but did NOT halt the cascade. Phantom narrator bursts continue at ~12–35 events per enemy turn. The HP>MaxHP guard was supposed to prevent the phantom (0,0) entry — it did not. Iteration 2's two cascade-relevant fixes both fell short. The FindAbility scope fix (orthogonal) holds.

The phantom-unit cascade now appears to have THREE distinct symptoms that all need resolution:
1. **Position-identity confusion:** enemy positional events tagged with Ramza identity in narrator output.
2. **Stale (0,0) entry:** phantom unit in unit list, status set [Confuse,Regen,Slow,Stop], HP overcap.
3. **Phantom ally count:** scan_move sees 0→2→1 allies when only Ramza exists, cascading into Desertion mis-classification.

Hypothesis for next iteration: the heap region the scanner walks contains stale corpse data from prior battles (player units from earlier saves). When live battle enemies' HP/status fingerprints overlap those stale entries by chance, the position-match returns the WRONG identity. Need to gate the heap walk by current-battle-only addresses, OR purge the heap region on battle entry.

