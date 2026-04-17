# Session Handoff — 2026-04-17 (Session 27)

Delete this file after reading.

## TL;DR

**10 commits, +25 tests (2108 → 2137), all on branch `auto-play-with-claude`.** Session theme: ship session 24/26 follow-ups that unblock chain navigation, plus strategic pivots on memory hunts that have resisted multiple sessions.

Biggest wins: chain-nav crash from sessions 22/24 is FIXED (live-verified Cloud→Agrias, the exact previously-crashy sequence, with screenshots). HpMpCache persists Ramza/Kenrick/Lloyd/Wilham HP/MP across reads. NavigationPlanner extracted as pure function with dry-run action. Chain-guard hard block removed after the user correctly observed we'd been fighting a false positive for 5 sessions. `detailedStats` verbose payload surfaces equipment-derived build stats on CharacterStatus.

Two dead-ends definitively documented: shop hovered-item-ID byte is not snapshot-diff-findable (needs widget vtable walk or save decode); zodiac byte is NOT in the 0x258 roster slot under any encoding (9 variants × 4 anchors → 0 matches).

**Commits (oldest → newest):**
1. `b5890f4` — Session 27 pt.1: HpMpCache persists 4-slot HP/MP across reads
2. `7af47c9` — Session 27 pt.2: SetJobCursor + resolver liveness check
3. `0b495d8` — Session 27 pt.3: find_toggle bridge action + shop-item-ID dead-end
4. `f3a3694` — Session 27 pt.4: dry-run nav harness for chain-nav viewedUnit lag
5. `e95038e` — Session 27 pt.6: remove chain-guard hard block
6. `32497e9` — Session 27 pt.7: regression tests for Mettle/Fundaments JP Next
7. `d6f2264` — Session 27 pt.8: wire NavigationPlanner into live, fix chain-nav timing
8. `374c225` — Session 27 pt.9: detailedStats verbose payload on CharacterStatus
9. `2937da4` — Session 27 pt.10: record new-recruit name-lookup blocker for JP Next

(pt.5 was rolled into pt.6 — no gap, just renumbered.)

## What landed, grouped by theme

### Chain-nav reliability (`b5890f4`, `7af47c9`, `f3a3694`, `e95038e`, `d6f2264`)

This session's headline. Sessions 22 and 24 both tried to fix the chain-nav viewedUnit lag (Cloud→Agrias chained nav lands on WorldMap instead of Agrias's EqA) and both reverted after crashing the game. Session 24 explicitly flagged this as "needs a safer harness before next attempt."

Session 27 shipped the harness AND the fix:

- **`NavigationPlanner.PlanNavigateToCharacterStatus`** — pure function returning a typed list of `KeyStep` records with `VkCode`, `SettleMs`, `Reason`, and optional `EarlyExitOnScreen`/`GroupId` for the escape-storm detection-poll. 10 unit tests lock the sequence and settle times.
- **`dry_run_nav` bridge action** — executes the planner without firing keys, logs the plan via `SessionCommandLog` + stderr. Now callers can preview any chain-nav plan before committing.
- **Live `NavigateToCharacterStatus` consumes the planner** — single source of truth. Plan tweaks land in one place and both dry-run + live pick them up.
- **Timing fix: escape settle 300→500ms, final escape 500→700ms.** Root cause: at 300ms the SM's `TravelList→WorldMap` override fires mid-transition (SM predicts WorldMap via key-count BEFORE the game finishes rendering the PartyMenu exit), causing the 2-read confirm to agree on a mid-transition state. Manual stepping with ~500ms settles always worked; the planner now matches that cadence.

**Live-verified across 5 chain hops**: WorldMap→Ramza EqA ✅, Ramza→Kenrick EqA ✅, Kenrick→Agrias EqA ✅, Agrias→Cloud EqA ✅, **Cloud→Agrias EqA ✅** (exact session 24 crash repro). All with screenshots confirming shell-game state match.

Memory note `feedback_chain_nav_timing.md` documents the pattern for future multi-step flows.

