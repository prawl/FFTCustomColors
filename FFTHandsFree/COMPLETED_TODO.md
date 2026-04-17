<!-- This file tracks completed work. Moved out of TODO.md during spring cleaning 2026-04-17. -->
# FFT Hands-Free — Completed Work Archive

Items fully shipped ([x]) across sessions. Kept out of TODO.md to make the active queue easier to scan. Partial ([~]) items stay in TODO.md in their original section.

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
