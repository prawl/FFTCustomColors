<!-- This file tracks completed work. Moved out of TODO.md during spring cleaning 2026-04-17. -->
# FFT Hands-Free — Completed Work Archive

Items fully shipped ([x]) across sessions. Kept out of TODO.md to make the active queue easier to scan. Partial ([~]) items stay in TODO.md in their original section.

---

### 2026-04-25 — Detection rule tightening + Victory→Desertion fix (11 commits, +26 tests 4469→4495)

Session theme: tight TDD cycle on detection rule ordering, plus a live-captured signal-byte fix for the long-standing Victory→Desertion misdetect (deferred twice across prior sessions). Four new pure helpers, eight shipped fixes, one bonus fix from live-repro capture.

**Commits:**
- `e909097` — BattleDialogue save-load rule: new detection branch for rawLocation 0..42 + battleMode==0 + IsRealEvent(eventId)
- `b2e7c0e` — ExecuteTurnPreflightValidator pure helper (10 tests) wired at ExecuteTurn entry
- `4eec7c9` — Pin FacingStrategy recommendation for Lenalian scenario (audit verified algorithm correct)
- `8ed1b4a` — 5 regression pins for save-load BattleDialogue rule ordering
- `25b3484` — 4-cardinal arc characterization pins + `CountArcsAtFacing` test helper
- `2a1ce72` — Suppress EqA-mirror ViewedUnit-force on Battle* screens (fixes polluted response field)
- `6f00304` — Reset `_movedThisTurn` / `_actedThisTurn` on `BattleLifecycleEvent.StartBattle`
- `47d1bfc` — CounterAttackInferrer MaxHp sanity check (4 tests) — defense-in-depth with `d9b2fd1` KO gate
- `cca3463` — battle_ability one-shot auto-retry on "Failed to enter targeting mode"
- `63a3e4c` — TODO entry updated with live-captured Victory→Desertion signal bytes
- `5243011` — Fix Victory misdetected as BattleDesertion (battleTeam==0 guard, new pin test)

**Shipped + LIVE-VERIFIED:**

- [x] **BattleDialogue save-load misdetect** — rawLocation=28 + battleMode==0 + IsRealEvent(eventId) now routes to BattleDialogue before the TravelList rule (`party==0 && ui==1`) can steal the frame. 5 regression pins guard against over-firing on legitimate world-map states.
- [x] **execute_turn pre-flight** — `Move already used this turn — only Act or Wait remain.` live-verified in 169ms, replacing the old misleading "Not in Move mode" error. Mirrors `8cf9197` entry-reset pattern.
- [x] **EqA ViewedUnit-force Battle\* exclusion** — no more `[EqA promote] Setting viewedUnit='Ramza'` logs during BattleMyTurn; response.screen.ViewedUnit stays clean on battle polls.
- [x] **Victory→Desertion fix via battleTeam==0 guard** — live-captured at Zeklaus Desert: solo-Ramza killed last enemy, battle_wait returned `[BattleDesertion]` before settling to `[WorldMap]`. Smoking-gun log `team=1 act=1 mov=1` showed stale battle-array slot-0 pollution at detection time. Real Desertion = player unit left field → battleTeam==0. New pin test captures the live byte signature.

**Shipped + not-yet-live-verified:**

- [x] **FacingStrategy 4-cardinal arc pins** — hand-traced the Lenalian scenario and pinned front/side/back counts for all 4 cardinals. Audit verified the algorithm is correct; earlier "0/4/2 expected" claim was hand-counting a different facing.
- [x] **Turn-flag reset on StartBattle** — `_movedThisTurn` / `_actedThisTurn` now clear on battle-lifecycle start so mid-turn battle exits (flee / GameOver) don't leak stale flags into the next battle.
- [x] **CounterAttackInferrer MaxHp sanity check** — rejects damage deltas > target MaxHp as animation-transient reads. Pairs with the `d9b2fd1` UnitScanDiff KO-stable MaxHp gate for two independent defenses against the same false-KO class.
- [x] **battle_ability one-shot auto-retry** — when nav fails with "Failed to enter targeting mode", it already calls EscapeToMyTurn internally; one retry from the clean state usually succeeds. Avoids the user-intervention cycle.

**Techniques worth propagating:**

- **Capture-then-fix for speculation-blocked bugs.** The Victory→Desertion bug had been deferred twice because regression pins blocked the obvious encA fix path. This session's live playthrough surfaced the byte signature (`team=1 act=1 mov=1`) that narrowed the fix to a single discriminator (battleTeam==0) all existing tests already satisfied. The `/prime`-initiated play sessions are how these speculation-blocked bugs get unblocked.

- **Audit before fixing.** The "Recommend Wait arc counts off" TODO triggered a hand-trace instead of a speculative code change. Audit found the algorithm was correct and the bug was my own incomplete hand-count. Shipped as a characterization pin that encodes the correct expected behavior instead of a broken fix.

- **Defense-in-depth for narrator false-KO.** Two independent guards now reject the same Knight-died-for-521 false positive: (a) `UnitScanDiff` refuses to classify HP→0 as `ko` when MaxHp shifted (commit `d9b2fd1`); (b) `CounterAttackInferrer` rejects deltas > target MaxHp (commit `47d1bfc`). Either guard alone closes the common case; both together close edge cases where one scan's MaxHp was stable but the delta was still implausible.

- **Regression pins positive AND negative.** The BattleDialogue fix shipped with 5 pins — 3 negative (TravelList/WorldMap still fire without eventId) and 2 positive (new rule fires when expected). Prevents future edits from breaking either side.

### 2026-04-24 — Detection + narrator + scan-polish pass (24 commits, +200 tests 4269→4469)

Scan fallbacks + detection-rule tightening. Seven new pure helpers, fifteen shipped+live-verified fixes. Test count 4269→4469.

**Commits:**
- `3b868ae` — JobBaseStatsTable: static WotL Move/Jump by job name (generic + monsters + story-unique), 103 tests
- `52fe7ea` — StaleBattleMovingClassifier: BattleMoving→BattleWaiting override window from last Wait Enter, 9 tests
- `26aa860` — MoveJumpFallbackResolver: active-unit BFS input path now shares the fallback resolver, 7 tests
- `7ca8b1d` — WorldMapBattleResidueClassifier: suppress WorldMap false positives within 3s of last Battle state, 9 tests
- `2f52a79` — HeapUnitMatchClassifier: score candidates by level-byte agreement at struct+0x09, 5 tests
- `481e64d` — TODO hygiene + _actedThisTurn flag for post-action cursor correction
- `34a2dbd` — TODO entry: CounterAttackInferrer false-KO when MaxHp changes
- `d9b2fd1` — UnitScanDiff: gate KO classification on stable MaxHp (suppresses animation transients)
- `3f93c21` — BattleVictory encA=255 sentinel tightened with gameOverFlag==1 guard
- `82635b6` — TODO: battle_wait speed audit (poll already tight; needs fast-forward hotkey hunt)
- `799833a` — Grenade fingerprint variant `D4-12-07-57-1E-1E-5A-73-27-55` added
- `f82007c` — TODO: two new scan-output bugs from live play (facing drift, dead-target marker)
- `e8d1cca` — BattleMapIdToLocation: curLoc= uses live map-id byte first with world-map latch fallback, 21 tests
- `8b57cec` — AttackTileOccupantClassifier: HP=0 guard filters animation-transient "alive" reads, 10 tests
- `55c5275` — Shell: rename `Facing:` → `Recommend Wait:` + file two facing TODOs
- `360cf8f` — screen() BattleMyTurn header now renders curLoc
- `c36ec53` — FacingDecider: flip N/S labels; +y = south in FFT grid (matches FacingByteDecoder)
- `5adeda1` — Narrator pre-snap settle 200ms → 400ms for slower action animations
- `679f382` — Attack tiles now render dead/crystal/petrified labels + jobName on non-attackable occupants, 9 tests
- `442ef5d` — CriticalHpInferrer: 6 edge-case boundary tests (exact threshold, tiny MaxHp, multi-events)
- `9d0a515` — Shell: player/ally facing letter (`f=X`) now renders alongside enemies
- `bbefc4d` — TODO entry: PLANNING-HEAVY menuCursor byte drift beyond action-state overrides
- `68d8060` — TODO entry: execute_turn lacks Act-consumed pre-flight validation
- `3bedba5` — TODO entry: BattleDialogue misdetected as TravelList after save-load

**Shipped + LIVE-VERIFIED:**

- [x] **Enemy Move/Jump fallback** — `JobBaseStatsTable` covers all generic jobs + 50+ monsters + story-unique classes with canonical WotL Mv/Jp. `MoveJumpFallbackResolver` composes (live heap read) with (table base). Wired at BattleUnitState render AND scan_move BFS-input paths. Live-verified: Goblin Mv=4 Jp=3, Knight Mv=3 Jp=3, Archer Mv=3 Jp=3, Exploder Mv=4 Jp=3 all match. Active-unit Mv=0 collapse after armor break also patched via same resolver.
- [x] **HeapUnitMatchClassifier** — scores heap candidates by struct+0x09 level match. Fixed the Archer-at-HP=4/452 relabeling as "Black Goblin" / "Knight" across chunks. Log line confirms selection: `picked base=0x... candLevel=89 score=100 from 3 candidates`.
- [x] **Grenade fingerprint** — `D4-12-07-57-1E-1E-5A-73-27-55` now resolves directly (was previously via cache fallback).
- [x] **WorldMapBattleResidueClassifier** — suppresses transient WorldMap flickers during enemy-turn animations. 20+ fires logged in a single battle_wait with delays 47ms / 141ms / 250ms / 313ms / 687ms / 1891ms.
- [x] **`_actedThisTurn` flag** — symmetric to `_movedThisTurn`; fixes post-action `ui=Abilities` stale when `battleActed` byte drifts to 0. Live logs confirm: `cursor=1 effective=0 acted=True` fires reliably.
- [x] **BattleVictory sentinel tightening** — `gameOverFlag==1` discriminator added to rule 4 (encA=255 sentinel). No `[BattleVictory]` flashes in logs during `battle_ability` responses.
- [x] **`curLoc=` via live map-id byte** — reads `0x14077D83C`, reverses through `BattleMapIdToLocation`. Live-verified: `curLoc=The Siedge Weald` (was stuck at "Lenalian Plateau" via `_lastWorldMapLocation` latch).
- [x] **Attack tiles dead/crystal/petrified labels** — `Right→(1,6) dead` / `Down→(5,4) petrified (Gobbledygook)` live-seen. HP=0 animation-transient guard also catches units that visually dead but haven't propagated the status bit.
- [x] **FacingDecider N/S label flip** — +y is south in FFT grid (matches `FacingByteDecoder`). Live-verified: `Recommend Wait: Face South` correctly renders after the flip.
- [x] **`curLoc=` in screen() BattleMyTurn header** — screen() had a bespoke renderer at fft.sh:3445 that bypassed the shared `_fmt_screen_compact`. Now consistent.
- [x] **`Recommend Wait:` rename** — was confusingly labeled `Facing:` (implies current facing); now clearly a recommendation.
- [x] **Player facing letter `f=X`** — shell now renders facing on player/ally rows too. Directly surfaced the separate player-facing-byte memory bug (Ramza reads East while visually West).
- [x] **SelfDestructInferrer** — previously UNVERIFIED, live-caught this session: narrator emitted `> Goblin self-destructed (dealt 336 to Black Goblin, 15 to Ramza)`.

**Shipped + not-yet-live-verified (code + tests green, waiting for organic repro):**

- [x] **StaleBattleMovingClassifier** — BattleMoving→BattleWaiting override when Wait Enter <500ms ago. Needs a specific timing (external `screen` poll inside the override window) to trigger.
- [x] **UnitScanDiff MaxHp-stable KO gate** — prevents Counter-narrator emitting false "Knight died for 521 dmg" when Defending buff drop shifts MaxHp 521→524 with HP transient-reading 0.
- [x] **CriticalHpInferrer edge-case tests** — 6 boundary tests (exact threshold, tiny MaxHp, multi-events in one window, negative HP defense).
- [x] **Narrator pre-snap 200→400ms bump** — covers slower action animations (Chaos Blade on-hit rolls). No false-positive counter lines observed post-bump this session.
- [x] **CounterAttackInferrer MaxHp-stable guard** — shipped via the UnitScanDiff KO gate; secondary effect same commit.

**Techniques worth propagating:**

- **Stale-byte override pattern** (template — shipped 3× this session: BattleMoving→BattleWaiting, WorldMap→cached-Battle, _actedThisTurn cursor correction). Pattern: pure classifier + `Environment.TickCount64` stamp at trigger site + CommandWatcher consult after Detect() + update cache-after-override so transients don't poison the cache. See `memory/feedback_stale_byte_override_pattern.md`.
- **`ClassifyOccupant` as enum-string return type** — preserving life-state information through the Attack-tiles render (instead of collapsing non-attackable to "empty") adds real value with no downstream cost. Apply elsewhere where we throw information away in a render.
- **Level-byte heap scoring** — when multiple heap slots match a common (HP, MaxHP) pattern, secondary bytes (level, team) discriminate the real struct from false positives. First-match selection is a scan-quality bug when HP is a common number.
- **Reverse-lookup helpers hand-coded in-memory** — `BattleMapIdToLocation` inverts `random_encounter_maps.json` in a static Dict<int,int>. Faster than file-read per scan and test-able without I/O mocking.

### Session 58 (2026-04-22) — 36 tasks, 17 new pure helpers, +207 tests

