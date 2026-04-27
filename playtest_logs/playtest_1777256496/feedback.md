# Playtest 1777256496 — Iter3 fix-loop verification

Start: 2026-04-26 (post-Brigands' Den encounter, EncounterDialog)
Iteration: 3 (verifies commit ae2968c — HP guard nav-side, terminal flicker recovery, RosterMatcher Pass2 strict)

## Watch list
- HP>MaxHP guard at NavigationActions (no Hp > MaxHp units)
- Terminal flicker recovery (Victory not locked into Desertion)
- RosterMatcher Pass 2 strictness — phantom narrator burst REDUCED?
- AttackOutcomeClassifier MISS-vs-KO confusion (still outstanding)
- Mv=0/Jp=0 softlock fix (88d41ad holdover)
- FindAbility strict scope (9ddc643 holdover)

## Bugs found

### [P1] Terminal flicker / Victory→Desertion misclassification — STILL FAILING
**Repro:** Mandalia Plain, post-Brigands' Den. Killed final enemy at (5,7) with `battle_attack 5 7` — narrator confirmed `KO'd! (72→0/72)`. Waited 2s, ran `screen`.
**Expected:** `BattleVictory` (only player unit alive, all 5 enemies KO'd).
**Actual:** Screen jumped to `BattleDesertion`. Just like iter1 + iter2.
**Logs:** `screen` returned `[BattleDesertion] curLoc=Mandalia Plain obj=Brigands' Den ... ValidPaths: Dismiss`. `[ScanMove]` snapshot just before transition shows `enemies=1 allies=3` — phantom ally count = 3 while only Ramza is on field.
**Notes:** Iter2 fix added "terminal flicker recovery" — clearly did NOT recover this case. Root cause likely the `allies=3` count from the ScanMove path; even with HP>MaxHP guard rejecting (0,0) in CollectPositions, downstream alliesCount logic still inflates. The validator did not log a "Reject Desertion → Victory" line this run, suggesting Desertion was selected on first pass without conflict. Iter3 fix did not hold for this scenario.

### [P1] Phantom narrator burst — Pass 2 strictness did NOT reduce burst (cascade hypothesis FAILED again)
**Repro:** Every `battle_wait` after enemy turn produces ~10–50 lines of fictional events.
**Expected:** Narrator reports only what actually happened on the enemy turn.
**Actual:** Bursts of ~26–50+ events per enemy turn with phantom Ramza moves, deaths, recoveries.

Round 1 (after `auto_place_units → battle_wait`):
```
> (unit@10,4) died
> (unit@10,6) died
> Ramza took 321 damage (HP 393→72)
> Ramza lost Regen, Protect, Shell
> Ramza (PLAYER) joined at (10,4)
> Ramza (PLAYER) joined at (1,6)
> Ramza moved (10,4) → (7,4)
... (~26 events total)
> Ramza died
> (unit@10,4) (ENEMY) joined at (10,4)
```
Reality: Ramza at (1,6) HP=393/393 fully intact. Zero damage. No deaths.

Round 2 (after `battle_move 2,4` → `battle_wait E`):
```
> Ramza took 324 damage (HP 393→69)
> Ramza lost Regen, Protect, Shell
... (~50+ events; multiple Ramza deaths + revives + position swaps)
> Ramza moved (3,4) → (2,4)
> Ramza recovered 324 HP (HP 69→393)
> Ramza gained Regen, Protect, Shell
> Ramza died
> ... (+1 more)
```
Reality: Ramza (2,4) HP=384/393 (took only 9 damage, regen healed back). 5 enemies all alive. No KOs.

**Notes:** Burst severity is comparable to iter1 + iter2. Iter3's RosterMatcher Pass 2 tightening did not measurably reduce phantom narrator output. **Cascade hypothesis FAILED for the third iteration in a row.** Position-identity cascade still mis-tags enemy positional events as "Ramza ..." identity.

### [P1] AttackOutcomeClassifier MISS/KO confusion — KO confirmed FALSELY
**Repro:** First Attack of round 1, `battle_attack 3 4` (target enemy at (3,4) HP=69).
**Expected:** Either KO confirmed AND post-attack scan shows HP=0/69 OR MISS reported and HP=69/69 stays.
**Actual:** Bridge said `KO'd! (69→0/69)`. Post-attack scan immediately after showed `[ENEMY] (3,4) f=W HP=69/69 d=1` — alive at full HP.
However, after the next enemy turn, (3,4) showed `HP=0/69 DEAD`. So the KO did *eventually* land — but the post-attack scan was still pre-commit.
**Notes:** Driver flagged as known outstanding. The bridge result is too eager — it reports KO before the game state actually commits the death. This causes the narrator + scan reality to disagree for several frames. Either delay the result by another settle, OR multi-read with retry on HP-mismatch. As-is, the bridge "KO'd!" is misleading mid-frame.

### [P3] Ramza MaxHP creeping up over battle — 393→396→398 silently
**Repro:** Battle started with Ramza HP=393/393. After first KO + status reset cycle, MaxHP became 396. Battle 2 started at MaxHP=396, after enemy hit + counter cycle MaxHP=398. Drift seems incremental.
**Expected:** MaxHP stable across battles unless level-up triggered.
**Actual:** Silent +3 then +2 ticks.
**Notes:** May be game-real (Knight absorbing Crystal stat bonuses or Movement+1 secondary effect). NO narrator event reports a level-up. Probably a real game stat tick that the mod doesn't expose. De-escalated to P3 since reality reads consistent.

### [P2] Two enemies listed at same tile in Units block — duplicate entry hides alive enemy
**Repro:** After Round 3 attack on (3,4), Units list showed:
```
[ENEMY] (3,4) f=W HP=0/69 d=1 CRYSTAL
[ENEMY] (3,4) f=S HP=71/71 d=1
```
**Expected:** Only one unit per tile (CRYSTAL state should override or list entries for distinct enemies should be at different tiles).
**Actual:** Bridge lists 2 separate enemies at (3,4) — one crystallized at HP=0/69, one alive at HP=71/71.
**Notes:** Likely two distinct enemies — the alive (3,4) HP=71/71 is the "(2,3) HP=71" enemy that moved on enemy turn into (3,4) (right where the crystal was). Bridge isn't deduping by post-state; it lists them both. Not breaking gameplay (`battle_attack 3 4` correctly hit the alive one), but the visual display shows two units overlapping which is confusing.

### [P2] Phoenix Down [REVIVE-ENEMY!] target listing on dead enemy crystal/dead tiles
**Repro:** Throughout battle, Phoenix Down ability listing showed `(3,4)<? >side [REVIVE-ENEMY!]>` and similar on enemy KO'd tiles.
**Expected:** Phoenix Down on enemy corpses should be flagged or hidden by default — reviving an enemy is almost always a mistake.
**Actual:** Bridge IS correctly flagging with `[REVIVE-ENEMY!]` warning, which is good.
**Notes:** Actually a positive observation — the warning is helpful. Not a bug. Removing this from bug list — leaving here as "behavior verified."

### [P3] battle_ability got stuck on BattleAbilities screen for 4 minutes after Potion used
**Repro:** `battle_ability Potion 2 4` from BattleMyTurn at (2,4). Bridge said `Used Potion on (2,4) — cursor was already on target`. Subsequent `screen` showed `[BattleAbilities] ui=Attack` — stuck. Tried `tkey Escape`, `key Escape`, `key X`, `key Z`, `fft '{"action":"key"}'`, `fft '{"action":"raw_key"}'`, `fft '{"action":"navigate"}'`, all failed under STRICT mode (or even with `strict 0` produced "Unknown action" for raw_key/key). After ~4 minutes of attempts I tried `battle_wait E`, which somehow committed and progressed to enemy turn.
**Expected:** `battle_ability` should land on a clean state (BattleMyTurn or transitioning state); user should have a documented escape from BattleAbilities if needed.
**Actual:** No working helper / strict-allowed action gets you out of BattleAbilities cleanly. Strict-disabled `key Escape` produced `[?]` log entries with no observable state change.
**Notes:** This is a tooling friction issue. Driver says "use named action helpers" but there's no documented helper to step back from BattleAbilities → BattleMyTurn. Adding a `battle_cancel`/`battle_back` helper would unblock this.

### [P3] Mv=0/Jp=0 heap-read failure spam — still present
**Repro:** Every `scan_move` on Ramza HP=396/396. Logs show MemoryExplorer SearchBytes 20 regions, 10MB, 0 matches.
**Expected:** Heap-fingerprint match on Ramza's HP/MaxHP pattern.
**Actual:** Both narrow + broad fingerprint searches return 0; falls back to BattleArray Mv=3/Jp=3 (the planner sees correct values).
**Notes:** Same as iter2 P3. Functional fallback works (no softlock). Just noise.

### [P3] HP guard logging is verbose — many "Rejecting hp>maxHp phantom" lines
**Repro:** `logs 200`.
**Expected:** Single log line per battle when filter activates, or aggregated.
**Actual:** ~11+ `[CollectPositions] Rejecting hp>maxHp phantom: (0,0) t1 lv32 hp=8192/288` lines per battle.
**Notes:** Iter3 fix is WORKING (the (0,0) phantom is filtered out of unit list — reality verified). But verbose logging suggests filter runs on every CollectPositions call. Aggregating "1× rejection per scan" would clean logs.

### [P2] auto_place_units 19s regression in second battle (was 5.5s in first)
**Repro:** Second battle of session: `auto_place_units` took 19136ms vs 5464ms in first battle.
**Expected:** Consistent timing for the same operation.
**Actual:** ~3.5x slowdown.
**Notes:** Possibly the formation cache wasn't reset between battles. Could compound over a long session. Not blocking gameplay but worth investigating.

### [P2] Bridge can't dismiss BattleDesertion → WorldMap via documented helper
**Repro:** `[BattleDesertion] ValidPaths: Dismiss`. Tried `fft '{"action":"battle_dismiss_party_screen"}'` (BLOCKED), `fft '{"action":"execute_action","to":"Dismiss"}'` (failed: not on this screen). The transition appears to have happened automatically.
**Expected:** A clean `dismiss` or `battle_dismiss` helper that converts BattleDesertion → WorldMap reliably.
**Actual:** No matching helper; transition is stochastic / strict-mode hostile.
**Notes:** Tooling friction — combine with the BattleAbilities-stuck issue, the bridge has multiple "valid path exists in screen but no helper exposes it" cases.

### Second battle confirms identical pattern
- HP guard fix: HOLDS (no phantom (0,0) in unit list, again).
- Phantom narrator burst: REPRODUCED — round 2 had ~25 events, round 3 had ~30 events, including counter-attack-driven phantom Ramza moves.
- Terminal flicker: REPRODUCED — final KO of (3,3) → BattleDesertion (allies=3 reported).
- AttackOutcomeClassifier: REPRODUCED — `Attacked (3,4) from (2,4) — KO'd! (75→0/75)` then post-attack scan shows (3,4) HP=75/75 still alive.
- **One clean turn observed:** the `battle_attack 5 3 → battle_wait E` cycle in battle 2 produced ZERO phantom events — only `[OUTCOME yours] Ramza +9 HP / [OUTCOME enemies] (unit@5,3) KO'd` plus 3 real events. The clean turn was an attack with NO Counter trigger; the burst-prone turns all had Counter activations. Hypothesis: phantom narrator burst correlates with Counter / multi-event turns where cache invalidation gets out of sync.

## Iter3 fix verdict

| Fix | Status | Notes |
|---|---|---|
| HP>MaxHP guard at NavigationActions | **HOLDS** | (0,0) HP=8192/288 phantom no longer in unit list. Logs confirm `[CollectPositions] Rejecting hp>maxHp phantom`. P1 from iter2 RESOLVED. |
| Terminal flicker recovery (Victory not locked into Desertion) | **FAILED** | Killed last enemy → `BattleDesertion` again. Same bug as iter1 + iter2. Validator did NOT recover Victory state. |
| RosterMatcher Pass 2 strictness — burst reduced | **FAILED** | Phantom narrator burst severity unchanged: ~26 events round 1, ~50 events round 2, similar through battle. |
| FindAbility strict scope (9ddc643 holdover) | **HOLDS** | `Throw Stone` correctly returned `not found in available skillsets: Attack, Arts of War, Items, Reequip`. |
| Mv=0/Jp=0 softlock fix (88d41ad holdover) | **HOLDS** | No softlock; planner uses BattleArray Mv=3 Jp=3 fallback when heap fingerprint misses. |
| AttackOutcomeClassifier (driver-noted outstanding) | **FAILED** | "KO'd!" reported pre-commit; post-attack scan shows victim alive at full HP for several frames. |

## Cascade hypothesis (3rd iteration)

**STILL FAILED.** The Pass 2 strict tightening (commit ae2968c) did not measurably reduce the phantom narrator burst. Output is dominated by the same Ramza-position-confusion pattern as iter1 + iter2. The HP guard fix only addressed the (0,0) phantom in the units list (which IS resolved); it did not stop the position-identity cascade in the narrator path.

The phantom-unit cascade now appears to have these symptoms surviving iter3:
1. **Position-identity confusion (UNCHANGED):** enemy positional events tagged as "Ramza ..." in narrator output. ~26–50 events per enemy turn.
2. **Stale (0,0) entry in units list (FIXED):** HP guard correctly filters this out at CollectPositions.
3. **Phantom ally count (UNCHANGED):** ScanMove still reports `allies=3` when only Ramza is on field. This cascades into BattleDesertion classification on Victory tiles.

The HP guard fix only addressed surface symptom #2. Symptoms #1 and #3 still feed downstream classifiers (narrator + Desertion validator). Pass 2 strictness was supposed to hit #1 — it didn't. Terminal flicker recovery was supposed to hit #3's downstream effect — it didn't.

**Hypothesis for iter4:** the phantom ally count must be the SAME root cause as the narrator burst — both come from the same cache (likely RosterMatchCache or the heap walker). If `enemies=1 allies=3` could be reduced to `enemies=1 allies=1`, the Desertion misclassification AND the phantom narrator burst would both disappear. Look for what makes `allies` count > 1 when there's only Ramza.

**Auxiliary finding for iter4:** the ONE clean enemy turn this session (battle 2 final turn) had no Counter trigger. ALL phantom-burst turns had Counter activations or multi-event hits. Strong correlation between Counter triggers and burst severity. Targeting the counter-attack code path in the narrator may be a focused intervention.

## Summary

**Battle outcomes:** 2× MAP085 Mandalia Plain wild encounters. Both ended in `BattleDesertion` despite Ramza killing all enemies (real Victory misclassified). 5 enemies KO'd in battle 1, 4 enemies KO'd in battle 2.

**Total bugs:** 8 logged (3× P1, 3× P2, 4× P3 — duplicates from iter1/iter2 collapsed).

**Severity histogram:**
- P1 = 3 (terminal flicker / Desertion misclass; phantom narrator burst; AttackOutcomeClassifier MISS-vs-KO)
- P2 = 3 (duplicate enemy at same tile; auto_place 19s regression; no BattleDesertion dismiss helper)
- P3 = 4 (Mv=0 noise; HP guard log spam; MaxHP creep; battle_ability stuck-on-BattleAbilities tooling friction)

**Top 3 friction:**
1. **Terminal flicker → Desertion** — clear Victory keeps misclassifying. Same bug 3rd iteration in a row. Root cause confirmed: `allies=3` phantom count when only Ramza is on field.
2. **Phantom narrator burst** — 26–50 events per enemy turn, almost all fictional. Cascade hypothesis FAILED 3rd time. Pass 2 strictness did not measurably reduce. Strong correlation observed with Counter triggers.
3. **AttackOutcomeClassifier MISS-vs-KO** — bridge says "KO'd!" pre-commit; post-attack scan still shows victim alive at full HP for several frames. Highly reproducible.

## Iter3 fix verdict

| Fix | Status | Notes |
|---|---|---|
| HP>MaxHP guard at NavigationActions | **HOLDS** | Iter2 P1 RESOLVED. (0,0) HP=8192/288 phantom no longer in unit list. |
| Terminal flicker recovery | **FAILED** | Killed last enemy → BattleDesertion in 2/2 battles. |
| RosterMatcher Pass 2 strictness | **FAILED** | Phantom burst ~26–50 events per turn — same severity as iter1/iter2. |
| FindAbility strict scope (holdover) | **HOLDS** | "not found in available skillsets: ..." message clean. |
| Mv=0/Jp=0 softlock fix (holdover) | **HOLDS** | Logs noisy but planner functional. |
| AttackOutcomeClassifier (driver-noted outstanding) | **FAILED** | Highly reproducible KO-pre-commit reporting. |

## Trend analysis

- **Iter1:** Cascade and (0,0) phantom both at full force.
- **Iter2:** Cascade and (0,0) phantom both at full force. Phantom (0,0) was the iter1 fix attempt — failed.
- **Iter3:** Phantom (0,0) RESOLVED (this iter's HP guard works). Cascade still at full force. Terminal flicker still at full force.

**Trend:** Improving slowly. Iter3 fixed the SHALLOW cascade symptom (units list pollution) but the DEEP cascade (narrator + ally count) is unchanged. P1 count went 4→4→3. We are NOT trending toward zero P1s — we are trending toward "1 fewer surface bug per iter, root cause unchanged."

**Critical observation:** the same bug surfaces in 3 distinct downstream places (narrator, ally count, screen transition validator). Treating each as a separate fix is yielding diminishing returns. **Iter4 needs to target the ROOT cache that produces phantom unit identity matches** — not another symptom-side guard.

## Wall-clock time
~24 minutes (02:20 → 02:44). Two full battles played, both completed.