### Chain-guard pivot (`e95038e`)

The user observed: "5 prior attempts to block chained commands have failed — let's support them instead." Live evidence: chained Bash calls worked reliably across dozens of iterations this session with no detectable races, no dropped keypresses, SM states always matching the game.

Deleted:
- `_fft_guard`'s `[NO] kill -9 $$` exit path
- Disk `fft_done.flag` file
- The 34-site reset dance in composite helpers (all reset sites still call `_fft_reset_guard`, but it's now a no-op)

Kept:
- `_track_key_call` counter + `[CHAIN INFO]` telemetry (rebranded from `[CHAIN WARN]` to match non-blocking role)
- `_is_key_sending` classifier (still useful)
- Bridge-side auto-delay (C# side, already handled the real race)

### HpMpCache (`b5890f4`)

Session 26 shipped partial HP/MP via `HoveredUnitArray.ReadStatsIfMatches` — first 4 units (Ramza/Kenrick/Lloyd/Wilham) populated, 10 others returned null. Session 27 added a disk-backed cache so even the first 4 stay visible across reads as the cursor moves around. Key discovery: the "hovered array" isn't a roving window — it's a **fixed 4-slot slab** anchored on Ramza's widget. Slot 4-13 widgets are independent heap allocations, not indexed by `ArrayBase + slot*stride`.

6 unit tests. Memory note `project_hovered_unit_array_partial.md` updated with the new findings.

### JobCursor drift-correction infrastructure (`7af47c9`)

Session 24 carryover: plumb `ResolveJobCursor` heap byte into SM drift snap. Done — `SetJobCursor(row, col)` added to SM (bounds-checked, 3 tests). CommandWatcher JobSelection read path compares mem byte to SM, logs `[SM-Drift] JobSelection (a,b) → (c,d)`, and snaps.

**But then I discovered during live test**: the resolver's 2-step axis verify (baseline→+1→+2) can accept **widget-state counters** that track during the rapid oscillation but stay put during real navigation. Symptom: cursor visibly at (0,1) per screenshot but resolved byte reads 0. If we'd trusted the snap, SM would be corrupted.

Added a **liveness probe** at the end of `ResolveJobCursor`: after restore, press Right, read the byte, expect baseline+1. Reject if no change. On this save, all 32 candidates failed liveness — so `_resolvedJobCursorAddr` stays 0 and the snap path stays dormant. Drift correction is plumbed but gated on a live byte being found; infrastructure ready for when one lands.

Memory note `project_jobcursor_liveness.md` documents the technique.

### detailedStats verbose payload (`374c225`)

Surfaces build-planning stats on CharacterStatus via `screen -v`. `UnitStatsAggregate` record aggregates:
- `hpBonus`/`mpBonus` — sum of equipped helm/body/accessory HP/MP bonuses from `ItemData`
- `weaponPower`/`weaponRange`/`weaponEvade`/`weaponElement` — right-hand weapon stats
- `leftHandPower` — dual-wield off-hand (auto-routed to shield fields when it's a shield, not a weapon)
- `shieldPhysicalEvade`/`shieldMagicEvade` — defense values

**Wiki-independent**: pure function computed from `ItemData` constants + roster equipment IDs. No memory hunt, no PSX-vs-IC concerns. 5 unit tests. Live-verified: Ramza hpBonus=350, Agrias shieldPE=75 via Escutcheon auto-routed, Mustadio all-zero (unequipped).

Move/Jump/Speed/PA/MA intentionally NOT surfaced — those need the FFT per-job formula path where wiki values may not match the IC remaster (`feedback_wiki_psx_vs_ic.md`).

### find_toggle + shop dead-end (`0b495d8`)

Shop hovered-item-ID hunt: shipped `find_toggle` bridge action exposing `FindToggleCandidates` to shell queries. Then definitively ruled out the hunt via this session's 3-way oscillation snapshot:
- Cursor row at `0x141870704` works (u32 0/1/2 per scroll)
- But 3-way snapshot diff with delta=1 returns 0 candidates for the hovered-item-ID byte
- Master pool at `0x5D9B52C0` reads all zeros (lazy-loaded or drifted)
- UTF-16 name search finds only the static pool (1 match per item), no heap copy
- u8/u16/u32 ID sequence search: 0 matches

Memory note `project_shop_itemid_deadend.md` lists every approach tried so next session goes straight to widget vtable walk / save-file decode / mod-side render-callback hook.

### Mettle/Fundaments JP regression guards (`32497e9`)

Discovered that Mettle/Fundaments JP costs were already populated in commit `c5bfb01`. Added 5 explicit regression tests so a future edit can't silently un-populate. Saves 20 min of "why is Squire's Next value null?" debugging in a future session.

### New-recruit name-lookup blocker (`2937da4`)

User recruited Crestian (Lv1 Squire, JP 137) to live-test JP Next. Discovered `NameTableLookup` resolves her slot-4 name bytes to "Reis" — collides with existing Lv92 Dragonkin Reis. Game renders "Crestian" correctly; only the shell decoder fails. Two entangled TODOs recorded with the verification path for when the name lookup is fixed.

## Technique discoveries worth propagating

### Timing-sensitive multi-key flows need ≥500ms between keys

Manual stepping at ~500ms/key works reliably; the original 300ms/escape was fast enough for the key to land but not enough for detection to stabilize past mid-transition artifacts. Universal pattern for any escape-storm or multi-step nav that crosses screen boundaries.

### Oscillation-based resolvers need a post-resolve liveness probe

The 2-step axis verify (baseline→+1→+2 on rapid oscillation) isn't a strong enough filter. Widget-state counters trigger on cursor-change events during rapid oscillation but stay put during real user nav. The fix is cheap: one more key press + value check after the oscillation ends. Rejects false positives without rejecting true positives.

### Fixed-slab memory structures look like arrays but aren't

The HoveredUnitArray documentation said "0x200 stride, index 0 = roster slot 0 = Ramza." True for slot 0. False for slots 4+. The array is a **fixed 4-slot slab around Ramza's widget** — other units' widgets are independent heap allocations that can't be indexed from Ramza's base. When a memory structure looks like an array, verify with anchors at index 0 AND index N (not just 0).

### Chain-guard hard blocks are lose-lose

Every iteration of the chain-guard has produced collateral false-positives without preventing the class of bug it targeted. The bridge is single-threaded and auto-delays real races. Don't add hard blocks; use telemetry + let the bridge do its job.

### Pivot to infrastructure when the memory hunt resists multiple sessions

Zodiac byte hunt — 2+ sessions failing, session 27 ruled it out of the roster slot definitively. Shop item ID — 3+ sessions, session 27 ruled it out of snapshot memory. Both got memory notes documenting what's been tried. Don't redo 0-match paths; memory notes are for preventing wheel-reinvention.

### Dry-run harness pattern transfers

`dry_run_nav` logs the plan without firing. Session 24 crashed twice doing blind chain-nav. If a helper is timing-sensitive and has crashed before, always give it a dry-run mode — preview and validate BEFORE firing for real.

## What's NOT done — top priorities for next session

### 1. Fix NameTableLookup for new recruits (`Crestian` → "Reis" collision)

User recruited Crestian for JP Next live-verify; `NameTableLookup` returned "Reis" (collision with existing Lv92 Dragonkin). Blocks `open_character_status Crestian` + two-Reis ambiguity in `GetSlotByDisplayOrder`. Test path: recruit a generic with a name outside the known story table, verify NameTableLookup reads the player-typed name from the live table rather than falling back to a story collision. Unblocks the JP Next live-verify that was queued for this session.

### 2. JP Next live-verify on Crestian (blocked on #1)

Once the name lookup is fixed, Crestian (Lv1 Squire, JP 137) is the ideal test candidate. Her Fundaments primary should show Next at 80 JP (Rush — if nothing learned) or 90 JP (Throw Stone — if Rush learned from the 150→137 JP spend). One-line verification.

### 3. JobCursor: find a byte that passes liveness

Drift-correction plumbed but dormant. All 32 candidates on this save fail liveness. Approaches to try: (a) heap_snapshot instead of module-memory (widget state likely in UE4 heap), (b) narrow the 2-step verify to +1 AND +1 (not +1 and +2) in case the byte is a "changed count" rather than absolute position, (c) multi-key-sequence verify (Right×3, expect +3) to catch candidates that only respond to the first key.

### 4. EqA row resolver: re-fire on detect-drift events

Session 24 carryover. Auto-resolver fires once on EqA entry but stales if the SM drifts mid-session (picker open/close). Consider re-firing on menuDepth re-read after a picker exit.

### 5. Shop item-ID via widget vtable walk

`find_toggle` is the reusable infra. Next path: find the OutfitterBuy widget's vtable via AoB, walk to its `HighlightedItem` field. Alternative: mod-side render-callback hook. Multi-session work.

### 6. Zodiac via heap-struct hunt

Session 27 ruled out the 0x258 roster slot. Three next approaches documented: (a) PartyMenu sort-cycle diff, (b) reverse from battle damage math (set up zodiac-opposite attacker/target, read modifier), (c) HoveredUnitArray dump beyond +0x37.

### 7. BattlePaused → SaveSlotPicker entry point

Session 25 shipped PartyMenuOptions → Save. BattlePaused → Save is a parallel entry the user mentioned. Needs a real battle to verify.

## Things that DIDN'T work (don't-repeat list)

1. **HoveredUnitArray per-unit AoB search via roster equipment bytes.** Widget struct bytes don't mirror roster equipment verbatim — 14-byte roster equipment pattern returned 0 matches. Session 27 attempt reverted; kept the fixed-slab reader + cache.

2. **JP Next investigation via `open_character_status Crestian`.** Name lookup returns "Reis" — collides with existing Reis. Blocked until the name table fix lands.

3. **Chain-guard via shell variables (or disk flag).** 5 attempts, all false-positive-heavy. Removed entirely session 27.

4. **Bridging shop item ID via snapshot-diff.** Widget data not in snapshot-reachable memory. All 3-way toggle searches returned 0 candidates.

5. **Zodiac in roster slot.** 9 encodings × 4 anchors → 0 matches. Ruled out.

6. **Trusting JobCursor's 2-step axis-verify without a final liveness check.** Accepted widget counters; would corrupt SM. Liveness check now mandatory.

## Things that DID work (repeat-this)

1. **Live screenshots after every nav hop.** Caught the chain-nav bug immediately — shell said EqA Kenrick, screenshot showed WorldMap. Instant diagnosis. Repeat for every timing-sensitive helper.

2. **Manual stepping to reproduce timing issues.** When the bridge-driven chain failed but manual stepping worked, the gap was ~200ms. Diagnosis: bridge too fast. Fix: match manual cadence.

3. **Pure functions extracted from live methods, tested in isolation.** `NavigationPlanner` + `UnitStatsAggregator` + `HpMpCache` — all got 5-10 unit tests locking behavior before wiring into live. Each live integration landed clean.

4. **Memory-note the dead-ends as thoroughly as the successes.** Zodiac + shop item-ID got 100+ line memory notes with every approach tried. Next session sees them and skips the 0-match paths.

5. **Dedicated TDD for data-entry tasks.** Mettle/Fundaments was already done, but the regression tests future-proof it. Worth the 10 minutes.

## Memory notes saved these sessions

New entries:

- `feedback_wiki_psx_vs_ic.md` — Wiki is PSX-sourced; prefer memory reads over wiki-formula re-implementations.
- `project_jobcursor_liveness.md` — JobSelection cursor resolver needs a post-resolve liveness check; oscillation+axis-verify alone accepts widget counters.
- `project_shop_itemid_deadend.md` — Shop hovered-item-ID byte unfindable via snapshot diff or AoB. Don't redo the 0-match paths.
- `project_zodiac_not_in_slot.md` — Zodiac byte NOT in the 0x258 roster slot (any encoding). Heap widget or save-file only.
- `feedback_chain_nav_timing.md` — Chain-nav escape storm needs ≥500ms/escape + 700ms final. Below that, SM-based detection override races mid-transition.
- Updated `project_hovered_unit_array_partial.md` with session 27 fixed-slab findings.

All indexed in `MEMORY.md`.

## Quick-start commands for next session

```bash
# Baseline
./RunTests.sh                              # 2137 passing
source ./fft.sh
running                                    # check game alive

# Chain-nav smoke test (was crashy in session 24, fixed 27):
open_eqa Ramza                             # fresh from WorldMap
open_eqa Kenrick                           # chain to Kenrick
open_eqa Agrias                            # chain to Agrias — all should work

# detailedStats on CharacterStatus:
open_character_status Ramza
screen -v                                  # detailedStats section in JSON

# dry-run a nav without firing keys:
fft '{"id":"d1","action":"dry_run_nav","to":"Agrias"}'

# Chain guard removed — this no longer blocks:
source ./fft.sh && screen && screen && screen    # 3 chained reads all succeed

# Check open TODOs
grep -cE "^- \[ \]" FFTHandsFree/TODO.md    # ~180 open
grep -nE "Session 27 — next-up follow-ups" FFTHandsFree/TODO.md
```

## Top-of-queue TODO items the next session should tackle first

These live in `TODO.md §0`:

1. **Fix NameTableLookup for new recruits** — unblocks Crestian JP Next live-verify + fixes the two-Reis ambiguity.
2. **JP Next live-verify on Crestian** — one-line test once #1 lands.
3. **JobCursor: find a byte that passes liveness** — drift correction is plumbed but dormant on this save.
4. **EqA row resolver: re-fire on detect-drift events** — session 24 carryover, partially addressed by the planner wiring but not fully solved.

## Insights / lessons captured

- **"Looks like an array but isn't" is a recurring trap.** HoveredUnitArray had clear documentation saying "0x200 stride, roster-wide" — it's a 4-slot slab. Verify structures with anchors at N distinct indices, not just index 0 and pattern-match.

- **Timing-sensitive helpers need both the settle delay AND manual-step comparison.** The chain-nav timing issue wasn't visible in code review. It showed up only when comparing "what works manually" (500ms between keys) with "what the bridge does" (300ms). Manual reproduction is the diagnostic tool for timing bugs.

- **Oscillation-based memory resolvers need liveness checks, not just axis checks.** Widget-state counters pass rapid oscillation tests but fail real-user-nav tests. Every future cursor-byte resolver should end with a liveness probe.

- **5 failed attempts is evidence, not bad luck.** The chain-guard was a lesson. When the same bug keeps recurring through iterations of the same fix, the fix is wrong. Pivot to a different model rather than iteration 6.

- **Pure functions are the TDD on-ramp for live-game code.** `NavigationPlanner`, `UnitStatsAggregator`, `HpMpCache` — all extractable pure functions with tests. Live integration then becomes a thin consumer. When a live method resists testing, look for the pure function hiding inside it.

- **Memory notes are load-bearing for avoiding wheel-reinvention.** Session 27 saved 4 hours by NOT redoing zodiac + shop item-ID hunts that prior sessions had partially explored. The inverse: if session 27 hadn't written good memory notes, session 28 might redo the 0-match paths we ruled out.

- **Screenshots are the ground-truth diff tool for shell-vs-game state.** Every chain-nav verification this session involved a screenshot. Shell-reported screen doesn't prove the game is there — screenshots do.

- **When the user interrupts to correct you, the correction is usually load-bearing.** "We've tried this 5 times, pivot to supporting" (chain guard) + "it's Crestian not Reis" (name lookup) + "wrong observation, Kenrick HP is 586" (my misread of a screenshot) were all moments where following the literal instruction was wrong — listening to the user's pattern recognition was right.
