# Proposal — fix menuCursor byte drift on BattleMyTurn / BattleActing

## Problem

`screen.ui` on BattleMyTurn shows the wrong action-menu item ("Abilities" when cursor is visually on "Wait", etc.) after non-action transitions like:

1. Pause menu opened + escaped back to BattleMyTurn
2. Abilities submenu opened + escaped back
3. Stale cursor byte read right after enemy turn ends
4. State restoration after a save-load mid-battle

Existing `_actedThisTurn` / `_movedThisTurn` flags (shipped `481e64d`) correct the memory byte after successful actions, but ONLY on the post-action path. Every other stale-read path is uncovered.

Failure mode: Claude reads `ui=Abilities`, sends Enter expecting the Abilities submenu, but the game actually opens whatever the real cursor is on (e.g., Wait's facing screen). Downstream navigation loops fail with confusing errors like "Failed to enter targeting mode (current: BattleMoving)".

## Constraint: memory byte IS the game's ground truth

The cursor byte at `0x1407FC620` is what the GAME reads when Enter is pressed. We cannot SM-shadow our way out of this for reporting purposes — if we report the shadow but the game has a different byte, Claude's Enter triggers the wrong menu item.

This rules out the pure SM-driven shadow pattern (`project_sm_cursor_tracking_pattern.md`) that worked for BattlePausedCursor. That pattern trusts the shadow because the shadow controls reality. On BattleMyTurn, the GAME controls reality via its own auto-advance rules, and the shadow is advisory at best.

## The four options from the TODO

Re-examined with the ground-truth constraint:

| Option | What it does | Risk |
|---|---|---|
| (a) Reset byte on fresh BattleMyTurn entry | **Write memory** 0x1407FC620 = 0 every time we observe a new BattleMyTurn | Invasive — we're overwriting game state. Could fight the game's own auto-advance if we race. |
| (b) Shadow via key-press history | Track expected cursor in SM. Report shadow on BattleMyTurn. | If shadow disagrees with memory, we might report a value that doesn't match what Enter would actually trigger. |
| (c) Write memory after known navigations | Write the byte right after we sent the keys | Race-prone; game may be rendering animation that reads the old value |
| (d) Drop `ui=` label on uncertain transitions | Report `ui=?` when we can't verify | Loses info; Claude can't make menu decisions without polling. Cheap fallback only. |

## Recommendation: Option (a) + (d) hybrid

**Primary fix: write `0x1407FC620 = 0` on detected fresh-entry transitions into BattleMyTurn.**

Rationale: the game doesn't have an auto-advance rule that triggers WITHOUT a prior move/act. Resetting to 0 (Move) on fresh entry mirrors what the game itself does between turns — each unit's turn visually starts on "Move". The game's own reset logic runs at turn-start; we're just making the byte reflect that state unambiguously.

The memory write is a single byte at a known address and happens on a detectable transition, so the race window is small: we observe BattleMyTurn → we write → Claude reads. Post-observation, the game takes over cursor updates via its own input handling (which we mirror through CommandWatcher key forwarding).

**Secondary fallback: `ui=?` on remaining uncertain transitions.** Specifically: when we detect BattleMyTurn from an unknown prior state (empty `LastDetectedScreen`, first-frame-of-battle, etc.), report `ui=?` instead of guessing. Claude can poll `screen` again or use `scan_move` to get authoritative battlefield state.

**No SM shadow.** The existing `_actedThisTurn` / `_movedThisTurn` flags are sufficient for the POST-ACTION path. We don't need a parallel shadow that tracks Up/Down key presses.

## Detection of "fresh entry"

The critical input. A "fresh entry" into BattleMyTurn is any transition FROM a non-action-menu state. Enumerate:

- `BattleEnemiesTurn` / `BattleAlliesTurn` → BattleMyTurn: fresh. Write 0.
- `BattlePaused` → BattleMyTurn: fresh. Write 0.
- `BattleFormation` / `BattleSequence` → BattleMyTurn: fresh. Write 0.
- `BattleDialogue` / `BattleChoice` → BattleMyTurn: fresh. Write 0.
- `BattleMoving` / `BattleWaiting` / `BattleAttacking` / `BattleCasting` → BattleMyTurn: **NOT fresh** (submenu escape). Preserve cursor — the game knows where it was.
- `BattleAbilities` → BattleMyTurn: **NOT fresh** (submenu escape). Preserve cursor.
- `BattleActing` → BattleMyTurn: **NOT fresh** (mid-turn transition). Preserve.

Implementation hook: `ScreenStateMachine.ObserveDetectedScreen` already tracks `prev` → `detected`. Add a branch there that, when `detected == "BattleMyTurn"` and `prev` is in the "fresh-entry" set, emits a side-effect request (via a public event or a queued write) that CommandWatcher handles by writing the byte.

## Implementation plan

### Phase 1 — Pure helper + tests (~1h)

`FreshBattleMyTurnEntryClassifier.IsFresh(string? prev, string detected) → bool`

Pure TDD. 8-10 tests covering the cases enumerated above. No game dependency.

### Phase 2 — MemoryExplorer.WriteByte wrapper (~30min)

Check if `MemoryExplorer` already exposes a write. If yes, use it. If not, add a wrapper around Scanner.WriteBytes for single-byte writes. Logging: every write emits `[MenuCursorReset] wrote 0x0 to 0x1407FC620 (prev={prev} → detected=BattleMyTurn)`.

### Phase 3 — Wire into CommandWatcher (~1h)

In the `screen.Name` resolution block (after `ScreenDetectionLogic.Detect()` and the override cascade), call the classifier. If `IsFresh` returns true, call the write. Do NOT gate on `_actedThisTurn`/`_movedThisTurn` — those are already zero on fresh entry.

Edge case: don't write during the very first frame of a battle (when detection might briefly flicker through non-battle states). Add a `_battleStarted` guard — only reset after the first battle-lifecycle StartBattle event has fired for the current battle.

### Phase 4 — Live verify (~1h)

Play a battle. Trigger each fresh-entry path explicitly:
- Open pause menu, escape, check `ui=Move` (not whatever the byte said)
- Enter abilities submenu, escape, check `ui=Abilities` (preserved — NOT reset per the rules above)
- Let an enemy turn end, check `ui=Move` on the new BattleMyTurn
- Save+load mid-battle, check `ui=Move` on the resumed BattleMyTurn

Watch for any `[MenuCursorReset]` logs firing during legitimate submenu-escape paths (shouldn't happen — those are preserved).

### Phase 5 — Fallback for remaining uncertainty (~30min)

For rare cases where even the fresh-entry write doesn't produce a consistent read (e.g., the byte WRITE fails, or the game races), report `ui=?`. Detection:

- Read byte
- If value is out of range (>4) or the transition is suspicious (e.g., LastDetectedScreen was empty)
- Render `ui=?` in CommandWatcher's per-screen block

This is a cheap safety net. Claude can `screen` again or `scan_move` to get authoritative state.

## Test strategy

- **Unit tests** for `FreshBattleMyTurnEntryClassifier` (~10 cases covering the transition set)
- **Integration test** for CommandWatcher that mocks the Explorer's byte reads and verifies the write request fires on fresh entry but not on submenu escape
- **Live verify** per Phase 4

## Risks + mitigations

| Risk | Mitigation |
|---|---|
| Memory write races the game's own cursor update | Only write on DETECTED fresh entry (i.e., we observed the transition). The game writes on key input; no concurrent race with enemy-turn-end handoff. |
| First-frame-of-battle flicker causes a spurious reset | `_battleStarted` guard — defer until StartBattle lifecycle event |
| Write fails silently | Log on every attempt; failing writes still fall through to the (d) `ui=?` fallback |
| Unknown prior state (LastDetectedScreen == null) | Treat as fresh and reset. If the game's actual cursor is non-zero (impossible per FFT rules at turn start), the write is still correct. |
| Some battle variants DO preserve cursor across turns | Flag in TODO as a known regression path; update the "fresh entry" set if a counterexample surfaces |

## Time estimate

- Phase 1 (pure helper + tests): ~1h
- Phase 2 (WriteByte wrapper): ~30min
- Phase 3 (wire into CommandWatcher): ~1h
- Phase 4 (live verify): ~1h
- Phase 5 (fallback): ~30min

**Total: ~4 hours.** Matches the PLANNING-HEAVY estimate (2-3h spec + 2-3h implement) with 1h saved by choosing option (a)+(d) over the SM-shadow pattern (which would have required more wiring).

## Open questions

1. **Does `MemoryExplorer` already have a single-byte write method?** If yes, skip Phase 2. If no, 30min to add.
2. **Is there a race between our write and the game's own turn-start cursor reset?** If the game also writes 0 on turn-start, we're harmless. If the game writes a non-zero value (unlikely but possible for some menu variants), we override it. First live-verify run should confirm.
3. **What's the behavior when we're on BattleMyTurn from the VERY first battle frame (post-formation, first-unit-acts)?** `LastDetectedScreen` might be empty or `BattleFormation`. Treat BattleFormation → BattleMyTurn as fresh (reset to 0). Already in the enumerated set.

## Not in scope

- **SM-driven shadow tracking of Up/Down presses** — rejected in favor of trusting the memory byte (which is the game's ground truth). If the byte proves unreliable even after the fresh-entry reset, revisit.
- **Reset on submenu-escape paths** — explicitly preserved. The game's own cursor tracks through submenus; we don't want to reset what the game considers valid.
- **User-typed keys during automated play** — out of scope per `UserInputMonitor` note in the pattern memory. Automated play doesn't need it.
