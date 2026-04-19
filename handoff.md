# Session Handoff — 2026-04-19 (Session 46)

Delete this file after reading.

## TL;DR

**Long state-detection desync hunt + crash investigation. 3 commits, tests 3283 → 3337 (+54), two live-verified fixes, one game-crash root cause identified and patched, one scaffold staged for next session.**

The headline: **the "state constantly desyncs" pain was many small fixes piled on top of each other**. Adversarial unit-test probes surfaced 7 desyncs in minutes. Live in-game stress testing turned up two game crashes that the mod log traced to a retry-storm in `HoveredUnitArray.Discover()` — the `_discoveryAttempted` flag was set but never read, so failed initial scans re-ran 200MB of heap scan per roster slot per screen query (~3GB under failure). One-line guard kills it. 83x reduction in SearchBytes calls post-fix (2/73 commands vs 86/29 previously).

Also shipped: SM auto-snap on category mismatch (pure-C#, silent realignment, no keypresses), within-PartyTree auto-snap via `menuDepth==0`, BattleVictory `encA=255` sentinel rule (with `battleTeam==0` guard), SM-driven BattlePaused cursor tracking (supersedes the flaky memory resolver), `LastDetectedScreen` string mirror that fixes session-45's `sourceScreen` stuck-at-TitleScreen bug, WorldMap↔TravelList ambiguity-resolver 4-arg overload (trusts SM when freshly-seeded). Removed `fft_resync` helper per user direction ("bandaid, not a fix").

## Commits

| Commit | Subject |
|---|---|
| `a32ab73` | State-detection fixes + SM auto-snap (session 46) — multi-rule bundle, 54 new tests |
| `0a19777` | UserInputMonitor scaffold (inert pending deploy) — polls user keys, de-duped against bridge |
| `35b068d` | HoveredUnitArray: respect _discoveryAttempted guard — 2-line crash fix |

## What landed, grouped by theme

### State detection (commit `a32ab73`)

- **BattleVictory encA=255 rule** (`ScreenDetectionLogic.cs:453-461`) — unique sentinel `encA==255 && encB==255 && battleMode==0 && paused==0 && battleTeam==0` runs BEFORE `IsMidBattleEvent` to stop post-victory eventId=41 from routing to BattleDialogue. Live-verified at Zeklaus win.
- **Turn-owner guard relaxed** — dropped `!actedOrMoved` from `battleTeam==1/2` rules. Enemies mid-action keep their team label instead of falling through to player submodes.
- **Tab-flag world-map guard** — `unitsTabFlag`/`inventoryTabFlag` skipped when world-map signal present (`hover 0..42` or `moveMode` active). Stops post-flee stale flags from latching WorldMap into PartyMenuUnits.
- **inBattle moveMode flicker guard** — when `slot9=0xFFFFFFFF && battleMode ∈ 1..5`, a one-frame `moveMode=13` flicker no longer escapes the in-battle branch.
- **Ambiguity-resolver 4-arg overload** — `ResolveAmbiguousScreen(smScreen, detectedName, keysSinceLastSetScreen, lastSetScreenFromKey)` trusts SM when freshly-seeded (post-boot/drift-recovery). Handles the byte-identical WorldMap↔TravelList case without breaking the existing "trust detection when SM is stale" contract.

### SM state-sync (commit `a32ab73`)

- **`LastDetectedScreen` string mirror** on `ScreenStateMachine` — tracks ANY detection result (including screens the enum doesn't model). Fixes session-45's `sourceScreen: "TitleScreen"` session-wide leak. `SetScreen()` keeps the mirror in lockstep.
- **`ObserveDetectedScreen(name)`** — updates the mirror on every `screen` query. Null/empty inputs are dropped so transient detection failures don't wipe last-known-good state.
- **`AutoSnapIfCategoryMismatch`** — pure-C# realignment when SM's enum category (InBattle / WorldSide / PartyTree / DialogueOrCutscene) disagrees with detection's. No keypresses fire. Dialogue tolerated on any category (overlay).
- **`SnapPartyTreeOuterIfDrifted(detectedName, menuDepth)`** — within-PartyTree realignment. Detection can't distinguish PartyMenuUnits from CharacterStatus/EqA/Picker/JobScreen (memory-identical), so uses `menuDepth==0` as the "outer grid" authority. When SM thinks deeper but memory says outer, realign.
- **`BattlePausedCursor`** — SM-tracked cursor row (0..5, wraps). Resets on entry via `ObserveDetectedScreen` transition detection. Updates on Up/Down via new `OnKeyPressedForDetectedScreen(vk)` method. Supersedes the flaky memory resolver that latched on first-Down candidate. Live-verified Data→Retry→Load→Settings→ReturnToWorldMap→Up→Settings at Grogh Heights.

### User-input hook (commit `0a19777`)

- **`UserInputMonitor`** class — new file. Polls `GetAsyncKeyState` for nav keys (arrows, Enter, Escape, Space, QEART, B, Y, X) every 20ms. Gated on `GetForegroundWindow == game window`. De-duped against bridge-sent keys via `MarkBridgeSent(vk)` 150ms timestamp window. Forwards user keys to `SM.OnKeyPressed` + `SM.OnKeyPressedForDetectedScreen`.
- **NOT wired into bootstrap** in the committed state. `ModBootstrapper.cs` change staged in working tree pending live-verify. The property slot is null by default; `MarkBridgeSent` calls are no-ops.

### Crash fix (commit `35b068d`)

- **`HoveredUnitArray.Discover()`** — 2-line guard. If `_discoveryAttempted && _arrayBase == 0`, return false immediately. Use `Invalidate()` to force rescan after save-load. Addresses the retry-storm that triggered two game crashes earlier this session. **Live-verified 83x reduction**: 2 scans across 73 commands vs previous 86 scans across 29 commands.

### Helper removal

- **`fft_resync` removed from `fft.sh`** — user direction: "bandaid, not a fix". The keypress-based escape-storm could drive the game into unintended states from hostile screens. State-detection bugs belong in the detection layer. Cleaned up 4 help-text references + deleted 2 obsolete memory notes (`feedback_use_fft_resync.md`, `feedback_fft_resync_forbidden_states.md`).

## The technique of the session

**Adversarial-probe unit-test battery.** Before touching the game, wrote 15 probes targeting rule-boundary + rule-collision + sticky-flag + sentinel-drift + crystal-encA-threshold scenarios. 7 of 15 probes caught real desyncs (~47% hit rate). Fixed each one with a narrow rule change + regression guard. Live testing in-game only confirmed the fixes; the hunt happened at unit-test speed. **Generalizable: when a class of bugs exists, push adversarial inputs at the rules — don't wait to repro live.**

**User hypothesis guides probes.** User asked "if I move the cursor off the node, does ui flip?" — good instinct. Answer was no (ui=0 before AND after), which disproved one theory but narrowed the search. Take user hypotheses seriously as probe candidates.

**Live log tells you when the mod didn't crash.** When the game died twice, the mod log was clean — no exceptions, no errors. That's evidence the process died from external pressure (sustained `ReadProcessMemory` load), not from mod-code bugs. The 200MB SearchBytes lines were the smoking gun.

## Remaining gaps (prioritized for next session)

### 🔴 1. UserInputMonitor — live-verify under user-driven play

Scaffold committed (`0a19777`), bootstrap wire-up staged in working tree (`ColorMod/Core/ModComponents/ModBootstrapper.cs` — NOT committed). **Actually deployed this session** to the running game; log confirms `[UserInputMonitor] started`. What's NOT verified: that user keystrokes typed directly into the focused game window flow to the SM.

**Next-session steps:**
1. `source ./fft.sh && restart` (deploys current committed state, which does NOT start the monitor).
2. Either:
   - Uncomment the `ModBootstrapper.cs` bootstrap block (staged in working tree — just re-apply it) and rebuild.
   - Or re-`git add` the uncommitted `ModBootstrapper.cs` change.
3. Enter battle, open pause menu via `execute_action Pause`, verify `ui=Data`.
4. Press Down **on the keyboard directly** (game window must be focused).
5. `screen` — verify `ui=Retry` (the SM should have seen the user's Down).
6. If it works: commit `ModBootstrapper.cs` with message "UserInputMonitor: wire into bootstrap".
7. If it misbehaves (double-counts bridge keys, lags, fires during non-game focus): debug the de-dup window / focus check in `UserInputMonitor.cs:84-108`.

### 🟡 2. Extend SM cursor tracking to other battle screens

Session 46 shipped `BattlePausedCursor`. Same pattern (`project_sm_cursor_tracking_pattern.md`) should extend to:

- **CharacterStatus sidebar** — 3 items (Equipment & Abilities / Job / Combat Sets), vertical wrap. Already enum-tracked via `SidebarIndex` but not exposed via `OnKeyPressedForDetectedScreen`.
- **BattleAbilities submenu** — variable count depending on unit job (Attack + each learned skillset + Wait + Status). Session 46 live test showed `ui=Attack` stuck after Down press.
- **BattleMoving grid cursor** — 2D (x,y) tracked via arrow keys + current camera rotation. Session 46 live test showed `ui=(3,0)` stuck after Up/Down.
- **TavernRumors/TavernErrands** — add `TavernCursorRow` property + reset on entry + update on Up/Down.

Each is a ~30-minute ship: property + reset branch + key-handling branch + CommandWatcher wiring + 5 tests.

### 🟡 3. Audit other `SearchBytesInAllMemory` callers for retry-storm

Session 46 fixed `HoveredUnitArray`. Callers that might have the same bug pattern (`_attempted = true` without a corresponding read-guard):

- `NavigationActions.cs:4190, 4430, 4492, 4494, 4580, 4925` — most look user-invoked but verify by tracing invocation paths.
- `ShopItemScraper.cs:43, 152` — should cache per-shop-entry; confirm caching is respected.
- `PickerListReader.cs:103` — declared but unused in CommandWatcher. Safe to ignore unless wired; add a TODO comment.
- `NameTableLookup.cs:291` — already correctly guarded by `_buildAttempted` (verified session 46).

**Audit pattern:** grep for `_attempted = true` or similar one-shot flags, then check whether the same field is read as a guard condition earlier in the same method. If not, it's a retry-storm candidate.

### 🟡 4. WorldMap vs TravelList memory discriminator (optional)

Current handling via `ResolveAmbiguousScreen` 4-arg overload works but relies on SM staying in sync. A pure memory byte that splits the two would be more robust. Session 46 attempted a heap-diff but the "WorldMap" snapshot was accidentally PartyMenu, giving 1.5M noisy changes. Retry with strict pre/post visual confirmation.

### 🟡 5. Fight→Formation transition settle

The generic settle loop caps at 1s (3s was tried and reverted due to menu-nav perf regression). Formation loads after `execute_action Fight` can exceed 3s (observed 5+s at Grogh Heights). Fix: per-action custom wait in the `Fight` action handler that polls until `BattleFormation` detected OR 10s elapsed, independent of the generic settle.

### 🟢 6. `execute_action` fail-loud on unknown action name

Stress test session 46: `execute_action Leave` on TavernRumors silently failed because the path there is named `Back`. Bridge doesn't error — just drops the command. Two fixes:
- (a) Add `Leave` as alias for `Back` in TavernRumors/Errands/pickers — quick compat shim.
- (b) Return `status=failed` with `Error: "Unknown action 'Leave'. Available: Back, CursorUp, CursorDown, ..."` — makes bugs loud.

Preference (b) but (a) is fine as a stopgap.

### 🟢 7. BattleSequence flag sticky after restart

Still open from session 33/44 (`project_battle_sequence_flag_sticky.md`). First-frame misfire after `restart` at a BattleSequence location labels WorldMap as BattleSequence. Low blast radius (transient) but real.

## Quick-start commands for next session

```bash
# Baseline sanity
./RunTests.sh                               # expect 3337 passing + 4 skipped

source ./fft.sh
running                                     # check game alive
screen                                      # should be WorldMap at Merchant City of Dorter (last-session state)

# Quick sanity that the HoveredUnitArray fix is still in effect:
# after 10+ menu-tree nav operations, live_log should show <30 SearchBytes total.
for i in 1 2 3 4 5; do screen; done
grep -c 'SearchBytes' "$FFT_MODDIR/claude_bridge/live_log.txt"

# If UserInputMonitor live-verify is the top priority:
# 1. Un-stash the ModBootstrapper change:
git diff ColorMod/Core/ModComponents/ModBootstrapper.cs
# 2. If it shows the UserInputMonitor bootstrap block, continue. If empty,
#    re-add the 10-line "// Session 46: user-input monitor..." block inside
#    the `if (CommandWatcher != null) { ... }` body.
# 3. Rebuild + deploy:
source ./fft.sh && restart
# 4. Live-verify: enter pause menu, press Down on keyboard directly.
# 5. `screen` should show ui=Retry not ui=Data.
```

## Things that DIDN'T work (don't repeat)

- **Raising the settle cap from 1s to 3s.** Made every menu nav slow (PartyMenu/CS queries jumped from ~170ms to ~3300ms in jitter-prone paths). The fix-target was Fight→Formation load (5s+), which exceeded both caps anyway. Reverted. Next attempt should be **per-action custom wait**, not a generic cap.
- **Trying to discriminate post-load WorldMap from TravelList via a new Detect() rule.** All 24 inputs are byte-identical. The rule I added (hover=254 guard) immediately broke the shop tests because Dorter OutfitterBuy also has hover=254. Reverted in favor of SM+key-count fallback.
- **Keypress-based `fft_resync` as a universal recovery helper.** Every state has a different exit protocol (BattlePaused needs cursor-aware nav to ReturnToWorldMap; BattleFormation needs Escape; Cutscene needs Enter-spam), and the cursor-memory bug meant BattlePaused nav failed anyway. Removed per user direction — fixes belong in detection, not in a post-hoc key-storm.
- **Auto-firing `fft_resync` on detection↔SM mismatch.** User pointed out: mismatches happen constantly in normal play (SM a tick stale during key-nav). Auto-firing a keypress recovery would hammer the game with unintended keys. The right answer is pure-C# SM-snap (which is what commit `a32ab73` actually did).

## Things that DID work (repeat)

- **Adversarial unit-test probes BEFORE live testing.** 15 probes, 7 hit. This is 10x faster than the traditional "play the game, find a bug, debug it" loop.
- **One-shot live capture via `dump_detection_inputs` + screenshot pair.** The `hover=254, moveMode=255, ...` fingerprint for post-load WorldMap took one screenshot + one dump. Compare against same-looking state's fingerprint to identify what's actually different.
- **Live log tail after a crash.** The mod log showed no exceptions + plenty of 200MB SearchBytes scans. That was the only signal needed to track the crash to HoveredUnitArray.
- **Commit-per-concept, not per-session.** Three focused commits (state-detection / UserInputMonitor scaffold / HoveredUnitArray fix) rather than one big session-46 commit. Lets future sessions cherry-pick or revert cleanly.
- **Running the full suite after every rule change.** Caught 3 regressions before they shipped (shop hover=254, TravelList ambiguity test, Stress_PostLoadWorldMap test).

## Memory notes saved this session

Added:
- `project_sm_cursor_tracking_pattern.md` — the BattlePaused-cursor pattern, extendable to other menus.
- `project_worldmap_vs_travellist_byteidentical.md` — 24-input fingerprint comparison proving memory-identical.
- `feedback_discovery_must_check_attempted_flag.md` — the HoveredUnitArray retry-storm pattern as a cross-codebase audit warning.

Removed (obsolete):
- `feedback_use_fft_resync.md` — helper removed.
- `feedback_fft_resync_forbidden_states.md` — helper removed.

Index `MEMORY.md` updated with the 3 new entries + pruned the 2 obsolete ones.

## Uncommitted work (call-out)

**`ColorMod/Core/ModComponents/ModBootstrapper.cs`** has an uncommitted change: 10-line `UserInputMonitor` bootstrap block that creates and starts the monitor. It's staged in the working tree but **not committed** because user direction was "don't commit until you've proven live it works" and user-driven verification requires them at the keyboard (they stepped away).

**To ship next session:** see "Remaining gaps #1 — UserInputMonitor" above for the live-verify procedure.

## Insights / lessons captured

- **When a "recovery helper" starts gaining complexity, that's a signal the underlying state signal is wrong.** `fft_resync` was getting per-state exit code, cursor-aware nav, dispatcher tables. Removing it forced fixes to land in the detection layer where they belong. Apply this heuristic to future helpers.
- **The difference between bridge-driven and user-driven gameplay matters for SM-tracked state.** Detection (memory reads) is correct regardless of who pressed the keys. SM (key-press history) is only correct when all keys flow through the bridge. Document every SM-tracked field with "fails under manual control unless UserInputMonitor is active."
- **Memory-identical states require SM+key-count discrimination.** WorldMap and TravelList post-load have zero distinguishing bytes. `keysSinceLastSetScreen==0 && !lastSetScreenFromKey` cleanly splits "SM freshly-seeded after boot" from "SM stale because a key wasn't observed."
- **Write-without-read on a guard flag is a silent regression.** `HoveredUnitArray._discoveryAttempted = true` was the setup for a one-shot scanner, but the absence of a corresponding `if (_discoveryAttempted) return` guard turned it into a retry-storm. Audit similar patterns across the codebase for this anti-pattern.
- **User hypotheses deserve fast probe-and-disprove loops.** The "ui=1 means cursor on node" theory took 3 minutes to probe and disprove. Even when the user is wrong, the process narrows the search. Don't dismiss; test.
- **Full-suite runs are cheap insurance.** Every time I shipped a rule change, `./RunTests.sh` caught at least one regression before deploy. The ~6s cost is trivial vs. the ~45s cost of a `restart` + manual repro.

---

Next session, **first task is the UserInputMonitor live-verify** — if that lands, we get user-driven + bridge-driven gameplay parity. After that, the SM cursor-tracking pattern extension is the biggest UX win (BattleAbilities / BattleMoving / CharacterStatus sidebar all reliably tracking live).

Memory-based crashes should be gone with the HoveredUnitArray fix, but if they recur, check the live log for new SearchBytes bursts and audit more callers per the pattern.