**2 commits (all TDD'd, zero regressions). Tests: 3967 → 4174.**

Commits:
- `342e5e4` — S58 — new pure helpers + DTOs (17 classes, 240 tests)
- `297b4bc` — S58 — wire up helpers across detection / lifecycle / battle actions

**17 new pure helpers (all TDD'd before wiring):**

- [x] **`AutoScanCommandClassifier`** — skip-list for post-action auto-scan. Prevents `[auto-scan] No ally found` on `auto_place_units` where the battle array is still populating post-commit.
- [x] **`SearchBytesPlan`** — `CommandRequest` → `SearchBytesInAllMemory` args (MinAddr, MaxAddr, BroadSearch). Plus `BroadSearch` bool field on CommandRequest. Unblocks per-unit-ct hunt.
- [x] **`ExecuteTurnResultAccumulator`** — HP delta / pre-post move / killed-unit diff across an `execute_turn` bundle. Wired via new `TurnSummary` + `KilledUnitSummary` DTOs on CommandResponse.
- [x] **`LiveHpAddressCache`** — per-battle memo of live-HP addresses keyed by (maxHp, level). Avoids the 500MB scan on every `battle_attack` when the same target is hit repeatedly.
- [x] **`HpTransitionClassifier`** — pure `(preHp, postHp) → None/Damage/Heal/Kill/Raise` classifier. Drives BattleStatTracker hook dispatch.
- [x] **`UnitDisplayName`** — `Name ?? JobName ?? "(unknown)"` fallback. Enemy-side KOs attribute to "Minotaur" instead of "(unknown)".
- [x] **`UnitMoveJumpCache`** — per-battle memo of successful heap Move/Jump reads. Fallback when `TryReadMoveJumpFromHeap` misses — prevents battle-wide Mv=0 collapse.
- [x] **`CounterAttackKoClassifier`** — detects active-unit-KO-from-reaction. Surfaces `[counter-KO]` in battle_ability response.Info.
- [x] **`MilestoneDetector`** — per-unit lifetime-stats diff → emoji callouts (first kill, 10/50/100/500 kills, 1k/5k/10k/50k dmg, 10/50/100 battles).
- [x] **`TurnInterruptionClassifier`** — classify mid-turn screen transitions. Aborts ExecuteTurn bundle on BattleEnded/OutOfBattle with `[turn-interrupt]` context.
- [x] **`LineAoeCalculator`** — cardinal-line AoE + PickBestDirection. For Shockwave / Ice Saber.
- [x] **`SelfCenteredAoeCalculator`** — Manhattan-diamond AoE with include/exclude-self. For Chakra / Cyclone / Bard/Dancer.
- [x] **`MultiHitTargetEnumerator`** — score + rank AoE centers by enemy coverage. For Bio / Ramuh.
- [x] **`GeomancySurfaceTable`** — 15 surface types → Elemental ability name lookup (PSX-canonical).
- [x] **`LiveBattleMapId`** — session-48 map-id byte at `0x14077D83C` extracted to constant + `IsValid(mapId)` range check.
- [x] **`CharacterStatusLeakGuard`** — filter for S31 detection leak (CharacterStatus flickers during battle_wait). Pure, not yet wired.
- [x] **`BattleMenuAvailability`** — per-slot enabled/grayed classification from (moved, acted). Pure, not yet surfaced in screen response.

**Wire-ups shipped:**

- [x] **Auto-scan BattleFormation guard** (S57 follow-up) — `auto_place_units` opts out of post-action auto-scan. Live-verified in S58: no more `[auto-scan] No ally found` on the response.
- [x] **BattleStatTracker damage/kill/heal/raise/move/ability hooks** (S57 follow-up) — `RecordAttackStats` in BattleAttack; OnAbilityUsed at 4 BattleAbility return paths; OnMove in MoveGrid. Live-verified at Zeklaus: Lloyd 1 kill + 127 dmg, Wilham 22 dmg, Ramza 588 dmg, milestones persisted to `lifetime_stats.json`.
- [x] **ReadLiveHp address cache** (S57 follow-up) — cache fast-path + memoization on full-search hit; `StartBattle` lifecycle clears.
- [x] **`broadSearch` on search_bytes** (S52 follow-up) — plumbed via `SearchBytesPlan.From(command)`. Schema pin tests.
- [x] **LiveBattleMapId regression pin** (S48 follow-up) — constant + IsValid extracted with 3 tests.
- [x] **battle_ability first-scan null/null race** (§1 Tier 3) — retry once when scan returns null/null with live active unit.
- [x] **Active unit name/job stale across battles** (§1 Tier 3) — cache reset on StartBattle lifecycle event.
- [x] **execute_turn HP-delta / move / kill accumulator** (§1 Tier 5) — all three atomic tasks shipped. Response carries `turnSummary` field.
- [x] **Detect failed move/attack retry** (§9) — BattleAttack 1500ms poll-retry for Abilities→Attacking + charging-confirm dismiss retry.
- [x] **Handle unexpected screen transitions during turn** (§9) — TurnInterruptionClassifier + abort-on-BattleEnded/OutOfBattle in ExecuteTurn.
- [x] **Counter attack KO** (§9) — CounterAttackKoClassifier + Info marker.
- [x] **Session aggregates + milestone announcements** (§13) — MilestoneDetector + RenderBattleSummary wire.
- [x] **Re-enable strict mode default** (Low Priority) — `StrictMode` default flipped to true.
- [x] **gameOverFlag==0 requirement audit** (Low Priority) — regression pin confirms no rule uses `gameOverFlag == 0` as positive assertion.

**Bug fixes shipped (from S58 live verification):**

- [x] **BattleLifecycleClassifier Victory→Desertion double-fire** — `previous == "BattleVictory"` early-returns None. `EndBattle` idempotent via `EndedAt` check.
- [x] **BattleChoice event catalog label access** (State Detection TOP) — `BattleChoiceEntry` record + `OptionLabel(eventId, row)` + `OptionLabelOrGeneric` fallback. Mandalia eventId=16 entry gets structured label access.
- [x] **BattleVictory sentinel mid-cast false-positive** (Fix #4) — encA=255 sentinel requires `submenuFlag==1`. Session-49 Siedge Weald capture still passes.
- [x] **`(unknown)` attacker name fallback** (Fix #6) — UnitDisplayName.For(Name, JobNameOverride).
- [x] **Mv=0 whole-battle collapse fallback** (Fix #2) — UnitMoveJumpCache serves as fallback when heap search misses.
- [x] **battle_attack submenu retry + charging-confirm dismiss** (Fix #3, Fix #5) — both now retry instead of fail-fast.

**Regression pins shipped (test-only):**

- [x] `DetectScreen_EncounterDialog_DoesNotDependOn_EncAEncB` — 36-combination sweep.
- [x] `DetectScreen_BattleDesertion_DoesNotDependOn_EncAEncB` — 36-combination sweep.
- [x] `DetectScreen_EncA255_MidCast_GuardedBySubmenuFlag_DoesNotFireVictory` — Shout-cast pin.
- [x] `DetectScreen_AbilityTargeting_BattleModeOne_StaysInBattleBranch` — battleActiveTurnFrame pin.
- [x] `DetectScreen_AbilityTargeting_AtStoryBattleLocation_NotCutscene` — atNamedLocation pin.
- [x] `DetectScreen_PostBattleRules_NoDependsOn_GameOverFlagZero` — sticky flag audit.
- [x] `BattleLifecycleClassifier.VictoryToDesertion_DoesNotReEndBattle` — double-fire guard.
- [x] `BattleLifecycleClassifier.VictoryToGameOver_DoesNotReEndBattle` — symmetric guard.

**Cumulative S58: 36 items shipped. 17 new pure helpers + wire-ups. +207 tests (3967 → 4174). Zero regressions.**

---

### Session 56 (2026-04-22) — battle turn cycle fixes + TODO sweep

**14 commits, 11 real bug fixes, all live-verified.** Tests: 3893 → 3930 (+37). End-to-end battle turn cycle (`execute_turn move + ability + wait`) now completes cleanly in ~14s (pre-S56: 142s then timeout).

Commits (in order):
- `d8d1431` — TODO sweep: archive 6 stale items, split 6 compound tasks, dedupe
- `a849bed` — Remove screenshot.ps1 (full-desktop); screenshot_crop.ps1 is correct default
- `08e9d99` — Fix battle_wait overshoot to Auto-battle after battle_move
- `bdcd5bb` — Drop wrong UIBuffer fallback when active-unit heap Move/Jump read fails
- `8bb692e` — Fix battle_ability picking wrong ability via escape-to-known-state (S55 🔴 BLOCKING)
- `b2f035d` — Retry battle_ability/battle_attack state-gate on transient non-turn reads
- `402bd92` — Add broad-search fallback for TryReadMoveJumpFromHeap
- `3ba67d5` — Tighten MoveGrid confirmation to BattleMyTurn/BattleActing only
- `07646ab` — battle_wait accepts BattleActing as friendly-turn-reached
- `85682fb` — battle_ability settles to turn state before returning completed
- `24f0348` — battle_wait: Enter fallback on facing-confirm + dismiss banners in poll
- `b3180e3` — Heap Move/Jump search uses MaxHP twice pattern (damage-invariant)
- `851a927` — battle_attack escapes re-targeting screen after a miss
- `6cc2e7d` — battle_wait exits poll on BattleVictory/BattleDesertion

**Fixes shipped — code changes (all live-verified):**

- [x] **🔴 S55 BLOCKING: battle_ability picks the wrong ability** (`8bb692e`) — new `BattleAbilityEntryReset` pure helper (29 TDD tests). Escapes 0-3 times to reach BattleMyTurn before each nav, guaranteeing widget reconstruction (both submenu and ability-list cursors reset to idx 0). Then `AbilityListCursorNavPlanner.Plan(0, targetIdx, listSize)` picks shorter Up/Down direction. Live-verified: Chakra (Martial Arts idx 6, Up×2), Haste (Time Magicks idx 0, None×0).

- [x] **battle_wait overshoots to Auto-battle after battle_move** (`08e9d99`) — new `BattleWaitLogic.ShouldRetryVerifyAfterNav` pure rule (6 TDD tests). Post-nav verify was re-reading the same stale cursor byte we'd already corrected-away-from, amplifying 1 Down into 3 Downs → Wait(2) → Auto-battle(4). Now trust the initial nav when the byte is stuck at its pre-corrected value.

- [x] **Active-unit Move/Jump falls back to UIBuffer noise** (`bdcd5bb`) — UIBuffer holds cursor-hovered unit's BASE stats, not active unit's effective stats. S56 repro: Wilham (Monk base Mv=3) got UIBuffer Mv=4 → BFS false-positive tile (7,9) → `battle_move` stuck. Fix: honest `Mv=0 Jmp=0` on heap miss; BFS returns empty; scan surfaces "Mv=0 Jmp=0 Move tiles: (none)" so Claude sees unknown state. 2 new MovementBfs tests pin Move=0 → empty.

- [x] **Heap search narrow filter misses most unit structs** (`402bd92`) — narrow only covers ~11MB of ~2GB heap. Fix: retry with broad search (IMAGE + WRITECOPY, 2GB budget) when narrow returns 0 matches. Rarely needed after the MaxHP-pattern fix below.

- [x] **Heap Move/Jump search pattern was wrong for damaged units** (`b3180e3`) — old pattern `(currentHp, MaxHp)` literally doesn't exist in memory for damaged units. The heap struct stores HP and MaxHP as *both = MaxHp* (damage-invariant base). S56 repro: Lloyd HP=370/628 → 0 matches anywhere for `72 01 74 02`, but `74 02 74 02` (MaxHP twice) matched cleanly at 0x4167086130. Fix: search for `(MaxHp, MaxHp)` pair. Works for full-HP and damaged units.

- [x] **execute_turn state-gate race on transient reads** (`b2f035d`) — move-animation tails cause detection to flicker to BattleEnemiesTurn transiently; single `_detectScreen` read fails the ability sub-step. Fix: new `WaitForTurnState(timeoutMs, out waitedMs)` helper retries until BattleMyTurn or BattleActing is seen. Applied to both `BattleAbility` and `BattleAttack` state gates.

- [x] **MoveGrid accepted any non-BattleMoving state as confirmed** (`3ba67d5`) — accepted BattleEnemiesTurn transient flickers as "move done," causing execute_turn to race the next sub-step. Fix: only BattleMyTurn or BattleActing count.

- [x] **battle_wait didn't recognize BattleActing as friendly-turn** (`07646ab`) — Haste-driven double turns or auto-acted allies land on BattleActing directly, bypassing the 300ms poll's BattleMyTurn window. Fix: accept both.

- [x] **battle_ability returned with stale BattleAttacking** (`85682fb`) — response.Screen read mid-animation before cast finalized, giving session logs a confused source→target. Fix: `WaitForTurnState(2000ms)` post-cast settle in all 4 battle_ability return paths (true-self, self-radius, cursor-already-on-target, navigated target).

- [x] **battle_wait stalled on facing-confirm F-key drops + mid-battle banners** (`24f0348`) — F key occasionally didn't advance the facing screen; BattleAbilityLearnedBanner/BattleRewardObtainedBanner screens appeared during enemy-turns wait and the poll loop didn't dismiss them. Fixes: Enter-fallback if F didn't advance; Enter-dismiss known banners in the poll loop.

- [x] **battle_attack re-targets after MISS** (`851a927`) — after a miss, the game re-opens the attack-targeting screen. Old code returned "completed" on BattleAttacking, the next battle_wait saw the re-target screen, fired F/Enter confirming a stray target, and polled 120s. Fix: post-animation check; if still on BattleAttacking, send Escape to back out to BattleActing.

- [x] **battle_wait polled 120s after Victory** (`6cc2e7d`) — kill_enemies on final turn → Victory → battle_wait polled for a friendly turn that would never come. Fix: exit poll on BattleVictory or BattleDesertion (post-loop Desertion auto-dismiss still runs).

**Stale items also archived from TODO hygiene pass (d8d1431):**

- [x] **battle_ability response says "Used" for cast-time abilities** — fixed long ago at `NavigationActions.cs:1167`.
- [x] **Abilities submenu remembers cursor position (for battle_attack)** — force-to-Attack reset at NavigationActions.cs:670-697.
- [x] **BattleVictory/BattleDesertion misdetect as BattlePaused (Orbonne slot0=0x67)** — `orbonneDesertion` variant rules + regression tests pinned.
- [x] **New state: BattleChoice** — detection wires `eventHasChoice` + `choiceModalFlag` at ScreenDetectionLogic.cs:344-378.
- [x] **Wire AbilityCursorDeltaPlanner.Decide** — wired at NavigationActions.cs:1127.
- [x] **Split KEY_DELAY into nav vs transition** — KeyDelayClassifier shipped.

---

### Session 29 (2026-04-17) — BFS tile cost rules + per-unit heap Move/Jump

Commits: `925de71` (pt.1 TDD ActiveUnitSummaryFormatter), `5091b61` (pt.2 wire activeUnitSummary into BattleMyTurn + shell + heldCount + Battle* branch widening), `320ec7a` (pt.3 TargetingLabelResolver fixes BattleAttacking ui=), `abc2b3c` (pt.4 richer MoveGrid timeout diagnostics), `49fef3b` (pt.5 TDD TileEdgeHeight), `cff6c99` (pt.6 integrate TileEdgeHeight into MovementBfs + stricter JobCursor liveness), `49fd756` (pt.7 TODO updates), `84530b5` (pt.8 unify scan_move BFS to shared MovementBfs function), `ad5acce` (pt.9 log root-cause finding for BFS miscounts), `e4ba516` (pt.10 ally pass-through + depth-based MoveCost + axis-continue), `3a57caa` (pt.11 read per-unit Move/Jump from heap struct), `8afd133` (pt.12 dry-run arg on open_\* helpers), `8354f12` (pt.13 JobCursor bidirectional liveness + EqA re-fire).

Tests: 2165 → 2188 (+23 new, 0 regressions).

**Features landed:**

- [x] **`screen` / `BattleMyTurn` response includes active unit name/job/HP** — pts.1+2. `ActiveUnitSummaryFormatter.Format(name, jobName, x, y, hp, maxHp) → "Wilham(Monk) (10,10) HP=477/477"`. `CacheLearnedAbilities` snapshots the active unit's identity from scan_move; `DetectScreen` emits `ActiveUnitSummary` on any `Battle*` state. fft.sh compact renderer widened from `Battle_*` to `Battle*`. Live-verified at Siedge Weald: `[BattleMyTurn] ui=Move Lloyd(Dragoon) (10,9) HP=628/628 MP=52/52`.
- [x] **`ui=` populated on BattleAttacking via manual Select nav** — pt.3. `TargetingLabelResolver.Resolve(lastAbilityName, selectedAbility, selectedItem)` precedence chain lets `BattleMenuTracker.SelectedAbility/SelectedItem` fill in when `_lastAbilityName` is null (manual-nav entry path). Live-verified: `[BattleAttack] ui=Attack` after `execute_action Abilities → Select`.
- [x] **Shell `heldCount` annotation on Items abilities** — pt.2 (bundled). `fmtAb` in fft.sh:~3122 prefixes ability names with `[xN]` (stock count) or `[OUT]` (unusable:true). Tests pass, shell compiles; live-verify deferred to next battle with an Items user.
- [x] **MoveGrid timeout diagnostics** — pt.4. Captures `lastScreenSeen` and poll count in the timeout error so the next false-negative repro has enough context to root-cause.
- [x] **TileEdgeHeight for canonical FFT slope rules** — pt.5. Pure function that returns a tile's edge height at a given cardinal direction. Flat = height everywhere; Incline raises one edge by slope_height; Convex/Concave raise two adjacent corner edges. 9 TDD cases.
- [x] **MovementBfs integrates TileEdgeHeight** — pt.6. BFS steps compare exit-edge of A vs entry-edge of B instead of averaged display heights. Existing 13 BFS tests still pass (flat maps compute identically); 2 new integration tests guard slope behaviour.
- [x] **Unify scan_move BFS with MovementBfs.ComputeValidTiles** — pt.8. Deleted the inline BFS in NavigationActions.cs (used averaged heights + occupied-tiles-block-all rule). Both scan_move and the Move-mode Populator now delegate to the shared pure function. Fixes apply everywhere.
- [x] **IC remaster BFS tile cost rules** — pt.10. Three rule changes live-verified to match the game exactly:
  - Ally pass-through costs the same as the underlying tile (no penalty, no discount). Ally tiles still can't be a final destination.
  - Depth-based MoveCost: any tile with `depth > 0` costs `1+depth` (not hardcoded-2 for Swamp).
  - Swamp axis-continue: continuing swamp→swamp along the SAME cardinal direction costs 1 (splash paid once per straight-line wade). Turning in swamp pays full 1+depth.
- [x] **🎯 Per-unit Move/Jump from heap unit struct** — pt.11. `NavigationActions.TryReadMoveJumpFromHeap(hp, maxHp)` searches the UE4 heap (range `0x4000000000..0x4200000000`) for the unit's HP+MaxHP u16 pair, computes struct base = match_addr - 0x10, reads Move at `+0x22` (u8) and Jump at `+0x23` (u8). Falls back to UIBuffer when heap search fails. Live-verified on Kenrick (Knight Mv=3 Jmp=3), Lloyd (Dragoon Mv=5 Jmp=4), Archer (enemy Mv=3 Jmp=3). New `dump_unit_struct` bridge action dumps 256 bytes of any unit's heap struct for future offset hunts.
- [x] **BFS matches game exactly across 4 live scenarios** — combined effect of pts.5+6+8+10+11. Live-verified:
  - Kenrick (9,9) Mv=3 Jmp=3 → 11 tiles ✓
  - Kenrick (8,5) Mv=3 Jmp=3 → 15 tiles ✓
  - Kenrick (8,2) Mv=3 Jmp=3 → 19 tiles ✓
  - Wilham  (10,11) Mv=3 Jmp=4 → 5 tiles ✓
- [x] **`dry-run` arg on open_\* helpers** — pt.12. `open_eqa Ramza dry-run` / `open_character_status Agrias dry-run` / `open_job_selection Cloud --dry-run` route to the `dry_run_nav` bridge action (shipped session 27) instead of firing. Prints the planned key sequence before committing to a live run — addresses the crashy chain-nav scenario from sessions 22/24. Live-verified: plan renders as `ESC(500ms, deep-tree escape 1/8) + ... + ENTER(300ms, open CharacterStatus) [total 5000ms]`.
- [x] **JobCursor resolver: bidirectional liveness probe** — pt.13. Extends the existing 3-Rights-expect-+3 probe with a second phase: 3 Lefts must return the byte to its pre-probe value. Change-count widgets (which increment on any nav) reach +6 total instead of returning to baseline, so they now fail phase 2. Current save still 0 candidates — awaits a save where a truly-live cursor byte exists.
- [x] **EqA row resolver re-fires on picker close** — pt.13. `_lastEqaMenuDepth` tracks the previous DetectScreen's MenuDepth while on EqA. When MenuDepth transitions from >2 (inside a picker / CharacterStatus side column) back to 2 (EqA main), the resolver re-fires — ~2s cost per picker close but prevents stale `ui=Right Hand (none)` after equipment edits.

**TODO cleanup (verified already-shipped from earlier sessions):**

- [x] **Wire dry-run nav harness into the real `NavigateToCharacterStatus`** — already done; live method delegates to `NavigationPlanner.PlanNavigateToCharacterStatus` at NavigationActions.cs:4528. Session 29 added the shell-side dry-run flag (see above).
- [x] **Orlandeau primary skillset label** — already done; `GetPrimarySkillsetByJobName` in CommandWatcher.cs:3693 maps `"Thunder God" => "Swordplay"`. Sub-skillset detection (Holy/Unyielding/Fell Sword per cursor) not implemented — defer until a use case surfaces.
- [x] **JP Next populate Mettle/Fundaments ability costs** — already done; `AbilityJpCosts.cs:29` has Focus/Rush/Throw Stone/Salve (Fundaments) and Tailwind/Chant/Steel/Shout/Ultima (Mettle). Values wiki-sourced, live-verify still pending.
- [x] **Apply mirror technique to JobSelection** — already done; `CommandWatcher.cs:6335-6361` snaps `ScreenMachine.CursorRow/Col` via `SetJobCursor` when `_resolvedJobCursorAddr` mem read disagrees. Blocked only by the JobCursor resolver not finding a live byte on this save.

**Memory notes saved this session:**

- `project_heap_unit_struct_movejump.md` — Move at heap struct +0x22 (u8), Jump at +0x23 (u8). Struct base = HP-pattern-match-addr - 0x10. Supersedes UIBuffer for per-unit effective stats.
- `project_bfs_tile_cost_rules.md` — 4 canonical BFS tile-cost rules (edge-height, ally pass-through, depth entry, swamp axis-continue) with fixture data for regression verification.
- `feedback_ui_buffer_stale_cursor.md` — UIBuffer at 0x1407AC7C0 is the CURSOR-hovered unit's BASE stats; don't use for active-unit effective stats.

### Session 28 (2026-04-17) — battle-task verification pass + move-tile memory hunt concluded

Commits: `9b9a627` (battle-task findings in TODO), `3d7ba31` (surface cursor x,y as ui= on BattleMoving), `f074719` (consolidate move-tile memory-read investigation), `b24f8b4` (BFS count warning — later disabled), `c0dcda3` (surface BFS mismatch in screen response + rich debug dump), `258b69a` (TDD pure functions: BfsTileVerifier, CursorFloodFill, ArrowKeyCalibration), `3f41b00` (disable BFS mismatch warning + ship experimental cursor_walk).

**Features landed:**

- [x] **BattleMoving `ui=(x,y)` cursor display** — commit `3d7ba31`. `PopulateBattleTileData` now writes `screen.UI = FormatCursor(CursorX, CursorY)` during BattleMoving. Pure `BattleCursorFormatter.FormatCursor(x, y) → "(x,y)"` extracted + 3 TDD tests. Live-verified: Ramza at (8,10) → `[BattleMoving] ui=(8,10)`, updates when cursor moves.
- [x] **Live-verify `SkillsetItemLookup` in a battle** — Session 19 wired, session 28 verified. Ramza (Gallant Knight / Items secondary) at Siedge Weald: `response.json` contains `heldCount` on every Items ability (Potion=4, Hi-Potion=1, X-Potion=94, Ether=99, Antidote=97, Echo Herbs=99, Gold Needle=99, Holy Water=99). `unusable:true` not observed because all stocks > 0. Follow-up (shell annotation) left as a new TODO.
- [x] **Live-verify `auto_place_units`** — session 23 shipped, session 28 verified. Triggered encounter at Siedge Weald, fired `auto_place_units` helper from BattleFormation — battle started cleanly, landed in BattleMyTurn. Memory note `feedback_auto_place_settle.md` added: after `auto_place_units` returns BattleMyTurn, enemies may still go first; wait ~30s before scanning.
- [x] **NavigationPlanner chain-nav TODO closed via planner wiring** (already in Session 27 archive but tested more extensively in 28).
- [x] **Pure function scaffolding for BFS verification** — commit `258b69a`. 3 new pure functions + 20 tests: `BfsTileVerifier.Compare(bfsList, gameList)` → `(agreements, falsePositives, falseNegatives)`; `CursorFloodFill.Flood(startX, startY, isValid)` → HashSet of visited valid tiles (4-direction cardinal, stops at invalid); `ArrowKeyCalibration.FromObservations(right,left,up,down).BuildPath(fromX,fromY,toX,toY)` → key sequence. Ready for cursor-walk flood-fill integration.
- [x] **Experimental `cursor_walk` bridge action** — commit `3f41b00`. Action wired in `CommandWatcher.RunCursorWalkDiagnostic`: calibrates arrow keys (auto-recalibrates when edge-blocked), flood-fills from unit start via cursor probe + snapshot-diff (`04→05` slot-flag transition count in `0x140DDF000..0x140DE8000`), compares result to `screen.Tiles` via `BfsTileVerifier`. Shell helper `cursor_walk` in `fft.sh`. **Experimental** — on Lloyd at Siedge Weald (20 visible blue tiles), the probe only catches 5 of 20. Probe reliability work in `§0 Urgent Bugs`.
- [x] **Move-tile memory-hunt concluded** — commits `f074719` (consolidation) and subsequent research documented in `memory/project_move_bitmap_hunt_s28.md`. Multiple deep dives proved: (a) no per-unit bitmap exists in memory (identical regions for Kenrick vs Lloyd); (b) slots in `0x140DE2xxx` are UE4 mesh-triangle records, not tile indices; (c) slot addresses recycle per cursor move (LRU pool). Conclusion: game computes valid tiles on-the-fly from static map topology + unit stats. Practical path = fix BFS directly against canonical PSX rules.

**Shipped but later DISABLED (kept in tree, commented-out):**

- [~] **BFS vs game-count mismatch warning** — commits `b24f8b4` (ship) and `c0dcda3` (surface in screen response with rich debug dump), then **DISABLED in `3f41b00`**. Built on belief that `0x142FEA008` encodes the active-unit valid-tile count. Live-verified FALSE: byte reads 0x0B (11) while user counts 20 actual blue tiles on screen for Lloyd at Siedge Weald. `LogBfsTileCountMismatch` call sites commented out. `MoveTileCountValidator` + `DetectedScreen.BfsMismatchWarning` + shell-side `⚠` rendering all still in place waiting for a real count signal.

**Session discoveries — memory notes saved:**

- `project_move_bitmap_hunt_s28.md` — Comprehensive account of session 28's move-tile memory hunt. Ruled-out signals, UE4 mesh structure, slot pool behavior, final conclusion that no persistent bitmap exists, corrections at the end about `0x142FEA008` not being the count byte.
- `feedback_auto_place_settle.md` — After `auto_place_units` returns BattleMyTurn, wait ~30s for enemy-first settle before scanning. Don't immediately call `scan_move`.
- `feedback_move_mode_read_crashes.md` — Rapid successive reads of `0x142FEA000+` or `0x140C6F000+` during Move mode crash the game. Limit to 1 `read_bytes` per BattleMoving session or use snapshot+diff (atomic copy).
- `project_bfs_manual_verify_workflow.md` — Per-map manual tile-probe workflow. Cursor + slot `+0x06` flag reads per tile. Arrow-key direction mapping varies with camera rotation.

### Session 27 (2026-04-17) — chain-nav fixed + HpMpCache + planner wiring + detailedStats

Commits: `b5890f4` (HpMpCache), `7af47c9` (SetJobCursor + resolver liveness), `0b495d8` (find_toggle + shop-item dead-end), `f3a3694` (NavigationPlanner + dry_run_nav), `e95038e` (remove chain-guard hard block), `32497e9` (Mettle/Fundaments JP tests), `d6f2264` (wire planner into live, fix chain-nav timing), `374c225` (detailedStats verbose payload), `2937da4` (new-recruit name-lookup TODO).

**Features landed:**

- [x] **`HpMpCache` persists 4-slot HP/MP across reads** — commit `b5890f4`. Disk-backed (`claude_bridge/hp_mp_cache.json`) keyed by (slotIndex, equipment signature). Observations from HoveredUnitArray get cached; falls through when live read returns null. Equipment change invalidates entry. Net: Ramza/Kenrick/Lloyd/Wilham HP/MP consistently visible in roster output (matches session 26 ceiling, now cached against flicker). Slots 4-13 remain unreachable — HoveredUnitArray is a fixed 4-slot slab anchored on Ramza's widget, not a roving hover window. 6 new unit tests (HpMpCacheTests). Memory note `project_hovered_unit_array_partial.md` updated with session 27 findings.
- [x] **`SetJobCursor` API + resolver liveness check** — commit `7af47c9`. New `SM.SetJobCursor(row, col)` bounds-checked setter for drift correction. `ResolveJobCursor` adds a post-resolve liveness probe (press Right, read byte, expect baseline+1) to filter widget-counter candidates that pass oscillation but don't track real navigation. Live-observed this session: all 32 candidates failed liveness on Ramza's save, saving SM from corruption by dead bytes. Resolver gate also fixed from `menuDepth==2` (never fired — JobSelection is menuDepth=0) to `KeysSinceLastSetScreen>=1` (animation-lag signal). Silent bug since feature first wired. CommandWatcher JobSelection read path now snaps SM via `SetJobCursor` when mem disagrees. 3 new SM tests. Memory note `project_jobcursor_liveness.md`.
- [x] **`find_toggle` bridge action** — commit `0b495d8`. Exposes `MemoryExplorer.FindToggleCandidates` to shell/node queries as a reusable infra action. Given 3 snapshot labels (baseline/advanced/back) and an expected delta, returns heap addresses that went a→b→a. Reusable for future cursor-byte hunts without baking a dedicated resolver. Allowlist test updated.
- [x] **Shop item-ID hunt — dead-end documented** — commit `0b495d8`. Row cursor at `0x141870704` confirmed working (0/1/2 u32 as scroll). But 3-way snapshot-diff + delta=1 returned ZERO candidates for a hovered-item-ID byte. Master item pool at `0x5D9B52C0` reads all zeros this session (lazy-loaded or drifted). UTF-16 name/description search finds only the static name pool (1 match), no heap copy of the hovered name. Contiguous u8/u16/u32 ID sequence search: 0 matches. Row→item mapping is blocked pending widget vtable walk, save-file decode, or mod-side render-callback hook. Memory note `project_shop_itemid_deadend.md` lists approaches tried so next session doesn't redo them.
- [x] **`NavigationPlanner` + `dry_run_nav` bridge action** — commit `f3a3694`. Pure function `PlanNavigateToCharacterStatus(currentScreen, displayOrder, rosterCount)` returns a typed list of key steps with per-step settle time, rationale, and optional `EarlyExitOnScreen`/`GroupId` hints for the escape-storm detection-poll. `dry_run_nav` action executes the planner without firing any keys, logs the plan to `SessionCommandLog` + stderr. Addresses session 24 footgun: "two prior attempts crashed the game" on chain-nav testing — now the plan is observable before committing to a live run. 10 new unit tests lock in the key sequence and settle times.
- [x] **Chain-guard hard block REMOVED** — commit `e95038e`. Five prior attempts to block chained shell calls all produced collateral false-positives (piped helpers killed, debugging sequences blocked). Live-tested this session: chained Bash calls work reliably — the single-threaded bridge sequences game-affecting commands, and bridge-side auto-delay handles the narrow race case. `_fft_guard` and `_fft_reset_guard` neutered to no-ops; the disk `fft_done.flag` path + `kill -9 $$` deleted. Counter + `[CHAIN INFO]` telemetry retained. Rebranded `[CHAIN WARN]` → `[CHAIN INFO]` to match non-blocking role. Live-verified: 3 chained `screen` reads, piped + second call, 2 chained `esc` — all succeed.
- [x] **Mettle/Fundaments JP Next regression tests** — commit `32497e9`. Costs were already populated in `AbilityJpCosts.CostByName` (commit `c5bfb01`); this session adds 5 explicit regression tests so a future edit can't silently un-populate them. Tests: Fundaments nothing-learned → Rush=80; Fundaments Rush-learned → Throw Stone=90; Mettle nothing-learned → Rush=80; Mettle cheap-four-learned → Tailwind=150; guard that every Mettle-exclusive ability has a populated cost.
- [x] **NavigationPlanner wired into live `NavigateToCharacterStatus` + chain-nav timing fix** — commit `d6f2264`. Live method now consumes the planner as single source of truth. Escape-storm settle bumped 300→500ms, final open-PartyMenu escape 500→700ms. Root cause of session 24 crash: at 300ms, SM's TravelList→WorldMap override fires mid-transition (SM predicts WorldMap via key-count BEFORE game finishes rendering the exit), causing 2-read confirm to agree falsely. Manual stepping with ~500ms works. Live-verified across 5 chain hops: WorldMap→Ramza EqA, Ramza→Kenrick, Kenrick→Agrias, Agrias→Cloud, Cloud→Agrias — including the exact session 24 crash repro. Memory note `feedback_chain_nav_timing.md`.
- [x] **`detailedStats` verbose payload on CharacterStatus** — commit `374c225`. New `UnitStatsAggregate` record (camelCase JSON: hpBonus/mpBonus/weaponPower/weaponRange/weaponEvade/weaponElement/leftHandPower/shieldPhysicalEvade/shieldMagicEvade) derived from roster equipment IDs + ItemData constants. Wiki-independent (PSX vs IC concerns don't apply to ItemData's bundled values). Left-hand slot auto-routes to shield fields when the item is a shield (no WP). Live-verified: Ramza hpBonus=350 (Grand Helm 150 + Maximillian 200), Agrias shieldPE=75 + shieldME=50 (Escutcheon in left hand), Mustadio all-zero (unequipped). 5 new unit tests (UnitStatsAggregatorTests). Move/Jump/Speed/PA/MA intentionally NOT included — they need the full FFT per-job formula path where wiki-PSX values may differ from IC remaster (memory note `feedback_wiki_psx_vs_ic.md`).
- [x] **New-recruit name-lookup blocker recorded** — commit `2937da4`. User recruited Crestian (Lv1 Squire, JP 137) to live-test JP Next. `NameTableLookup` resolves her slot-4 name bytes to "Reis" (collides with existing Lv92 Dragonkin Reis). Two entangled TODOs added at top of §0 with the verification path for when the name lookup is fixed. Infrastructure (JP Next + costs) is correct per unit tests; the live-verify needed to wait.

**Session discoveries — memory notes saved:**

- `feedback_wiki_psx_vs_ic.md` — Wiki values are PSX-sourced; prefer memory reads over wiki-formula re-implementations.
- `project_jobcursor_liveness.md` — JobSelection cursor resolver needs liveness check; oscillation+axis-verify alone accepts widget counters that don't track real nav.
- `project_shop_itemid_deadend.md` — Shop item-ID byte unfindable via snapshot diff or AoB; needs widget vtable walk or save-file decode.
- `project_zodiac_not_in_slot.md` — Zodiac byte is NOT in the 0x258 roster slot (any encoding). Heap widget or save-file only. 4-anchor cross-ref confirms.
- `feedback_chain_nav_timing.md` — Chain-nav escape storm needs ≥500ms per escape + 700ms for final open. Below that, SM predicts state transitions before game renders them.
- Updated `project_hovered_unit_array_partial.md` with session 27 findings on the fixed-slab behavior.

**Investigated / not-shipped:**

- **Zodiac byte in roster slot** — ruled out. 9 encodings × 4 known-zodiac anchors returned zero matches. Next attempts documented: PartyMenu sort cycle diff, reverse from battle damage math, HoveredUnitArray struct dump beyond +0x37.
- **Shop item-ID in snapshot-reachable memory** — ruled out. `TakeHeapSnapshot` filters to <4MB PAGE_READWRITE private/mapped; shop widget data lives outside this. `find_toggle` action shipped as the reusable infra when future work finds a different path.
- **JobCursor drift correction firing** — infrastructure ready, liveness check shipped, but no live-tracking byte exists on this save (32 candidates all rejected). Memory note documents next-attempt approaches.

### Session 26 (2026-04-17) — save action + detection cleanup + Tavern + HP/MP

Commits: `473ac53` (session cmd log + KEY_DELAY split + TitleScreen tightening), `d1e7160` (Tavern + TavernRumors + TavernErrands state tracking), `7ac9f22` (HP/MP partial), `134da68` (save action + detection reorder + return_to_world_map guard).

**Features landed:**

- [x] **`SessionCommandLog` per-session JSONL observability** — commit `473ac53`. New `SessionCommandLog` class writes `claude_bridge/session_<stamp>.jsonl`; one line per command with id/timestamp/action/source→target/status/error/latencyMs. Never throws (observability must not take down command processing). Wired into `CommandWatcher` main + error paths. 7 unit tests.
- [x] **Per-key-type KEY_DELAY split** — commit `473ac53`. New `KeyDelayClassifier.DelayMsFor(vk)` returns 200ms for cursor keys (Up/Down/Left/Right), 350ms for transition/tab-cycle/unknown. `NavigationActions.SendKey` uses the classifier. Shaves 1-2s off nav-heavy flows (open_eqa party grid nav). 9 unit tests.
- [x] **`TitleScreen` rule tightened** — commit `473ac53`. Removed the loose `rawLocation==255 → TitleScreen` fallback at the end of the out-of-battle branch. Residuals now return "Unknown" so callers see ambiguity instead of being mislabeled as TitleScreen after GameOver/post-battle stale. Added 2 coverage tests.
- [x] **`Tavern` + `TavernRumors` + `TavernErrands` SM tracking** — commit `d1e7160`. Live-scanned at Sal Ghidos: all three are byte-identical to LocationMenu in detection inputs. SM-based solution: `GameScreen.Tavern/TavernRumors/TavernErrands` enum values, `SM.TavernCursor` (0=Rumors, 1=Errands) with wrap on Up/Down, `HandleTavern`/`HandleTavernSubScreen` for Enter/Escape. `ResolveAmbiguousScreen` extended to map (SM=Tavern sub, detected=LocationMenu) → SM name. `screen` query path now syncs SM to detection for non-party-tree transitions so the SM doesn't stay stuck at WorldMap. NavigationPaths for both sub-states (Select deliberately omitted from Errands). 9 new SM tests + 3 detection tests. Live-verified full round-trip.
- [x] **HP/MaxHp/Mp/MaxMp in roster** (partial `[~]`) — commit `7ac9f22`. Wired `HoveredUnitArray.ReadStatsIfMatches` into the roster assembly; first 4 units populate correctly (Ramza Lv99 HP 719/719 MP 138/138 verified). Other 10 units return null because the "hovered-unit array" is per-hover, not roster-wide. Full roster needs formula-based recompute (TODO §10.6).
- [x] **Detection rule reorder** — commit `134da68`. `party==1 / encounterFlag / eventId` authoritative rules now run BEFORE hover/location heuristics. Prevents "opened PartyMenu while hovering a map location" from misdetecting as WorldMap, and EncounterDialog from losing to hover-based rules. 2 new coverage tests.
- [x] **`eventId` filter verification** — commit `134da68`. Confirmed the out-of-battle rules already use `< 400 && != 0xFFFF` (the in-battle `< 200` is intentional — eventId address aliases as active-unit nameId during combat). 2 new tests covering Orbonne eventId=302.
- [x] **`save` C# action implemented** — commit `134da68`. Drives from any non-battle state to SaveSlotPicker via: escape-to-WorldMap (2-consecutive-read confirm for the Escape-on-WorldMap toggle gotcha) → open PartyMenu → Q-cycle to Options tab (tab-count derived from SM) → Up×5 → Enter. State-guarded against Battle* / Encounter / Cutscene / GameOver. Live-verified from WorldMap, PartyMenuOptions, and 3-level-nested EqA.
- [x] **`return_to_world_map` battle-state guard** — commit `134da68`. Helper refuses from 13 unsafe states (Battle* / Encounter / Cutscene / GameOver / Formation / Sequence / EnemiesTurn / AlliesTurn) with clear pointers to `battle_flee` or `execute_action ReturnToWorldMap`. Closes the footgun where Escape on BattlePaused would resume the battle.
- [x] **Fix Battle_Dialogue / Cutscene eventId filter** — commit `134da68`. Already fixed in prior sessions; session 26 added 2 coverage tests for Orbonne eventId=302 to prove the behavior holds.
- [x] **Add hover to ScreenDetectionLogic inputs** — closed stale. `hover` has been a parameter of `Detect` for a while (line 65); TODO entry was stale.

### Session 25 (2026-04-17) — SaveSlotPicker + chain-guard pipe-subshell fix

Commit: `c847d42`.

**Features landed:**

- [x] **Track SaveSlotPicker state (PartyMenuOptions → Save)** — commit `c847d42`. New `GameScreen.SaveSlotPicker`, `HandlePartyMenu` Enter-on-OptionsIndex-0 transition, Escape back to PartyMenuOptions. Detection override: SaveSlotPicker and TravelList are byte-identical across all 28 detection inputs (memory hunt documented in `memory/project_saveslotpicker_vs_travellist.md`). `ScreenDetectionLogic.ResolveAmbiguousScreen(SM, detected)` maps (SM=SaveSlotPicker, detected=TravelList) → "SaveSlotPicker". Wired into 3 CommandWatcher paths (screen query, key-sending fallback, execute_action path lookup). ScrollUp/Down/Select/Cancel ValidPaths via `SaveGame_Menu` alias. 7 new tests.
- [x] **Chain guard pipe-subshell fix** — commit `c847d42`. `_FFT_DONE` shell var was bypassed when helpers were piped (`screen | tail`) because the function ran in the pipe's subshell. Added disk flag `claude_bridge/fft_done.flag` that survives subshells, cleared at `source` time. 34 composite-helper reset sites rewritten to call new `_fft_reset_guard` helper. Live-verified: piped-first-call + second-call blocks correctly with `[NO]` + exit 137.
- [x] **`fft_resync` forbidden-state guard** — commit `c847d42`. Helper refuses from Battle*/EncounterDialog/Cutscene/BattleSequence/BattleFormation/GameOver because its escape storm mis-handles those transitions. Block-list (not allow-list) so new non-battle screens are automatically safe.

### Session 24 (2026-04-16 / 2026-04-17) — TODO cleanup sweep + ModStateFlags + Ctrl focus-leak

Commits: `91fa2cb` (rename + guards + UI backfill), `5cf018a` (shell UX + chain guard), `c5bfb01` (labels + JP + auto-resolvers + docs), `9a4acf9` (ModStateFlags + AbilitiesAndJobs.md + fft_resync + Ctrl focus-leak fix).

**Core features landed:**

- [x] **Rename `PartyMenu` → `PartyMenuUnits`** — commit `91fa2cb`. Enum member, string literals, shell whitelists, tests, Instructions docs. Sibling tabs (Inventory/Chronicle/Options) unchanged. Live-verified.
- [x] **`world_travel_to status=rejected` surfaces reason** — commit `91fa2cb`. `fft.sh` renderer only surfaced errors on `failed`; added `[REJECTED] <reason>` branch. Live-verified.
- [x] **Construct 8 locked ability slots** — commit `91fa2cb`. `JobGridLayout.LockedAbilityUnits` set + shell `_change_ability` guard. 2 new unit tests. Live-verified on all four slots.
- [x] **WorldMap `ui=` shows hovered location name** — commit `91fa2cb`. `GetLocationName(hover)` when `hover < 255`. Live-verified.
- [x] **`ui=<element>` backfilled** — commit `91fa2cb`. Cutscene/CharacterDialog → "Advance", CombatSets → "Combat Sets". Remaining holdouts (TravelList, TitleScreen) intentional.
- [x] **EnterLocation delay per-location tuning** — commit `91fa2cb`. Live-verified at Dorter, Gariland, Lesalia with 500ms default. No per-location tuning needed.
- [x] **EqA compact format single-line** — commit `5cf018a`. Already single-line (`Equip:` / `Abilities:` rows); verbose keeps the grid. Ticked after live verification.
- [x] **JobSelection unlock-requirements text** — commit `5cf018a`. Already fully wired via `JobGridLayout.JobPrereqs` + `GetUnlockRequirements`. Ticked after live verification (Bard on Ramza → `requires=Summoner Lv. 5, Orator Lv. 5 (male only)`).
- [x] **`unequip_all` per-slot progress + runtime docs** — commit `5cf018a`. Header comment documents ~25-30s runtime + ≥35s Bash-timeout recommendation; per-slot progress `N/5 <label>: <item> → removing...`. Live-verified on Cloud (5 empty slots).
- [x] **`remove_equipment` position-agnostic entry** — commit `5cf018a`. Reads `cursorCol`; col 0 proceeds, col 1 auto-Lefts, else refuses. Live-verified.
- [x] **Block improper chained `fft` calls** — commit `5cf018a`. `_is_key_sending` classifier + `_track_key_call` counter; second key-sending call without `_FFT_ALLOW_CHAIN=1` triggers `[CHAIN WARN]` to stderr. Composites annotated. Live-verified.
- [x] **`return_to_world_map` from PartyMenuInventory/Chronicle/Options** — commit `5cf018a`. Live-verified from all three non-Units tabs.
- [x] **Orlandeau primary skillset label** — commit `c5bfb01`. "Thunder God" → "Swordplay" (was "Holy Sword"). Live-verified.
- [x] **JP Next: Mettle costs populated** — commit `c5bfb01`. Tailwind 150, Chant 300, Steel 200, Shout 600, Ultima 4000. Wiki-sourced; in-game verification still pending but values surface correctly.
- [x] **EqA `ui=` auto-resolver on entry** — commit `c5bfb01`. `_eqaRowAutoResolveAttempted` latch fires `DoEqaRowResolve` once per EqA entry. Clears on EqA exit. Live-verified: `[CommandBridge] auto EqA row: 0 (unequip 36 → 0)`.
- [x] **Mime hardcoded-Locked → skillset-union proxy** — commit `c5bfb01`. Checks viewed unit + party for Summon/Speechcraft/Geomancy/Jump. Live-verified on Orlandeau: `state=Visible requires=Squire Lv. 8...`. 3 new unit tests.
- [x] **Story scene handling docs** — commit `c5bfb01`. `Rules.md` "Story Scenes" section + existing `CutsceneDialogue.md`. Covers both the TODO "Story scene handling" behavior item and the "StoryScenes.md" doc item.
- [x] **AbilitiesAndJobs.md** — commit `9a4acf9`. New 95-line `Instructions/AbilitiesAndJobs.md`. Ability slots, JP economy, unlock tree, state fields, cross-class reasoning, story-character locks, gotchas, command mapping.
- [x] **ModStateFlags helper class** — commit `9a4acf9`. Disk-backed named-flag store in `claude_bridge/mod_state.json`. Bridge actions `get_flag`/`set_flag`/`list_flags` + shell helpers. 8 unit tests. Live-verified set → disk → get round-trip.
- [x] **Ctrl fast-forward focus-leak fix** — commit `9a4acf9`. Root cause: `SendInputKeyDown` called `SetForegroundWindow` + global `SendInput` every tick, hijacking user's terminal Ctrl state when tabbed away. Fix: `IsGameForeground()` + `ctrlHeldGlobally` state; global Ctrl released when focus leaves game, re-asserted when it returns. PostMessage path keeps DirectInput signal alive. Live-verified by user: "It works. It interrupted me but it didn't continue to hold ctrl."
- [x] **`fft_resync` state-reset helper** — commit `9a4acf9`. Shell helper + C# `reset_state_machine` bridge action. Escapes to WorldMap with 2-consecutive-confirm, then clears SM + every auto-resolve latch. ~5s vs `restart`'s ~45s; preserves mod memory. Live-verified.

**Investigated / not-shipped:**

- Chronicle/Options tab discriminator — NO stable byte. `0x140900824` looked promising (9/6 within session) but failed restart test (becomes a nav-history counter). Notes added to TODO entry for next attempt.
- JobSelection mirror-technique byte hunt — NO stable byte. Module-memory 0→1 ∩ 2→3 intersect empty. Real gap is plumbing existing `resolve_job_cursor` output into SM correction. Notes added.
- Mod-owned memory state flags (user's idea) — VIABLE for within-session ephemeral state, infeasible for save-aware or cross-restart use. Full viability report in session log. Led to building ModStateFlags with disk backing.



- [x] **Stale `unitsTabFlag`/`inventoryTabFlag` after shop exit** — FIXED commit `5dcd234`. `0x140D3A41E` was latching at 1 on WorldMap after shop exit, causing detection to return "PartyMenu" instead of "WorldMap". Added `menuDepth` parameter to `ScreenDetectionLogic.Detect`; rule now skipped when `menuDepth==0` (outer screen confirmed).

- [x] **SM-Drift racing animation lag on party-tree transitions** — FIXED commit `e634a35`. The drift check fired during the 50-200ms CharacterStatus → EqA animation when menuDepth still reads 0, snapping the correct SM back to WorldMap. Now gated on `smJustTransitioned` (SM.CurrentScreen != _smScreenBeforeKeys); SM rides out its own animation window.

- [x] **`DelayBetweenMs` not propagated from PathEntry to CommandRequest** — FIXED commit `588af0a`. `ExecuteValidPath` was converting PathEntry keys but never copying `path.DelayBetweenMs`. Every validpath with custom timing (ReturnToWorldMap 800ms, OpenChronicle 500ms, etc.) was running at the bridge default. One-line fix at `CommandWatcher.cs:2195`. Verified ReturnToWorldMap from JobSelection now lands on WorldMap with 852ms between Escapes.

- [x] **EqA-promote heuristic stomping non-EqA party-tree screens** — FIXED commit `f356fb3`. The equipment-mirror promote heuristic was firing on PartyMenuInventory/Chronicle/Options because the mirror stays populated (same roster visible). Extended `detectionSaysWorldSide` guard to include all non-EqA party-tree screens.

- [x] **World-map drift snap stomping legit party-tree screens** — FIXED commit `e9eef6f`. JobSelection/PartyMenuChronicle/PartyMenuOptions all read `menuDepth=0` legitimately. Drift detector was snapping SM back to WorldMap whenever it saw 3 reads of (SM in tree + raw=TravelList + menuDepth=0). Exempted via `smOnNonUnitsPartyTab` and `smOnJobSelection`.

- [x] **Helpers for all detectable states** — DONE commit `aef8514`. `_show_helpers` cases added for CharacterDialog, BattleMoving/Attacking/Casting/Abilities, BattleAlliesTurn/EnemiesTurn/Waiting, shop interiors (Outfitter/Tavern/WarriorsGuild/PoachersDen/SaveGame). Every state detection can produce now has both ValidPaths and helpers.

- [x] **NavigationActions.SendKey wired to SM.OnKeyPressed** — DONE commit `82ccb65`. Compound nav helpers (open_eqa, open_character_status, etc.) were driving the game without notifying the state machine. Added `NavigationActions.ScreenMachine` property + `OnKeyPressed(vk)` call inside `SendKey`. `NavigateToCharacterStatus` rewritten to fail-fast on bad unit name, drop wrap-to-(0,0) for fresh PartyMenu open, sync SM.RosterCount/GridRows before nav.

- [x] **NavigationActions KEY_DELAY 150ms → 350ms + 1s post-Escape** — DONE commit `4fd29ae`. 150ms was too fast — Down keys dropped during PartyMenu open animation. 350ms matches manual-test pace.

- [x] **State-validation guards on all helpers** — DONE commit `b08bb04`. Every helper now validates `_current_screen` against an allowed-state regex BEFORE firing keys. `_require_state` helper prints "[helper] ERROR: cannot run from <state>. Allowed states: ..." and returns 1 on rejection. Verified live: `change_job_to Knight` from Outfitter rejected without firing keys.

- [x] **`return_to_world_map` helper** — DONE commit `ca00160`. Universal escape, iterates Escape with detection until WorldMap. Up to 8 attempts. Wired into helpers list for PartyMenu, CharacterStatus, JobSelection, EquipmentAndAbilities. Live-verified from EqA.

- [x] **`view_unit <name>` helper** — DONE commit `ca00160`. Read-only roster dump (name/job/lv/jp/brave/faith/zodiac/equip). No nav, no key presses. Works on any screen exposing roster data.

- [x] **`unequip_all <unit>` helper** — DONE commit `ca00160`. Strips all 5 equipment slots, skips already-empty, reports counts. Live-verified on Cloud — all 5 slots stripped successfully.

- [x] **`travel_safe <id>` helper** — DONE commit `ca00160`. world_travel_to with auto-flee on encounters. Polls 10s, auto-Flees up to 5 times. Live-verified Dorter → TheSiedgeWeald.

- [x] **`scan_inventory` helper** — DONE commit `ca00160`. Opens PartyMenuInventory in verbose mode, dumps full inventory grouped by category. Live-verified — 210 unique items, 2305 owned across 33 categories.

- [x] **`save_game` / `load_game` stubs** — DONE commit `ca00160`. Helper functions added with clear "NOT IMPLEMENTED" message. Underlying C# `save`/`load` actions need real implementations (currently return "Not implemented yet").

- [x] **`start_encounter` helper** — DONE earlier session 23. Validates battleground (IDs 24-42), C+Enter to trigger, 2s wait for animation, Enter to accept Fight. Lands on BattleFormation. Live-verified.

- [x] **`battle_flee` validation** — DONE earlier session 23. Rejects from BattleFormation/EncounterDialog/WorldMap/PartyMenu with friendly message "Can't battle_flee from X. Start the battle first and try again."

- [x] **`fft()` error surfacing** — DONE earlier session 23. Failed commands now print `[FAILED] <error message>` instead of silently returning 0. Unescapes `\u0027` → `'` for readable apostrophes.

- [x] **Compact format: `loc=` → `curLoc=`, gil only on shop screens, WorldMap ui= suppressed** — DONE earlier session 23 (commit `d0e9bc6`). Cleaner one-liner, less noise.

- [x] **JobSelection compact: state/cursor/requires** — DONE commit `d0e9bc6`. `state=Unlocked`, `cursor=(r0,c2)`, `requires=...` for Visible cells.

- [x] **JobSelection verbose: 3-row job grid** — DONE commit `d0e9bc6`. Renders the static layout with `cursor->` row marker and `[ClassName]` cell brackets.

- [x] **EncounterDialog `ui=Fight`** — DONE commit `d0e9bc6`. Hardcoded since cursor always defaults to Fight on encounter prompts.

- [x] **Cutscene `eventId=N`** — DONE commit `d0e9bc6`. Surfaces eventId in compact line.

- [x] **`execute_action` guard via `_fft_guard`** — DONE earlier session 23 (commit `d0e9bc6`). Chained execute_action calls now blocked instead of firing into races.

### Session 19 (2026-04-15) — verification + queued tasks

- [x] **Suppress `ui=Move` outside battle** — VERIFIED already fixed. `CommandWatcher.cs:3328` sets `UI = null` at detector construction; `BattleMyTurn`/`BattleActing` block at 3405 is the only path that writes Move/Abilities/Wait/Status/AutoBattle labels. Non-battle screens show their own context labels (Ramza, Equipment & Abilities, etc.) or stay null. Live-verified 2026-04-15 on WorldMap, PartyMenu, CharacterStatus — no leak observed.

- [x] ⚠ UNVERIFIED — **JP Next on CharacterStatus header** — SHIPPED commit fe8d41e. `AbilityJpCosts.cs` (13 skillsets + blanket Geomancy/Bardsong/Dance) → `RosterReader.ComputeNextJp(slotIndex, primarySkillset)` → `screen.nextJp` → fft.sh header rendering as `Next N`. 10 unit tests. Live-verify deferred: save has no unit with partially-learned priced primary skillset (all Lv99 generics + story chars on Limit/Holy Sword/etc).

- [x] **Zodiac sign per unit (story chars)** — SHIPPED commit 1674bb6. `ZodiacData.cs` covers 13 story characters by nameId. Ramza and generics return null pending memory-hunt for the roster zodiac byte. Live-verified on Agrias (Cancer glyph ✓). 11 unit tests. Generic zodiac still TODO — hunted 0x00-0x100+ offsets with 4 anchor points (Agrias/Mustadio/Orlandeau/Cloud canonical values), no match found; encoding may be nibble-packed, outside the 0x258 stride, or non-standard.

- [x] ⚠ UNVERIFIED — **Wire `SkillsetItemLookup` into scan_move ability surfacing** — SHIPPED commit 0c25e29. `AbilityEntry` gained `HeldCount`/`Unusable` fields. `ScanMove` reads inventory bytes once per scan and probes Items/Iaido/Throw for each ability. Live-verify deferred: requires a battle with a Chemist/Ninja/Samurai active.

### Session 18 (2026-04-15) — verified via audit agents

- [x] ⚠ UNVERIFIED — **`change_reaction_ability_to <name>` helper** — shipped session 13.
- [x] ⚠ UNVERIFIED — **`change_support_ability_to <name>` helper** — shipped session 13.
- [x] ⚠ UNVERIFIED — **`change_movement_ability_to <name>` helper** — shipped session 13.
- [x] ⚠ UNVERIFIED — **`change_secondary_ability_to <skillsetName>` helper** — shipped session 13.
- [x] ⚠ UNVERIFIED — **`remove_ability <name>` helper** — shipped session 13. Unequip a passive by re-pressing Enter on the already-equipped entry.
- [x] **`change_job_to <jobName>` helper** — DONE 2026-04-15 commit c25f0f4. Routes through JobSelection → JobActionMenu → JobChangeConfirmation. Refuses on Locked/Visible cells. Live-verified Ramza Gallant Knight ↔ Chemist ↔ Monk.
- [x] ⚠ UNVERIFIED — **`JobActionMenu` ui= label** — DONE 2026-04-15. `ui=Learn Abilities` / `ui=Change Job` driven by `JobActionIndex`.
- [x] ⚠ UNVERIFIED — **`JobChangeConfirmation` ui= label** — DONE 2026-04-15. `ui=Confirm` / `ui=Cancel` driven by `JobChangeConfirmSelected`.
- [x] ⚠ UNVERIFIED — **JobSelection cursor row-cross desync** — FIXED session 15 commit 5fdefa6. Widget heap reallocates per row cross (`0x11EC34D3C` → `0x1370CF4A0` after single Down). `InvalidateJobCursorOnRowCross` clears resolved address on every Up/Down key while on JobSelection, forcing re-resolve. Horizontal movement doesn't trigger it.

- [x] ⚠ UNVERIFIED — **JobSelection cell state (Locked / Visible / Unlocked) three-state classification** — SHIPPED session 15 commit 129f279. `screen.jobCellState` populated via party-wide skillset union proxy; `ui=` reflects state; `change_job_to` refuses on Locked/Visible. (Follow-up leaf tasks — live verification, Mime prereq, unlock text, etc. — broken out into individual open items above.)

- [x] ⚠ UNVERIFIED — **Normalize screen state names to CamelCase (no underscores)** — DONE commit 3087140. All `Battle_*` and `Outfitter_*` state names drop underscores. `ScreenDetectionLogic.cs` returns `OutfitterSell`/`BattleAttacking`/etc. Zero underscored state names remain.

- [x] **Full sell inventory inline at Outfitter_Sell** — DONE session 18 (commits 9287e5e, 93b5579). `InventoryReader.ReadSellable()` + `ItemPrices.cs` + `CommandWatcher` populates `screen.inventory` on OutfitterSell with `{name, heldCount, sellPrice, type}`. Verified + estimated sell-price distinction (`sell=` vs `sell~`). Live-verified session 21: 146 sellable items, grouped by type, with counts and prices.

- [x] ⚠ UNVERIFIED — **Full equipment picker inline at Outfitter_Fitting** — DONE commit 93b5579. `screen.inventory` (ReadAll) surfaces on OutfitterFitting. Slot-type filter deferred (requires Fitting picker-depth state tracking).

- [x] ⚠ UNVERIFIED — **Read player inventory for Sell** — DONE commit 0438aca. Inventory store cracked at `0x1411A17C0` (272 bytes, flat u8 array indexed by FFTPatcher item ID). `InventoryReader.cs` + 10 unit tests.

- [x] ⚠ UNVERIFIED — **`ReturnToWorldMap` validPath on every PartyMenu-tree screen** — DONE commit 34b5927. `NavigationPaths.cs` has 15+ entries across PartyMenu, PartyMenuInventory, CharacterStatus, EquipmentAndAbilities, JobSelection, JobActionMenu, pickers, Chronicle, Options with graduated Escape counts (1–5).

- [x] ⚠ UNVERIFIED — **Delete `Battle_AutoBattle` rule** — DONE. `ScreenDetectionLogic.cs:16` — UI label on Battle_MyTurn handles cursor=4.

- [x] ⚠ UNVERIFIED — **Collapse `Battle_Casting` into `Battle_Attacking`** — DONE. `ScreenDetectionLogic.cs:17,289` — cast-time and instant collapse into BattleAttacking; ct>0 tracked client-side.

- [x] ⚠ UNVERIFIED — **Add `LoadGame` rule** — DONE. `ScreenDetectionLogic.cs:235-240` — `slot0==255 && paused==0 && battleMode==0 && !atNamedLocation → LoadGame`.

- [x] ⚠ UNVERIFIED — **Add `LocationMenu` rule** — DONE. `ScreenDetectionLogic.cs:172-178` — `atNamedLocation → LocationMenu`.

- [x] ⚠ UNVERIFIED — **Rename `clearlyOnWorldMap` to `atNamedLocation`** — DONE. `ScreenDetectionLogic.cs:39` — zero `clearlyOnWorldMap` references remain.

### Earlier sessions

- [x] **EquipmentAndAbilities Abilities column surfaces `ui=(none)` on slots with no equipped ability** — DONE 2026-04-15 session 17 (commit e8aaa9f). Slot-aware fallback now emits `Right Hand (none)` / `Left Hand (none)` / `Headware (none)` / `Combat Garb (none)` / `Accessory (none)` on the equipment column and `Primary (none — skillset table missing for this job)` / `Secondary (empty)` / `Reaction (empty)` / `Support (empty)` / `Movement (empty)` on the ability column. Also populated 17 story-class primaries in `GetPrimarySkillsetByJobName` so Cloud/Mustadio/Rapha/etc no longer hit the Primary fallback at all. Live-verified on Cloud (`ui=Limit`) and his empty Secondary row (`ui=Secondary (empty)`). — logged 2026-04-14 session 13. Repro: open Cloud's EquipmentAndAbilities (Cloud's Primary is blank because "Soldier" isn't in `GetPrimarySkillsetByJobName`), cursor Right into the Abilities column. `ui=(none)` surfaces — bare and uninformative. Better: (a) populate the missing story-class primaries (Soldier=Limit, Dragonkin=Dragon, Steel Giant=Work, Machinist=Snipe, Skyseer/Netherseer=Sky/Nether Mantra, Divine Knight=Unyielding Blade, Templar=Spellblade, Thunder God=All Swordskills, Sky Pirate=Sky Pirating, Game Hunter=Hunting — verify each in-game before adding). (b) Change the `(none)` fallback in `CommandWatcher` EquipmentAndAbilities ability-cursor branch to `Primary (none)` / `Secondary (none)` / `Reaction (empty)` / `Support (empty)` / `Movement (empty)` so the row intent is at least visible. (c) Consider: Primary row should never surface `(none)` anyway — it's job-locked, so we should always know the primary skillset name from the job; a blank means our job-name map is incomplete.



- [x] **Extend `ItemInfo` with attributeBonus / equipmentEffects / attackEffects / dualWield / twoHanded fields, then populate** — DONE 2026-04-15 session 17 (commits 0752b3f + latest). `ItemInfo` record grew 6 fields (`AttributeBonuses`, `EquipmentEffects`, `AttackEffects`, `CanDualWield`, `CanWieldTwoHanded`, `Element`). Populated ~30 hero items: Materia Blade, Defender, Save the Queen, Excalibur, Ragnarok, Chaos Blade, Aegis/Escutcheon+/Kaiser/Ice/Flame Shield, Gold Hairpin, Thief's Cap, Ribbon, Genji Glove/Armor, Bracer, Power Gauntlet, Magepower Glove, Rubber Suit, White/Black/Lordly Robe, Hermes/Battle/Germinas/Rubber/Red/Spiked Boots, Angel Ring, Cursed Ring, Reflect Ring, Chantage, Nu Khai Armband, Mirage Vest. Surfaced through `UiDetail` + fft.sh detail panel. 12 unit tests in `Tests/Utilities/BuildUiDetailTests.cs`. Still to-do: remaining ~200 items unpopulated (NXD bulk-port is a follow-up; hero-item coverage is the hot path). — added 2026-04-14. The game's item info panel has 3 pages (verified live in Outfitter Try-then-Buy on Ragnarok): page 1 = WP/evade/range (already in ItemInfo), page 2 = Attribute Bonuses (e.g. PA+1, MA+2), Equipment Effects (e.g. "Permanent Shell"), Standard Attack Effects (e.g. on-hit Petrify), page 3 = Weapon Type flags (Can Dual Wield / Can Wield Two-handed) and Eligible Jobs. Without these fields the `uiDetail` description is incomplete for many items and Claude can't tell that Ragnarok grants permanent Shell etc. Strategy: extend `ItemInfo` record with the new fields, populate the ~30 most-used hero items by hand from the FFHacktics wiki (Ragnarok, Excalibur, Chaos Blade, Maximillian, Crystal Mail, Bracer, Chantage, etc.), then bulk-populate the rest from the game's NXD item table in a follow-up. Skip Eligible Jobs for now (low value, lots of data). Surface the new fields in `UiDetail` and render in fft.sh below the existing stats line.



- [x] ⚠ UNVERIFIED — **`screen -v` doesn't include the new EquipmentAndAbilities/picker payloads** — DONE 2026-04-15 session 17. EqA detail panel renders new `ItemInfo` fields inline (Bonuses / Effects / On-hit / weapon-type flags / element). Pickers (Secondary/Reaction/Support/Movement) carry `Job` + `Description` per `AvailableAbility`, rendered as `- <name>  (<job>)  [equipped]` + wrapped description under `screen -v`. Compact stays single-line. — added 2026-04-14. Compact `screen` shows the three-column Equipment/Abilities/Detail layout + cursor marker on EquipmentAndAbilities, and the `Available skillsets/reactions/supports/movement (N):` list on pickers. Verbose mode (`screen -v`) currently only changes PartyMenu output (full roster grid). It should ALSO surface fuller detail when -v is set on EquipmentAndAbilities / pickers — e.g. show the full long-form description (we currently wrap at 40 chars in compact, could be 80+ in verbose), expand all picker entries with their stats (Job + Description preview per row), maybe show all three pages of the in-game item info panel (Attribute Bonuses, Equipment Effects, Standard Attack Effects, Eligible Jobs) once `ItemInfo` carries that data. Implementation: add `if (verbose)` branches in fft.sh's EquipmentAndAbilities and picker rendering blocks.



- [x] **Block `world_travel_to` to current location** — DONE 2026-04-15 session 17 (commit e8aaa9f). `NavigationActions.TravelTo` reads world location byte at 0x14077D208 before any keys fire; same-location returns `status=rejected`. Live-verified. — Calling world_travel_to with the location ID of the current standing node opens the travel list with the cursor on the current node, and the blind "press Enter to confirm" flow selects it. The game then gets stuck in an undefined state (travel modal opens, input routing goes to a subwindow, subsequent Enter presses are swallowed). Detect and refuse: if `locationId == currentLocationId` (where currentLocationId is the WorldMap cursor hover OR the last-arrived location), return `{status: "rejected", error: "Already at <name>. Use execute_action EnterLocation to enter the location menu."}`. 2026-04-14 — observed breaking the Dorter shop run.


- [x] **Pre-press `C` (or middle mouse button) to recenter cursor before `EnterLocation`** — DONE 2026-04-15 session 17 (commit e8aaa9f). `GetWorldMapPaths` emits `[C, Enter]` with `DelayBetweenMs = 200`. Live-verified (`keysProcessed=2`, lands on LocationMenu). — User discovered 2026-04-14: the game binds `C` / middle-mouse to "recenter WorldMap cursor on current node". This is the clean fix for the "Enter does nothing because cursor drifted" problem. Implementation: in the `EnterLocation` ValidPath handler (NavigationPaths.cs → GetWorldMapPaths), prepend a `C` key press before the Enter. Single key, deterministic, no memory-reading needed. This supersedes the "Block EnterLocation when cursor isn't on the current settlement" TODO below — just always recenter first and the edge case disappears.


- [x] **Locked/unrevealed locations** — DONE 2026-04-15 session 17 (commits 0752b3f + latest). `0x1411A10B0` is NOT a bitmask — it's **1 byte per location** (0x01 unlocked, 0x00 locked), verified live. Two features: (a) `TravelTo` refuses travel to locked locations with a clear error, (b) `screen.unlockedLocations` surfaces the full array on WorldMap/TravelList (bytes 0..52). Live-verified: 50 unlocked IDs in endgame save, location 35 correctly excluded. — Read unlock bitmask at 0x1411A10B0 and skip locked locations.


- [x] **Ability picker state machine desync: Enter equipped an ability but state machine incorrectly returned to EquipmentAndAbilities** — FIXED 2026-04-14 session 13. Root cause: `ScreenStateMachine.HandleAbilityPicker` treated both `VK_RETURN` and `VK_ESCAPE` as picker-close events, but in the real game `Enter` only equips (picker stays open, shows checkmark); only `Escape` actually closes the picker. Fix: removed Enter from the close-transitions in HandleAbilityPicker; the picker now stays open on Enter and consumers (fft.sh helpers, Claude navigation) must send Escape to close. Verified live by running `change_reaction_ability_to` helper which cleanly equips + Escape-closes back to EquipmentAndAbilities.



- [x] **JobSelection auto-resolver race during screen transitions** — FIXED 2026-04-15 session 15 via the MenuDepth==2 memory gate (commit 3d8638b). The auto-resolver now waits until `screen.MenuDepth` reads 2 (game-confirmed inner panel render) before firing its 6 Right/Left keys. State machine still flips to JobScreen synchronously on Enter, but the resolver no longer races the open animation. Live-verified: batched `Down → Enter → Down → Enter` from a clean PartyMenu lands cleanly on the target unit's JobSelection with no outer-cursor drift.



- [x] **State machine drifts from reality on PartyMenu entry** [Detection] — PARTIALLY FIXED 2026-04-14 session 13 via memory-backed drift recovery. Memory byte `0x14077CB67` (menuDepth) cleanly distinguishes outer party-menu-tree screens (WorldMap/PartyMenu/CharacterStatus = 0) from inner panels (EquipmentAndAbilities/ability picker = 2). CommandWatcher.DetectScreen now runs a debounced check — if the state machine thinks we're on an inner panel but menuDepth reads 0 for 3 consecutive reads, snaps back to CharacterStatus (with `MarkKeyProcessed()` to prevent cascade into the older PartyMenu stale-state recovery). Live-verified in session 13 — helpers that used to desync now self-correct. Still outstanding: the inner-panel mid-restart case (restart happens while player is already on EqA or a picker) isn't directly covered — the byte reads 2 correctly, but the state machine has no way to know WHICH inner panel (EqA vs which picker). Lower priority because the common case (restart on PartyMenu/CharacterStatus) is now fixed.



- [x] ~~**Block `EnterLocation` when WorldMap cursor isn't on the current settlement**~~ — Superseded 2026-04-14 by the `C`-key recenter fix above. Leaving the strikethrough for history: the original symptom was `EnterLocation` silently no-oping because the cursor had drifted off the node. Instead of refusing the action, we just recenter before pressing Enter. Keeping the TODO marked done so the fix implementation is tracked in one place.


- [x] **PartyManagement.md** — Written 2026-04-13.


- [x] **Shopping.md** — Written 2026-04-14. See `FFTHandsFree/Instructions/Shopping.md`.


- [x] **FormationScreen.md** — Written 2026-04-13.


- [x] **SaveLoad.md** — Written 2026-04-13.


- [x] ⚠ UNVERIFIED — LocationMenu detection (locationMenuFlag at 0x140D43481) — mapped 2026-04-14


- [x] ⚠ UNVERIFIED — LocationMenu shop type (Outfitter/Tavern/WarriorsGuild/PoachersDen) via shopTypeIndex at 0x140D435F0 — mapped 2026-04-14


- [x] ⚠ UNVERIFIED — ShopInterior detection (insideShopFlag at 0x141844DD0) — mapped 2026-04-14, partially reliable (doesn't always fire on a fresh process)


- [x] ⚠ UNVERIFIED — Outfitter sub-actions: Outfitter_Buy / Outfitter_Sell / Outfitter_Fitting via shopSubMenuIndex at 0x14184276C (values 1/4/6) — mapped 2026-04-14



- [x] ⚠ UNVERIFIED — **Rename `ShopInterior` → `SettlementMenu`** — DONE 2026-04-14. ❗ **But done WRONG.** The rename was applied to the WRONG layer — see follow-up below.


- [x] ⚠ UNVERIFIED — **Gil in state** [P0 quick win] — DONE 2026-04-14. Gil at 0x140D39CD0 surfaces on shop-adjacent screens (WorldMap, PartyMenu, LocationMenu, ShopInterior, Outfitter_Buy/Sell/Fitting) via ShopGilPolicy.


- [x] ⚠ UNVERIFIED — **Format gil with thousands separators** — DONE 2026-04-14. `_fmt_gil` helper in fft.sh renders via `printf "%'d"` under `LC_ALL=en_US.UTF-8`. JSON unchanged.


- [x] ⚠ UNVERIFIED — **Shop list cursor row index** — DONE 2026-04-14. `0x141870704` (u32) tracks the currently-highlighted row inside Outfitter_Buy/Sell/Fitting. Row 0 = top item, increments per ScrollDown. Persists across sub-action cycling. Found via 4-way module_snap diff at Dorter Outfitter (rows Oak→White→Serpent→Oak).


- [x] **Shopping.md instruction guide** — DONE 2026-04-14. Initial version covers detection and ValidPaths flow; will need revisions as action helpers (`buy_item`, etc.) land.



- [x] ⚠ UNVERIFIED — **`PartyMenu` top-level tabs** — DONE 2026-04-14. Uses `ScreenStateMachine.Tab` (driven by Q/E key history, now wraps both directions) to resolve detection to `PartyMenu` / `PartyMenuInventory` / `PartyMenuChronicle` / `PartyMenuOptions`. Memory scan for a tab-index byte was inconclusive — heap diff found 2029 candidates with the right 0/1/2/3 shape but none survived re-verification (UE4 widget heap reallocates per keypress). State-machine-driven detection is the working answer. Each tab has its OWN screen name (not just `PartyMenu ui=<tab>`) because the content differs entirely per tab:
  - `PartyMenu` — Units tab (the roster grid; covered below)
  - `PartyMenuInventory` — Inventory tab (item catalog; covered below)
  - `PartyMenuChronicle` — Chronicle tab (lore/events browser; covered below)
  - `PartyMenuOptions` — Options tab (save/load/settings; covered below)
  Shared ValidPaths across all four: `NextTab` (E wraps), `PrevTab` (Q wraps), `WorldMap` (Escape back out).


- [x] ⚠ UNVERIFIED — **Full roster grid on `PartyMenu` (Units tab)** — DONE 2026-04-14 (see "Data surfacing" entry below for delivery notes). 5-col grid with cursor marker, name/level/job/brave/faith. `navHints` block was NOT shipped and not needed — Claude reads cursor + grid and plans navigation directly. Roster capacity (`14/50`) shipped. HP/MP per unit still missing — separate item, deferred.


- [x] ⚠ UNVERIFIED — **`PartyMenuChronicle` tab** — DONE 2026-04-14. State machine tracks `ChronicleIndex` (0-9 flat) over the 3-4-3 grid (Encyclopedia/StateOfRealm/Events / Auracite/Reading/Collection/Errands / Stratagems/Lessons/AkademicReport). `screen.UI` surfaces tile name (`Encyclopedia`, `Auracite`, etc.). Verified row transitions live: Encyc→Auracite, SoR→Reading, Events→Collection, Errands→Akademic (last col wraps left), Akademic→Collection (up). Memory hunt for the cursor address failed (UE4 widget heap reallocates per keypress producing false positives — same wall as PartyMenuInventory — see `project_shop_stock_array.md`). Each tile opens its own sub-screen via Enter, surfaces as `ChronicleEncyclopedia`/`ChronicleStateOfRealm`/etc. Sub-screens currently model only the boundary (Escape back) — inner-state navigation (Encyclopedia tabs, scrollable lists, etc.) is deferred to §10.7 below.


- [x] ⚠ UNVERIFIED — **`PartyMenuOptions` tab** — DONE 2026-04-14. State machine tracks `OptionsIndex` (0-4 vertical, wraps both directions). `screen.UI` surfaces action name (`Save`, `Load`, `Settings`, `Return to Title`, `Exit Game`). Enter on Settings opens new `OptionsSettings` screen (boundary only). Save/Load/ReturnToTitle/ExitGame Enter actions don't open sub-screens via the state machine — those flows are handled by their own existing systems (`save`/`load` actions, title-screen/quit sequences not yet modelled).


- [x] ⚠ UNVERIFIED — **`CharacterStatus` sidebar** — DONE 2026-04-14. `screen.UI` populated from `ScreenStateMachine.SidebarIndex` (now wraps both directions). Reads "Equipment & Abilities" / "Job" / "Combat Sets". No memory scan needed — sidebar is purely keyboard-driven and the state machine tracks Up/Down reliably.


- [x] ⚠ UNVERIFIED — **Equipment Effects toggle (`R` key on `EquipmentAndAbilities`)** — DONE 2026-04-14. State machine tracks `EquipmentEffectsView` (toggled by `R`); CommandWatcher surfaces it as `equipmentEffectsView` boolean on the screen response. Resets when leaving the screen. Effects panel TEXT scrape (e.g. "Permanent Shell", "Immune Blindness") still TODO — needs a memory scan or widget hook. Sub-bullets below also done:
  - Default view: `ui=<highlighted item or ability name>` (current spec).
  - Effects view: new sub-state or a flag like `EquipmentAndAbilities ui=EquipmentEffects view=Effects`. The bottom-right hint reads `[R] Equipment Effects` in the default view and `[R] View Equipment` in the effects view — confirming it's a binary toggle on the same screen.
  - ValidPaths: add `ToggleEffectsView` action that wraps the `R` key. Detection: needs a memory scan for the view flag (binary). Scrape the effects panel text as its own payload field when the flag is on.


- [x] ⚠ UNVERIFIED — **Full stats panel toggle (`1` key on `CharacterStatus`)** — DONE 2026-04-14. State machine tracks `StatsExpanded` (toggled by `1`); CommandWatcher surfaces it as `statsExpanded` boolean. Resets when leaving CharacterStatus. The actual stat NUMBERS (Move/Jump/PA/MA/PE/ME/WP-R/WP-L/Parry/etc.) still TODO — needs a memory scan for each stat's address.
  - Model as a view flag on `CharacterStatus`: `statsExpanded: true/false`.
  - When expanded, surface the full stat block in the screen response (NOT just the cursor label). This supersedes the "Full stat panel on CharacterStatus" entry below — the data IS already rendered numbers, just needs scraping when the flag is on.
  - ValidPaths: add `ToggleStatsPanel` action that wraps the `1` key.
  - Detection: needs a memory scan for the stats-expanded flag (binary).


- [x] **Character dialog (spacebar on `CharacterStatus`)** — DONE 2026-04-14. New state `CharacterDialog` detects via state machine. Only Enter advances (Escape is a no-op on dialogs in this game). Detection live-verified.


- [x] **Dismiss Unit flow (hold B on `CharacterStatus`)** — DONE 2026-04-14. Added `hold_key <vk> <durationMs>` action in CommandWatcher and `dismiss_unit` shell helper in fft.sh. When VK_B is held ≥3s on CharacterStatus, the state machine transitions to DismissUnit. Cursor defaults to Back (safe). `ui=Back/Confirm` reflects the toggle. Live-verified on Kenrick. Action helper `dismiss_unit <name>` (find unit, navigate to status, hold B, confirm) still TODO — current `dismiss_unit` only fires the held key, doesn't navigate.


- [x] ⚠ UNVERIFIED — **Rename `Equipment_Screen` → `EquipmentAndAbilities`** — DONE 2026-04-14 (screen name only; `GameScreen.EquipmentScreen` enum still legacy, renamed in the ScreenDetectionLogic → CommandWatcher mapper). The `ui=<highlighted item name>` inner-cursor work (Ragnarok / Escutcheon / Mettle / etc.) is still TODO — requires decoding the game's cursor position inside the two-column panel.


- [x] **New states for ability slots** — DONE 2026-04-14. `SecondaryAbilities` / `ReactionAbilities` / `SupportAbilities` / `MovementAbilities` detected via state machine routing on EquipmentAndAbilities Enter (right column rows 1-4). Row 0 (Primary action) is intentionally a no-op — the primary skillset is job-locked. All 4 pickers live-verified. Inner `ui=<skillset/ability name>` cursor decoding still TODO — needs the picker's selected-row address. The slot details below are now historical context:
  - `SecondaryAbilities` — ui=<skillset name> (Items, Arts of War, Aim, Martial Arts, White Magicks, Black Magicks, Time Magicks, ...). Screenshot showed Mettle + Items + Arts of War + Aim + Martial Arts + White/Black/Time Magicks. Primary action (row 0) is job-locked — no picker opens; change job via JobSelection to change the primary skillset.
  - `ReactionAbilities` — ui=<ability name> (Parry, Counter, Auto-Potion, ...).
  - `SupportAbilities` — ui=<ability name> (Magick Defense Boost, Equip Heavy Armor, Concentration, ...).
  - `MovementAbilities` — ui=<ability name> (Movement +3, Move-Find Item, Teleport, ...).


- [x] **New states for equipment slots** — DONE 2026-04-14. `EquippableWeapons` / `EquippableShields` / `EquippableHeadware` / `EquippableCombatGarb` / `EquippableAccessories` detected via state machine routing on EquipmentAndAbilities Enter (left column rows 0-4). All 5 pickers live-verified. `EquipmentSlot` enum added to ScreenStateModels.cs; CommandWatcher uses `CurrentEquipmentSlot` (captured at Enter time) to surface the correct slot-specific screen name. Inner `ui=<item name>` decoding still TODO. Slot details below are historical context:
  - `EquippableWeapons` — ui=<weapon name> (Ragnarok, Materia Blade, Chaos Blade, Blood Sword, Excalibur, Save the Queen, ...). Columns show Equipped/Held counts.
  - `EquippableShields` — ui=<shield name> (Escutcheon, Aegis Shield, ...).
  - `EquippableHeadware` — ui=<helm name> (Grand Helm, Crystal Helm, ...).
  - `EquippableCombatGarb` — ui=<armor name> (Maximillian, Carabini Mail, ...).
  - `EquippableAccessories` — ui=<accessory name> (Bracers, Genji Gauntlet, Reflect Ring, ...).


- [x] ⚠ UNVERIFIED — **Rename `Job_Screen` → `JobSelection` + `ui=<job name>`** — DONE 2026-04-14 (rename) and 2026-04-15 (ui= inner-cursor work — commits 4a0b53e + 777c189). Heap rescan-on-entry resolver finds the cursor byte; `screen.UI` surfaces the hovered class with cell-state aware labels (`<name>`, `<name> (not unlocked)`, or `(locked)`). `screen.cursorRow`, `screen.cursorCol`, `screen.jobCellState`, `screen.viewedUnit` all populated. `GameScreen.JobScreen` enum name still legacy.


- [x] ⚠ UNVERIFIED — **`CombatSets` state** — DONE 2026-04-14 (boundary detection only). Pressing Enter on the third sidebar item now transitions to `CombatSets` in the state machine; Escape returns to CharacterStatus. Inner navigation NOT modeled — user explicitly opted to defer (loadouts feature not in use). Add Up/Down/Enter handlers when needed.



- [x] **Full roster grid on `PartyMenu` (Units tab)** — 2026-04-14 landed slot-indexed list with slot, name, level, job, brave, faith. Empty-slot rule verified = `unitIndex != 0xFF && level > 0`. **Display order solved 2026-04-14 (session 13)**: roster byte `+0x122` (1 byte per slot) holds each unit's 0-indexed grid position under the game's current Sort option (default: Time Recruited). Discovered by dumping all 14 active slots' first 600 bytes and scanning for a strictly-monotonic ranking — offset 290 (0x122) was a perfect `[0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13]`. Verified live per-slot: Ramza s0=0, Kenrick s1=1, ..., Mustadio s11=4 (displays 5th in grid, before Reis s6 which has DisplayOrder=12). **NOW DELIVERED:** sorted list matches visible grid, cursor `(row, col)` tracked by state machine, `ui=<hovered name>`, drill-in to any unit surfaces that unit's real loadout/abilities/stats. fft.sh compact mode renders a 5-col grid matching the game's layout with `cursor->` gutter; `screen -v` dumps raw JSON with `gridCols`, `gridRows`, `cursorRow`, `cursorCol`, `hoveredName`, and `displayOrder` per unit. See `RosterReader.DisplayOrder`, `RosterReader.GetSlotByDisplayOrder`, `ScreenStateMachine.ViewedGridIndex`.
  - **HP/MP still not in roster.** Scanned Ramza's full 0x258 bytes for his displayed HP (719 = 0x02CF) and MP (138 = 0x008A): zero matches. Theory: runtime-computed from job base + equipment bonuses, OR stored in a separate per-slot live stats table in the UE4 heap. Partial answer via the hovered-unit heap array (BATTLE_MEMORY_MAP.md §19) — populated for a handful of units only. Future work: recompute from FFTPatcher formulas OR widget pointer-chain walk. Separate item below.
  - **Custom sort modes (Job / Level) — not yet tested.** The `+0x122` byte is re-written by the game when the player changes the Sort option, so display order stays accurate under any sort — but `IsRamza` in the state machine still assumes grid-pos-0 === Ramza, which breaks under Level sort (multiple lv99 units tie; game picks one deterministically). If non-default sort becomes a goal, resolve `IsRamza` via slot identity instead of grid position. Documented inline in `ScreenStateMachine.HandlePartyMenu`.


- [x] ⚠ UNVERIFIED — **Viewed-unit identification on EquipmentAndAbilities** — DONE 2026-04-14 (session 13). Resolved purely from the state machine: cursor (row, col) on PartyMenu → grid index (row × 5 + col) → roster slot whose `+0x122` byte equals that grid index (see `ScreenStateMachine.ViewedGridIndex`, `RosterReader.GetSlotByDisplayOrder`). Zero heap scan, zero AoB — the display-order byte lives in the stable roster array at `0x1411A18D0`. Previous plans (a/b/c above in the history) are no longer needed. The hovered-unit heap array from BATTLE_MEMORY_MAP.md §19 is still useful IF we want runtime HP/MP (not stored in the roster), but it's now a separate concern.


- [x] **Element resistance grid** — DROPPED 2026-04-15 session 15. Niche planning data; Claude doesn't pick equipment reactively to elements between battles. Reconsider only if a build-optimization flow emerges.


- [x] **Equipped items with stat totals on `EquipmentAndAbilities`** — DROPPED 2026-04-15 session 15. Equipment stats are derivable from individual `ItemInfo` records; aggregating them server-side is convenience that doesn't change a decision. If Claude needs the total, it can compute from per-item data already surfaced.


- [x] **JP totals per job on `JobSelection`** — DROPPED 2026-04-15 session 15 per the "What Goes In Compact vs Verbose vs Nowhere" principle above. Claude doesn't need 19 JP values to make a job-change decision; hovering a cell already shows Lv + JP in-game (info panel). Reconsider only if a concrete decision flow emerges that needs the full grid in one round trip.


- [x] ⚠ UNVERIFIED — **Ability list with learned/unlearned inside picker screens** — DONE 2026-04-14. `screen.availableAbilities` surfaces the full learned list for SecondaryAbilities (unlocked skillsets), ReactionAbilities (19 for Ramza), SupportAbilities (23), MovementAbilities (12). SecondaryAbilities puts the equipped skillset first (matches game's default cursor); other pickers use canonical ID-sorted order with the equipped ability marked in place. Decoded via roster byte 2 of the per-job bitfield at +0x32+jobIdx*3+2 (MSB-first over each job's ID-sorted passive list — see `ABILITY_IDS.md` and `RosterReader.ReadLearnedPassives`). JP cost + "unlearned-but-could-be-learned" still TODO — requires a separate learnable-set, not just learned-set.



- [x] ⚠ UNVERIFIED — **`PartyMenu` tab switch actions** — `OpenInventory`, `OpenChronicle`, `OpenOptions`, `OpenUnits` all wired in `NavigationPaths.cs` across every PartyMenu tab (each tab gets a no-op entry for its own name and key sequences for the other three). Verified 2026-04-15 session 16 — stale TODO item; already landed alongside the Chronicle/Options work in session 13.


- [x] **`EquipmentAndAbilities` directional semantics** — DROPPED 2026-04-15 session 15. `FocusEquipmentColumn` / `FocusAbilitiesColumn` were going to wrap Left/Right with named aliases. Left/Right are unambiguous; the named version adds noise to validPaths without changing any decision.


- [x] ⚠ UNVERIFIED — **`JobSelection` validPaths (grid nav, Select, Back)** — DONE 2026-04-15 session 15 (already present in the JobScreen response).


- [x] ⚠ UNVERIFIED — Chronicle tab + sub-tile detection — done 2026-04-14, see §10.6 / §10.7



- [x] ⚠ UNVERIFIED — **Memory scan for shop-type discriminator** — DONE 2026-04-14. shopTypeIndex at 0x140D435F0 distinguishes Outfitter/Tavern/WarriorsGuild/PoachersDen at LocationMenu. Outfitter sub-actions (Buy/Sell/Fitting) further split by shopSubMenuIndex at 0x14184276C. Save Game and other shop sub-actions still TODO — see Section 10.


- [x] ⚠ UNVERIFIED — **scan_move disrupts targeting mode** [State] — Fixed 2026-04-13: removed Battle_Attacking and Battle_Casting from scan-safe screens.


- [x] **EncounterDialog detection: wire 0x140D87830 as encounter flag** — DONE session 21. Wired into ScreenAddresses[28], added `encounterFlag` param to `Detect()`, re-enabled both EncounterDialog rules. Cross-session verified: flag=10 during encounter, 0 after flee, no false triggers on WorldMap/PartyMenu/LocationMenu. 5 new tests (2048 total).


- [x] **PartyMenu tab desync: delay/confirm approaches** — SUPERSEDED by session 20 SM-first architecture. Party-tree key responses now use SM state directly, bypassing detection entirely. Multi-press tab jumps no longer cause stale reads.


- [x] **PartyMenu tab desync: find non-Units tab discriminator memory byte** — **Inventory flag DONE** session 20 commit 5da81c2: `0x140D3A38E` = 1 on Inventory. Chronicle/Options discriminator NOT FOUND — deferred. Session 20 also overhauled detection architecture (SM-first for party tree) which eliminated most tab-related drift.


- [x] **PartyMenu screen identity drift (a)** — FIXED session 20. SM-first architecture eliminates stale-byte reads for party-tree transitions. Tab flags (41E/38E) wired into detection as fallback. EqA mirror promotion guarded by tab flags + world-side detection + SM-in-party-tree check.

### Session 30 (2026-04-17) — battle correctness + memory hunts

Commits (single session commit planned after handoff): picker-cursor fixes, cast-time "Queued" vs "Used", facing byte + battle_wait arg, element affinity, DetectScreen Move-mode-off-grid fix, damage-preview hunt ruled out.

Tests: 2185 → 2253 (+68 new, 0 regressions).

**Features landed:**

- [x] **Cast-time abilities return "Queued" instead of "Used"** — `BattleAbilityNavigation.AbilityLocation` gained a `castSpeed` field populated from `ActionAbilityLookup` skillset tables. `NavigationActions.BattleAbility` uses `"Queued <name> ct=N"` for ct>0 spells. 7 new unit tests in `BattleAbilityNavigationTests.FindAbility_SurfacesCastSpeed`.

- [x] **`BattleAttack` sticky submenu cursor fix** — When the Abilities submenu was entered in a previous turn action (e.g. cursor left on Martial Arts), `battle_attack` blindly Enter-selected that skillset instead of Attack. Fix reads submenu ui= to determine current cursor, computes Up-count to reach Attack at index 0. Live-verified.

- [x] **`BattleAbility` wrong-ability picker fix** — Ability list cursor also stuck on previously-selected ability across turns, making `battle_ability "Aurablast"` select the wrong ability. Fix presses Up×(listSize+1) to wrap-reset to index 0 before Down×abilityIndex. Deterministic, ~0.5s overhead.

- [x] **🎯 Per-unit facing byte decoded** — Static battle array slot +0x35 (immediately after gridY at +0x34). Encoding: 0=South, 1=West, 2=North, 3=East. Live-verified across all 4 player units at Siedge Weald (Ramza S, Wilham S, Lloyd E, Kenrick W). New `FacingByteDecoder` pure module, 13 unit tests, `ScannedUnit.Facing` populated on every scan, `response.units[].facing` on payload. Memory note: `project_facing_byte_s30.md`.

- [x] **`battle_wait <N|S|E|W>` direction argument** — Optional direction arg overrides `FacingStrategy.ComputeOptimalFacing` auto-pick. Accepts abbreviated or full names case-insensitively. No arg → current auto-behavior. `NavigationActions.ParseFacingDirection` pure function with 15 tests. Shell helper updated.

- [x] **🎯 Element affinity bytes decoded** — Static battle array slots +0x5A..+0x5E covering 5 fields: Absorb / Cancel / Half / Weak / **Strengthen** (new — outgoing damage × 1.25). All 5 use the same 8-bit element layout: Fire=7, Lightning=6, Ice=5, Wind=4, Earth=3, Water=2, Holy=1, Dark=0. Live-verified 7 of 8 elements (Wind inferred from pattern) via Flame/Ice/Kaiser/Venetian shields + Gaia Gear + Chameleon Robe; Dark confirmed post-wire on Bonesnatch. New `ElementAffinityDecoder` pure module, 25 unit tests. `ScannedUnit.ElementAbsorb/Cancel/Half/Weak/Strengthen` populated on every scan. Response payload `elementAbsorb/elementNull/elementHalf/elementWeak/elementStrengthen` surfaced. Memory note: `project_element_affinity_s30.md`.

- [x] **DetectScreen misdetects BattleMoving as BattleAttacking when cursor off-grid** — Root cause not transient flicker: battleMode reads `0x01` STABLY when the Move-mode cursor sits outside the highlighted blue grid. Discriminator shipped: `battleMode==1 && menuCursor==0 && submenuFlag==1 → BattleMoving` (Move was selected from action menu; real targeting has menuCursor>=1). Fix in `ScreenDetectionLogic.Detect`. Regression test `DetectScreen_InBattle_MoveMode_CursorOnOffGridTile_BattleMode1_ShouldStayBattleMoving`. Live-verified: cursor at (9,3) with battleMode=01 stably reads `[BattleMoving]`.

**Investigated / ruled out:**

- **Damage preview at statBase-62/-96** — Ruled out session 30. Live-verified that neither (attacker MaxHP+MaxHP) nor (target HP+MaxHP) anchors find the widget struct holding projected damage / hit% in IC remaster. 10 struct copies dumped across 0x140xxx/0x141xxx/0x15Axxx/0x4166xxx; none contain preview data at the PSX-era offsets. `ReadDamagePreview` stub retained but always returns (0,0). Delta-HP from `ReadLiveHp` is ground truth. Memory note: `project_damage_preview_hunt_s30.md`.

- **Static array stale mid-turn** — Audited. Damage-preview removal eliminated the main trigger (attacker-MaxHP-at-cursor-origin lookup). `ReadLiveHp` already uses readonly-region copies that update in real time. CollectPositions only runs at turn boundaries (blocked during Acting/Casting/Attacking/Enemies/Allies turns). Marked `[~]` — reopen only if new scenarios surface stale-HP symptoms.

**Memory notes saved this session:**

- `project_facing_byte_s30.md` 🎯 — facing byte layout + convention gotcha.
- `project_element_affinity_s30.md` 🎯 — 5 fields, 8-element bit map, hunt technique.
- `project_damage_preview_hunt_s30.md` — dead-end documented with ruled-out anchors + next-path suggestions.

---

### Session 31 (2026-04-17) — decision-aid surfacing + defensive helper hardening

13 commits, 2253 → 2373 tests (+120, 0 regressions). Four ship batches.

**Batch 1-2 — Decision-aid field shipping** (commit `afdc7a6`):

- [x] **`heldCount` `[xN]` / `[OUT]` rendering in `fft.sh`** — C# pipe (`BattleTracker.AbilityEntry.HeldCount` + `SkillsetItemLookup`) was complete since session 29; fft.sh never consumed it. Wired at the scan-ability render site. Live-verified: Ramza's Items secondary shows `Potion [x4]`, `Hi-Potion [x1]`, `X-Potion [x94]`, `Phoenix Down [x99]`. Non-inventory abilities (Cure, Fire, Attack) render unchanged.

- [x] **Element-affinity per-tile sigils + verbose unit lists** — New pure `ElementAffinityAnnotator` module (14 tests) + `ValidTargetTile.Affinity` field. Priority: absorb > null > half > weak > strengthen. Shell sigils: `+absorb / =null / ~half / !weak / ^strengthen` after the unit name. Also added `+abs: / =null: / ~half: / !weak: / ^str:` prefixes to verbose Units block. Live-verified via verbose output: `[ENEMY] Black Goblin !weak:Ice`, `[PLAYER] Ramza +abs:Earth ^str:Fire,Lightning,Ice,Earth`. Per-tile sigils untriggered live (no matching element+affinity combination in current save).

- [x] **Backstab-aware arc field + sigils** — New pure `BackstabArcCalculator` module (21 tests) with dot-product-on-facing-axis rule. Added `Arc` field to `ValidTargetTile` AND `AttackTileInfo`. Shell renders `>BACK` (backstab: +50% hit, crit bonus) / `>side` (modest). Front omitted as default. Live-verified: `arc:"front"` populated on enemy tiles in Ramza's Throw Stone / Ultima / Items target lists. Sigils untriggered (all enemies east of attacker, all front-arc).

- [x] **NameTableLookup Reis-collision hardening** — Pure `SelectBestRosterBase` selector (7 tests): prefer LOWEST-ADDRESS candidate whose slot 0 == "Ramza" AND count == max observed. Defends against stale high-address heap copies containing pre-recruit roster ghost data. Also: `Invalidate()` hook wired in `CommandWatcher.ExecuteNavActionWithAutoScan` on `load` action. Unverified without a fresh generic recruit.

- [x] **Cutscene-over-LoadGame sticky gameOverFlag fix** — `ScreenDetectionLogic:328` LoadGame rule now requires `eventId ∈ {0, 0xFFFF}`. Real cutscenes (eventId 1..399) with sticky `gameOverFlag=1` after a prior GameOver no longer mis-detect. 3 regression tests in `CutsceneDetectionTests`.

- [x] **LoS pure function + ProjectileAbilityClassifier** — Shipped `LineOfSightCalculator` (DDA walk + linear altitude interp, 13 tests). Shipped `ProjectileAbilityClassifier` pure rule: ranged `Attack` + Ninja `Throw` qualify; spells/summons/Iaido don't (9 tests). Magic flies over walls per FFT canon.

- [x] **Auto-end-turn `— TURN ENDED` suffix** — New `AutoEndTurnAbilities` pure helper (8 tests). Currently Jump only. Wired into all 4 `BattleAbility` completion paths. Live-verified on Lloyd's Jump: `"Used Jump on (5,11) — TURN ENDED"`.

- [x] **Weather damage modifier pure formula** — New `WeatherDamageModifier` table (12 tests). Rain → Lightning × 1.25, Fire × 0.75. Snow → Ice × 1.25, Fire × 0.75. Thunderstorm → Lightning × 1.25. PSX-canonical; needs IC remaster confirmation. Not yet wired into scan_move (blocked on weather-byte memory hunt).

- [x] **BattleModalChoice scaffold** — Pure `GetHighlightedLabel` + `ValidPathNames` helper (6 tests) for the eventual `BattleObjectiveChoice` / `RecruitOffer` detection modals. Detection discriminator still needs live memory hunt.

- [x] **Ability-list counter-delta Up-reset — ATTEMPTED and REVERTED** — Added counter-delta verification to the `BattleAbility` Up-wrap reset to mirror the Down-loop pattern. Broke Lloyd's Jump live: cursor counter at `0x140C0EB20` reports NEGATIVE deltas on Up-wrap (expected +3, got 0 → -6 → -24 → -65); retry math exploded. Reverted blind Up×(listSize+1). Wrap-reset is correct without verification; the counter's whole semantics is wrong for wrap scenarios.

**Batch 3 — Battle render polish** (commit `90fb5fe`, 6 cleanups per `SCREEN_AUDIT.md`):

- [x] **Filter empty tiles from ally-target ability lists** — Compact render used to dump the full 27-tile spell range per Items ability → ~450 coord tuples of noise per scan. Now only occupied tiles + trailing `(N empty)` count. Biggest single-scan compression of the session.

- [x] **Round Move-tile heights to integer** — `h=4.5` half-step slope midpoints don't change high-ground decisions; saved ~30% line length.

- [x] **Drop Attack tiles line when all 4 cardinals are empty** — Pure noise when nobody adjacent; render only when at least one occupant present.

- [x] **Suppress `f=<dir>` facing suffix for allies** — Keep on enemies (backstab signal). Ally facing rarely drives current-turn decisions.

- [x] **Fix empty-parens cosmetic** — `[ENEMY] (Black Goblin)` → `[ENEMY] Black Goblin` when no name.

- [x] **Skip undefined stat fields in verbose** — `PA/MA/Br/Fa=undefined` was appearing because backend doesn't always populate. Conditional render.

**Batch 4 — LoS wire-up + AoE affinities** (commits `59ee1e3`, `b9cb292`):

- [x] **LoS wire-up into scan_move** — `NavigationActions.AnnotateTile` now populates `ValidTargetTile.LosBlocked` for point-target projectile abilities using `MapData.GetDisplayHeight` as the terrain callback. Shell renders `!blocked` sigil.

- [x] **AoE `EnemyAffinities` / `AllyAffinities` parallel lists on `SplashCenter` + `DirectionalHit`** — Positionally aligned with `Enemies[]` / `Allies[]`. Populated when ability has an element + hit unit has matching affinity. Null when non-elemental.

**Batch 5 — Defensive helper hardening + observability** (commits `5640e27`, `417e25c`, `c36dea1`, `9627939`, `30ab760`, `1d0e999`, `85a56ab`):

- [x] **`change_job_to` verifies job actually changed post-commit** — Reads viewed unit's job BEFORE commit, reads again AFTER, errors loudly when pre==post && pre!=target. Previously the helper reported success when the commit sequence silently misfired.

- [x] **`open_eqa` / `open_character_status` / `open_job_selection` verify landed viewedUnit** — New `_verify_open_viewed_unit` shell helper; WARN line when requested != landed. Converts silent helper drift into loud failure messages.

- [x] **`session_tail` shell helper** — Reads most recent `claude_bridge/session_*.jsonl` (written by `SessionCommandLog`). Modes: last-N, `failed` filter, `slow [ms]` filter. Live-verified: `session_tail slow 1500` immediately exposed the new detection-leak bug (battle_wait rows reporting `sourceScreen=CharacterStatus` during facing animations).

- [x] **`SessionCommandLog` — previously shipped, now marked** — Already existed (`ColorMod/GameBridge/SessionCommandLog.cs` wired in `CommandWatcher:1418`). Audit-only this session; added `session_tail` as companion reader.

- [x] **Element-affinity-aware splash scoring** — New `AbilityTargetCalculator.SplashAffinityAdjustment` pure function (12 tests). Delta added to base `ComputeSplashScore`. Weights (enemy-target): weak +2, half -1, null -3, absorb -5, strengthen 0. Ally-target flips sign. Wired into both splash-center and line-direction scoring loops.

- [x] **`ItemNamePoolParser` pure parser** — Pure parser for the static item-name pool at `0x3F18000` (10 tests). Decodes UTF-16LE records with sentinel-stop. Decode / GetByIndex / FindByName. Ready for when the future hover-widget resolver lands.

- [x] **`ShellOutputLinterTests` — static guard on fft.sh render rules** — 4 xUnit assertions: no literal `undefined` from u.pa/u.ma/u.brave/u.faith concatenation; ally-facing hidden; `Math.round(t.h)` present on move-tile render; `occupiedAtk` filter present on Attack-tiles line. Pins the battle-render polish so future edits can't silently regress.

**Technique discoveries — worth propagating:**

- **Defensive post-commit verification over "landed on expected screen" proxy.** Session 31 caught `change_job_to Archer` silently lying ("landed on EqA!") while Lloyd stayed Dragoon. The fix wasn't fixing the nav — it was reading the actual post-commit state and comparing to target. Pattern: capture pre-state, run the operation, read post-state, error if pre==post && pre!=target. Applied to `change_job_to` and `open_*` helpers. Ability-slot helpers already had this pattern.

- **`session_tail slow N` as a bug-finder.** Filtering the JSONL command log for slow commands surfaced three latent bugs in under a minute: (1) `open_eqa` 5937ms landing on the same screen (silent-drift), (2) `battle_wait` 15-23s with `sourceScreen=CharacterStatus` during animation frames (detection leak), (3) `open_job_selection` 11s to wrong unit (viewedUnit drift). Post-hoc latency filtering is a cheap bug finder.

- **Static shell-linter xUnit tests.** Shell output isn't exercised by C# tests. Writing xUnit assertions that scan `fft.sh` as text and assert render-rule patterns pins the design discipline. Beats relying on "the next person will notice."

**Investigated / reverted:**

- **Counter-delta verification on ability-list Up-reset** — reverted. Counter at `0x140C0EB20` reports negative deltas on Up-wrap; the whole premise of verifying against a monotonically-increasing counter breaks when the operation's purpose is to wrap past the top.

**Memory notes saved this session:**

(None new — this session was about surfacing existing data in new ways and hardening helper reliability, not memory hunts. The `project_session_31_shipments.md` note created mid-session was a work-in-progress scratchpad; merged into this archive entry and deleted.)

**Key files added this session:**

- `ColorMod/GameBridge/AutoEndTurnAbilities.cs` + tests
- `ColorMod/GameBridge/BackstabArcCalculator.cs` + tests
- `ColorMod/GameBridge/BattleModalChoice.cs` + tests
- `ColorMod/GameBridge/ElementAffinityAnnotator.cs` + tests
- `ColorMod/GameBridge/ItemNamePoolParser.cs` + tests
- `ColorMod/GameBridge/LineOfSightCalculator.cs` + tests
- `ColorMod/GameBridge/ProjectileAbilityClassifier.cs` + tests
- `ColorMod/GameBridge/WeatherDamageModifier.cs` + tests
- `Tests/GameBridge/ShellOutputLinterTests.cs`
- `Tests/GameBridge/SplashAffinityAdjustmentTests.cs`
- `FFTHandsFree/SCREEN_AUDIT.md` (battle-render audit artifact; keep until all punch-list items ship)

---

### Session 32 (2026-04-18) — speed audit + Tavern Scope A

Commits: `d5914e2` (fft.sh speed pass S1+S2 + per-command timing prefix), `8fb6d58` (C1: skip DetectScreenSettled on screen-query), `191e667` (Tavern Scope A: enter_tavern + read_rumor + read_errand + scan_tavern).

Tests: 2373 unchanged (all still passing; Scope A is pure shell).

**Features landed:**

- [x] **fft.sh S1: single-node fold in `_fmt_screen_compact`** — commit `d5914e2`. Replaced 7 separate `cat RESP | node -e "..."` pipelines + 14 `echo "$R" | grep -o ... | head -1 | cut` chains with ONE node pass that reads the response file once and emits tab-separated values parsed via `IFS=$'\t' read`. Windows node cold-start is ~60-100ms per spawn; old code cost ~500-700ms per render. Live-measured new render: ~100ms flat.

- [x] **fft.sh S2: pure-bash `id()`** — commit `d5914e2`. Replaced `echo "c$(date +%s%N | tail -c 8)$RANDOM"` (3 subprocesses) with `echo "c${EPOCHREALTIME//.}${RANDOM}"` (zero subprocesses, bash-5-native). ~80-120ms saved per `id()` call across 70 call sites.

- [x] **Per-command timing suffix** — commit `d5914e2`. Every `fft()` / `screen` call now appends `t=Nms[action]` to the main screen line, colored green/yellow/red by `FFT_SLOW_MS` threshold (default 800ms; `!` at threshold, `!!` at 2×). `[action]` tag parses `"action":"foo"` → `[foo]` or `"keys":[{"name":"Up"}]` → `[key:Up]` or empty keys → `[screen]`. `FFT_TIME=0` silences. Pairs with server-side `latencyMs` in `session_*.jsonl`. Live baseline post-commit: keys ~215ms, screen-query ~180ms, scan_move via screen ~450ms.

- [x] **🎯 C1: skip `DetectScreenSettled` on pure screen queries** — commit `8fb6d58`. Screen-query commands (empty keys list, no action) can't be mid-transition — nothing just changed — so the 50ms-sleep × 3-consecutive-stable-reads settle loop is pure wasted latency on them. Added `requireSettle` parameter; the default no-arg overload keeps the old behavior. At `CommandWatcher.cs:1349` the main post-execution call site now passes `!isScreenQuery`. Live-measured: screen-query bridge time dropped from ~320ms to ~190ms median.

- [x] **Tavern Scope A — `enter_tavern` shell helper** — commit `191e667`. WorldMap → LocationMenu → cursor-down to Tavern → EnterShop, with settlement-ID guard (0-14) and post-nav `ui=Tavern` verification. Live-verified at Dorter: one command lands on Tavern root.

- [x] **Tavern Scope A — `read_rumor [idx]` / `read_errand [idx]`** — commit `191e667`. From Tavern root or the specific subscreen, opens the rumor/errand list and scrolls the cursor to row `idx` (0-based). Live-verified at Dorter: `read_rumor 2` lands on "The Horror of Riovanes" (screenshot-confirmed); `read_errand` lands on TavernErrands showing "Minimas the Mournful."

- [x] **Tavern Scope A — `scan_tavern` shell helper** — commit `191e667`. ScrollDown-until-wrap detection by watching `cursorRow` for return to start. Caveat: `cursorRow` isn't populated on TavernRumors / TavernErrands yet, so it falls back to "≥30 entries" after max-scan. Open follow-up in §0.

- [x] **`_show_helpers` entries for Tavern, TavernRumors, TavernErrands** — commit `191e667`. Split from the generic shop-group handler; each state advertises the helpers relevant to it.

- [x] **Update `/prime` command** — includes the new timing-suffix explainer + `session_tail` instructions. (Not in repo; lives at `C:\Users\ptyRa\.claude\commands\prime.md`.)

**Memory notes saved this session:**

(None — investigation was file-format oriented, not memory-address oriented. The key finding — "rumor body text is NOT in plain-string RAM; source is `world_wldmes_bin.en.bin` PSX-encoded" — lives in TODO.md §0 Session-32 follow-ups since it blocks the Scope B work, not in a standalone memory note.)

**Files added or modified:**

- `fft.sh` — S1 + S2 + timing suffix + 4 Tavern helpers
- `ColorMod/Utilities/CommandWatcher.cs` — `DetectScreenSettled(bool requireSettle)` overload
- `FFTHandsFree/Instructions/Shopping.md` — Tavern section added
- `FFTHandsFree/TODO.md` — Scope B + follow-ups at top of §0

---

### Session 33 (2026-04-18) — Tavern Scope B + detection fixes + pure-class test hardening

Seven commits: `c3e24a5` (decoder + BattleVictory/Desertion fix), `0917e34` (title map + Self-Destruct + Orbonne guards), `8ea53db` (per-tag timings + corpus regression + title-map workflow doc), `39fc3f9` (equippability table + ability ordering + null-safety), `c0a31bd` (ArmorEquippability + pure-class hardening), `3254c58` (ZodiacData.GetCompatibility + pure-class hardening), `7e15ff3` (KeyDelayClassifier/ItemPrices/AttackDirectionLogic/MesDecoder/CharacterData/AbilityCompactor hardening).

Tests: 2373 → 2852 (+479 new, 0 regressions). Zero live-play required for any of the hardening batches.

**Features landed:**

- [x] **Rumor decoder SHIPPED** — commit `c3e24a5`. `WorldMesDecoder.cs` parses `world_wldmes_bin.en.bin` pre-title region into 26 Brave Story rumor bodies. PSX byte→char map extended with 0x95 space, 0x8D/0x8E punctuation variants, 0x91 quote, 0x93 apostrophe, `DA 74` "," digraph, `D1 1D` "-" digraph. `F8` paragraph space, `FE` section newline, `E3 XX` structural skip, `F5 66 F6 XX F5 YY F6 ZZ` 8-byte date-stamp glyph elision. Split strategy: FE or F5 66 glyph-start, whichever first. Batch 4 switched from file-read to hardcoded `RumorCorpus.cs` (16KB string array) to avoid shipping a 4MB binary.

- [x] **`get_rumor` / `list_rumors` bridge actions + `read_rumor` / `list_rumors` shell helpers** — commits `c3e24a5`, `0917e34`. Three resolution paths: exact title match → body substring match → integer index. Live-verified at Dorter: "Zodiac Braves" → #10, "Riovanes" → #19, "These crystals" → #11.

- [x] **Hardcoded title→corpus index map** — commit `0917e34`. Dictionary in `RumorLookup.cs` with 3 known Tavern rumor titles (Zodiac Braves → 10, Zodiac Stones → 11, Horror of Riovanes → 19). Case-insensitive, trims whitespace. Extended `get_rumor` action to try title lookup before body substring.

- [x] **🎯 BattleVictory/BattleDesertion Orbonne variant fix** — commit `c3e24a5`. Session 21 captured slot0=0x67 (not 255) at Orbonne; old `unitSlotsPopulated`-gated rules misdetected as BattlePaused. New branches fire when `battleModeActive && battleMode==0 && party==1 && ui==1 && eventId 1..399 && slot0 != 0xFFFFFFFF && slot0 != 255`. Guard tests pin that EncounterDialog and stale-flag WorldMap post-battle states do NOT trigger the new rule.

- [x] **`AutoEndTurnAbilities`: Self-Destruct added** — commit `0917e34`. Bomb monster suicide attack now treated as auto-end. Wish / Blood Price / Ultima explicitly documented as NOT auto-end. Needs live repro on a Bomb enemy.

- [x] **Per-tag `FFT_SLOW_MS` thresholds** — commit `8ea53db`. Replaced flat 800ms default with per-action table: screen/snapshot 300ms, keys 400ms, scan_* 700ms, save/load/travel 8000ms, heap_diff 2000ms. Override any one via `FFT_SLOW_MS_<TAG>` env var (upper-case, non-alphanumerics → _).

- [x] **TavernRumorTitleMap.md workflow doc** — commit `8ea53db`. Documents how to add new title mappings: list_rumors → read_rumor lookup → add to dict → add regression test. Also documents "Bael's End" class of unmappable titles.

- [x] **WeaponEquippability pure static class** — commit `39fc3f9`. 19 weapon types × up to 12 jobs, case-insensitive. Source: Wiki/weapons.txt. Does NOT include Equip-* support-skill overrides (caller layers those on top). 48 tests.

- [x] **ArmorEquippability pure static class** — commit `c0a31bd`. 6 armor types (Armor, Helmet, Robe, Shield inclusive; Clothes, Hat exclusive-list). 47 tests. Source: Wiki/armor.txt.

- [x] **ZodiacData.GetCompatibility + MultiplierFor** — commit `3254c58`. Covers same/opposite sign × same/opposite gender matrix, Serpentarius neutral. 120°-apart good-pair / 150°-apart bad-pair same-gender tables deferred until live damage samples validate. Tests pin all 12 opposite pairs + involution property + symmetry of best↔worst and good↔bad.

**Test hardening (+278 tests across 20 files, zero code changes):**

- [x] **ExtractRumors corpus regression** — commit `8ea53db`. 9 tests pin entries 0/4/10/11/15/19 by distinctive phrase, all-non-empty, all-≥100-chars, ≥26 entries.
- [x] **ScreenDetection TitleScreen/Orbonne/StaleWorldMap guards** — commits `8ea53db`, `3254c58`. 2 TitleScreen guards, 4 SaveSlotPicker ambiguity edges, 4 StaleWorldMap post-battle negatives.
- [x] **BattleModalChoice edge cases** — commit `39fc3f9`. +6 tests.
- [x] **ActionAbility skillset ordering pins** — commit `39fc3f9`. 7 tests lock Martial Arts order so the "Aurablast → Pummel" off-by-one nav bug regresses visibly.
- [x] **AbilityJpCosts Wiki-value pins** — commit `39fc3f9`. Mettle unverified-Wiki costs + Theory sweep of 8 high-visibility spell costs.
- [x] **RumorLookup null-safety** — commit `39fc3f9`. 11 tests.
- [x] **AutoEndTurnAbilities negative cases** — commit `c0a31bd`. 7 tests.
- [x] **FacingByteDecoder edges** — commit `c0a31bd`. 14 tests.
- [x] **ElementAffinityDecoder round-trip** — commit `c0a31bd`. 19 tests.
- [x] **ZodiacData comprehensive** — commit `c0a31bd`. 39 tests.
- [x] **BattleFieldHelper edges** — commit `c0a31bd`. 10 tests.
- [x] **ShopTypeLabels edges** — commit `3254c58`. 6 tests.
- [x] **WeatherDamageModifier edges** — commit `3254c58`. 13 tests.
- [x] **TileEdgeHeight edges** — commit `3254c58`. 16 tests.
- [x] **StatusDecoder edges** — commit `3254c58`. 17 tests.
- [x] **ShopGilPolicy new file** — commit `3254c58`. 34 tests.
- [x] **KeyDelayClassifier edges** — commit `7e15ff3`. 7 tests.
- [x] **ItemPrices property tests** — commit `7e15ff3`. 8 tests. Discovered Mage Masher sells 750 / buys 600 — sell>buy IS legitimate in FFT.
- [x] **AttackDirectionLogic new file** — commit `7e15ff3`. 20 tests.
- [x] **MesDecoder edges** — commit `7e15ff3`. 12 tests.
- [x] **CharacterData.GetName** — commit `7e15ff3`. 24 tests.
- [x] **AbilityCompactor new file** — commit `7e15ff3`. 12 tests.

**Memory notes saved this session:**

- `project_tavern_rumor_cursor.md` — TavernRumors cursor-row byte at heap `0x13090F968` (widget base `0x13090F940` +0x28). Verified 0→1→2→3→wrap→0 at Dorter. Heap address so may shuffle across restarts — needs pointer-chain walk for production use. Memory note includes full widget-struct layout found via snapshot-diff + xref.

**Files added (new):**

- `ColorMod/GameBridge/WorldMesDecoder.cs`
- `ColorMod/GameBridge/RumorCorpus.cs`
- `ColorMod/GameBridge/RumorLookup.cs`
- `ColorMod/GameBridge/WeaponEquippability.cs`
- `ColorMod/GameBridge/ArmorEquippability.cs`
- `Tests/GameBridge/WorldMesDecoderIterationTests.cs` (iteration harness)
- `Tests/GameBridge/WorldMesDecoderTests.cs`
- `Tests/GameBridge/WeaponEquippabilityTests.cs`
- `Tests/GameBridge/ArmorEquippabilityTests.cs`
- `Tests/GameBridge/ActionAbilitySkillsetOrderingTests.cs`
- `Tests/GameBridge/ShopGilPolicyTests.cs`
- `Tests/GameBridge/AttackDirectionLogicTests.cs`
- `Tests/GameBridge/AbilityCompactorTests.cs`
- `FFTHandsFree/TavernRumorTitleMap.md`

**Files modified (selected):**

- `ColorMod/Core/ModComponents/ModBootstrapper.cs` — wire `RumorLookup` at init
- `ColorMod/Utilities/CommandWatcher.cs` — `get_rumor` / `list_rumors` actions
- `ColorMod/GameBridge/ScreenDetectionLogic.cs` — Orbonne Victory/Desertion variants
- `ColorMod/GameBridge/ZodiacData.cs` — `GetCompatibility` + `MultiplierFor`
- `ColorMod/GameBridge/AutoEndTurnAbilities.cs` — Self-Destruct + doc expansion
- `fft.sh` — per-tag `_slow_threshold_for_tag` + `read_rumor` / `list_rumors` helpers
- `FFTHandsFree/Instructions/CutsceneDialogue.md` — BattleDialogue section

---

### Sessions 34-43 (2026-04-18) — Tavern rumor city+row system + test hardening + refactors

Commits: `5b0a9ac` (s34-36 bundle: city+row infra, FirstSentence preview, Orbonne boundary tests, WeatherDamageModifier hardening, Gariland seed, full-suite refactors, TavernRumorTitleMap.md), `52377e5` (s37: RumorResolver pure-class extraction, ScreenNamePredicates.IsBattleState centralization across 10 callsites, EventIdMidBattleMaxExclusive constant, CityId.NameFor/IdFor, AbilityJpCosts hardening with Zodiark characterization test), `fd324e6` (s38: Zodiark 0-cost filter FIX, Yardrow seed, IsPartyMenuTab/IsPartyTree predicates, SkillsetNameReferenceTests meta-test, 5 Yardrow sword sell overrides), `8188fb7` (s39: Goug seed, GetSellPriceWithEstimate tuple API, IsPartyTree scope alignment, GameBridge/README.md), `84c971b` (s40: Zaland seed, Chapter1UniformRows constant extraction, 10 Zaland sell overrides), `1a15eaf` (s41: Lesalia seed, IsChapter1UniformCity predicate, AbilityJpCosts skillset coverage audit surfacing Jump/HolySword gaps, ItemPrices coverage floor), `48c7f94` (s42: Gollund seed + FIRST Chapter-1 divergence with corpus #20 Haunted Mine, IsShopState predicate, RumorResolver priority ambiguity tests, ReturnToWorldMap docstring, 5 more sell overrides), `3bc6d01` (s43: Bervenia seed, CityRumors.TableSnapshot accessor, CorpusCityMentionTests diagnostic, missing-override audit).

Tests: 2852 → 3201 (+349 new, 0 regressions). Live-verified at 9 of 15 Chapter-1 settlements.

**Infrastructure landed:**

- [x] **`CityRumors.cs` — `(cityId, row) → corpusIndex` map + `CityId` constants** — Keyed by settlement id. Shared `Chapter1UniformRows` dictionary referenced by 8 uniform cities. `Lookup(city, row)`, `CitiesFor(corpusIdx)`, `IsChapter1UniformCity(city)`, `CityId.NameFor(id) / IdFor(name)` case-insensitive round-trip, `AllMappings` iterator, `TableSnapshot` read-only view.

- [x] **`get_rumor` bridge action — 4-tier resolution** — Extracted to `RumorResolver.Resolve` pure function. Priority: exact title → body substring → `{locationId, unitIndex}` via CityRumors → raw integer index. `RumorResolver.Result` struct returns `{Ok, Rumor, Error}`. Priority-ambiguity tests pin that title beats substring beats city+row, and city+row only fires when searchLabel empty.

- [x] **`RumorLookup.FirstSentence(string)` + `GetPreview(int)`** — Leading-sentence extraction: everything up to `.!?` or 120 chars + ellipsis. Used by `list_rumors` so previews are title-matchable at a glance.

- [x] **`ScreenNamePredicates.cs` — centralized screen-name checks** — `IsBattleState` (10-callsite refactor in NavigationActions/CommandWatcher/TurnAutoScanner), `IsPartyMenuTab`, `IsPartyTree`, `IsShopState`. Null-safe predicates replace scattered StartsWith-Battle / OR-chain checks. Disjoint invariants pinned (shop vs battle, etc.).

- [x] **`ScreenDetectionLogic` eventId constants + helpers** — `IsRealEvent`, `IsEventIdUnset`, `IsMidBattleEvent` + `EventIdRealMin/MaxExclusive/UnsetAlt/MidBattleMaxExclusive`. Named the magic numbers. 5 call-site refactors ran under existing 112-test ScreenDetection coverage.

- [x] **`ItemPrices.GetSellPriceWithEstimate(id)`** — One-call `(Price, IsGroundTruth)?` tuple replaces paired `GetSellPrice` + `IsSellPriceGroundTruth`. `InventoryReader.DecodeRaw` adopts it.

- [x] **`AbilityCompactor.IsHidden(entry)` public helper** — Extracted the `Target == enemy && !HasEnemyOccupant` check.

- [x] **Bug fix: Zodiark 0-cost sentinel no longer surfaces as Next: 0** — `ComputeNextJpForSkillset` now filters `cost <= 0` alongside `cost == null`. Characterization test flipped to correct assertion (Moogle 110, not Zodiark 0).

- [x] **Bug fix: Chapter-1 Orbonne Victory/Desertion variants with slot0=0x67** — New branches in ScreenDetectionLogic fire when `battleModeActive && battleMode==0 && party==1 && ui==1 && eventId 1..399 && slot0 != 0xFFFFFFFF && slot0 != 255`. Guard tests pin EncounterDialog and stale-flag WorldMap do NOT trigger.

**Cities seeded live-verified (9 of 15 Chapter-1 settlements):**

- [x] **8 uniform cities** — Dorter/Gariland/Warjilis/Yardrow/Goug/Zaland/Lesalia/Bervenia share `Chapter1UniformRows` = `{0: #10 Zodiac Braves, 1: #11 Zodiac Stones, 2: #19 Horror of Riovanes}`. Each live-traveled, screenshot-cross-referenced, 4 per-city tests.

- [x] **Gollund (8) — FIRST Chapter-1 divergence** — Row 3 adds corpus #20 "The Haunted Mine". Own dictionary. Breaks the uniform hypothesis; `IsChapter1UniformCity(Gollund)=false`. `CitiesFor(20)` returns only `(Gollund, 3)`.

**Sell-price overrides live-captured (28 new):**

- [x] **Daggers/Swords/Katanas/Staves/Axes/Poles/Bows** — Gariland (archive) + Yardrow (5 swords) + Goug (4 late-game) + Zaland (10 katanas+staves) + Gollund (5 mixed). Novel ratio patterns: swords 9-29%, katanas/staves ~50%, axes sell=buy, Mage Masher/Serpent Staff sell>buy. Memory note: `project_sell_price_ratio_variance.md`.

**Test hardening:**

- [x] **+278 tests across 20 pure-class files (s34-36)** — Orbonne boundary sweep (+11), WeatherDamageModifier edges (+15), LocationSaveLogic (+13), StoryObjective (+4), AbilityCompactor (+5), ItemPrices (+6), CityRumors reverse-lookup (+7), RumorLookup.All ordering (+4), CityRumors validity guards (+4).

- [x] **SkillsetNameReferenceTests meta-test** — Pins all 22 canonical skillset names resolve + 4 counter-examples return null. Motivated by the s37 silent-no-op finding.

- [x] **CoverageAudit tests** — Surfaced 2 latent bugs: Zodiark 0-cost (fixed s38) + Jump/Holy Sword skillsets have 0% cost coverage (characterized; backfill deferred).

- [x] **CorpusCityMentionTests diagnostic** — 8 tests scan corpus bodies for city-name mentions. Flagged unmapped-but-mentioned: #12 Warjilis, #15 Lionel, #23 Bervenia+Dorter — candidates for Chapter-2+ rehost.

**Doc updates:**

- [x] **Shopping.md: 4-tier rumor resolution**
- [x] **TavernRumorTitleMap.md: city+row workflow section**
- [x] **ColorMod/GameBridge/README.md (new, 58 lines)** — jump-table for all pure-class APIs
- [x] **CLAUDE.md / README.md / docs/ARCHITECTURE.md: stale "1101 tests" count removed**
- [x] **NavigationPaths.ReturnToWorldMap docstring** — escape-count depth semantics

**Files created:**

- `ColorMod/GameBridge/CityRumors.cs`
- `ColorMod/GameBridge/RumorResolver.cs`
- `ColorMod/GameBridge/ScreenNamePredicates.cs`
- `ColorMod/GameBridge/README.md`
- `Tests/GameBridge/CityRumorsTests.cs` (93 tests by s43)
- `Tests/GameBridge/RumorResolverTests.cs` (14 tests)
- `Tests/GameBridge/RumorPreviewTests.cs` (18 tests)
- `Tests/GameBridge/ScreenNamePredicatesTests.cs` (68 tests by s42)
- `Tests/GameBridge/EventIdClassifierTests.cs` (32 tests)
- `Tests/GameBridge/SkillsetNameReferenceTests.cs` (26 tests)
- `Tests/GameBridge/CorpusCityMentionTests.cs` (8 tests)


### Session 44 (2026-04-18 → 2026-04-19) — State-detection sweep: BattleChoice, BattleSequence, GameOver + resolvers + god_ramza

Commits: `8ee63d0`, `249f660`, `856d288`, `5c0b59e`, `fe6e952`, `5954d80`, `afff421`, `daec206`, `e458f66`, `cd0d754`, `29c547c`, `4dee442`, `941646b`, `392681b`, `a01422c`, `8107123`, `c1a793c`, `ad507fb`, `a778601`, `377bfd8`

**Shipped:**

- [x] **Task 24: BattleSequence discriminator** — Main-module byte at `0x14077D1F8` reads 1 on the Orbonne-style minimap / 0 on plain WorldMap. New `battleSequenceFlag` param on `ScreenDetectionLogic.Detect`; the dormant rule is live. 11 tests. Live-verified at Orbonne vs Bervenia. `cd0d754`.
- [x] **Task 29: BattleChoice detection via .mes 0xFB + runtime modal flag** — Two-part detector: (a) `eventHasChoice` = the event's `.mes` file contains byte `0xFB` (pre-scanned at load into `EventScript.HasChoice`), (b) `choiceModalFlag` = runtime byte at `0x140CBC38D` that's non-zero only while the 2-option modal is drawn. Found via 6-pass narrow-down from 2,751 candidates to ~14. Live-verified at Mandalia event 016: BattleChoice fires on modal visible, BattleDialogue on narration prefix. `a01422c` + `8107123`.
- [x] **GameOver detection fix** — Dropped the `!actedOrMoved` requirement; `paused==1 && battleMode==0 && gameOverFlag==1` is authoritative. Live-captured fingerprint at Siedge Weald had `battleActed=1, battleMoved=1` (kill action completed immediately before the banner), which blocked the old rule. `377bfd8`.
- [x] **Task 21: BattlePaused cursor resolver + label map** — `BattlePauseMenuLabels` with 6-item menu (Data/Retry/Load/Settings/ReturnToWorldMap/ReturnToTitle) + 11 tests. `ResolveBattlePauseCursor` method + auto-resolve wiring. Live-verified `ui="Data"` at row 0 on Mount Bervenia pause; discrimination limitation carries forward (see open `[~]`). `5954d80`.
- [x] **Task 23: TavernRumors/Errands cursor resolver** — `ResolveTavernCursor` method + wiring, gated on `ScreenMachine.CurrentScreen == TavernRumors/Errands` (NOT `screen.Name`) because detection returns "LocationMenu" before the outer SM-override rewrites the name. Live-verified at Bervenia tavern. `daec206`.
- [x] **Task 25: AbilityJpCosts coverage floor tests** — Three regression guards. 1) `CostByName.Count >= 50`. 2) Every JP-purchasable skillset has at least one costed ability. 3) `UnresolvedNames.Count == 0` hard floor. `29c547c`.
- [x] **Task 26: TODO dedup sweep** — Merged 3 clusters of duplicate entries (deathCounter ×3, chargingAbility ×2, !weak/+absorb sigils ×2). Net −11 lines of stale duplication. `4dee442`.
- [x] **Task 27: `TargetingLabelResolver.ResolveOrCursor` cursorX=-1 fix** — Returns null (not "(-1,-1)") when ability inputs are null AND cursor is uninitialized. Return type changed to `string?`. 4 new tests. `941646b`.
- [x] **Task 30: Characterization-test sentinel** — Meta-test that pins the count of `CoverageAudit_Known*` / `*Characterization*` / `*DocumentsCurrentBehavior*` tests across the suite. Enforces the session-43 "pin the bug, flip when fixed" convention. `392681b`.
- [x] **Session 44 pt 1: IsPartyTree refactor live-verified** — Roster populates with all 15 units on CharacterStatus. Session 39 refactor caused no regression. `8ee63d0`.
- [x] **Session 44 pt 1: BattleAttacking ui=(x,y) fallback** — `ResolveOrCursor` returns the cursor-tile string when no ability has latched, matching BattleMoving semantics. Live-verified. `8ee63d0`.
- [x] **Session 44 pt 4: 3 stale `[ ]` tasks closed via code audit** — "Reorder detection rules", "Scope menuCursor interpretation", "Location address unreliable" were all already handled in prior sessions without being marked done. `5c0b59e`.
- [x] **Session 44 pt 5: BattleStatus ui=\<activeUnit\>** — New `BattleStatusUiResolver` + 2 tests; also fixed a hidden EqA-promote block at CommandWatcher:6190 that was renaming `screen.Name` from BattleStatus to EquipmentAndAbilities whenever the equipment mirror matched. `31cbe68`.
- [x] **Session 44 pt 3: Element decode 3-of-5 fields live-confirmed** — `elementWeak` (Piscodaemon Lightning, Red Panther Earth), `elementAbsorb` (Piscodaemon Water), `elementHalf` (Knight Dark) across 4 enemy archetypes at Lenalian Plateau. `elementNull` + `elementStrengthen` still deferred (specific enemies/gear not available). `856d288`.
- [x] **fft.sh obj= regression** — Bash `IFS=$'\t' read` was collapsing adjacent empty tab fields, shifting every field after the first empty one one slot LEFT. Fix: switch the JS-side delimiter and the bash IFS to `\x01` (non-whitespace). `afff421`.
- [x] **SaveSlotPicker from BattlePaused — closed as myth** — User live-corrected: pause menu has 6 items (Data/Retry/Load/Settings/ReturnToWorldMap/ReturnToTitle), no Save option. Two dup TODO entries closed. `8ee63d0`.
- [x] **Ctrl fast-forward during enemy turns** — moved from §0 to Low Priority / Deferred per user direction. No code change. `(no commit — TODO edit only)`.
- [x] **`god_ramza` helper shipped** — writes endgame gear (Ragnarok / Kaiser Shield / Grand Helm / Maximillian / Bracer) + Brave/Faith 95 to Ramza's roster slot. Level/EXP NOT changed (leveling to 99 scaled random encounters to Lv99 enemies and killed the party). `c1a793c` + `377bfd8`.

**Files created:**
- `ColorMod/GameBridge/BattlePauseMenuLabels.cs`
- `ColorMod/GameBridge/BattleStatusUiResolver.cs`
- `Tests/GameBridge/BattlePauseMenuLabelsTests.cs`
- `Tests/GameBridge/BattleStatusUiResolverTests.cs`
- `Tests/GameBridge/CharacterizationTestSentinelTests.cs`

**Files modified (major):**
- `ColorMod/GameBridge/EventScriptLookup.cs` — new `HasChoice` bool pre-scanned at load
- `ColorMod/GameBridge/ScreenDetectionLogic.cs` — BattleSequence, BattleChoice, GameOver rule changes + 2 new params
- `ColorMod/GameBridge/TargetingLabelResolver.cs` — new `ResolveOrCursor` overload + -1 cursor handling
- `ColorMod/GameBridge/NavigationPaths.cs` — BattleChoice validPath mapping
- `ColorMod/Utilities/CommandWatcher.cs` — BattleSequence/BattleChoice/TavernRumors/BattlePaused resolvers + eventHasChoice + choiceModalFlag read
- `fft.sh` — `god_ramza` helper + IFS regression fix
- `Tests/GameBridge/ScreenDetectionTests.cs` — 11 BattleSequence + 4 BattleChoice + (reverted) BattleVictory tests
- `Tests/GameBridge/TargetingLabelResolverTests.cs` — 4 new cursor-fallback tests
- `Tests/GameBridge/AbilityJpCostsTests.cs` — 3 coverage floor tests

**Memory notes added:**
- `project_battle_pause_cursor.md` — triple-diff technique proven at BattlePaused
- `project_tavern_rumor_cursor.md` — updated with per-session re-locator technique
- `project_battle_sequence_discriminator.md` — hunt methodology + 2 flag addresses
- `project_battle_sequence_flag_sticky.md` — known edge case, 3 fix approaches
- `project_battle_choice_cursor.md` — session-cursor hunt methodology (deprioritized in favor of .mes scan)
- `feedback_no_autonomous_save.md` — never save without explicit user ask
- `feedback_battle_sequence_loc_auto_opens.md` — cursor-arrival opens minimap
- `feedback_battle_sequence_exit.md` — Hold-B or restart to exit minimap

**Tests:** 3201 → 3242 passing (+41). 0 regressions.

---

### Session 45 (2026-04-19) — Live dialogue rendering + crystal/treasure state detection + BFS corpse fix

**Commits:**
- `eb53261` — auto_place_units: accept BattleDialogue/Cutscene as end states
- `207f66b` — Surface current dialogue box on BattleDialogue/Cutscene/BattleChoice
- `9cc1f8a` — MesDecoder: flip F8/FE roles — FE is the bubble boundary, F8 is intra-line wrap
- `b0bea6c` — MesDecoder: 2+ consecutive 0xF8 is a bubble boundary too
- `3e12c31` — ScreenDetectionLogic: gate BattleMoving/Attacking/Waiting on battleTeam==0
- `155b8e6` — BFS: treat corpses like allies — pass-through, no stop
- `d083c11` — Detect 4 crystal-pickup states: MoveConfirm / Reward / AcquireConfirm / LearnedBanner
- `663f630` — Split BattleRewardObtainedBanner from BattleCrystalMoveConfirm

**Completed tasks** (moved from TODO.md):
- [x] **⚠ UNVERIFIED auto_place_units** — Session 45 live-verified at Dorter + Zeklaus. End-state poll loop now exits on BattleDialogue/Cutscene (~19s instead of timing out at 40s).
- [x] **Battle state verification — BattleAlliesTurn** — Live-verified at Zeklaus (Cornell as guest ally). Remaining: BattleActing (transient, hard to catch) deferred.

**Shipped features:**

1. **Live in-game dialogue rendering** — `screen` on BattleDialogue/Cutscene/BattleChoice now surfaces the current box text + speaker + box index. Lets the user pace the story without screenshots. Components:
   - `DialogueProgressTracker` — pure counter, bumps on advance, resets on eventId change. 6 tests.
   - `MesDecoder.DecodeBoxes` — boundary rules corrected through live walkthrough: FE=bubble boundary, F8=intra-bubble line wrap, F8≥2 consecutive = also a boundary, speaker change = implicit boundary.
   - `DetectedScreen.CurrentDialogueLine` payload — state-gated both server- and shell-side.
   - fft.sh compact render under the main header line.

2. **5 new battle screen states for crystal/treasure pickup** — all four states in the "step onto crystal" sequence plus the "Obtained X" chest banner now detected:
   - `BattleCrystalMoveConfirm` (Yes/No "open/obtain?")
   - `BattleCrystalReward` (Acquire/Restore HP&MP chooser)
   - `BattleAbilityAcquireConfirm` (Yes/No "acquire this ability?")
   - `BattleAbilityLearnedBanner` ("Ability learned!")
   - `BattleRewardObtainedBanner` ("Obtained X!" chest loot)

   Discriminator: `moveMode=255` is normal BattleMoving, `moveMode=0` + encA (0/1/2/4/7) splits the modals. 5 characterization tests.

3. **BFS corpse-awareness fix** — Dead-unit tiles were being dropped entirely from BFS inputs via `if (u.Hp <= 0) continue;`. Fix classifies by lifeState: `dead`→allyPositions (pass-through, no stop), `crystal`/`treasure`→skip entirely. Same applied to `GetEnemyPositions`/`GetAllyPositions` helpers. 1 test.

4. **Enemy/ally-turn state-leak fix** — `battleMode=1/2/4/5` rules (BattleMoving/Attacking/Waiting) were firing regardless of whose turn it was. Promoted the team-owner rules to run BEFORE submode rules. Live-verified at Zeklaus — enemy turns now correctly report `BattleEnemiesTurn` instead of false-positive `BattleMoving`. 3 new tests.

5. **auto_place_units doesn't hang on story battles** — Pre-battle dialogue now accepted as a "battle started" end-state for the poll loop. Helper completes ~19s instead of hitting the 30s timeout.

**Files created:**
- `ColorMod/GameBridge/AutoPlaceUnitsEndState.cs`
- `ColorMod/GameBridge/DialogueProgressTracker.cs`
- `Tests/GameBridge/AutoPlaceUnitsEndStateTests.cs`
- `Tests/GameBridge/DialogueProgressTrackerTests.cs`
- `Tests/GameBridge/MesDecoderBoxGroupingTests.cs`
- `Tests/GameBridge/CrystalStateDetectionTests.cs`

**Files modified (major):**
- `ColorMod/GameBridge/MesDecoder.cs` — new `DecodeBoxes` + `DialogueBox` record
- `ColorMod/GameBridge/EventScriptLookup.cs` — `EventScript` record gains `Boxes` list
- `ColorMod/GameBridge/ScreenDetectionLogic.cs` — team-owner rule promotion, 4-state crystal detection block, obtained-banner split
- `ColorMod/GameBridge/NavigationActions.cs` — BFS corpse classification, `AdvanceDialogue` + AutoPlace end-state hook, `GetEnemy/AllyPositions` corpse-aware
- `ColorMod/GameBridge/NavigationPaths.cs` — 4 new path dictionaries for crystal states, `GetYesNoConfirmPaths` helper
- `ColorMod/Utilities/CommandBridgeModels.cs` — new `DialogueBoxPayload`, `CurrentDialogueLine` field
- `ColorMod/Utilities/CommandWatcher.cs` — tracker wiring + dialogue payload population
- `fft.sh` — compact-renderer gains DLG_SPK/DLG_TXT/DLG_POS fields, state-gated dialogue print

**Memory notes added:**
- `project_crystal_states_undetected.md` — fingerprint table for all 4 crystal states + reward banner
- `project_battle_victory_encA255.md` — drafted fix for Victory detection via encA=255
- `project_kill_enemies_helper.md` — search_bytes cap blocker for speedrun helper
- `feedback_auto_place_crashes_dorter.md` — flaky formation-race warning

**Tests:** 3242 → 3283 passing (+41). 0 regressions.

**Technique discoveries:**
- **Live ground-truth walkthrough beats offline byte analysis.** The MesDecoder went through 3 wrong iterations of F8/FE split rules before a user-typed bubble-by-bubble walkthrough of Dorter event 38 (45 real bubbles) settled the correct rule. Next time a boundary-rule hunt kicks off, collect one walkthrough EARLY.
- **State discriminator quality continuum.** Single-byte sentinels like `encA=255` (Victory) are the holy grail. Multi-byte compound rules (crystal states) are next. Screenshot-matching is the worst (required for the chest-vs-crystal confusion).
- **Commit-per-task cadence preserves reversibility.** 8 commits this session, each tight. The decoder fix required THREE commits because rules were discovered iteratively — each as its own commit lets future readers trace "when did the behavior change?".

---

### Session 46 (2026-04-19) — State-detection desync hunt + SM auto-snap + retry-storm fix

Commits: `a32ab73` (state-detection fixes + SM auto-snap), `0a19777` (UserInputMonitor scaffold, inert), `35b068d` (HoveredUnitArray retry-storm fix).

Tests: 3283 → 3337 (+54 new, 0 regressions).

**Features landed:**

- [x] **BattleVictory detection via encA=255 sentinel** — Shipped with `battleTeam==0` guard so the unique `encA=255 && encB=255` post-battle signature doesn't steal from enemy/ally-turn states. Runs BEFORE `IsMidBattleEvent` so post-victory eventId=41 no longer misroutes to BattleDialogue. Live-verified at Zeklaus win. See `ScreenDetectionLogic.cs:453-461` + `memory/project_battle_victory_encA255.md`.

- [x] **BattlePaused cursor — SM-driven tracking (supersedes memory resolver)** — New `BattlePausedCursor` property on `ScreenStateMachine` with reset-on-entry (via `ObserveDetectedScreen` transition detection) and wrap-aware update on Up/Down via new `OnKeyPressedForDetectedScreen(vk)` method. CommandWatcher prefers the SM tracker over the flaky memory resolver (which latched on first-Down candidate but didn't track subsequent nav). Live-verified at Grogh Heights: `Data → Retry → Load → Settings → ReturnToWorldMap → Up → Settings` tracked exactly. 5 new tests. Replaces the `[~]` "BattlePaused cursor byte — resolver SHIPPED, discrimination LIMITED" entry from session 44.

- [x] **SM auto-snap on category mismatch** — New `ScreenStateMachine.AutoSnapIfCategoryMismatch(detectedName)` + `CategorizeScreenName()` classify screens into InBattle / WorldSide / PartyTree / DialogueOrCutscene. When detection disagrees with SM's enum category, snap SM to a safe anchor (Unknown for in-battle, WorldMap for world-side, PartyMenuUnits for party tree). No keypresses fire. Dialogue/Cutscene tolerated on any category (overlay). 6 new tests.

- [x] **Within-PartyTree auto-snap** — New `SnapPartyTreeOuterIfDrifted(detectedName, menuDepth)`. Detection can't distinguish PartyMenuUnits from CharacterStatus/EqA/Picker/JobScreen (memory-identical), so uses `menuDepth==0` as the authoritative "outer grid" signal. When SM is deeper but memory says outer, realign. 6 new tests. Addresses the live-repro at Dorter where bridge stayed reporting CharacterStatus after a failed SelectUnit.

- [x] **`LastDetectedScreen` string mirror on SM** — New property + `ObserveDetectedScreen(detectedName)` method. Tracks ANY detection result, not just SM-modeled transitions. Fixes the session-45 bug where `sourceScreen` stuck at boot-time `TitleScreen` for entire battle sessions. Session log + ambiguity-resolver now pull from this.

- [x] **Ambiguity-resolver 4-arg overload for post-load WorldMap** — `ResolveAmbiguousScreen(smScreen, detectedName, keysSinceLastSetScreen, lastSetScreenFromKey)`. Live capture 2026-04-19 at Grogh Heights confirmed post-load WorldMap and freshly-opened TravelList produce byte-identical detection inputs (`hover=254, moveMode=255, party=0, ui=1`). Detect() defaults to TravelList; the overload trusts SM when freshly-seeded (`KeysSinceLastSetScreen==0 && !LastSetScreenFromKey`). Preserves the existing "trust detection when SM is stale" contract for the normal-play path. 3 new tests.

- [x] **Turn-owner rules: drop `!actedOrMoved` guard** — Enemy/ally turns now correctly report their team regardless of acted/moved flags. Stress probe showed a moved=1 enemy turn falling through to `BattleAttacking`/`BattleWaiting` as if the player were acting.

- [x] **Tab-flag world-map guard** — `unitsTabFlag=1`/`inventoryTabFlag=1` detection skipped when there's an affirmative world-map signal. Stale flags after `battle_flee` no longer latch WorldMap into PartyMenuUnits.

- [x] **inBattle moveMode flicker guard** — When `slot9=0xFFFFFFFF` AND `battleMode ∈ 1..5`, a one-frame `moveMode=13` flicker no longer escapes to WorldMap. Narrow scope: only active-turn frames, not post-battle stale states.

- [x] **fft_resync helper removed** — User direction: "bandaid, not a fix." Removed from `fft.sh`, cleaned up 4 help-text references, deleted `feedback_use_fft_resync.md` + `feedback_fft_resync_forbidden_states.md` memory notes.

- [x] **Session command log uses `LastDetectedScreen` for sourceScreen** — All 3 log sites updated. Fixes the session-45 desync where every command logged `sourceScreen: "TitleScreen"` even mid-battle.

- [x] **UserInputMonitor scaffold (inert pending deploy)** — New class polls GetAsyncKeyState for nav keys every 20ms. Gated on `GetForegroundWindow == game window`. De-duped against bridge-sent keys via `MarkBridgeSent(vk)` (150ms window). Forwards user-typed keys to `OnKeyPressed` + `OnKeyPressedForDetectedScreen`. Not wired into bootstrap in the committed state — bootstrap line staged in working tree pending live-verify. Enable by uncommenting the ModBootstrapper block.

- [x] **🎯 HoveredUnitArray retry-storm fix** — `Discover()` set `_discoveryAttempted = true` but never checked it. Failed scans re-ran full-heap on every `ReadStatsIfMatches` call (once per roster slot). Late-game: ~3GB of scans per screen query. Almost certainly the root of two game crashes observed earlier this session. Fix: 2-line guard. Use `Invalidate()` to force rescan after save-load. **Live-verified**: 2 scans across 73 commands (0.036/cmd) vs. previous 86 scans across 29 commands (2.97/cmd) — **83x reduction**.

**Technique discoveries:**

- **Adversarial-probe batteries catch bugs before live testing.** 15-probe stress battery (rule-boundary + collision + sticky-flag + sentinel-drift + crystal encA threshold) caught 7 real desyncs at unit-test time. ~47% hit rate. When a bug class exists, push adversarial inputs at the rules — don't wait to repro live.
- **Memory-identical states need SM+key-count discrimination.** WorldMap and TravelList have byte-identical detection inputs post-load. The `keysSinceLastSetScreen==0 && !lastSetScreenFromKey` check is the cleanest way to distinguish "SM freshly-seeded after boot" from "SM stale because a user keypress wasn't observed" without a pure memory byte.
- **The `_attempted` flag pattern must be checked, not just set.** HoveredUnitArray had the right intent but `_discoveryAttempted = true` was written but never read as a guard. Audit similar "one-shot discovery" paths for boolean fields that are written but never read.
- **Heavy SearchBytes scans crash the host under sustained pressure.** Full-heap scans at 30+ calls/screen-query apparently trigger game-side issues. Rule: full-heap scans MUST be one-shot per session with explicit `Invalidate()` to rescan. Background timers that rescan periodically are a footgun.
- **Keypress-based fallback tooling is a bandaid when the root is detection.** `fft_resync` compensated for state-detection bugs; removing it forced fixes to land in the detection layer. When a "recovery helper" gains complexity, that's a signal the underlying state signal is wrong.
- **User input changes the rules for SM-tracked state.** Everything SM-tracked (cursor positions, tab indices) assumes keys flow through the bridge. User keys desync every SM-tracked field. The UserInputMonitor hook is the architectural answer — needs live-verify next session.

---

### Session 47 (2026-04-19) — Code-only task sweep: 29 tasks across 3 commits, zero game verification

Commits: `6818a88` (part 1: 9 tasks), `96281c8` (part 2: 10 tasks), `b47a1e0` (part 3: 10 tasks).

Tests: 3337 → 3629 (+292 new, 0 regressions, 4 skipped).

Session focus: every TODO item below was completable with `./RunTests.sh` alone — no game boot, no memory probes, no live-verify. Pure TDD / refactor / lookup-table ports.

**Part 1 (commit `6818a88`):**

- [x] **Audited `SearchBytesInAllMemory` callers for retry-storm pattern** — Session 46 fixed `HoveredUnitArray.Discover()`. This session grep'd all 10 callers (NavigationActions×6, ShopItemScraper×2, PickerListReader×1, CommandWatcher×3, NameTableLookup×1, HoveredUnitArray×1). Every caller is either already guarded (NameTableLookup `_buildAttempted`, HoveredUnitArray `_discoveryAttempted`) or only invoked via explicit user-initiated bridge commands (scan_units/scan_move/search_bytes/probe_status/dump_unit_struct) with fail-and-skip semantics (no retry loops). PickerListReader is dead code — field declared but never wired; added a guard-on-wire TODO comment. **No retry-storm candidates found.**

- [x] **`execute_action` fail-loud on unknown action name** — The bridge already returned `status=failed` with an "Available: ..." error, but the list was just names. Session 47 also shipped `NavigationPathsDescription.FormatAvailableActions(screenName)` which now renders "Name — Desc; ..." (aliases coalesced with "/"). Wired into CommandWatcher:2884 so mistyped actions get actionable feedback. 4 new tests.

- [x] **`Leave` alias for `Back` on TavernRumors/TavernErrands/pickers** — Added post-processor in `NavigationPaths.GetPaths` that adds Leave if Back exists (or vice versa). Session 47 part 2 extended this to a full alias-group helper (`ActionNameAliases`). 9 new tests.

- [x] **SM cursor tracking: CharacterStatus sidebar** — New `SidebarIndex` updates via `OnKeyPressedForDetectedScreen` on CharacterStatus. 3-item wrap (Equipment/Job/CombatSets). Reset on transition into. 5 new tests.

- [x] **SM cursor tracking: TavernRumors/TavernErrands** — New `TavernCursorRow` property, unclamped-positive pattern (list length varies per city). Reset on entry including between Rumors↔Errands. 8 new tests. Supersedes the flaky memory resolver that latched on first-Down candidate.

- [x] **`JobEquippability` reverse lookup helper** — Given a job, returns all equipment types it can equip. Wraps `WeaponEquippability.AllWeaponTypes` + `ArmorEquippability.AllArmorTypes` + their per-type `CanJobEquip` into one call. 8 new tests. Supports the `availableWeapons[]` verbose catalog for EquippableWeapons picker.

- [x] **Backfill `AbilityJpCosts` Jump + pin Holy Sword null** — Re-shipped session 44's reverted backfill. Added 12 per-ability costs (Horizontal +1/+2/+3/+4/+7 @ 150/350/550/800/1100, Vertical +2..+8 @ 100/250/400/550/700/1000/1500) per ABILITY_COSTS.md. Holy Sword pinned as intentional-null via renamed characterization test. 13 new tests. Session 44 was reverted for lacking live-verify; session 47 ships under the user's new "code-only" rubric.

- [x] **`search_bytes` bridge action: `minAddr` / `maxAddr` params** — New optional CommandRequest fields with hex-or-decimal parsing (`ParseAddrOrDefault`). Omitted when null. CommandWatcher's `search_bytes` handler uses the 4-arg overload when either is set. Unblocks heap-targeted scans that the default 100-match cap couldn't reach through main-module noise. 8 new tests.

- [x] **Hook raw Enter in `ExecuteKeyCommand` into `DialogueProgressTracker`** — New `DialogueTrackerKeyHook` pure helper + `HandleKeyPress` method. Wired into CommandWatcher's raw-key path so the `enter` shell helper bumps the box counter on Cutscene/BattleDialogue/BattleChoice. Removed the now-redundant explicit bump in `ExecuteValidPath` to avoid double-counting. 16 new tests.

**Part 2 (commit `96281c8`):**

- [x] **BattleChoice eventId catalog** — New `BattleChoiceEventIds` class with known list (Mandalia Plain event 16 verified session 44). Regression tests confirm signal-based detection classifies each catalogued event as BattleChoice — the list is a documentation + regression pin, not a replacement rule. 5 new tests.

- [x] **Zodiac Good/Bad pair tables (triangle/square partners)** — Shipped the 120°-apart "good pair" and 150°-apart "bad pair" tables per Wiki compatibility chart. Fire/Earth/Air/Water element trios are Good with each other; 12 Bad pairs per Wiki. `GetCompatibility` now returns the right answer for every chart cell. 25 new tests.

- [x] **`Yes`/`No` aliases on confirm modals (ShopConfirmDialog + crystal Yes/No)** — Yes commits safely via UP+Enter (vertical) or LEFT+Enter (horizontal) depending on modal layout; No is Escape. 11 new tests.

- [x] **SM cursor tracking: BattleAbilities submenu** — New `BattleAbilitiesCursor` property, unclamped-row pattern (count varies per unit's learned skillsets). Reset on entry. 6 new tests.

- [x] **WarriorsGuild dedicated single-item paths** — Session 44 confirmed Bervenia guild has one sub-action (Recruit) with no-op Up/Down. New `GetWarriorsGuildPaths` with just Recruit + Leave (removed confusing CursorUp/Down/Select). Back↔Leave alias post-processor is now bidirectional. 5 new tests.

- [x] **UnlearnableAbilitySentinel audit** — Pins cost=0 entries in `AbilityJpCosts.CostByName`. Only Zodiark (Summoner capstone, crystal drop only) should be cost=0. Any other cost=0 is a regression. 4 new tests.

- [x] **Extracted `ActionNameAliases` static class** — Moved inline post-processor logic from `NavigationPaths.GetPaths` into a testable class with `Groups` array for future extension. 8 new tests covering every branch.

- [x] **`NavigationPathsDescription` helper** — `GetPathDescription(screen, action)` + `FormatAvailableActions(screen)` return "Name — Desc; ..." with aliases coalesced via "/". Wired into CommandWatcher's fail-loud error. 9 new tests.

- [x] **CommandRequest JSON schema characterization tests** — 26 tests pinning every `[JsonPropertyName]` field, defaults, and unknown-field-tolerance. Catches silent renames that would break shell helpers (e.g. `to` → `toScreen` would break every `execute_action` call). 26 new tests.

- [x] **`ScreenCompactFormatter` pure class** — Extracted from fft.sh's Node pipeline. `FormatHeader(screen, status)` returns the "[Screen] loc=... ui=... status=... objective=..." line. Shell can delegate incrementally. 10 new tests. Session 47 part 3 extended with gil + eventId rendering.

**Part 3 (commit `b47a1e0`):**

- [x] **`BattleWaitLogic` / `BattleFieldHelper` / `TurnOrderPredictor` / `MonsterAbilityLookup` test coverage** — 4 existing pure-logic files had zero direct tests. Added 41 tests covering `ShouldSkipMenuNavigation` / `NeedsConfirmation` / `CanStartBattleWait`, `GetOccupiedPositions` / `AllEnemiesDefeated` / defeat sentinels (crystal/treasure/petrify), CT simulation / tie-breaking / maxTurns cap, per-ability metadata + MonsterAbilities cross-check invariant.

- [x] **`MesDecoder.DecodeByte` completeness audit** — Pins every byte 0x00-0xFF as either decoded-to-char or null. Catches silent additions (new byte starts decoding) AND silent deletions (an entry vanishes from DecodeByte). 5 new tests.

- [x] **`ScreenCompactFormatter` extension: gil + eventId** — Gil renders with thousands separator; omitted when 0 (unread sentinel). EventId renders when in real-event range (1-399); out-of-range sentinels (0xFFFF) omitted. 5 new tests.

- [x] **`ActionNameAliases.Groups` expansion** — Added `Exit` to exit verbs group; new affirmative group `Confirm`/`OK`/`Yes`. Propagates only on screens that define one of the names (WorldMap unaffected). Yes stays distinct from Confirm on ShopConfirmDialog because they have different keypress sequences. 5 new tests.

- [x] **`ZodiacElementLookup` helper** — Classical-element affinity (Fire/Earth/Air/Water/None) per sign. Invariant test cross-verifies: same-element distinct signs ⇒ Good compatibility. 16 new tests.

- [x] **`NavigationPathsDescription.FormatActionNames` slim variant** — Comma-separated name list with aliases coalesced via "/" (no descriptions). For terse log-line use. 5 new tests.

- [x] **`AbilityJpCostsTotalsTests` — per-skillset total pins** — 14 skillsets pinned to their current total JP cost (Items 4040, Arts of War 2200, Aim 3300, White Magicks 6270, Black Magicks 4900, Summon 8400, Time Magicks 5530, Martial Arts 2700, Iaido 5500, Darkness 2700, Mettle 5870, Jump 7450, Holy Sword 0). Any add/remove/repricing of an ability fires one of these.

**Technique discoveries:**

- **Tests over descriptions.** In sessions 44-46, new features often landed with narrow tests pinning the new behavior. Session 47 flipped the prioritization: for 29 tasks across 3 commits, I started each task by asking "what's the pure slice I can test without touching the game?" The answer was almost always more substantial than expected — even supposedly-infra tasks (search_bytes minAddr, fail-loud) ended up with 8-26 new tests pinning the JSON shape, error message format, alias propagation rules.
- **Coverage audit is a cheap bug-finder.** Walking `for f in ColorMod/GameBridge/*.cs` and finding files WITHOUT corresponding test files surfaced 4 fully-untested pure-logic classes (BattleWaitLogic, BattleFieldHelper, TurnOrderPredictor, MonsterAbilityLookup). All tested green on first TDD pass — no bugs found — but the coverage pin prevents future regressions.
- **Characterization tests pin known limitations.** Several tasks (BattleChoice eventIds, Holy Sword null, UnlearnableAbilitySentinel zodiark-only, MesDecoder completeness) went from "we know this is narrow but haven't written it down" to "any silent change fires a test." Cheap insurance.
- **Refactor-with-tests over refactor-without.** The `ActionNameAliases` extraction could have been a pure cleanup with zero functional change. Instead shipped with 8 new tests covering every branch — doubles as a pin against future regression and documents the alias semantics for the next contributor.
- **Totals pins catch bulk-edit drift.** `AbilityJpCostsTotalsTests` pins 14 sums. A single cost change fires exactly one test; a bulk rename fires many. The `WhiteMagicks_Total` test caught my first sum estimate being wrong (4550 vs actual 4900) — had I shipped without verifying, a later dev would have seen a failing test with no clear pathway to update the pinned value. Running `dotnet test` against each pin surfaced the actual number before commit.

---

### Session 47 Part 4 (2026-04-19) — 5 meaningful features, not just coverage

After 29 coverage/refactor tasks, pivoted to real unshipped features. User pushed back: "Do we have any tasks, not just write some tests?" Honest audit surfaced 5 genuine code-only features worth shipping.

Tests: 3629 → 3680 (+51 new).

**Features landed:**

- [x] **`execute_turn` bundled action** — New `TurnPlan` + `TurnStep` pure types convert a bundled turn intent (optional move, optional ability, optional wait with facing override, optional SkipWait) into an ordered list of the existing primitives (battle_move / battle_ability / battle_wait). `CommandWatcher.ExecuteTurn` dispatches each sub-step through the main `ExecuteAction` pipeline so each runs its normal scan + validation + retry. Aborts at first non-completed step. TODO §1 Tier 5. 9 new tests cover every branch: empty plan, move-only, attack-only, move+attack, self-target, skipWait, direction, ability-without-name guard.

- [x] **`swap_unit_to <name>` pure planner** — New `UnitCyclePlanner.Plan(fromIndex, toIndex, rosterCount)` returns the shortest Q/E sequence to cycle the viewed unit on PartyMenu nested screens. Wraps correctly at ring boundaries; halfway ties prefer forward (E). Handles invalid inputs (out-of-range indices, non-positive count) by returning empty sequence so callers can detect-and-error. 12 new tests. Shell wrapper + dispatcher now a one-liner follow-up.

- [x] **`AbilityCursorDeltaPlanner`** — Pure decision function that decides when a counter-delta read after an Up/Down press can be trusted. Session 31 shipped counter-delta but Up-wrap broke Lloyd's Jump targeting (negative deltas exploded retry math). This planner formalizes the trust rules: sign must match expected direction, magnitude must be &lt; listLength (wrap is suspect), nonzero, and not wildly off expected magnitude. Returns `{TrustDelta, RemainingKeys}` so the caller falls back to blind-count on untrusted reads. 10 new tests pin every branch. TODO §12 "Ability list navigation: use counter-delta instead of brute-force scroll".

- [x] **`MvpSelector` extract** — The battle-MVP scoring formula (`kills * 300 + damageDealt + healingDealt/2 - timesKOd*200`) was inline in `BattleStatTracker.EndBattle`. Extracted into a pure static with public `Score(unit)` and `Select(dict)` methods. 10 new tests pin the formula + tie-breaking (first-inserted wins) + empty-input behavior. `BattleStatTracker` now delegates. Zero-net-behavior-change refactor with proper coverage.

- [x] **`FacingDecider` — unify override+auto-pick paths** — Scattered `facingOverride ?? FacingStrategy.ComputeOptimalFacingDetailed(...)` pattern consolidated into `FacingDecider.Decide(override, ally, enemies)` returning a `FacingDecision` with dx, dy, DirectionName, Front/Side/Back arc counts, and a `FromOverride` flag. Cardinal name formatting (`(1,0)→"East"` etc.) exposed as `FacingDecider.NameFor(dx, dy)`. 10 new tests. `NavigationActions.cs:2657` refactored to delegate.

**What made this batch different from earlier session 47 batches:**

- **User pushback surfaces truth.** After 29 mostly-coverage tasks I'd convinced myself the well was dry. User asked "any real tasks?" which forced an honest re-scan. 5 genuine features found — smaller pool than the first two batches (28 tests vs 200+) but each one ships behavior, not just assertions.
- **Extract-with-tests > inline + indirect tests.** `MvpSelector` was covered indirectly by `BattleStatTrackerTests`. Extracting gave it dedicated tests that name the formula, catch boundary cases, and document the ranking semantics for next editor. Same for `FacingDecider` — the override-precedence rule was never directly asserted before.
- **Pure planner + thin dispatcher is the shipping pattern.** TurnPlan, UnitCyclePlanner, AbilityCursorDeltaPlanner each separate "figure out what to do" (pure, tested) from "do it" (game-touching, not tested). Makes the decision logic the thing that gets regression coverage; the dispatch layer stays thin enough to read at a glance.


### Session 48 (2026-04-19) — screen-detection deep-fixes + authoritative map-id byte

**Commits (17):**

- `164339c` — Map Warriors' Guild + Poachers' Den sub-action screens
- `e0f1bfe` — world_travel_to accepts location names (substring match)
- `2a80130` — BattleSequence discriminator + Flee compound action
- `0cd2a37` — BattleDialogue detection on ally-turn frames + EqA promote exclusion
- `2e8fa3d` — Gate BattleSequence rule on slot9 so it stops eating enemy turns
- `8b19fdb` — Ramza Ch2 Squire job name + broaden EqA promote exclusion
- `9bd362c` — BattleDialogue/BattleChoice detection for Ch2 formation-phase events
- `c8e1fee` — WorldMap C-snap on arrival + one-line ValidPaths in compact view
- `730c53f` — Collapse Helpers into one-line + mark session-48 TODOs done
- `c009a90` — Dedup active-unit isActive when multiple slots share HP
- `3a64900` — TODO: equipment IDs are live — refute stale-reads claim
- `4c94e25` — Thread LoS check through the basic Attack ability
- `2038019` — Override active unit position with live grid cursor after move
- `4eb3e37` — Add ATTACK_DEBUG log + file attack-range-calculator bug
- `9f87bfc` — Live map-id byte 0x14077D83C — authoritative battle map

**Shipped this session:**

- [x] **Warriors' Guild: WarriorsGuildRecruit + WarriorsGuildRename sub-states** — Captured `shopSubMenuIndex` at `0x14184276C` by stepping into each option at Bervenia. Recruit=0x2B, Rename=0x1A. Wired in `ResolveShopSubAction` case 2 + `NavigationPaths.Dispatch` + SM WorldSide category. Prior-session note that "only Recruit visible" was stale — Bervenia's WG shows both. Commit `164339c`.

- [x] **Poachers' Den: PoachersDenProcessCarcasses + PoachersDenSellCarcasses sub-states** — Discriminators ProcessCarcasses=0x0F, SellCarcasses=0x12 captured at Dorter. Wired identically to WG. Live-verified post-restart. Commit `164339c`.

- [x] **`world_travel_to` accepts location names** — Shell-side case-insensitive substring match against the 43-entry table. Numeric IDs still work. Exact-name short-circuits; unique substring resolves; multiple matches reject with a candidate list ("Bervenia" → ambiguous between Free City of Bervenia + Mount Bervenia). Live-verified: `"Free City"`→13, `"Dorter"`→9, `"Xyzzy"`→rejected. Table mirrors C# dict at `CommandWatcher.cs:3875`. Commit `e0f1bfe`.

- [x] **BattleSequence runtime discriminator** — Swapped the save-baked `0x14077D1F8` flag for runtime `0x1407774B4` (u32: 2=minimap open, 1=plain WorldMap). Found via full-module snapshot/diff at Orbonne. Fixes the sticky-flag bug where post-restart WorldMap reported as BattleSequence. Commits `2a80130`, `2e8fa3d` (added slot9 guard so the rule stops eating enemy-turn frames mid-battle).

- [x] **BattleSequence Flee compound action** — Hold-B 3500ms → 400ms modal fade-in → auto-tap Enter on preselected Yes → WorldMap. Caller never sees the intermediate Yes/No modal. Required extending `hold_key` with `FollowUpVk` + `FollowUpDelayMs` and routing hold_key paths through `ExecuteAction` (not `NavigationActions`). Commit `2a80130`.

- [x] **BattleDialogue detection on ally-turn frames (team=2)** — New rule using the looser `IsRealEvent` range (<400) instead of the `IsMidBattleEvent` cap (<200) when battleTeam==2. Combat-animation nameId aliasing doesn't apply on team-2 phases. Fixes Orbonne Vaults Loffrey scene (event 302). Commit `0cd2a37`.

- [x] **BattleDialogue detection for Ch2 formation-phase events** — New formation-phase rule: battleMode==1 + real event + rawLocation in range + slot9 != 0xFFFFFFFF. Catches Mandalia Brigade scene (event 16) that fires after auto_place but before the battle sentinels flip. Commit `9bd362c`.

- [x] **BattleChoice modal byte swap** — Old `0x140CBC38D` stopped firing on Ch2 Mandalia. Snapshot/diff surfaced the cluster `0x140D370xx`; picked `0x140D3706D` (cleanest 0→1). Live-verified: detection now hits BattleChoice when the 2-option modal is live. Commit `9bd362c`.

- [x] **EqA promote exclusion broadened** — Promote override (equipment-mirror-matches-Ramza triggers screen rename) now skips ANY Battle* screen, not just BattleStatus. Was incorrectly promoting BattleMyTurn / BattleEnemiesTurn / BattleAlliesTurn / BattleDialogue / BattleChoice to EquipmentAndAbilities because party-unit equipment sits in the mirror bytes throughout combat. Commits `0cd2a37`, `8b19fdb`.

- [x] **Ramza Ch2 Squire job name** — `CharacterData.GetRamzaJob(jobByte)` maps chapter-aware variants: 0x01→Squire (Ch2/3), 0x03→Gallant Knight (Ch4), 0xA0/0xA1 variants. Previously fell through to generic PSX JobNameById[1]=Chemist. `NavigationActions.CollectUnitPositionsFull` checks nameId==1 first. Commit `8b19fdb`.

- [x] **Active-unit isActive dedup** — When multiple battle-array slots share HP (e.g. Lv 1 Ramza + Lv 1 Delita both at 49/49 in Mandalia Ch2), the HP-only isActive check flagged both. After roster match, keep IsActive only on the unit whose RosterNameId matches the condensed struct's active nameId. Live-verified: scan reports `Ramza(Squire) (4,1)` without the ghost. Commit `c009a90`.

- [x] **WorldMap C-snap on arrival** — Auto-tap C after any `WaitForScreen=WorldMap` succeeds. Map cursor recenters on the player's current node. Skipped when the wait times out. Fixes the whole class of "travel targets the wrong place" bugs after battle_flee / Leave / Exit. Commit `c8e1fee`.

- [x] **Compact view: one-line ValidPaths + Helpers** — `execute_action` output now prints `ValidPaths: X, Y, Z` and `Helpers: a, b, c` on single lines. Descriptions moved to `screen -v` verbose mode. Commits `c8e1fee`, `730c53f`.

- [x] **LoS check threaded through basic Attack** — The skillset-ability path already annotated `ValidTargetTile.LosBlocked` via `LineOfSightCalculator` when the ability was a physical projectile (ranged Attack / Ninja Throw). The prepended basic Attack bypassed this annotation. Reused `ProjectileAbilityClassifier` for the ("Attack", "Attack", range>1) signature. Live-trace: 15 computed tiles, 11 flagged LosBlocked across a ridge. Commit `4c94e25`.

- [x] **Active-unit position lives at live grid cursor after move** — Battle-array slot +0x33/+0x34 holds pre-move coords for the whole turn. Active unit's GridX/Y now pulls from AddrGridX/Y (0x140C64A54 / 0x140C6496C). Non-active units still use slot bytes. Live-verified: battle_move (4,0)→(5,0) reports correctly on the next scan. Commit `2038019`.

- [x] **Live battle map-id byte 0x14077D83C** — 🎯 **The session headline.** Found via snapshot/diff across two real battles (Dugeura Pass MAP086 / 0x56 → Beddha Sandwaste MAP082 / 0x52). Eight main-module addresses flipped 86→82 in lockstep; picked the lowest. Wired as Try 0 before the locId-based lookups in NavigationActions. Live-verified across three maps (Dugeura, Beddha, Araguay) + survives restart/save-load. Replaces the `screen.location`-keyed lookups that drifted on random encounters (travel-to-Zeklaus → encounter-at-Dugeura → mod loaded MAP076 Zeklaus — the bug that triggered this whole hunt). Commit `9f87bfc`.

- [x] **Equipment IDs ARE live (TODO refuted)** — Live-read Ramza's roster slot +0x0E..+0x1B at Lv 1 Mandalia and cross-referenced with the in-game equipment panel. Non-sequential layout: +0x0E Helm, +0x10 Body, +0x12 Accessory, +0x14 Weapon, +0x16 LeftHand, +0x18 Unused, +0x1A Shield. `god_ramza` writes to these offsets produce Grand Helm / Maximillian / Bracer / Ragnarok / Kaiser Shield at the expected positions. **Caveat:** PartyMenu continuously syncs from a master store while open, clobbering live writes — memory note `feedback_partymenu_roster_sync.md` covers the gotcha. Commit `3a64900`.

**What made this session's wins stick:**

- **Snapshot+diff is the master key.** Two discoveries this session — BattleSequence runtime byte `0x1407774B4` and the live battle map-id `0x14077D83C` — came from the same technique: snapshot state A, cause a known change, snapshot state B, diff for exact target transitions. Both wins happened in under 20 minutes each after we committed to the technique. Earlier scoring heuristics (candidate pools, dimension-tightness tiebreakers) burned hours for worse results. Going forward: when stuck on a memory hunt with any ability to toggle the target value, snapshot+diff first.

- **User pushback shortens the path.** When I proposed a complicated scored-candidate map-resolver after finding `FindScenarioMapIdCandidates` returned 100+ hits, user said "why is this so difficult, I thought we were passing in the mapId" and immediately redirected us to find one authoritative byte. Single-byte result shipped in 20 minutes.

- **Extract-with-tests stayed valuable.** `MapResolutionPlanner` got 8 tests for a priority-decision function that's currently disabled but ready if we need to re-enable scenario-struct fallbacks. Easier to turn back on safely than to re-derive the priority order from scratch.


### Session 49 (2026-04-20) — master HP table discovery, BattleVictory detection, scan ghost-filter, dev tools

**Commits (8):**

- `8ec732e` — Ship kill_enemies + BattleVictory encA=255 sentinel ordering
- `9ddec07` — Fix scan undercount: ghost slots had inBattle=0 AND CT=0
- `154e4e1` — Add buff_all + execute_turn shell wrappers
- `d1f4d09` — Guard BattleVictory sentinel against battle-start encA=0xFF misfire
- `d44836d` — Fix Victory-with-Ramza-dying: battleTeam guards GameOver/Victory
- `8ee178c` — Add kill_enemies_hard shell wrapper + mark session-49 TODO progress
- `51f61e1` — Extend KillEnemiesPlanner with Reraise-clear + add revive_all
- `(handoff)` — Session 49 handoff — TODO cleanup + memory notes

**Shipped this session:**

- [x] **`kill_enemies` insta-win cheat — master HP table at `0x14184xxxx`** — Damage-diff hunt at Zeklaus found the master HP store: stride 0x200, per-slot `+0x00 u16 HP`, `+0x02 u16 MaxHp`, `+0x31` bit 5 (0x20) = DeadFlag. Writes persist (no re-derivation — session 48's battle-array `0x140893C00` is a MIRROR). `KillEnemiesPlanner` (pure, 9 tests) + `cheat_kill_enemies` dispatcher with broad-search anchor discovery. Per-battle table base shifts (observed `0x14184D8C0` and `0x14184E4C0`); search any player's `(HP, MaxHp)` 4-byte fingerprint, walk backward at stride 0x200 allowing interior empty slots. Live-verified 3 battles at Siedge Weald. Commit `8ec732e`.

- [x] **Scan undercount fix** — Battle array at `0x140893C00` had 8 valid-looking slots but only 3 had `inBattle=1`. Of the other 5, one was a real Bomb at (0,10) with `inBattle=0` + `CT=100`; four were ghost slots (`inBattle=0` + `CT=0`) left over from prior battles. New filter: accept if `inBattle==1 OR CT>0`. Live-verified 4-enemy Siedge Weald battle matches on-screen reality. Commit `9ddec07`.

- [x] **`buff_all` multi-party invincibility** — `cheat_mode_buff` with `pattern:"all"` iterates every roster-matched player-team slot. Reuses `BuffPlanner.PlanInvincibilityWrites`. Shell helper `buff_all [hp]` in fft.sh. Commit `154e4e1`.

- [x] **`execute_turn` shell wrapper** — Positional helper around the bridge action: `execute_turn moveX moveY [ability [tx ty [dir [--nowait]]]]`. Empty strings omit fields. Commands.md updated with examples. Commit `154e4e1`.

- [x] **BattleVictory `encA=255`/`encB=255` sentinel moved above LoadGame / TitleScreen / GameOver** — Session-45 note captured `encA=255` as unique Victory discriminator, but the rule was positioned below LoadGame which preempted it. Session-49 poll during live Victory caught the 30-tick timeline: `GameOver → TitleScreen → LoadGame` (misdetected) with `encA=255/encB=255` firing at t=6/7 (~1s banner window). Reordering the sentinel to fire first makes the banner detect cleanly. Commit `8ec732e`.

- [x] **`actedOrMoved` guard on Victory sentinel** — Battle-start first frame transiently reads `encA=0xFF` before being set to real values. Without the guard, fresh-battle returned `BattleVictory` on BattleMyTurn t=186ms. `actedOrMoved=true` is a reliable post-action signal that's false pre-first-turn. Regression tests pin both the positive case and the battle-start guard. Commit `d1f4d09`.

- [x] **`battleTeam` guard splits GameOver from Victory-with-Ramza-dying** — When a unit counter-kills the last enemy on the same frame it dies itself, the post-banner state shares paused=1, gameOverFlag=1, battleMode=0 with GameOver. Discriminator: `battleTeam` = the team of the unit that just acted. Player-triggered final action → team=0 (Victory); enemy killed the last party member → team=1 (GameOver). Live-captured GameOver encA=05 (not 255) confirms Victory sentinel stays safe. Commit `d44836d`.

- [x] **`kill_enemies_hard` shell wrapper** — Double-taps `kill_enemies` with a `battle_wait` between to catch undead that Reraise after the first pass. Commit `8ee178c`.

- [x] **KillEnemiesPlanner extended with Reraise-clear** — `KillEnemySlot` gained optional `BattleArraySlotBase` + `CurrentStatusByte2` fields. When set and the Reraise bit (0x20 at battle-array `+0x47`) is on, planner emits an extra clear-write preserving other status bits. Dispatcher builds a `(HP, MaxHp) → battle-array slot` fingerprint map; Reraise-clear is opt-in per slot (0 extra writes for non-undead). 3 new planner tests. Commit `51f61e1`.

- [x] **`revive_all` dev-tool helper** — Reverse of `kill_enemies` for recovering from ally wipes during testing. `KillEnemiesPlanner.PlanReviveAllies` writes `HP = MaxHp` + clears dead-bit for every dead (HP=0) player-team slot. Dispatcher reuses `kill_enemies` anchor-search with MaxHp-only matching (dead fingerprint is `(0, MaxHp)` so a HP-and-MaxHp match would miss dead players). Shell helper `revive_all` in fft.sh. 5 new planner tests. Commit `51f61e1`.

- [x] **Ghost GameOver rule tightened** — Added `battleTeam != 0` to the GameOver rule so it doesn't swallow Victory-with-dying-Ramza edges. Existing rule at `ScreenDetectionLogic.cs:583` kept paused+gameOverFlag+battleMode guards. Regression test updated to team=1 (was team=0 originally — that original test was actually reading a Victory fingerprint, not GameOver). Commit `d44836d`.

**Rejected / didn't work:**

- **Hammering HP=0 writes faster to beat re-derivation (session 48 carryover).** The 50×10ms hammer loop doesn't outrun the game's derivation tick. Answer is to find the MASTER, not to out-write the mirror. Master HP table at `0x14184xxxx` ends this problem for kill_enemies.

- **Walking the full `0x141800000..0x141900000` range at stride 0x200 without an anchor.** The master HP table base offset within that region is NOT a multiple of 0x200 from SearchMin — it starts at `+0xC0` or `+0xD8C0`. Stride-aligned enumeration from the search-region start misses the table entirely. Anchor-search + walk-out is the right pattern.

- **Hypothesis that GameOver also shows `encA=255`.** Thought it might, after observing `encA=05` post-Victory-banner and seeing `GameOver` label fire. Real GameOver captured mid-session (Ramza died) showed `encA=05` directly — so 255 is Victory-banner-window-only and the sentinel IS safe.

**Repeat-this patterns:**

- **Damage-diff for HP-related byte hunts.** Snapshot → deal known damage D → snapshot → diff → filter for u16 that decreased by exactly D, with range guard `100 ≤ oldValue ≤ 2000`. Session 48 found BattleSequence byte + map-id byte this way in 20 min each; session 49 found master HP store (`325 → 193`, D=132) on the first attempt.

- **Anchor-search with `broadSearch=true` for read-only main-module pages.** `SearchBytesInAllMemory` in narrow mode (default) only scans READWRITE private/mapped memory. Main module at `0x14184xxxx` is RX — narrow mode returns zero matches. `broadSearch: true` is mandatory for this region.

- **Live-poll during transitions to capture banner-window fingerprints.** Session-45 tried static snapshots and captured `encA=255`. Session-49 ran a 30-tick poll across the Victory banner transition and captured the full timeline: setup → banner → post-banner-clear. The timeline resolved the question of whether post-banner Victory still shows 255 (no — clears to 05).

- **Characterization tests for known-buggy behavior.** Session 49 wrote `DetectScreen_KnownBug_VictoryWithRamzaDying_MisdetectsAsGameOver` asserting current (wrong) output. The fix was then a one-assertion flip to `BattleVictory`. Plays well with strict TDD.

**Don't-repeat list:**

- **Don't blindly enumerate addresses at stride when the table base isn't stride-aligned from your scan start.** Anchor-search with a known value (e.g. player's HP fingerprint), then walk the stride from the matched address.

- **Don't rely on `GameOver` rule as the sole loss-vs-Victory discriminator post-banner.** Without the `battleTeam` guard, Victory-with-dying-Ramza mislabeled as GameOver for several seconds.

- **Don't set `+0x31` bit 0x20 on a unit and claim `+0x29` is the deathCounter without a control run.** The write may initialize the counter artificially — only a natural KO's post-death decrement confirms the byte is game-driven.

- **Don't trust scan's unit count without cross-referencing the master HP table.** The battle array has ghost slots; the master table is authoritative for who's actually alive.

**What made this session's wins stick:**

- **Master vs mirror is the key mental model.** Once I understood that `0x14184xxxx` is the MASTER and `0x140893C00` is a mirror (gets re-derived each frame), every failing session-48 write made sense. Writing to a mirror is writing to sand. This pattern probably repeats for other stats (status bytes, position, MP) — future hunts should assume "there's a master somewhere else" before giving up.

- **Damage-diff is load-bearing.** Two of this session's three memory finds (master HP store, Reraise bit verification) came from the snapshot/diff technique. Fast, reliable, directly answers the question. Scoring heuristics and AoB scanning can't compete when the target byte has a direct known-delta behavior.

- **Strict TDD clicked for detection fixes.** The Victory-battleTeam guard shipped as: (1) characterization test asserting wrong behavior, (2) impl change, (3) flip the assertion, (4) add positive + negative regression tests. Takes longer per feature than "change code → pray" but the regression coverage is real and the next edit is safer.

- **Live-poll capture over static snapshot.** A 30-tick poll across a Victory transition showed things a single post-event snapshot would miss: the `encA=0xFF` banner window, the battle-start `encA=0xFF` false-positive, the `FF FF FF FF FF FF FF FF` all-FF banner signature. Multi-tick polling is cheap (~15s) and surfaces timing-sensitive state the detection rules have to handle.

---

### Session 50 (2026-04-20) — Detection + dev tools: Gariland Victory, attack-tile VR fix, swap_unit_to, kill_one, scan_snapshot/diff

**Commits:**
- `68b29a8` — Fix attack-tile VR=0 for ranged weapons + Gariland Victory detection
- `2f4539c` — Ship swap_unit_to, kill_one, scan_snapshot/scan_diff dev tools
- `cf45864` — Pin regression tests for scan-move learned-ability scoping

**Completed items moved from TODO.md:**

- [x] **BattleVictory misdetects as BattleDesertion on Gariland post-battle** — Shipped encA=255 Victory sentinel at top of `inBattle` branch, firing BEFORE `postBattlePausedState → Desertion`. Regression test `DetectScreen_Victory_Gariland_EncA255_WinsOverDesertion`. See `memory/project_gariland_victory_fix.md`.

- [x] **Attack-tile calculator 12 vs 18** (carryover from session 48) — Root-caused: `ItemData.BuildAttackAbilityInfo` hardcoded `VRange=0` for all weapons, falling back to caster Jump in `AbilityTargetCalculator` and rejecting elevated targets. Fixed by setting `VRange=99` for bow/gun/crossbow. Melee keeps `VR=0` so jump fallback still bounds them. See `memory/project_attack_vr0_bug.md`.

- [x] **`swap_unit_to <name>` shell wrapper** — Shipped bridge action + shell helper that cycles viewed unit on nested party-tree screens (CharacterStatus/EqA/JobSelection/pickers) via Q/E keys. Uses existing `UnitCyclePlanner`.

- [x] **Extract scan-snapshot + scan-diff bridge actions** — Shipped `UnitScanDiff` pure planner (15 tests) + `scan_snapshot <label>` / `scan_diff <from> <to>` bridge actions + shell wrappers. Identity-match by name → rosterNameId → pre-snapshot position fallback.

- [x] **Live-verify `buff_ramza` on a fresh-game battle** — PASSED at Siedge Weald. `scan_move` showed Ramza HP=999/999 post-buff. Writes landed on first player slot correctly.

- [x] **Extend `cheat_mode_buff` to buff all party members** — `pattern:"all"` mode + shell `buff_all [hp]` wrapper shipped. Iterates every roster-matched team=0 slot.

- [x] **Scan learned-ability filter audit (session 44 bug)** — Filter logic verified correct. Root cause was upstream job-byte resolution, fixed in commit `8b49cb4` (session 48). Session 51 pinned regression tests: `GetRamzaJob` chapter-byte map + skillset ability counts (Fundaments=4, Mettle=9).

- [x] **`scan_move` reports entire skillset, not learned-only abilities** — Dupe of above; fixed in commit `8b49cb4`. Regression tests pinned.

- [x] **Investigate 0xB0 in cast queue** — Confirmed 0xB0 is a slot-state tag (charging). Second-spell test (Curaja vs Protect) showed identical `01 B0 | 0A B0 | 09 B0` pattern regardless of ability. Session 51 observed fire transition: record 1's byte[1] goes 0xB0→0x70 when cast fires. Records 2-3 are NOT live queue slots.

---

### Session 51 (2026-04-20) — 10 more tasks: AbilityCursorDeltaPlanner wire + scan_diff JSON

**Commits:**
- `32d733d` — Wire AbilityCursorDeltaPlanner + add structured scan_diff events
- `43f6197` — Pin regression tests + close stale TODO items

**Completed items moved from TODO.md:**

- [x] **Wire `AbilityCursorDeltaPlanner.Decide` into ability-list scroll loop** — Wired into the Down-scroll retry path in `NavigationActions.cs`. Naive `delta != expected` check replaced with planner's sign-match + magnitude-guard + 3×-expected-guard rules. Falls back to blind scrolling on untrusted deltas. Session 31 Up-wrap explosion is the regression guardrail.

- [x] **BattleSequence detection over-fires after restart at story location** — FIXED session 48 via swap to runtime u32 `0x1407774B4` (==2 when minimap panel open, ==1 on plain WorldMap). TODO entry was stale.

- [x] **Cutscene misdetects as LoadGame after GameOver** — Fixed pre-session-52: LoadGame rule at `ScreenDetectionLogic.cs:557` now requires `IsEventIdUnset(eventId)`. Session 52 pinned regression tests.

- [x] **AutoEndTurnAbilities.SelfDestruct regression test** — Added allow-list theory pinning `Jump` + `Self-Destruct`. New name additions now force test update. Also added negative theory for lookalike names.

---

### Session 52 (2026-04-20) — Shop stock partial crack + party audit + ranged attack live-verified

**Commits:** (no code commits — investigation session, memory notes only)

**Completed items moved from TODO.md:**

- [x] **Live-verify ranged attack VR=99 fix** — PASSED at Siedge Weald. Wilham with Blaster (gun HR=8) shows `totalTargets: 49` — unconstrained by zDelta, confirming session-51 fix works in-game.

- [x] **Party audit** — Surfaced gear gaps across 15-unit roster. Biggest: Orlandeau (Lv88 Thunder God) with a plain Broadsword. Mustadio/Reis/Cloud/Construct 8 unequipped.

---

### Session 53 (2026-04-20) — 🎯 Shop stock bitmap CRACKED

**Commits:** (no code commits — investigation session, memory notes only)

**Key discovery: Dorter Ch1 Outfitter weapons stock at `0x5C7C2880`:**
- 8-byte weapon bitmap, bit N of byte B = FFTPatcher item ID `(B*8 + N + 42)`
- u32 count at `+0x08`
- 128-byte record stride, multiple shops observed in same region
- Verified: Dorter's `00 06 76 00 00 00 00 00` bitmap decodes to exactly the 7 visible weapons

See `memory/project_shop_stock_CRACKED.md`.

**Not done (next-session priorities):**
- Find Gariland's shop record. Gariland sells daggers (IDs 1-7) which can't fit in +42-offset 8-byte bitmap. Either (a) different offset per-shop, (b) different bitmap per ID range (bmp for IDs 0-63, bmp for 64-127), or (c) totally different record structure.
- Find the mapping from `location_id` → record_address. Dorter row's `+0x00 = 05` didn't match location ID 9 directly.
- Find shields/helms/body/accessory/consumable bitmaps (likely separate regions or separate bmp fields in same record).

**Technique that worked:** compute the EXPECTED bitmap pattern given the known visible stock (IDs 51, 52, 59, 60, 62, 63, 64 → `00 06 76 00 00 00 00 00` with offset +42) and search for that exact byte string. Single match at `0x5C7C287B` — cracked in one shot after 4+ prior sessions failed by searching flat ID arrays / price sequences.

**Technique that didn't work (don't-repeat, for Gariland):**
- Same-offset search with dagger IDs: daggers 1-7 don't fit in 8 bytes at offset +42 (negative positions).
- Various offset brute-force search (-8, -7, -1, 0, +1, +8): 0 matches with row signature context.
- Contiguous dagger prices (u32 or u16): 0 matches — prices not stored linearly.
- `{u16 id, u32 price}` AoB: 0 matches.

---

### Session 54 (2026-04-21) — 🎯🎯🎯 Shop stock decoder shipped: ALL 6 Outfitter categories working

**Commits:**
- `b0bf242` — Ship shop_stock decoder — all 6 Outfitter categories from memory

**Headline:** End-of-game save with full city access enabled a tour of every Chapter-1 Outfitter Buy across all 6 tabs at Dorter + Yardrow (plus spot checks at 13 others). Three distinct record formats cracked; auto-mode works for 14 of 15 settlements × (up to) 6 categories. Only Goug's 8-item weapons tab still has the Mythril Gun gap. +77 new tests (3853 total passing).

**Key discovery — 3 record formats cover all Outfitter stock:**

| Format | Stride | Categories | Offset | Example |
|---|---|---|---|---|
| **Bitmap8** | 8-byte bitmap + u32 count | Weapons (staves/ranged), Consumables | 42, 240 | `00 06 76 00 00 00 00 00` (7 staves) |
| **Bitmap4** | 4-byte bitmap + u32 count | Daggers, Body, Accessories | 1, 186, 208 | `7F 00 00 00` (7 ids) |
| **IdArray** | 8 u8 ids, 0-terminated | Shields, Helms | 0 (direct ids) | `80 81 82 83 84 85 86 00` |

**The same physical Bitmap4 record at `0x3E4FFC0`** (pattern `7F 00 00 00 07 00 00 00`) is shared across daggers + body + accessories — only the category-offset differs (1 / 186 / 208), producing 3 different 7-item ID lists from 1 record.

**Coverage — weapons (14 of 15 shops):**
- **Staves shops** (bitmap `00 06 76 00 00 00 00 00` + offset 42): Yardrow(7), Gollund(8), Dorter(9), Zaland(10), Warjilis(12), Bervenia(13), Sal Ghidos(14). All 7 sell the same 7 items (Rod/Thunder Rod/Oak/White/Serpent/Mage's/Golden Staff). Dorter/Yardrow/Gollund/Bervenia/Sal Ghidos carry Ch1-discount prices; Zaland/Warjilis carry end-game.
- **Dagger shops** (bitmap `7F 00 00 00` + offset 1): Lesalia(0), Riovanes(1), Eagrose(2), Lionel(3), Limberry(4), Zeltennia(5), Gariland(6). All 7 share the single Bitmap4 record; Ch1 discount pricing identical across all 7 shops (100/200/300/700/1500/2500/4000).

**Coverage — non-weapons (registered for all 15 settlements):**
- **Helms (hats)** — ids 157-163 via IdArray. Dorter + Yardrow live-verified; others registered by analogy.
- **Body** — ids 186-192 via Bitmap4 at offset 186. Dorter + Yardrow live-verified.
- **Accessories** — ids 208-214 via Bitmap4 at offset 208. Dorter + Yardrow live-verified.
- **Consumables** — ids 240-244, 246 via Bitmap8 at offset 240. Dorter + Yardrow live-verified. Bit 5 (Elixir, id 245) intentionally skipped in Ch1 stock.

**Key files shipped:**
- `ColorMod/GameBridge/ShopStockDecoder.cs` — pure decoder with `DecodeBitmap` + `DecodeIdArray` + `LocateBitmapRecord` + `LocateIdArrayRecord`. RecordFormat enum + per-category format/offset mapping.
- `ColorMod/GameBridge/ShopBitmapRegistry.cs` — 73 entries seeded across 15 shops × 6 categories (Goug weapons + non-Dorter shields excluded).
- `ColorMod/GameBridge/ChapterShopPrices.cs` — Ch1 discount overrides for staves shops + dagger shops + Dorter Bronze Shield. Static-ctor loop populates 49 dagger-shop price entries.
- `ColorMod/Utilities/CommandWatcher.cs` — `shop_stock` bridge action. Auto-mode (no args) reads location byte `0x14077D208`, defaults chapter=1, probes all 7 categories via registry. Manual mode (pattern supplied) seeds new shops.
- 77 new unit tests across `ShopStockDecoderTests` / `ShopBitmapRegistryTests` / `ChapterShopPricesTests` / `StrictModeTests`.

**Auto-mode live verification (Yardrow Ch1):**
```
shop_stock Weapons [auto(loc=7,ch=1)] — 7 staves (Rod/Thunder/Oak/White/Serpent/Mage/Golden)
shop_stock Helms [auto(loc=7,ch=1)] — 7 hats (Leather Cap/Plumed/Red/Headgear/Wizard/Green/Headband)
shop_stock Body [auto(loc=7,ch=1)] — 7 clothing (Clothing/Leather*2/Ringmail/Mythril/Adamant/Wizard Clothing)
shop_stock Accessories [auto(loc=7,ch=1)] — 7 shoes (Battle/Spiked/Germinas/Rubber/Winged/Hermes/Red)
shop_stock Consumables [auto(loc=7,ch=1)] — 6 items (Potion/Hi-Potion/X-Potion/Ether/Hi-Ether/Antidote)
```

All prices match in-game display exactly.

**Not done (see `TODO.md` §0 session-54 follow-ups):**
- Verify shields at 13 non-Dorter shops (Yardrow confirmed EMPTY — not all shops carry shields).
- Verify helms/body/accessories/consumables at 13 non-verified shops (registered by analogy).
- Goug 8-item weapons tab: 7-bit bitmap exists but Mythril Gun (8th, id 72) missing. Likely requires heap pointer-chain walk or a second "chapter upgrade" record not yet found.
- Wire `screen.stockItems` on OutfitterBuy so every shop response includes decoded stock.
- Chapter byte hunt (auto-mode defaults chapter=1 until found).

**Technique that worked:** dump the active-widget region after navigating to each tab, search for the first 7 bytes of expected-IDs-or-bitmap followed by a 0-terminator or count field. Heap widgets shift addresses per tab-switch but the byte signature is searchable every time. Two-phase narrow+broad AoB search in `LocateBitmapRecord` / `LocateIdArrayRecord` handles both private-heap and memory-mapped regions.

**Technique that didn't work (don't-repeat):**
- Searching for shop-stock as flat `{u16, u32 price}` records: 0 matches everywhere.
- Searching for contiguous price sequences: prices computed from ItemData at render time, not stored flat.
- Assuming one record format covers all 6 categories: each category uses its own format + offset. Decoder dispatch required.
- Chasing `0D`-tagged static records past the first few: weapons subtable is well-defined at `0x5Cxxxxxx` but other categories don't follow that structure.

**Memory notes updated:**
- `project_shop_stock_SHIPPED.md` — full write-up of 3 formats + coverage + calling patterns.

### Session 55 (2026-04-22) — `screen.stockItems` wired + crash-hardened + Ch1 mismatch surfaced

**Commits (oldest first):**
- `a5e545e` — Wire screen.stockItems on OutfitterBuy + harden decoder
- `5cd202c` — Cache resolved shop record addresses to stabilize stockItems
- `d5add59` — Stop screen.stockItems from crashing the game
- `5485987` — Cache-only screen path + seed_shop_stock helper
- `746144a` — Add LiveShopScanner experiment (opt-in, --description=auto)

**Headline:** `screen.stockItems` now auto-populates on OutfitterBuy via cache; live verification at Ch1-early Dorter revealed the registry's "Ch1" data is actually for a later chapter state (real Ch1-early has 1-2 items per equipment tab + a different consumables set). Decoder math is correct — the architectural fix (active-widget pointer-chain walk) is the next-session investigation target.

**What landed by theme:**

- **`screen.stockItems` field** (`a5e545e`): new `Dictionary<string, List<ShopStockItem>>` on `DetectedScreen`, auto-populated on OutfitterBuy from a new `ShopStockResolver.DecodeAll`. Catalog ordered Weapons → Daggers → Shields → Helms → Body → Accessories → Consumables. JSON shape stable for shell/Claude callers.

- **Decoder hardening** (`a5e545e`): two bugs surfaced + fixed during 15-shop tour:
  - Bitmap4 phantom items (Gariland Accessories saw count-byte `0x07` decoded as bits 0-2 → phantom IDs 240/241/242). Fix: `DecodeStockAt` zeroes high 4 bytes on Bitmap4 before decoding.
  - False-positive locate hits (Lesalia/Warjilis Consumables saw Vesper + Sagittarius Bow appear from a transient memory region). Fix: `DecodeStockAt` accepts `expectedCount` and rejects mismatches via new public `ValidateAgainstExpected` helper. Both `shop_stock` action and resolver pass it.

- **Address cache** (`5cd202c`): `ShopStockResolver` caches resolved record addresses keyed by `(location, chapter, category)`. Cache-hit path re-reads from cached address; validator invalidates and re-locates if bytes shift. Stabilized 9/10 polls returning all 6 categories at Dorter (vs. flapping pre-cache).

- **Crash hardening** (`d5add59`): three defenses against repeated polling crashing the game:
  - Negative cache + 30s back-off: failed locates suppressed for 30s.
  - Per-call cold-locate budget: `DecodeAll` defaults to at most 1 cold (cache-miss) AoB scan per invocation.
  - `narrowOnly` mode on `LocateBitmapRecord` and `LocateIdArrayRecord`: skips ~500 MB broad memory-mapped scan. Resolver path always uses it; dedicated `shop_stock` keeps both phases for first-time discovery and seeds the resolver cache via `ShopStockResolver.SeedCache`.

- **`seed_shop_stock` bridge action + cache-only screen path** (`5485987`): screen-assembly path does ZERO AoB scans. New action sweeps every registered category at the current shop, runs broad-search locate once each, and seeds the cache so subsequent screen reads are pure cache hits. Pre-fix: game crashed within ~5-10 polls. Post-fix: game survived 20+ polls plus seeding.

- **`LiveShopScanner` experiment** (`746144a`): new `ColorMod/GameBridge/LiveShopScanner.cs` scans the active-widget heap region (`0x15A000000..0x15D000000`) for byte sequences matching `[bitmap N bytes][count u32]` to find live shop records WITHOUT pre-baked registry data. Opt-in via `seed_shop_stock description=auto`. Currently too noisy — count-anchor `01 00 00 00`..`08 00 00 00` matches random heap integers. Kept behind opt-in flag for future iteration.

**Live verification summary:**
- All 15 Outfitter shops visited at end-game state. Decoder returns correct items + Ch1 prices for the registered (late-Ch1) data.
- Ch1-early Dorter live test: 5 tabs all return WRONG counts via auto-mode (registry has the wrong stock for early-Ch1). Manual `shop_stock pattern=...` returns the right items when given the right bitmap. Verified Ch1-early Dorter Weapons (`00 02 02 00 00 00 00 00` → Rod+Oak Staff) and Consumables (`C1 27 00 00 00 00 00 00` → 7 status cures: Potion, Antidote, Eye Drops, Echo Herbs, Maiden's Kiss, Gold Needle, Phoenix Down).

**Tests:** 3878 passing (+25 this session: 6 registry enumeration, 10 resolver bit-counting, 4 validation-contract, 2 cache-state, 1 Bitmap4 raw-decode regression pin, 2 serialization).

**Things that DID work (repeat-this list):**
- Manual `shop_stock pattern=<hex>` to verify a known-bitmap signature finds the live record. Direct way to compare in-game stock to memory.
- Cache-on-success + negative-cache + per-call budget keeps the bridge alive under repeated polling. The screen path doing zero scans isolates stockItems' stability from polling cadence.
- Validating decoded count against registry expected count rejects false-positive locates fail-safe (returns empty rather than wrong data).
- Computing the bitmap by hand from the in-game item list is reliable: `id - offset` gives bit position; verify in-memory via `shop_stock pattern=<hex> searchValue=<count>`.

**Things that DIDN'T work (don't-repeat list):**
- Pre-baking bitmaps in `ShopBitmapRegistry` per chapter — registry data turned out to be for a LATER chapter state than expected, breaking early-game shops on every category.
- Auto-locating from the screen-assembly path (cumulative ~150 MB scans per poll across cache-miss categories crashed the game). Disabled.
- Loose count-anchor heuristic (`01 00 00 00`..`08 00 00 00`) for finding shop records in the active-widget heap. Too many random heap integers match; need a proper structural anchor (vtable + offset) instead.
- `scrape_shop_items` (FString-based item-name reader) at Ch1 Dorter OutfitterBuy: catches dialogue fragments and command echoes, no item names. Vtable discovery requires a known header string ("Weapons") which isn't visible in this UI state.
- Reading multiple `shop_stock` results in quick succession: each call writes to `response.json`; subsequent reads race with later writes. Wait at least 1-2s between calls or accept the latest.

**Memory notes added:**
- `project_shop_stock_active_widget_hunt.md` — vtable signatures from session 54 + scanner experiment results + concrete next-session steps.
- `project_ch1_dorter_actual_stock.md` — verified bitmaps for Ch1-early Dorter all 6 categories.
- `feedback_shop_stock_screen_path_must_not_scan.md` — screen-assembly path crashes the game when it does AoB searches; cache-only is the contract going forward.
- `feedback_response_json_race.md` — bridge response.json race when chaining multiple bridge calls.


### Session 55 — battle automation slice (2026-04-22) — scan_diff identity + AOB cursor experiment + ui correction

**Commits (oldest first, kept):**
- `d2c60a9` — Split battle-vs-deferred work in TODO; archive completed items
- `6f70d11` — Disambiguate scan_diff identity via class fingerprint + scan-order rank
- `9f57894` — Add find_monotonic memory-scan action for high-confidence cursor hunts
- `ae34ed9` — Add AbilityListCursorNavPlanner pure planner (12 tests)
- `cb3967c` — Wire EffectiveMenuCursor into screen.UI rendering

**Commits (oldest first, REVERTED):**
- `7def3c2` — Add ResolveAbilityListCursor + FindDeltaSequenceCandidates → reverted in `35e1e5f`
- `892f979` — WIP: ResolveAbilitiesSubmenuCursor + nav rewrite using real cursors → reverted in `8e72b76`
- `b4d7d98` — Ship silent AOB resolver for ability-list cursor → reverted in `a05037d`

**Headline:** Locked in two real wins (scan_diff duplicate-name fix, post-cast `ui=Move` correction) and one infrastructure win (`find_monotonic` + planner). Spent ~2h chasing a silent AOB-based ability-list cursor resolver — found the AOB pattern, shipped it, succeeded once on first cast, then it consistently failed because the cursor's offset within the widget shifts between widget allocations. Reverted all three resolver commits; ability nav back to pre-S55 state. Memory note `project_ability_list_cursor_addr.md` documents the exact failure mode so future sessions don't redo the same dead end.

**What landed by theme (kept):**

- **scan_diff duplicate-name fix** (`6f70d11`): `UnitScanDiff.Key(u)` now combines name (or rosterNameId) with the 11-byte heap fingerprint at +0x69 when present. New `KeyInList` layers a scan-order `#N` suffix when multiple units in the same scan share a base key, so identical-fingerprint duplicates stay distinguishable across before/after snapshots. **Live-verified at Yardrow random encounter:** three Black Goblins on field, end Ramza's turn, two goblins move (2,6)→(4,6) and (1,9)→(3,10). Pre-fix this emitted 4 events (remove+add per goblin); post-fix emits 2 `moved` events as intended. Third goblin separately died on Lloyd's counter and correctly surfaced as `removed` with a new spawned ENEMY `added` at (6,10). 3 new unit tests pin: same-fingerprint anonymous duplicates, same-job-name duplicates with shared fingerprint, and same-name units with DIFFERENT fingerprints.

- **`find_monotonic` bridge action** (`9f57894`): `MemoryExplorer.FindMonotonicByteCandidates(labels[], values[])` finds heap bytes whose values across N snapshots match an expected sequence (e.g. `[0,1,2,3,4]`). 5-snap monotonic has ~256^4 odds against random match vs ~2,000-to-1 for a 3-snap toggle. Bridge action: `pattern`=labels CSV, `to`=expected u8 values CSV. Used live to find the ability-list cursor mirrors at `0x13034F920+` (5 candidates clustered in the same widget allocation).

- **`AbilityListCursorNavPlanner`** (`ae34ed9`): 12 unit tests, pure function. `Plan(currentIdx, targetIdx, listSize)` returns optimal `(direction, count)` — Down or Up, whichever is fewer presses with wrap. Tie-breaker Down. Ready to wire whenever a reliable cursor source exists.

- **`EffectiveMenuCursor` in `screen.UI`** (`cb3967c`): The action-menu cursor byte at `0x1407FC620` has a 1-frame stale-read race after auto-advance — post-move the game UI shows "Abilities" while memory still reads 0; post-cast (no move) the game shows "Move" while memory still reads 1. The corrector that detects this via `moved`/`acted` flags has existed since session 30 but was only applied in the nav code path — the `screen.UI` renderer read `screen.MenuCursor` directly and surfaced the stale value to Claude as `ui=<wrong-item>`. Now the renderer uses `EffectiveMenuCursor` too. Live-verified post-Cure-queued state: memory reads 1 (Abilities) but corrector returns 0 (Move).

**Things that DIDN'T work (don't-repeat list):**

- **AOB-based cursor resolvers** — found the signature `<listSize_u64> + 0x1407FC6D8 vtable` (16 bytes), worked once on a fresh restart (first `battle_ability "Cure"` queued correctly), then failed on subsequent attempts because the cursor's offset within the widget isn't structurally fixed. Sometimes cursor is at +0x10 from the AOB match; sometimes a second-vtable pointer pushes it further; sometimes cursor isn't in the dump window at all. Reverted entirely. **Don't re-attempt without first solving the structural-offset instability** — see `project_ability_list_cursor_addr.md` "What to try NEXT" for the three remaining strategies (pointer chains, brute search + verification, escape-to-known-state).

- **Keypress-oscillation submenu/list resolvers** (the fallback path): `ResolveAbilityListCursor` / `ResolveAbilitiesSubmenuCursor` did Down×4 + Up×4 + 5-snap monotonic + cluster picker + live-verify. Picked noise candidates roughly 50% of the time (`PickClusterRepresentative` favored a tight cluster but a noise byte coincidentally sat in a tighter cluster than the real mirrors). Verify-before-Enter caught the resulting bad picks but the user couldn't actually cast anything. Reverted.

- **Cluster-based candidate picker** (`PickClusterRepresentative`): assumed real cursor mirrors form the largest tight cluster of UE4-heap addresses (within 0x100). True some sessions, false others — noise can form tighter clusters. Discriminator (heap pointer at +8 from match) helped one case but isn't bulletproof.

**Things that DID work (repeat-this list):**

- **Live-verifying the user-visible behavior, not just the bridge response.** AOB resolver returned `Queued Cure` in the response but the ACTUAL game state was Attack-targeting. Always cross-check with screenshot or user confirmation.

- **5-snap monotonic + visual confirmation** as a manual cursor-byte hunting tool. Faster + lower noise than 3-snap toggle for finding cursor-like memory.

- **Class fingerprint as a secondary identity key for scan_diff.** The 11-byte heap bytes at +0x69 disambiguate same-named units cleanly. Generalizable pattern for any "many of the same enemy on field" scenario.

- **Reverting on the same day rather than half-shipping.** When the AOB experiment didn't pan out across two restarts, the right move was three `git revert`s + memory note + commit. Branch is back in a known-good state without the sunk-cost trap.

**Memory notes updated:**
- `project_ability_list_cursor_addr.md` — full S55 record: addresses cracked, AOB pattern, why AOB failed structurally, three options to try next.

---

### Session 57 (2026-04-22) — speed pass + lifetime stats + Aurablast fix

**16 commits.** Battle-turn cycle is now dramatically faster across every action. Cumulative speedups: Shout 9010 → 4831ms (-46%), scan_move 289 → 167ms (-42%), keys 53 → 28ms (-47%), battle_wait median 4140 → 2855ms (-31%). Lifetime stat tracker wired. `session_stats` tool ships the slow-culprit report. Aurablast data bug fixed.

**Commits (in order):**

- `b9a6c59` — Wire lifetime stats foundation + `stats` shell helper
- `3a95585` — Auto-scan on `screen` query when landing on fresh BattleMyTurn
- `e07b3ab` — Add `session_stats` — per-action latency summary
- `51759bd` — Fix shell unescape for `stats` and `session_stats`
- `662ccb3` — Speed up battle_ability; guard against double-act / double-move
- `b5c8a71` — Speed up battle_wait setup: 4140ms median → 3061ms (-26%)
- `14ff34f` — Speed up battle_attack setup + adaptive animation wait
- `a54e943` — Speed up battle_move: 3922ms → 2363ms for short moves (-40%)
- `1d4b94f` — Tighten DetectScreenSettled: 150ms → 60ms minimum
- `dc13cbe` — Cut key-press hold time: 50ms → 25ms (compounds across every action)
- `49f6503` — Skip pre-action scan when turn already scanned; add cast-verify guard
- `433d8c7` — battle_ability: merge Step 1+3 into unified poll-retry
- `d9f143a` — Fix Aurablast incident: use FULL skillset list for nav, not learned-filtered
- `c30ca29` — Tighten per-press delays: NavigateMenuCursor, submenu, ability-list
- `8ffb292` — Tighten post-action poll intervals: 100ms → 40ms
- `f1e091d` — Tighten bridge polling fallback: 50ms → 20ms

**Features shipped:**

- [x] **Auto-scan on Battle_MyTurn** (`3a95585`) — screen-query responses auto-scan on fresh BattleMyTurn. Saves a round-trip per turn. Gated on `_turnTracker.ShouldAutoScan` (at most once per friendly turn). Fires only after `DetectScreenSettled` returns so the prior "Reset Move" bug doesn't recur.

- [x] **Latency measurement + `session_stats`** (`e07b3ab`, `51759bd`) — new pure `SessionStatsCalculator` (10 TDD tests) reads the session's JSONL log and emits per-action-type stats sorted by max-latency-desc. Bridge action + `session_stats` shell helper. Immediately surfaced real bugs (Shout 9s, auto-scan-during-formation).

- [x] **Lifetime stats foundation + `stats` shell helper** (`b9a6c59`) — `BattleStatTracker` (previously orphan, 160 lines of tests, never instantiated) wired into `ModBootstrapper`. Save path `claude_bridge/lifetime_stats.json`, loaded at startup. New pure `BattleLifecycleClassifier` (27 TDD tests) edge-triggers StartBattle / EndBattleVictory / EndBattleDefeat from screen transitions. `OnTurnTaken` hook on `battle_wait` credits the active unit. New bridge actions: `render_lifetime_summary`, `render_battle_summary`. Shell: `stats` / `stats battle`. Detailed per-action hooks (damage/kills/etc.) are the remaining work — tracked as S57 follow-up.

- [x] **Post-battle summary display** (`b9a6c59`) — the tracker's `RenderBattleSummary` + `RenderLifetimeSummary` (pre-existing code) now reachable via bridge + shell helper. `stats battle` shows MVP + per-unit contributions.

**Speed pass shipped (compound cuts across the whole battle cycle):**

- [x] **battle_ability speed** (`662ccb3`) — 6 fixed-sleep cuts + new `WaitForActionResolved` accepts terminal states (Victory/Defeat/EnemyTurn/AlliesTurn/GameOver). Shout 9010 → 6107ms in Victory-ending case.

- [x] **battle_wait speed** (`b5c8a71`) — 4 setup sleeps trimmed; poll interval 300 → 150ms. Median 4140 → 3061ms (-26%).

- [x] **battle_attack speed + adaptive animation wait** (`14ff34f`) — 5 setup sleeps trimmed + biggest win: post-attack `Thread.Sleep(2000)` replaced by poll for post-animation resolved state. Saves 800-1300ms per attack.

- [x] **battle_move speed** (`a54e943`) — 500ms post-Enter → 300ms, 500ms post-F → 150ms. 3922 → 2363ms on short moves.

- [x] **DetectScreenSettled floor cut** (`1d4b94f`) — 150ms (3× 50ms stable) → 60ms (2× 30ms stable). Compounds on every response. scan_move 289 → 172ms.

- [x] **Key-press hold time** (`dc13cbe`) — `InputSimulator.SendKeyPressToWindow` key-down → key-up interval 50ms → 25ms. keys median 53 → 28ms (-47%). Compounds across every menu nav.

- [x] **Skip pre-action scan when turn already scanned** (`49f6503`) — new `BattleTurnTracker.WasScannedThisTurn` property. When caller already ran `screen` (which auto-scans), battle_move/ability/attack skip their pre-action scan. Saves ~170ms per action = ~340ms per 2-3-action turn.

- [x] **Pre-cast ability verification guard** (`49f6503`) — reads ui= right before Enter in battle_ability. If ui= is recognizable and doesn't match the requested ability, fails with clear error instead of casting wrong thing. Best-effort: ui= isn't always populated on ability-list states, so null proceeds; clear mismatch aborts.

- [x] **Already-acted / already-moved guards** (`662ccb3`, `a54e943`) — battle_ability/attack refuse up-front when `BattleActed==1` with "You've already acted this turn. You cannot perform another action." battle_move refuses when `BattleMoved==1`. Fast-fail instead of stalling in a grayed menu.

- [x] **battle_ability Step 1+3 merge** (`433d8c7`) — unified poll-with-retry replaces old fixed-sleep + one-shot check + expensive retry. Fast case: ~150-300ms (vs 650ms). Retry case: ~900ms (vs 2000ms).

- [x] **Per-press delay cuts** (`c30ca29`) — NavigateMenuCursor 150 → 80ms, submenu Down 200 → 100ms, ability-list nav 150 → 80ms. Compounds across every menu-heavy action.

- [x] **Post-action poll interval cut** (`8ffb292`) — `WaitForTurnState` / `WaitForActionResolved` 100 → 40ms. 2.5× faster state-change detection.

- [x] **Bridge polling fallback cut** (`f1e091d`) — CommandWatcher fallback poll 50 → 20ms. Self-target post-cast `Thread.Sleep(300)` → 150ms.

**Correctness fix:**

- [x] **Aurablast incident** (`d9f143a`) — `battle_ability "Chakra"` on Kenrick was casting **Aurablast** instead. Root cause: `GetAbilityListForSkillset` returned a learned-filtered list but the game's ability submenu shows every ability of the skillset (unlearned ones greyed but still occupying their slot). Kenrick's filtered Martial Arts was 4 entries; the game's full list is 8. Chakra at bridge-index 2 was game-index 6. Down×2 landed on Aurablast. Fix: return the full canonical skillset list; learned-filtering remains correct for the user-facing `abilities[]` array (per-unit decision aid).

  **Live-verified 7/7 across units/skillsets/depths:** Aurablast on Dryad (Lloyd, MA idx 2), Haste on self (Wilham, TM idx 0), Tailwind on Lloyd (Ramza, Mettle idx 3), Protect on Ramza (Kenrick, WM idx 8), Slowja on Dryad (Wilham, TM idx 3), **Doom Fist on enemy** (Lloyd, MA idx 4 — previously entirely absent from filter), **Holy on undead Bonesnatch** (Kenrick, WM idx 14 — deepest index).

**Measured speedups (baseline → after S57):**

| Action | Before | After | Delta |
|---|---|---|---|
| `battle_ability` Shout | 9010ms | **4831ms** | **-46%** |
| `battle_wait` median | 4140ms | **2855ms** | **-31%** |
| `scan_move` | 289ms | **167ms** | **-42%** |
| `keys` | 53ms | **28ms** | **-47%** |
| `session_stats` | 164ms | **44ms** | **-73%** |
| `battle_move` (short) | 3922ms | **2363ms** | **-40%** |

**Technique discoveries worth propagating:**

- **Key-press hold time 50 → 25ms** was the single highest-compound cut. 60fps is 16.7ms/frame; 25ms is ~1.5 frames, well above the game's key-read threshold.
- **`WaitForActionResolved` > `WaitForTurnState`** for post-cast settles — accepts terminal states (Victory/Defeat/EnemyTurn/etc.) so we don't pay a 2000ms timeout when the cast ended the battle.
- **Full-skillset-list nav** is the permanent rule — the game's ability submenu shows every ability of the skillset (unlearned greyed). Filtering corrupts index math.
- **Stacked-settle pattern:** `SendKey + Thread.Sleep(500) + poll-for-state-change` is redundant. Let the poll be authoritative; keep a minimal (150ms) input-debounce floor.
- **`session_stats` IS the bug-finder.** Sort-by-max surfaces outliers directly.
- **`_turnTracker.WasScannedThisTurn` gate** across pre-action scans saves ~340ms per typical turn.

**Things that DID work (repeat-this list):**

- **Measure first, cut second.** `session_stats` pointed the scalpel at specific action types. Every cut was validated live before proceeding.
- **Poll-with-adaptive-retry > fixed sleep + one-shot check + expensive retry.** The Step 1+3 merge is the template for future settle-retry logic.
- **Pure-helper TDD before wiring.** `BattleLifecycleClassifier` (27 tests), `SessionStatsCalculator` (10 tests), both TDD'd before wiring.
- **7/7 live verification sweep** proved the Aurablast fix across varied unit/skillset/depth combinations. Data-correctness fixes need a sweep, not a spot-check.
- **Honest "fail-fast" on unverifiable state.** Pre-cast verify aborts when ui= shows the wrong ability — better than casting wrong.

**Things that DIDN'T work (don't-repeat list):**

- **Cutting the line-1028 sleep to 150ms alone** caused Step 3 retry to fire often (adding 1500ms). Fix: merge Steps 1+3 into a unified poll-retry — only then the aggressive cut is safe.
- **Strict "ui= must match" verification** fails on ability-list states because ui= is widget-driven via `_battleMenuTracker` (not memory-resident). Left it as best-effort: fail only on clear mismatch, proceed on null.
- **Building a `BattleSituation` aggregator** (nearest enemy, hurt allies) — drafted, then reverted per user direction. Existing per-unit distance in scan output is enough.
- **DLC item spawn side-quest** — confirmed items aren't in memory or on disk; no DLC content to spawn. Skipped per user direction.
- **Post-Victory LoadGame misdetect investigation** — paused per user direction. Data captured.

**Memory notes added/updated:**
- `project_s57_speed_pass.md` — new: consolidated speed-pass techniques + measurement patterns.
- `project_aurablast_learned_filter_bug.md` — new: root cause + fix for the Aurablast incident; permanent rule that nav uses full skillset list.
- `feedback_session_stats_is_the_bug_finder.md` — new: `session_stats` as the default end-of-session audit, paid for itself within minutes.
- `MEMORY.md` — index updated with S57 entries.

**Tests:** 3893 passing (+15 this slice: 3 scan_diff identity, 12 AbilityListCursorNavPlanner, +1 strict-mode allowlist for `find_monotonic`).


### Session 59 (2026-04-23) — live-play bug fixes + S58 wire-up completion

**9 commits. Tests: 4174 → 4194 (+20).**

Commits:
- `7d27c09` — play-session bug fixes + S58 wire-up completion (10-item bundle)
- `5a247dc` — BattleAttack recoverable-state entry reset + Fight→Formation 10s settle
- `283e074` — TODO — mark S59 fixes shipped + flag memory re-verification priorities
- `e0c2bc3` — MoveGrid rotation-cache fallback + post-Victory WorldMap pins
- `bbfb124` — extend BattleAbilityEntryReset to BattleStatus + BattleAutoBattle
- `97be012` — auto_place_units: poll for BattleFormation entry
- `379e8cb` — cursor_probe diagnostic helper for S60 memory hunt
- `2d4bdbe` — TODO — close S59-shipped and audited items
- `6a52926` — live-play bug fixes: secondary inference + post-battle WorldMap

**Wire-ups (S58 pure helpers):**

- [x] **CharacterStatusLeakGuard → CommandWatcher** — wired into main settle path with `_previousSettledScreen` tracking. Filters transient CharacterStatus/CombatSets detections when prev was a battle state and no key could have triggered real drill-in. 9 TDD tests (+3 S59 edge cases).
- [x] **BattleMenuAvailability → DetectedScreen.MenuAvailability** — populates on BattleMyTurn with per-slot {Name, Slot, Available} derived from BattleMoved/BattleActed. Verified live — JSON shows all 5 slots. CAVEAT: accuracy blocked by battleActed address drift (see TODO §0).

**Strict mode allowlist:**

- [x] **`execute_turn` added to AllowedGameActions** — was blocked by S58 default-on flip. Live-verified post-fix.

**Entry-reset coverage (escape-to-known-state):**

- [x] **BattlePaused** → 1 Escape to BattleMyTurn. Closes the "cursor is on 'Data'" pause-menu leak bug.
- [x] **BattleStatus** → 1 Escape. Same pattern.
- [x] **BattleAutoBattle** → 1 Escape. Same pattern.
- [x] **`battle_ability` + `battle_attack` relaxed recoverable-state gate** — accept any IsResetableBattleScreen as starting state (not just BattleMyTurn/BattleActing), then run entry reset to reach MyTurn.
- [x] **`battle_move` + `battle_wait` entry-reset wired** — mirrors the battle_ability pattern.

**Stale-cursor recovery (memory read lags key input):**

- [x] **MoveGrid Escape+retry-Up** — when Enter lands on BattleAbilities due to stale menuCursor byte, Escape + blind Up + Enter to recover.
- [x] **BattleWait Escape+retry** — same for BattleAbilities (Down to Wait) and BattleAutoBattle (Up×2 to Wait).
- [x] **MoveGrid rotation-cache fallback** — when all 4 arrow keys report "no grid movement" (probable stale AddrGridX/Y read), reuse `_lastDetectedRightDelta` from a prior successful move instead of aborting.

**Post-Victory loss surfacing:**

- [x] **`BattleStats.PostVictoryNote` + `NotePostVictoryLoss` + render** — post-Victory Desertion/GameOver flicker attaches a note to the battle summary so unit loss isn't silently swallowed. 4 TDD tests.

**fft.sh shell fixes:**

- [x] **Header formatter `?(JobName)` → `JobName`** — when name missing (first-turn stale scan), fall back to job label only. Cleaner output.
- [x] **`scan_move <mv> <jmp>` override restored** — dispatches via locationId/unitIndex bridge fields. Workaround for Mv=0 Jmp=0 heap-search failures.
- [x] **`cursor_probe <key>` diagnostic** — samples menuCursor+battleActed+battleMoved before/after a key press, reports "memory did NOT change" when address has drifted. Live-verified the 0x1407FC620 staleness this session.

**Path settles:**

- [x] **EncounterDialog Fight path** — `WaitForScreen=BattleFormation` with 10s ceiling (was WaitUntilScreenNot EncounterDialog / 5s). Handles story-battle Formation loads that exceed 5s.
- [x] **auto_place_units poll-based entry** — polls for BattleFormation detection up to 12s with 500ms settle (was fixed 4s sleep). Dorter formation crash mitigated.

**Detection logic:**

- [x] **S58 Victory encA=255 sentinel tightened with submenuFlag==1** — Shout-mid-cast false positive killed.
- [x] **Post-battle WorldMap at battleground node** — added `postBattleWorldMapAtNode` short-circuit to inBattle classification. Fixes mis-detect as BattleActing when standing on a node with stale battle sentinels.
- [x] **Secondary skillset inference from abilities[]** — when SecondaryAbility byte reads 0 but abilities list contains non-primary entries, infer the secondary skillset via `ActionAbilityLookup.GetSkillsetForAbility(name)`. Live-repro fix for Ramza's disappearing Items submenu mid-battle.

**Regression pins (tests):**

- [x] `DetectScreen_PostVictoryWorldMap_StickyGameOverFlag_DoesNotMisdetectAsLoadGame`
- [x] `DetectScreen_PostVictoryWorldMap_StaleSentinels_RoutesViaWorldMapSignal`
- [x] `NotePostVictoryLoss_AttachesNote_BeforeEndBattle`
- [x] `NotePostVictoryLoss_Idempotent_FirstWriteWins`
- [x] `NotePostVictoryLoss_NoBattle_IsNoop`
- [x] `RenderBattleSummary_IncludesPostVictoryNote`
- [x] `EscapeCount_FromBattlePaused_IsOne` / `_FromBattleStatus_IsOne` / `_FromBattleAutoBattle_IsOne`
- [x] Plus 3 new CharacterStatusLeakGuard edge cases (empty-string prev, high keys-since, non-battle prev)

**Documentation / audits closed (no code change needed):**

- [x] **rawLocation==255 → TitleScreen preemption** — audited: 3 rules each have multiple positive signals, not blanket fallthroughs.
- [x] **Fix stale location address (255) after restart** — audited: disk-backed `claude_bridge/last_location.txt` fallback already in place, edge case non-blocking.
- [x] **AOB resolver dead-end** — superseded by the escape-to-known-state pattern; informational note only now.
- [x] **Extend SM cursor tracking — 1D cases** — shipped S47. 2D BattleMoving grid tracking is a separate follow-up item.

**Memory notes added/updated:**
- `project_s59_shipped_fixes.md` — new: summary of all S59 wire-ups, stale-cursor recovery patterns, and blocked-on-live items.
- `project_s59_live_battle_bugs.md` — new: live-play bug catalog with menuCursor/battleActed address-drift diagnosis.
- `MEMORY.md` — index updated with S59 entries.

**Blocked on live memory investigation (S60 priority):**
- `0x1407FC620` menuCursor byte drift (live-confirmed stale via `cursor_probe Down`)
- `0x14077CA8C` battleActed byte drift (doesn't flip after Shout)
- Likely caused by game-version swap (Deluxe Edition) earlier in the session

**Tests:** 4194 passing (+20 this session).

---

### Session 60 (2026-04-23/24) — 12 commits, Enemy-Turn Narrator + DLC catalog + scan polish + Items secondary fix, +79 tests

**Tests: 4190 → 4269 (+79).** Clean branch. Feature-complete Phase 1+2 of a live-streaming enemy-turn narrator, plus a suite of scan-render + helper fixes, all TDD'd and most live-verified.

**Commits (chronological):**
- `3998868` — S60 — DLC item catalog: Akademy set + Materia Blade+ + IC unused probe
- `05c647f` — god_ramza — upgrade loadout to Chaos Blade + Escutcheon strong + Sortilege
- `567d898` — S60 — TODO reset: wipe stale cruft, seed with 15 live battle gaps
- `da1335d` — auto_place_units — place only Ramza, leave other 3 slots unplaced
- `cabb99d` — S60 — Enemy-Turn Narrator Phase 1: before/after diff in battle_wait
- `33648d0` — S60 — Enemy-Turn Narrator Phase 1.5: live streaming + chunked wait
- `95d356d` — S60 — Enemy-Turn Narrator Phase 2: counter + self-destruct inferrers
- `978b8b2` — S60 — Phase 2.5 + scan polish: dedupe, petrified, attack-always-visible
- `a7be866` — S60 — narrator fixes live-verified: counter attribution, statuses, names
- `b080b72` — S60 — narrator: reset persistent snap + inline Units header
- `396d116` — S60 — battle_ability: HP delta suffix in response.Info
- `1c3125f` — S60 — Items secondary persists across transient SecondaryAbility=0 scans

**DLC item catalog (commit 3998868):**

- [x] **TIC Deluxe bonus items live-verified at FFTPatcher IDs 257-260** — Akademy Blade (257, sword), Akademy Beret (258, hat), Akademy Tunic (259, clothing), Ring of Aptitude (260, ring w/ JP Boost). Read off Ramza's roster slot after claiming the Deluxe entitlement.
- [x] **Materia Blade+ discovered at ID 256** — IC Remaster renamed/replaced the PSP Chaosbringer slot.
- [x] **PSP IDs 261-277 probed and tagged `[IC:unused]`** — all PSP-exclusive weapons (Moonblade, Onion Sword, Ras Algethi, Fomalhaut, Francisca, Golden Axe, Orochi, Moonsilk Blade, Nirvana, Dreamwaker, Stardust Rod, Crown Scepter, Vesper, Sagittarius Bow, Durandal, Gae Bolg, Gungnir) render as empty placeholders. ID 262 Onion Sword fully CRASHES the game on PartyMenu render — tagged `[IC:CRASHES]`.
- [x] **`dlc_items [count]` shell helper** — writes inventory counts to `0x1411A18C1..C4` so Claude can spawn the Deluxe set on demand.
- [x] **Memory note `project_dlc_item_ids.md`** + `feedback_invalid_item_id_crashes.md` (⛔ equipping an unused ID is a PartyMenu-render crash; probe via inventory writes only).

**Ramza loadout (commit 05c647f):**

- [x] **`god_ramza` upgraded** — Weapon: Ragnarok (36) → Chaos Blade (37, WP 40, on-hit Petrify); Shield: Kaiser (141) → Escutcheon strong (143, PhysEv 75 / MagEv 50); Accessory: Bracer (218) → Sortilege (239, perfume). Helm (Grand Helm 156), Body (Maximillian 185), Brave/Faith 95 unchanged.

**`auto_place_units` (commit da1335d):**

- [x] **Place only Ramza on formation** — was placing all 4 slots (Enter×2 each). Now a single Enter×2 for Ramza + Space+Enter to commence. Other 3 slots stay unplaced so solo-Ramza playtests work.

**Enemy-Turn Narrator feature (commits cabb99d / 33648d0 / 95d356d / 978b8b2 / a7be866 / b080b72):**

- [x] **Phase 1 — before/after diff MVP** — `BattleNarratorRenderer` pure helper (14 tests). Pre-wait snapshot at `BattleWait` entry, post-wait snapshot on poll exit, `UnitScanDiff.Compare` → render `> ...` lines into `response.Info`. +7 pipeline tests. `CaptureCurrentUnitSnapshot` helper wraps ScannedUnit → UnitScanDiff.UnitSnap conversion.
- [x] **Phase 1.5 — live streaming + chunked mode** — new `NarrationEventLog` append-only log at `claude_bridge/live_events.log`. Mid-poll narration every ~450ms (every 3rd 150ms tick). New `maxPollMs` field on battle_wait bridge command → chunked mode returns "partial" status for Claude to loop. `battle_wait_live [dir] [chunk_ms]` shell helper loops chunks until friendly turn. File-position-delta read replaces tail-subshell (Git Bash wasn't cleanly killing the orphan pipeline between chunks).
- [x] **Phase 2 — inferrers** — `CounterAttackInferrer` (10 tests) + `SelfDestructInferrer` (9 tests). `EmitNarrationBatch` helper consolidates the three narration emit sites (mid-poll / friendly-turn catch-up / chunked-timeout catch-up).
- [x] **Phase 2.5 — dedupe** — `BattleNarratorRenderer.Render` takes optional `HashSet<string> suppressedKoLabels`; `EmitNarrationBatch` builds the set by string-matching counter/self-destruct output so raw `> X died` doesn't duplicate inferred attribution lines. +3 renderer tests.
- [x] **Identity-fix batch — 3 live-surfaced bugs** (commit a7be866):
    - CounterAttackInferrer derives the counter-attacker from events (PLAYER-team damaged unit) instead of trusting `narratorActivePlayerName` which shifts to the acting ENEMY mid-chunked-turn. +2 tests.
    - BattleNarratorRenderer emits gained/lost status lines alongside damaged/healed/ko/revived events (UnitScanDiff collapses Kind="damaged" when HP+status both change; status deltas were silently dropped). +3 tests.
    - Class-level `_narratorPersistentLastSnap` carries forward across chunked BattleWait calls so a fresh scan that lost enemy names can backfill from the prior chunk's snap instead of emitting "(unit@x,y)" fallbacks.
- [x] **Reset-narrator flag** (commit b080b72) — new `resetNarrator` bool on the battle_wait bridge command. Shell sets it on the first chunk of each `battle_wait_live` / `battle_wait` sequence so the prior battle's persistent snap doesn't emit phantom "recovered N HP" / "lost Status" events on the first diff of a new battle. Also auto-cleared on BattleVictory / BattleDesertion.

**Scan polish (commit 978b8b2):**

- [x] **Petrified units tagged as effectively-dead** — `StatusDecoder.GetLifeState` returns "petrified" when the Petrify byte-1 mask 0x80 is set. fft.sh renders ` STONE` / ` CRYSTAL` suffixes alongside ` DEAD`. Priority: crystal > treasure > dead > petrified > alive. +4 tests.
- [x] **Attack tiles respect life state** — `NavigationActions` attack-tile builder marks any non-"alive" occupant as "empty". Dead / crystal / treasure / petrified no longer appear as valid Attack targets.
- [x] **Attack entry always visible** — `AbilityCompactor.IsHidden` exempts the basic "Attack" entry so Claude sees the weapon's range + element + on-hit effect even when no enemy is adjacent. +2 tests.
- [x] **Weapon tag helpers** — `ItemData.GetEquippedWeapon` + `ItemData.ComposeWeaponTag` produce "{Name}" or "{Name} onHit:{effect}" with "On hit: " prefix stripped. `ActiveUnitSummaryFormatter.Format` takes optional weaponTag. +4 ItemData tests + 4 formatter tests. **Wire-up to `_cachedActiveUnitWeaponTag` still pending** — blocked on BattleUnitState not exposing Equipment.

**Inline Units header** (commit b080b72):

- [x] **Scan output Units: inline with first entry** — was `Units:\n  [ENEMY] X...`, now `Units:  [ENEMY] X...\n        [ENEMY] Y...`. Saves a vertical line; per user request.

**battle_ability HP delta** (commit 396d116):

- [x] **`battle_ability` returns HP delta** — targeted non-cast abilities now report `(preHp→postHp/maxHp)` / ` — KO'd! (preHp→0/maxHp)` / ` — revived (0→postHp/maxHp)` in response.Info, matching BattleAttack's shape. Cast-time abilities (CastSpeed>0) skip the delta (they queue in Combat Timeline; HP won't change until the cast triggers). New `AbilityHpDeltaFormatter` pure helper (+8 tests).

**Items-secondary-not-in-submenu fix** (commit 1c3125f):

- [x] **`SecondarySkillsetResolver` persists across transient SecondaryAbility=0 reads** — the byte reads 0 transiently mid-turn on IC Remaster. Old code blanked `_cachedSecondarySkillset` to null on that miss, so `GetAbilitiesSubmenuItems` returned `[Attack, Mettle]` with no "Items" and every `battle_ability "Phoenix Down"` / Potion / etc. failed with `Skillset 'Items' not in submenu`. Pure resolver now preserves the last-known-good cache when the current scan can't confirm a secondary. +9 resolver tests. **Live-verified this session** — Phoenix Down on dead undead navigated correctly through Items submenu.

**Narrator live verifications (committed + live-verified):**

- [x] **Counter attribution correct** — "> Ramza countered Black Chocobo for 336 dmg" (no self-countering).
- [x] **Petrify proc captured** — "> Black Chocobo gained Petrify, Critical" alongside the damage event.
- [x] **Names stick across chunked calls** — no "(unit@x,y)" fallback after fix.
- [x] **Petrified / Crystal suffixes render** — `[Petrify,Float,Critical] STONE` / `[Crystal,Undead] CRYSTAL`.
- [x] **Inline `Units:` header** — renders as designed.
- [x] **`Attack → (no targets in range)` always visible** — weapon profile shown even alone.
- [x] **No phantom "recovered HP" cross-battle** — resetNarrator flag cleared persistent snap.

**Still UNVERIFIED (awaiting live repro):**

- Counter-KO ko-suppression in one window — attack + KO split across chunks on live runs; logic-tested only.
- Self-destruct inferrer — no live Bomb self-destruct caught yet.
- `battle_ability` HP delta suffix — tests-only, no live Throw Stone / Cura / Phoenix Down HP-delta capture yet.

**Memory notes added/updated this session:**
- `project_dlc_item_ids.md` — new: FFTPatcher IDs 257-260 live-verified.
- `feedback_invalid_item_id_crashes.md` — new: IC Remaster drops PSP-exclusive items; equipping an unused ID crashes on PartyMenu render.
- `MEMORY.md` — index updated with S60 entries.

**TODO reset:**
- `567d898` wiped all stale S56-S59 items from TODO.md per user direction and seeded fresh S60 bugs surfaced during live play.

---

### 2026-04-24 — Scan polish batch + narrator fine-tuning + live-verify sweep

**Tests: 4269 → 4288 (+19).** Four commits. Multiple live-verifications of previously-UNVERIFIED features; three new polish fixes for gaps surfaced during testing.

**Commits:**
- `8cf9197` — Five TODO items: weapon tag, acted/moved tags, Act-validation, execute_turn timeout, scan_move doc
- `5261ae4` — Units list polish — weapon tag per player, Mv/Jmp in verbose rows
- `11660b8` — Narrator critical-HP line + self-target tag + scan_move override render
- `24a0746` — Petrified-filter extends to ability target tiles + narrator settle delay

**Shipped:**

- [x] **Weapon tag wired end-to-end into active-unit banner** (8cf9197) — new `WeaponTag` string field on `BattleUnitState`, populated server-side via `ItemData.ComposeWeaponTag(u.Equipment)` in `CollectUnitPositionsFull`. CommandWatcher reads `activeUnit.WeaponTag` into `_cachedActiveUnitWeaponTag`; compact banner now renders `[BattleMyTurn] Ramza(Gallant Knight) [Chaos Blade onHit:chance to add Stone] (2,1) HP=719/719`. Live-verified.

- [x] **`acted` / `moved` tags in compact header** (8cf9197) — `_fmt_screen_compact` pulls `screen.battleActed` / `battleMoved` and appends colored chips on `BattleMyTurn`/`BattleActing` when the bytes are 1. Works correctly given the byte; byte drift is a separate memory issue.

- [x] **Act-consumed re-check after escape-to-known-state** (8cf9197) — `BattleAttack` + `BattleAbility` both now re-check `screen.BattleActed == 1` on the FRESH BattleMyTurn after the escape path. Previously retrying `battle_attack` from a recoverable post-action state passed the initial guard, escaped back, and silently failed deep in the targeting flow. Now returns clean `Act already used this turn — only Move or Wait remain.`

- [x] **`execute_turn` fft timeout bumped 5s → 120s** (8cf9197) — live-repro fixed: `execute_turn 4 6 "Phoenix Down" 4 7` previously timed out at 5s mid-move-confirm. Shell-side `fft "$json"` now uses 120 to match blocking battle_wait. Live-verified this session with `execute_turn 2 4 Attack 2 5` completing cleanly in 10s.

- [x] **scan_move deprecation — documented forwarding path** (8cf9197) — clarifying comment block; no-args path already forwards to `screen()`.

- [x] **Weapon tag per player in Units listing** (5261ae4) — `[PLAYER] Ramza(Gallant Knight) (2,1) HP=719/719 [Chaos Blade onHit:chance to add Stone] *`. Lets Claude see every party member's weapon at a glance.

- [x] **Mv / Jmp in verbose unit rows** (5261ae4) — `screen -v` renders `Mv=7 Jmp=3` alongside PA/MA/Spd. BattleUnitState gains Move/Jump fields from ScannedUnit. Populated for player units; enemy Move/Jump needs a bigger change (static job base-stats table or per-enemy heap search) — tracked as open.

- [x] **`BattleUnitStateWeaponTagTests`** (5261ae4) — +6 characterization tests pinning `ComposeWeaponTag` shape (name-only, onHit format, unarmed, armor-only, no-leading/trailing-space).

- [x] **`CriticalHpInferrer`** (11660b8) — new pure helper. Emits `> Ramza reached critical HP (400→180/719)` when a PLAYER unit crosses below 1/3 MaxHp during an enemy turn. Fires only on threshold crossing (not repeatedly while already critical). Excludes KO (already has its own event), enemies, healed events. Wired into `EmitNarrationBatch`. +9 tests.

- [x] **Self-target `SELF` vs `ALLY` tag** (11660b8) — `Focus → (2,1)<Ramza SELF>` instead of misleading `<Ramza ALLY>`. Caster tile now gets `SELF`, other friendly tiles keep `ALLY`, enemy tiles stay untagged. Live-verified.

- [x] **`scan_move <mv> <jmp>` override renders full output** (11660b8) — was printing only the one-line bridge header; now dispatches the action and calls `screen()` so Move tiles / Attack tiles / Units / Abilities all render.

- [x] **Petrified filter extends to ability `validTargetTiles`** (24a0746) — `aliveByPos` / `deadByPos` indexes now exclude `lifeState=="petrified"` units so ability target lists no longer suggest wasted actions on statues. Live-surfaced during testing: `Attack → (3,1)<?>` on a STONE'd Skeletal Fiend. Fix mirrors the earlier AttackTiles fix pattern.

- [x] **200ms settle before fresh narrator pre-snap** (24a0746) — live-surfaced false positive: `> Ramza countered Skeletal Fiend for 275 dmg` when Ramza's own Phoenix Down actually landed the 275-dmg kill on the player turn. Stale static-array HP read lagged behind the memory write. 200ms Thread.Sleep in `BattleWait` before `CaptureCurrentUnitSnapshot` (non-chunked path only — chunked continuations reuse cached snap) lets the game's memory regions flush first.

**Live-verified this session (was UNVERIFIED):**

- [x] **Counter attribution correct** — `> Ramza countered Black Chocobo for 336 dmg`
- [x] **Chaos Blade on-hit Petrify** — `> Black Chocobo gained Petrify, Critical` alongside damage event
- [x] **Counter-KO ko-suppression** — `> Ramza countered (unit@3,1) for 275 dmg — (unit@3,1) died` (single line, no duplicate raw `> X died`)
- [x] **`battle_ability` HP delta** — `Used Throw Stone on (2,4) (629→611/629)`
- [x] **`battle_ability` `— KO'd!` suffix** — `Used Phoenix Down on (3,1) — KO'd! (275→0/629)`
- [x] **Items submenu lookup after SecondarySkillsetResolver** — Phoenix Down routed through Items correctly
- [x] **`execute_turn` timeout bump** — 10s end-to-end, no 5s failure
- [x] **Weapon tag banner + Units list** — all renders correct
- [x] **Inline `Units:` header** — layout correct
- [x] **`SELF` vs `ALLY` tag** — all self-target abilities show SELF
- [x] **`Attack → (no targets in range)` always visible** — correct
- [x] **Petrify/Crystal/Dead/Treasure life states render** — all suffixes firing

**Still UNVERIFIED (awaiting live repro):**

- SelfDestructInferrer — no live Bomb self-destruct caught yet
- CriticalHpInferrer — Regen kept Ramza above threshold; need a harder hit

**Known limitations that are memory-level (not our code):**

- `battleActed` / `battleMoved` bytes read 0 transiently after actions — the `acted`/`moved` tag feature is correct, the bytes themselves are unreliable. Memory hunt still pending.
- Enemy name misattribution across chunks (e.g. Chocobo re-labeled as Skeletal Fiend) — scan's fingerprint lookup is unstable for some enemy classes. Narrator's backfill helps but doesn't fully fix it.
- `[BattleVictory]` spurious detection flash during non-Victory actions — detection rule; needs audit.

---

### 2026-04-25 — Bridge robustness + chest/crystal flow + Reequip submenu (30 commits, +30 tests 4530→4560)

Big-pile session: bridge crash recovery, chest/crystal modal handling end-to-end, Reequip / Evasive Stance submenu support, the long-standing menuCursor byte drift fix, the player-facing convention drift, and a stack of detection-leak guards captured from live play. Eight new pure helpers shipped, eleven `[x]` items closed.

**Commits (chronological, oldest → newest):**

Convention + detection groundwork:
- `219e60a` — Fix ParseFacingDirection coord convention — +y is south in FFT grid
- `9d67fb9` — Pin coord-convention agreement across 3 facing helpers
- `70195da` — TODO updates from live-verify session

PLANNING-HEAVY menuCursor drift fix (Phases 1-4):
- `9518a81` — Proposal: fix menuCursor byte drift via fresh-entry write + ui=? fallback
- `7401a2c` — Phase 1: FreshBattleMyTurnEntryClassifier pure helper (8 tests)
- `d471e1d` — Phase 3: wire fresh-entry menuCursor reset into DetectScreen
- `729987d` — Set _actedThisTurn on mid-flight battle_ability failure
- `5f71fa4` — Set _actedThisTurn at commit-to-act time, not just on completion (later reverted)

Memory-hunt infrastructure:
- `4836932` — MemoryDiffCalculator pure helper for future memory hunts
- `0455332` — Wire memory_diff bridge action for memory hunts

Bridge robustness:
- `e1d6937` — Quarantine malformed command.json so a single bad write doesn't jam the bridge
- `8cfe9ef` — Re-enable Ctrl-hold fast-forward in battle_wait
- `35717c6` — Surface non-success bridge statuses in fft() shell render
- `298307e` — Route enter() helper through advance_dialogue to bypass strict-mode block
- `6710ab7` — Auto-print game-running status on every shell command timeout

Chest / crystal modal flow (detection + auto-dismiss):
- `68dcb4f` — Detect BattleRewardObtainedBanner regardless of battleTeam
- `f03772b` — Detect BattleCrystalMoveConfirm regardless of battleTeam / save-restore
- `f3b5a80` — battle_move auto-dismisses chest/crystal/ability-learned modals
- `07fddc7` — TODO: mark chest-dialog and strict-mode raw-Enter items shipped
- `ad5e70f` — Gate turn-owner rules on battleMode != 0

acted/moved + facing fixes:
- `21c0ace` — Override raw battleActed/battleMoved bytes with bridge commit-time flags (6 tests)
- `7bcf660` — Fix FacingArrowDelta to use -y=North (matches rest of facing pipeline)
- `28cbce1` — TODO: close out remaining items — code-only work complete

Validation + leak guards:
- `2a61e54` — Pre-validate battle_attack/battle_move tile coords with helpful error (5 tests)
- `4d94180` — EqaLeakGuard: filter spurious EqA detections after key on Battle*/GameOver (5 tests)
- `7444b0d` — Revert _actedThisTurn pre-flight set: interacts badly with override
- `b1bbcaf` — battle_attack: up-front range validation against scan_move tile cache
- `6654d18` — Invalidate attack tile cache on successful move

Reequip / Evasive Stance submenu support:
- `7af89b7` — Detect Reequip-mid-battle EqA panel as BattleStatus (1 test)
- `e808110` — Add Reequip / Evasive Stance as 4th row in BattleAbilities submenu (5 tests)

**Shipped + LIVE-VERIFIED:**

- [x] **PLANNING-HEAVY menuCursor byte drift** (Phases 1-4) — Picked Option (a): write `0x1407FC620=0` on fresh-BattleMyTurn detection via FreshBattleMyTurnEntryClassifier. Submenu-escape paths preserve cursor; turn-boundary paths reset to Move. Phase 5 (ui=? fallback) deferred — fresh-entry write proved sufficient.
- [x] **Ctrl-hold fast-forward in battle_wait** — Stale 2026-04-12 "doesn't work" note was wrong; Ctrl IS recognized. Focus-aware (release on tab-away, re-assert on tab-back) so terminal typing isn't hijacked. Live samples Siedge Weald: 5.4s / 9.3s / 9.0s, down from ~10s/turn baseline.
- [x] **Quarantine malformed command.json** — bare-string write (`screen` instead of JSON) used to spam parse errors and jam the bridge until manual cleanup. Now renames to `command.json.bad-{timestamp}` and continues. Plus shell-side JSON-shape check rejects bare-string args.
- [x] **Shell surfaces all non-success statuses** — `fft()` only handled `failed` / `rejected`; `blocked` / `partial` / `timeout` / `error` rendered as success. Added catchall: any non-completed/encounter status now prints `[STATUS] error` line.
- [x] **enter() routes through advance_dialogue** — strict mode blocks raw key arrays except Escape; `enter()` was effectively dead. Routed through the `advance_dialogue` named action (in AllowedGameActions, identical SendKey internally).
- [x] **TIMEOUT prints `running` status** — every TIMEOUT in shell helpers (block, rv, memory_diff, execute_action, screen) now calls `running` so the cause (game closed vs bridge hung) is one line.
- [x] **Chest/crystal modal detection + auto-dismiss** — three commits cover the end-to-end flow: BattleRewardObtainedBanner ("Obtained X!") and BattleCrystalMoveConfirm ("Use the crystal..." / "Move to this tile?") both fire regardless of battleTeam (the chest move auto-ends Ramza's turn and team cycles before the modal renders); MoveGrid poll loop auto-Enters on those + BattleAbilityLearnedBanner. Live-verified at Siedge Weald with both chests and a crystal.
- [x] **Turn-owner rules gated on battleMode != 0** — post-battle world-map state had stale `battleTeam=1` from prior enemy turn; turn-owner rule fired BattleEnemiesTurn forever until SM resync. Adding the battleMode guard fixes it.
- [x] **EqaLeakGuard for transient EqA misdetect** — bridge sent Enter on GameOver, first post-key detection returned `EquipmentAndAbilities` (transient frame); BattleRetry's internal _detectScreen got the bad frame and bailed. Now filtered when prior settled state was GameOver/Battle*/Victory/Desertion (no legitimate single-cycle transition into EqA).
- [x] **battleActed/battleMoved override** — raw bytes stale-read 0 right after actions; UI tag computed from `_actedThisTurn` flag was correct but response.screen.battleActed lagged. Override forces 1 when flag is true, restoring consistency between the tag and the raw response field.
- [x] **CriticalHpInferrer threshold-crossing** — UNVERIFIED for 2 sessions, finally LIVE-VERIFIED at Siedge Weald: Ramza took 300-dmg hit (HP 432→132, threshold 239.67), narrator emitted `> Ramza reached critical HP (432→132/719)` immediately following `> Ramza gained Critical`.
- [x] **FacingArrowDelta -y=North fix** — boundary drift between ParseFacingDirection (uses -y=N post-`219e60a`) and FacingStrategy.GetFacingArrowKey's table (still used +y=N). Caused battle_wait N/S/E/W to silently send the wrong arrow on the facing screen, with the actual visual offset depending on camera rotation. Captured at Siedge Weald rot=3 (requested N, got W). Extended consistency test to pin the GetFacingArrowKey boundary.
- [x] **TileTargetValidator** — `battle_attack` / `battle_move` with wrong JSON keys (`{"x":8,"y":11}` instead of `{"locationId":8,"unitIndex":11}`) used to surface as `Cursor miss: at (0,0) expected (-1,0)` — confusing delta values. Now rejected up-front naming the action, the tile, and the correct JSON shape.
- [x] **battle_attack range validation** — `battle_attack` to an out-of-range tile used to enter targeting mode and "MISSED!"; now uses cached attack tiles from scan_move (handles weapon range correctly: melee=1, bow=5, gun=8). Cache invalidated on successful move.
- [x] **Reequip mid-battle detection** — clicking Reequip from Abilities submenu opens the same EqA panel as Status from action menu; bytes differ (party=1, battleMode=0, menuCursor=1, submenuFlag=0 vs party=0, battleMode=3, menuCursor=3, submenuFlag=1) but both share `paused=1, encA=9, encB=9`. New rule keys off the encA=9 marker.
- [x] **Reequip / Evasive Stance as 4th submenu row** — bridge cycled cursor only Attack/Mettle/Items, mis-labeling 4th position as Attack (wrap). Now appends the support ability NAME (Reequip / Evasive Stance — IC remaster shows ability name as the row label, not "Defend"). SupportAbilityBattleCommand pure helper. BattleMenuTracker.RefreshSubmenuItems updates without resetting cursor. Cache preserves prior value when scan returns null active unit (transient post-key reads). Live-verified: Attack → Mettle → Items → Evasive Stance → Attack (wrap).
- [x] **Player facing byte (root-caused via the FacingArrowDelta fix)** — Lenalian repro (visual W → byte E, 180°) and Siedge Weald repro (visual N → byte W, 90°) both consistent with the +y/-y flip; offset depends on camera rotation. battle_wait now sends the correct arrow.

**Shipped + accepted-without-live-verify (passive observation):**

- [x] **Pre-snap 400ms settle adequate** — multiple battles played without organic false-positive counter line; closing because passive monitoring without an observed failure isn't an actionable TODO. Re-open if one surfaces.
- [x] **`battleActed` / `battleMoved` byte drift** — software-side override path taken (the alternative listed in the original TODO); memory hunt for an alternative authoritative byte deferred since the override resolves the user-visible inconsistency.

**Techniques worth propagating:**

- **Pre-flight flag set + override = self-rejection trap.** `5f71fa4` set `_actedThisTurn=true` BEFORE ExecuteNavAction "for safety on mid-flight aborts." Same session shipped `21c0ace` exposing that flag through `screen.BattleActed`. Result: BattleAttack's own internal screen-read saw acted=1 before any action ran and self-rejected with "You've already acted this turn." Lesson: when adding an override that exposes private state into a response field, audit every guard that consumes the field to make sure they don't fire during the same in-flight call. Reverted in `7444b0d`; mid-flight failure handler at lines 3866-3874 already covered the original concern.

- **Cache preservation pattern for transient empty-active-unit scans.** Post-key scans sometimes return activeUnit with empty Name and null fields for ~1 frame before settling. Naive cache-update overwrites the known-good value with null. Pattern: `if (newValue != null) cache = newValue;` — preserves prior value across the transient. SecondarySkillsetResolver had this; SupportAbility cache now does too. Reset is handled by turn-boundary sites (battle_wait, StartBattle), not by null reads.

- **Detection-rule fingerprint capture rule.** When you add a detection rule from a live capture, snap the SAME state TWICE — once in the natural triggering context, once after save+resume. Bytes that differ between captures (gameOverFlag, submenuFlag, etc.) are NOT load-bearing in the fingerprint and shouldn't be in the rule. Stable bytes across both captures are the real discriminator. Caught by the BattleCrystalMoveConfirm save-resume case where submenuFlag flipped 1→0 across save.

- **Leak guards for "X never reachable from Y in one cycle".** When a state transition is structurally impossible (GameOver → EqA, Battle* → EqA), wrap detection with a guard that holds the prior name. CharacterStatusLeakGuard + EqaLeakGuard now follow the same shape. Conservative: only filter when the prior settled state is in the explicit "can't transition to current" list — legitimate flows still work.

- **Drift-then-pin for cross-helper conventions.** FacingArrowDelta carried +y=North while ParseFacingDirection / FacingDecider / FacingByteDecoder had all flipped to -y=North across `c36ec53` and `219e60a`. The drift went unnoticed because each helper's tests were green in isolation. Extended FacingCoordConventionConsistencyTests to walk every cardinal name through ParseFacingDirection → GetFacingArrowKey at each rotation; if the table ever drifts again, that test catches it.

- **`memory_diff` bridge action unblocks future memory hunts.** Snap region (4096-byte block via `block`), do thing, snap again (`memory_diff` returns `0xNN: XX -> YY` lines for changed bytes). No more handcrafted snapshot+diff scripts. Pairs with the chunked-`battle_wait` (`maxPollMs:1500`) for capturing inside specific game states.

- **Up-front range/coord validation > deep nav-loop errors.** Two foot-guns this session (battle_attack with wrong JSON keys, battle_attack out-of-range) both surfaced as confusing nav-loop errors deep in the targeting flow. Both fixed by adding pre-flight rejection that names the action, the offending input, and the correct shape. battle_move had this already; aligning battle_attack to the same pattern was a small change with big readability win.

**Don't-repeat:**

- **Don't set commit-to-act flags before guard checks.** See "Pre-flight flag set + override" above.
- **Don't use rotation-aware helpers without checking the convention.** `FacingArrowDelta` table inverse to ParseFacingDirection silently broke battle_wait facing.
- **Don't trust SM-derived screen names when raw detection clearly says GameOver/Victory/Battle*.** EqA leak in BattleRetry was downstream of this — detection knew GameOver but SM said EqA, and BattleRetry consulted the SM-aware path.
- **Don't snap a detection fingerprint in only one context.** Save-resume cleared submenuFlag; capturing only the pre-save state would have produced a fragile rule.


### 2026-04-25 — Playtest-driven friction fixes (12 commits / 5 playtest runs / +13 tests 4587→4600)

Five sub-agent playtests spawned via the new `feedback_playtest_friction_pattern.md` shape produced ~30 concrete friction items. ~22 shipped this session as bridge / UX / doc fixes; the rest tracked in `project_multi_unit_turn_handoff_bug.md` and the open narrator-damage-gap.

**Commits (chronological):**
- `c315efa` — `AttackOutcomeClassifier` keyed off post-animation screen state. Fixes basic Attack false-MISSED bug where ReadLiveHp couldn't fingerprint the target. (+12 tests)
- `7b4bedf` — `IsHeightStrict` flag on Martial Arts melee abilities. Pummel / Doom Fist / Cyclone / Chakra / Purification / Revive enforce strict zDelta=0 instead of caster-jump fallback. (+3 tests)
- `77aa799` — `[ACTED]` / `[MOVED]` header tags + "Abilities: (already used this turn)" replacement. Root caused via `BattleLifecycleClassifier.IsInsideBattle` missing terminal states + `FreshBattleMyTurnEntryClassifier` resetting on terminal-prior transitions. (+8 tests)
- `094b1fe` — PostAction trailer pins to caster pos for attack/ability (was target tile post-attack, mixing target coords with caster HP).
- `548a580` — Playtest #1 batch: `./fft` wrapper for per-Bash-call agents, R:/S:/M: passives in compact mode, dead-body filter on Attack tiles, Timeline turnOrder line, `execute_turn` Info aggregation, Commands.md doc adds.
- `748605e` — `./fft` wrapper drops `set -e` (helpers tolerate non-zero exits). Renderer cleanup after bisect.
- `61df79f` — Playtest #2 batch: Heights line, equipment tag (shield/helm/body/accessory), screen-override transient note, move-and-attack range note in BattleTurns.md.
- `12605a3` — `ReviveTargetClassifier` (REVIVE / KO / REVIVE-ENEMY! / KO-ALLY!). Phoenix Down / Raise / Arise targeting now distinguishes dead allies from undead enemies (kill-via-reverse-revive). (+8 tests)
- `9de952c` — Stats-keyed cache fallback in `UnitNameCache`. Dead unit's name preserved even when fingerprint search fails post-KO. Survives moves and deaths. (+5 tests)
- `85229f1` — Playtest #3 batch: SendKey + per-helper guards refusing keys during BattleEnemiesTurn / BattleAlliesTurn. Per-ability range validation in battle_ability. Self-target auto-fill, error message rewrite, `execute_turn` cursor reset, PostAction backfill, scan_move deprecation removed, BattleDesertion documented, slow-helper threshold calibration.
- `bcb7fea` — Playtest #4 batch: X-Potion phantom heal (dual-read static + live HP). Self-target auto-fill expanded. `execute_turn` flicker tolerance. AttackTiles `inRange` flag. Stale active-unit cache cleared on terminal screens. Guest-joined narrator event. Multi-enemy CT THREAT warning.
- `83bd033` — Bash heredoc unescaped-quote silent renderer failure fix.

**Shipped + LIVE-VERIFIED:**

- [x] **`battle_attack` MISSED-on-hit bug** — `ReadLiveHp` heap-search can fail to fingerprint the target struct, falls back to `preHp == postHp`, formatter declared MISSED. Replaced with `AttackOutcomeClassifier` keyed off post-animation screen state. BattleMoving = HIT (HP=0 → KO), BattleAttacking = MISS, BattleVictory + HP=0 = KO, BattleVictory + HP>0 = Hit (flicker false-positive guard).
- [x] **Martial Arts melee abilities require same height** — Pummel / Doom Fist / Cyclone / Chakra / Purification / Revive enforce strict zDelta=0. Aurablast (HRange=3 ranged) and Shockwave (line attack governed by HoE) keep their existing behavior.
- [x] **Acted/Moved consumption surfaced clearly** — `screen` tags header with `[ACTED]`/`[MOVED]` and replaces consumed section with "(already used this turn)". Fixed two underlying bugs: `BattleLifecycleClassifier.IsInsideBattle` was missing terminal states (mid-attack flicker fired spurious StartBattle event clobbering `_actedThisTurn`); `FreshBattleMyTurnEntryClassifier.NotFreshPriors` didn't include those terminal states.
- [x] **PostAction trailer caster-pos fix** — was reading cursor pos (target tile) + condensedBase HP (caster HP). Conflated target coords with caster HP. Added position-explicit overload.
- [x] **`./fft` wrapper** — kills the `source ./fft.sh && <helper>` tax for agent drivers. Each Bash tool call is a fresh shell.
- [x] **Passives R:/S:/M: in compact mode** — Counter / Auto-Potion / Hamedo are tactically critical. Live-observed: undisclosed Wisenkin Counter killed Ramza in playtest #1.
- [x] **`execute_turn` Info aggregation** — bundled response now carries `Moved | Attacked ... HIT/MISS/KO'd | Waited` instead of just empty wait Info.
- [x] **Dead bodies filtered from Attack tiles** — corpses excluded from cardinal Attack panel.
- [x] **Timeline / turnOrder rendered** — `Timeline: E(E,ct=30) → *Ramza(P,ct=10)` line. Was in JSON but not rendered.
- [x] **Heights context summary** — `H` field on BattleUnitState. Renderer shows `Heights: caster h=5 vs enemies h=3`.
- [x] **Equipment tag (non-weapon)** — `ItemData.ComposeEquipmentTag` for shield/helm/body/accessory. Active-unit row reads `[Chaos Blade onHit:...] [Grand Helm, Maximillian, Sortilege, Escutcheon (strong)]`.
- [x] **Phoenix Down intent tagging** — `ReviveTargetClassifier` maps target/caster/hp/status → REVIVE / REVIVE-ENEMY! / KO / KO-ALLY!. PD on undead enemy = kill move (reverse-revive); previously the playtest agent missed this use case.
- [x] **Dead unit name preservation** — secondary stats-keyed cache `(maxHp, level, team) → name` survives moves and deaths.
- [x] **SendKey enemy-turn guard** — refuses to send keys during BattleEnemiesTurn / BattleAlliesTurn (any key during enemy turn opens the pause menu).
- [x] **`battle_ability` range validation** — fixes Phoenix Down phantom-success. Per-ability tile cache populated during scan; out-of-range calls fail with clear error.
- [x] **Self-target auto-fill** — `battle_ability "Tailwind"` (no coords) defaults to caster's tile when caster IS in valid set.
- [x] **`battle_ability` error vocabulary rewrite** — was "requires a target (locationId=x, unitIndex=y)" leaking JSON field names; now "Usage: battle_ability \"Steel\" <x> <y>".
- [x] **`execute_turn` pre-flight cursor reset** — forces menu cursor to Move slot before first sub-step dispatch.
- [x] **`execute_turn` PostAction backfill** — backfill from accumulator's FinalPostAction or fresh ReadPostActionState when battle_wait doesn't populate one.
- [x] **scan_move deprecation banner removed** — docs reference scan_move as canonical; the "USE screen" reminder contradicted that.
- [x] **BattleDesertion / BattleVictory / GameOver** added to BattleTurns.md screen-states table.
- [x] **Slow-helper red threshold** — `execute_turn` / `battle_wait` / `auto_place_units` calibrated to 12s warn (was 800ms global).
- [x] **X-Potion phantom heal** — `ReadLiveHp`'s heap search can collide with a wrong-struct's saved-MaxHp value. Cross-check against ReadStaticArrayHpAt; if disagree by more than half MaxHp, prefer static.
- [x] **`<SELF>` marker semantics doc note** — clarified in BattleTurns.md that SELF means "your tile is one valid target," not "this is a self-only ability."
- [x] **`execute_turn` flicker tolerance** — re-check after 800ms before aborting on transient terminal states.
- [x] **AttackTiles `inRange` flag** — `[TOO CLOSE]` tag for d=1 cardinals when bow/gun MinRange=2.
- [x] **Stale active-unit cache cleared on terminal screens** — agent saw "Tietra" (a story character not present in this battle) in the header right before GameOver. Clear on BattleVictory / BattleDesertion / GameOver.
- [x] **Guest-joined narrator event** — `case "added"` in BattleNarratorRenderer surfaces `> X (TEAM) joined at (x,y)`.
- [x] **Multi-enemy CT THREAT warning** — `!! THREAT: N enemies act before your next turn` line when 3+ enemies precede caster in turnOrder. Live-flagged: agent went 548→36 HP in one battle_wait because three enemies converged.
- [x] **Bash heredoc unescaped-quote silent failure** — adding `"..."` in JS comment inside `node -e "..."` heredoc closes the bash double-quote, swallowed by `2>/dev/null`.

**Open from this session (next-session priorities):**

- [~] **Multi-unit party turn-handoff confusion** — proposal drafted (`project_multi_unit_turn_handoff_bug.md`); three-fix plan ready to ship. Banner + cache invalidation on turn-boundary + auto-scan post-wait. Highest priority.
- [~] **Narrator damage/KO gap** — playtest #3 agent saw a Skeleton mysteriously die with no narrator entry. `BattleNarratorRenderer` supports `damaged`/`ko` events but `UnitScanDiff` may not be capturing all of them.

**Techniques worth propagating** (now in memory):

- See `feedback_playtest_friction_pattern.md` — fresh-eyes sub-agent playtests surface friction the dev can't see; pattern + driver-prompt rules + post-run triage.
- See `feedback_phantom_success_pattern.md` — generalized fix shape for any helper that uses a resource and reports outcome.
- See `feedback_bash_heredoc_quote_breakage.md` — diagnostic for silent renderer failures.
- See `project_2026_04_25_playtest_fixes.md` — full commit roster for this session.
- See `project_multi_unit_turn_handoff_bug.md` — detailed proposal for the open multi-unit bug.


### 2026-04-26 — Dialogue decoder rewrite (event 045 walk-through)

Live walk-through of event 045 (Eagrose Castle, 29 paginated bubbles) reset the load-bearing rules in `MesDecoder.DecodeBoxes`. User drove Enter through the cutscene and flagged speaker / placeholder / pagination mismatches; each correction tightened the byte semantics. See `project_dialogue_decoder_2026_04_26.md`.

**Commit:** `bcd6cc4` — Dialogue decoder: FE-N pagination, 0xE0 / 0xDA-0x68 placeholders, segment-only speakers (+16 tests, 4709 → 4722 passing).

**Shipped:**

- [x] **Trailing 0xFE-run length = bubble count for the segment** — was previously collapsed to "one boundary plus a beat". With the new rule, a segment with FE×N at the end is split into N visible bubbles by sentence balance. Verified at event 045 FE×3 (127/114/90 char split) and FE×4 (110/108/104/76 char split).
- [x] **Sentence-char-balanced pagination** — target = total_chars / N per bubble, soft cap 1.3×, reserve at least one sentence per remaining bubble. Reproduces the in-game 2-2-1 distribution for 5 sentences across 3 bubbles and 2-1-2-1 for 6 across 4.
- [x] **0xF8 (single OR run) is intra-bubble whitespace** — corrects the earlier "F8×2 = bubble break" reading. Verified at event 045 byte 0x021e where F8×2 sits inside the "Might I pose a question, Ramza? What purpose…heed?" bubble.
- [x] **0xE0 = "Ramza" placeholder** — fixes "Might I pose a question, ?" → "Might I pose a question, Ramza?" and "'Tis 's noble disposition" → "'Tis Ramza's noble disposition".
- [x] **0xDA 0x68 = em-dash, NOT player name** — earlier reading produced "forgetRamzathey did save"; user verified the game shows "And let us not forget—they did save the marquis's life." Same fix corrects "your nameor" → "your name—or".
- [x] **Speaker null when the segment lacks its own 0xE3 0x08 marker** — inherited speakers across FE boundaries produced false confidence (event 045 boxes 4–11 read "Delita" but the game shows Dycedarg/Ramza). fft.sh now falls back to neutral `[narrator]` for those boxes instead of lying. The .mes file does not encode mid-scene speaker changes; mid-scene accuracy stays open in `DEFERRED_TODO.md`.
- [x] **Old "F8×N = bubble boundary" tests updated** — `MesDecoderBoxGroupingTests.DecodeBoxes_ConsecutiveF8_*` rewritten to assert the corrected intra-bubble-whitespace contract.

**Not yet done (carried into DEFERRED_TODO.md):**

- [ ] ⚠ UNVERIFIED — Dorter event 38 (the original 45-bubble repro) hasn't been re-tested with the new pagination model; needs a live re-count next time the cutscene plays.
- [ ] Mid-scene speaker accuracy — three options described in DEFERRED_TODO.md (hand-curate, memory hunt, .evt parse).

**Memory notes saved:**

- See `project_dialogue_decoder_2026_04_26.md` — full byte semantics writeup (FE/F8/E0/DA68/E3 08), sentence-balanced pagination algo, and the open speaker-accuracy paths.
- See `feedback_tdd_default.md` — user explicitly asked for test-first discipline after this session went impl-first; default to TDD on future bug fixes.
