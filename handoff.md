# Session Handoff — 2026-04-18 (Sessions 34-43)

Delete this file after reading.

## TL;DR

**Ten consecutive 6-task batches grew the Tavern rumor city+row system from zero to 9 settlements seeded, extracted three pure-class modules (RumorResolver, ScreenNamePredicates, CityRumors), and fixed two latent bugs (Zodiark 0-cost, Orbonne Victory/Desertion). Tests 2852 → 3201 (+349 new, 0 regressions).**

Chapter-1 "uniform rumor hypothesis" held for 8 consecutive cities (Dorter/Gariland/Warjilis/Yardrow/Goug/Zaland/Lesalia/Bervenia all share the same 3 rows = corpus #10/#11/#19) before Gollund broke it at session 42 by adding row 3 = corpus #20 "The Haunted Mine" — a Gollund-specific rumor. The `Chapter1UniformRows` shared dictionary now services 8 cities; Gollund has its own dict; `IsChapter1UniformCity(cityId)` is the predicate.

28 live-captured sell-price overrides surfaced a novel ratio finding: swords sell for **9-29% of buy** (not 50%), katanas/staves cluster at ~50%, Battle Axe/Giant's Axe sell at 100% (sell=buy), and Mage Masher + Serpent Staff sell above buy. The old "buy/2" fallback is wrong for swords; `GetSellPriceWithEstimate` + ground-truth flag surfaces the estimate vs override distinction to callers.

## Commits

| Commit | Session | Subject |
|---|---|---|
| `5b0a9ac` | s34-36 bundle | city+row rumors + test hardening + refactors |
| `52377e5` | s37 | Warjilis rumors + RumorResolver + IsBattleState extraction |
| `fd324e6` | s38 | Zodiark fix + Yardrow seed + party-menu predicates + sell prices |
| `8188fb7` | s39 | Goug seed + GetSellPriceWithEstimate + IsPartyTree fix + docs |
| `84c971b` | s40 | Zaland seed + Chapter1UniformRows refactor + more sell prices |
| `1a15eaf` | s41 | Lesalia seed + IsChapter1UniformCity + coverage audits |
| `48c7f94` | s42 | Gollund seed (FIRST divergence) + IsShopState + RumorResolver priority |
| `3bc6d01` | s43 | Bervenia seed + TableSnapshot + corpus city-mention audit |

## What landed, grouped by theme

### Tavern rumor city+row system (primary deliverable, sessions 34-43)

- **`CityRumors.cs` (new)** — `(cityId, row) → corpusIndex` map, `CityId` constants for all 15 settlements, `Chapter1UniformRows` shared dictionary (8 uniform cities point to it via `ReferenceEquals`), `Lookup`/`CitiesFor`/`IsChapter1UniformCity`/`TableSnapshot`/`AllMappings` API, `NameFor`/`IdFor` case-insensitive round-trip.
- **`RumorResolver.cs` (new, session 37)** — pure 4-tier resolution extracted from CommandWatcher. `Resolve(lookup, searchLabel, locationId, unitIndex) → Result{Ok, Rumor, Error}`. Priority: title → substring → city+row → raw index. Priority-ambiguity tests pin branch order.
- **`get_rumor` bridge action** — accepts `{searchLabel: "title or phrase"}` OR `{locationId, unitIndex}` OR `{unitIndex: N}`. Delegates to `RumorResolver`.
- **9 live-verified cities** seeded: Dorter (s33), Gariland (s36), Warjilis (s37), Yardrow (s38), Goug (s39), Zaland (s40), Lesalia (s41), Bervenia (s43) all uniform; Gollund (s42) divergent.
- **`list_rumors` FirstSentence previews** — replaced 80-char raw truncation with sentence-terminated (or 120-char + ellipsis) first-sentence extraction for easy title matching.

### Screen classification refactors (session 37-42)

- **`ScreenNamePredicates.cs` (new)** — null-safe predicates replacing scattered string checks:
  - `IsBattleState(name)` — refactored 10 call sites across NavigationActions/CommandWatcher/TurnAutoScanner (session 37). All 3058 tests green after.
  - `IsPartyMenuTab(name)` / `IsPartyTree(name)` — distinct scopes for the 4 tabs vs the Units-tree subtree (session 38).
  - `IsShopState(name)` — 8 shop-adjacent screens (session 42). Disjoint-with-IsBattleState invariant pinned.
- **`ScreenDetectionLogic` eventId constants** — named the `>= 1 && < 400` and `== 0 || == 0xFFFF` magic numbers: `EventIdRealMin`, `EventIdRealMaxExclusive`, `EventIdMidBattleMaxExclusive`, `EventIdUnsetAlt`. Helpers: `IsRealEvent`, `IsEventIdUnset`, `IsMidBattleEvent`.

### Bug fixes

- **Zodiark 0-cost filter (session 38)** — `ComputeNextJpForSkillset` was surfacing cost=0 as minimum for Summoners with unlearned Zodiark. Fix: filter `cost <= 0` alongside `cost == null`. Test flipped from characterization of the bug to the correct behavior (Moogle 110, not Zodiark 0).
- **Orbonne Victory/Desertion (session 34)** — Session 21 had captured slot0=0x67 at Orbonne; the unitSlotsPopulated rule missed. New branches in ScreenDetectionLogic handle the variant with 11 boundary tests pinning eventId 1/399/0/400 + paused×submenuFlag grid + party/ui gates.
- **IsPartyTree scope alignment (session 39)** — my session-38 `IsPartyTree` included JobSelection; production roster-populate block did not. Aligned helper to match production (excluded JobSelection — different cursor semantics), refactored the production site to delegate.

### Item price ground-truth expansion

- **28 sell-price overrides live-captured** across Gariland/Yardrow/Goug/Zaland/Gollund. Memory note `project_sell_price_ratio_variance.md` documents novel ratio findings.
- **`GetSellPriceWithEstimate(id)` tuple API (session 39)** — one-call `(Price, IsGroundTruth)?` replaces paired `GetSellPrice` + `IsSellPriceGroundTruth`. `InventoryReader.DecodeRaw` adopts it.
- **ItemPrices coverage floor test (session 41)** + missing-override audit (session 43) — floor at 15% ground-truth coverage (actual ~20+%), orphan-override guard invariant.

### Meta-testing / coverage audits

- **`SkillsetNameReferenceTests` (session 37, new)** — pins canonical skillset names. Motivated by a silent-no-op bug where a test used "Summon Magicks" (wrong) instead of "Summon" (correct) — the `if (x == null) return;` defensive pattern turned the test into a trivial pass.
- **`AbilityJpCosts` CoverageAudit tests (session 41)** — surfaced Jump + Holy Sword skillsets at 100% cost-unknown rate. Characterized the gap; backfill deferred (see `memory/feedback_characterization_tests_for_known_bugs.md`).
- **`CorpusCityMentionTests` (session 43, new)** — scans corpus bodies for city name mentions. Pins known mapping (Gollund→#20) + flags candidate rehosts for Chapter-2+ (#12 Warjilis, #15 Lionel, #23 Bervenia+Dorter).

### Doc updates

- **`ColorMod/GameBridge/README.md` (new, session 39)** — 58-line jump-table for all small pure-class APIs.
- **`Shopping.md`** — 4-tier rumor resolution documented.
- **`TavernRumorTitleMap.md`** — city+row workflow + known-unmappable section.
- **`CLAUDE.md` / `README.md` / `docs/ARCHITECTURE.md`** — stale "1101 tests" count removed.
- **`NavigationPaths.ReturnToWorldMap` docstring** — escape-count → nesting-depth table.

## Technique discoveries worth propagating

### 1. Characterization tests for known-latent bugs (memory note: `feedback_characterization_tests_for_known_bugs.md`)

When a coverage/audit test reveals a real bug, pin the CURRENT (wrong) behavior as a test. On fix, flip the assertion. Used successfully on Zodiark 0-cost (session 38 flipped a session-37 char test) and Jump/Holy Sword gaps (session 41 characterized without fixing).

### 2. Shared-data-structure + ReferenceEquals predicate (`Chapter1UniformRows`, session 40)

All 8 uniform Chapter-1 cities point to the same `IReadOnlyDictionary<int, int>` instance. `IsChapter1UniformCity(cityId)` uses `ReferenceEquals(Table[cityId], Chapter1UniformRows)`. Adding a new uniform city is a 1-line table entry; detecting divergence is a free side-effect.

### 3. Screenshot-cross-reference for live-verifying data-plane seeds (sessions 34-43)

Pattern: travel → enter Tavern → screenshot UI → `read_rumor "distinctive phrase"` → confirm bridge returns the body that matches the on-screen text. Catches both data-seed errors AND bridge-wiring errors in one step. Used 9× this cycle; only Gollund (session 42) produced a novel finding (row 3 divergence).

### 4. Disjoint-invariant tests for screen-predicate categories

`IsShopState` and `IsBattleState` have an explicit test that no name is both. Cheap (one test) and catches scope drift when either predicate grows.

### 5. Meta-tests for test-suite correctness (`SkillsetNameReferenceTests`)

Pinning the canonical names that tests reference catches silent no-ops (like session 37's "Summon Magicks" typo). Explicit counter-examples in the test prove the ambient contract (case-sensitive, exact match).

## What's NOT done — top priorities for next session

### 1. Live-verify session-39 IsPartyTree refactor

Session 41 tried live-verify but the game crashed. Low risk (behavior-preserving refactor, 3058 unit tests green) but unconfirmed in-UI. Next session with a live game: open CharacterStatus on any unit, `screen -v`, confirm `roster` is populated. 30 seconds once the game's up.

### 2. Seed remaining 6 Chapter-1 settlements

Riovanes(1), Eagrose(2), Lionel(3), Limberry(4), Zeltennia(5), Sal Ghidos(14) — most are battle-locked or story-progression-locked in Chapter 1. Seed them as each becomes accessible. If any breaks the uniform set, split a per-chapter/per-city dictionary.

### 3. Check the 3 candidate city-specific rumors at Chapter-2+

`CorpusCityMentionTests` flagged corpus #12 (Warjilis), #15 (Lionel), #23 (Bervenia+Dorter) as "mentions this city by name but doesn't appear at that city's Chapter-1 tavern." Re-verify each after Chapter-2 story progress. Likely rehost candidates (same pattern as #20 at Gollund).

### 4. Backfill Jump + Holy Sword JP costs OR remove from coverage audit

Session 41 characterized both as 100% uncovered. Fix path: (a) populate real costs from Wiki/live-capture OR (b) explicitly skip them in the audit (already done via `CoverageAudit_KnownUncoveredSkillsets_StillReturnNull`) and document the structural reason (Jump collapses sub-abilities).

### 5. Live-verify queue (carryover from sessions 31-33)

Unchanged since session 33 handoff — `!weak` / `+absorb` / `>BACK` / `!blocked` per-tile sigils + Self-Destruct auto-end + weather damage modifier. All code paths shipped; blocked on in-game setup (need specific enemy types + caster classes).

### 6. Tavern rumor cursorRow pointer-chain walk (session 33 memory note)

Memory address `0x13090F968` is the live cursor byte but heap-shuffles across restarts. Memory note `project_tavern_rumor_cursor.md` has the widget-struct layout. Needs an AoB anchor or widget-vtable walk to re-derive at runtime. Would unblock fully-automated rumor reading from cursor position.

## Things that DIDN'T work (don't-repeat list)

1. **Writing `[Theory]` tests that reference a non-existent skillset name and pair it with `if (x == null) return;`** — session 37 hit this with "Summon Magicks" (real name is "Summon"). `SkillsetNameReferenceTests` now guards against future instances. Always assert the lookup is non-null before the defensive return.

2. **Assuming the "buy/2" sell-price formula holds universally** — session 38 and 42 confirmed it's wrong for swords (9-29%) and axes (100%). Don't trust buy/2 for any category without live capture; use `GetSellPriceWithEstimate` to surface the ground-truth flag.

3. **Assuming uniform behavior across all Chapter-1 cities** — session 42 Gollund shattered the 8-city uniform pattern. The `Chapter1UniformRows` shared-dict design now elegantly handles the divergence without rewriting the whole table.

4. **Long scroll loops via the bridge** — Gollund session 42 task 54 tried 100× ScrollDown; bridge crashed. 30-40× is the safe cap. Break into smaller batches.

5. **Tests that assert BOTH shopState AND gilSurfacing logic via ShopGilPolicy** — session 43 tried to adopt `IsShopState` in `ShopGilPolicy` but their scopes differ (gil screens include WorldMap/PartyMenuUnits, `IsShopState` includes TavernRumors/Errands). Different abstractions; don't conflate.

## Things that DID work (repeat-this list)

1. **One-commit-per-session cadence with live-verify as the last step** — each session ended in a commit and a restart-and-verify for the session's data-plane changes. 8 consecutive clean commits, 0 regressions across 349 tests.

2. **TDD on pure-class extractions** — `RumorResolver`, `ScreenNamePredicates`, `EventIdClassifier` helpers all extracted with red-test-first. Zero mid-extraction surprises; each refactor immediately ran under broad existing test coverage.

3. **`Chapter1UniformRows` shared-dict pattern** — saw the duplication at session 39 (5 cities with identical bodies), extracted session 40. Divergence (Gollund) slotted in cleanly without breaking the 8 aligned cities.

4. **Live-capture sell prices in small tier batches** — 5 swords (s38), 4 late-game (s39), 10 katanas+staves (s40), 5 mixed (s42) — each batch small enough to screenshot, verify, ship in one session without bridge fatigue.

5. **Memory notes that capture the NEGATIVE finding** — `project_sell_price_ratio_variance.md` pins the fact that there's no simple sell formula. Future sessions won't re-derive "buy/2 must work, I just need to find the exception."

6. **Characterization tests for latent bugs** — Zodiark 0-cost, Jump/Holy Sword gaps. Flip-the-assert fix pattern makes the bug visible without requiring immediate repair.

## Memory notes saved this session

- **`project_chapter1_rumor_uniformity.md`** (new) — 8 uniform cities + Gollund divergence mapping; pattern for new city seeding; candidate rehosts at Chapter-2+.
- **`project_sell_price_ratio_variance.md`** (new) — sell/buy ratios by category; novel sell=buy (axes) and sell>buy (Mage Masher/Serpent Staff) patterns documented.
- **`feedback_characterization_tests_for_known_bugs.md`** (new) — flip-the-assert pattern for known latent bugs.
- Updated `MEMORY.md` index with all three.

## Quick-start commands for next session

```bash
# Baseline sanity
./RunTests.sh                               # expect 3201 passing
source ./fft.sh
running                                      # check game alive

# Verify Gollund divergence still works (session 42's flagship finding)
fft "{\"id\":\"$(id)\",\"action\":\"get_rumor\",\"locationId\":8,\"unitIndex\":3}"
# Expect: corpus #20 "The Haunted Mine" body

# Verify uniform city (Dorter) row 3 is still null
fft "{\"id\":\"$(id)\",\"action\":\"get_rumor\",\"locationId\":9,\"unitIndex\":3}"
# Expect: failed, "No rumor mapped for city 9 row 3"

# Sell-price tuple API smoke test
# (via InventoryReader — check an item with a live override like Dagger id=1)

# Rumor corpus size check
list_rumors | grep -c "@0x"                 # expect 26

# Session-43 COMPLETED inventory
# See FFTHandsFree/COMPLETED_TODO.md under "Sessions 34-43 (2026-04-18)"
```

## Top-of-queue TODO items the next session should tackle first

From `FFTHandsFree/TODO.md §0`:

1. **Live-verify IsPartyTree refactor** — 30-sec regression check deferred session 41.
2. **Seed remaining Chapter-1 settlements** — 6 more (Riovanes/Eagrose/Lionel/Limberry/Zeltennia/Sal Ghidos), mostly story-locked.
3. **Check candidate city-specific rumors at Chapter-2+** — #12 Warjilis, #15 Lionel, #23 Bervenia+Dorter.
4. **Jump + Holy Sword JP cost backfill OR explicit skip** — current status characterized, fix deferred.
5. **Live-verify queue** — `!weak` / `+absorb` / `>BACK` / `!blocked` / Self-Destruct / weather damage modifier (carryover from sessions 31-33).

## Insights / lessons captured

- **One small hypothesis per session, verified live, then committed.** The city+row rumors work structured cleanly across 10 sessions because each session seeded 1 new city and tested 1 new invariant. Gollund's divergence (session 42) was a high-signal finding precisely because we'd already verified 7 uniform cities — the contrast made the divergence legible.

- **Extract pure-class helpers when a refactor wants to fire.** `RumorResolver`, `ScreenNamePredicates`, `IsRealEvent` etc. were all "inlined in CommandWatcher" until the test needed to characterize the priority/scope separately. Each extraction paid for itself within 1-2 sessions.

- **Disjoint-invariant tests cost one line and catch real bugs.** `IsShopState` + `IsBattleState` should never both fire. Pinning that saves future contributors from accidentally adding "Cutscene" to both scopes.

- **Shared data structures with ReferenceEquals predicates scale nicely.** `Chapter1UniformRows` + `IsChapter1UniformCity` pattern: 1 dictionary, 8 city entries, 1 predicate, easy divergence handling. Would work for any "most entries are the same, a few are special" data table.

- **A latent bug characterized + tracked is better than one silently left.** Session 37 found Zodiark, session 38 fixed it. Session 41 found Jump/Holy Sword, characterized for next session. This is cheaper than gate-keeping "only ship fixes" and more legible than "silently skip the failing case."

- **Live-verify is cheap insurance against data-plane typos.** 9 cities × ~2 min each = 18 min spent on live-verify across 10 sessions. Surfaced the Gollund divergence that 3 sessions of pure unit testing would have missed.
