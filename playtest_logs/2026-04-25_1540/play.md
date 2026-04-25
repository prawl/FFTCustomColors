# Playtest play log — 2026-04-25_1540

Append timestamped beats as you play. One or two lines each. Capture
friction in the moment — confusing output, slow commands, surprises,
wins. Don't worry about prose; a fragment is fine.

Example beats:
- `T+0:30 — Reading docs. Commands.md is dense; needed two passes.`
- `T+3:15 — First scan_move. Attack tiles empty? Confused.`
- `T+8:00 — Aurablast → "HIT (damage unread)". What's "damage unread" mean?`

---

- T+0:00 — Start. Read all 7 instruction MDs. Sourced via `./fft <helper>` per call.
- T+2:30 — `screen` already returns the rich scan dump on first call (no separate scan_move needed). Nice. ui=Move on a fresh BattleMyTurn matches the docs.
- T+3:00 — Plan: Ramza buffed (Regen/Protect/Shell, Chaos Blade), 3 enemies all 10-13 tiles away. Move west to close distance, wait. Skeleton is Undead (Phoenix Down OHKO target).
- T+3:30 — `scan_move` is deprecated → redirected to `screen` ("USE screen" banner). Docs everywhere still recommend `scan_move`. Friction.
- T+4:00 — `battle_ability "Phoenix Down" 4 6` returned "Used Phoenix Down on (4,6)" but screen stayed `BattleAttacking ui=Phoenix Down` — turn never resolved. Had to call `battle_wait` to force end. Phoenix Down didn't actually land (Skeleton 680/680 unchanged). Tried again at (4,7), same outcome. Strong friction — the success message is misleading.
- T+4:30 — `execute_turn 4 8 Attack 4 7` worked great: Chaos Blade hit Skeleton for 336 (680→344). Bomb died from Counter (380 dmg). Header missing active-unit info on execute_turn responses (just `[BattleMyTurn] ui=Move`).
- T+5:00 — Beat marker. Ramza HP 635/719 (Regen healing well). Skeleton fled, Goblin closing.
- T+5:30 — Throw Stone via `battle_ability "Throw Stone" 2 6` worked CLEAN. Format: `Used Throw Stone on (2,6) (344→335/680)` — exactly the inline HP delta I want. This is the ideal response shape.
- T+5:45 — `battle_move 3 7` returned `[BattlePaused] failed: Not in Move mode`. Pause menu was open unexpectedly — maybe earlier `cancel`s went too deep. Recovered with `execute_action Resume` (landed on BattleAbilities), then `execute_action Cancel` to BattleMyTurn. The transition was opaque.
- T+6:30 — `execute_turn 4 6 Steel` failed with "Not in Move mode" because cursor `ui=Abilities` (not Move). But `battle_move 4 6` worked from same state. Inconsistency: bare battle_move handles menu-nav fallback; execute_turn does not.
- T+7:00 — `battle_ability "Steel"` (no coords) for self-target failed with "requires a target (locationId=x, unitIndex=y)". scan listed Steel as `(4,10)<Ramza SELF>`. Followed up with `battle_ability "Steel" 4 6` — failed "Cursor miss: at (4,10) expected (4,6)". Stale cursor address bug. After this the bridge dropped me into BattleEnemiesTurn for ~25s.
- T+8:30 — Came back to MyTurn at (4,6). Skeleton mysteriously DEAD on the field — never saw what killed it (Ramza was at full PA self-buffed? counter? Chaos Blade Stone proc? unclear from any output).
- T+10:00 — Tried `execute_turn 4 7 Attack 4 8` to Goblin. Returned `[BattleDesertion]` — battle abandoned. Now on WorldMap. Per instructions: do not retry. Stopping play.
- T+10:42 — Stopping play, will write feedback. Total ~12 bridge calls, mostly slowness in execute_turn (11-15s) and battle_wait (12s).
