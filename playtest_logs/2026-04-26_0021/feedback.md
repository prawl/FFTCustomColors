# Playtest feedback — 2026-04-26_0021

Fill out between T+25 and T+30. Be specific — quote actual command
output where relevant. Friction is the point; "wins" matter too so we
don't accidentally remove what's working.

---

## 1. Confusing
> The screen-line header bleeds CURSOR position into UNIT position during BattleMoving. After a move where the Confirm step times out, `screen` returns "Ramza(Gallant Knight) (4,7) HP=719/719 ui=(4,7)" — that "(4,7)" looks like Ramza's actual tile but it's the cursor position; the unit is still physically at (6,7). I burned ~3 turns on this misreading because everything downstream (battle_move "Already at (4,7)", subsequent attacks) appeared to confirm the lie. A separate `cursorAt=(X,Y)` field on BattleMoving screens — kept distinct from `unitAt=` — would dissolve the confusion.
>
> The "TURN HANDOFF" banner, advertised in BattleTurns.md as a "loud banner" you can rely on, only fires from `battle_wait` / `execute_turn` returns. When a handoff happens silently DURING another command (a long-running battle_move or execute_action that times out across the boundary), the active unit changes with no banner — and the next command's response leaks the new unit's HP/job into the screen header without flagging it. I had a `battle_move 5 7` come back saying "Already at (5,7) → (5,7) HP=432/432" while my "scan_move just told me Wilham (HP=528) is active" — HP=432 is Lloyd's, but the bridge gave me no banner. Fix: emit the handoff banner on EVERY command response if the active unit changed since the prior call, not just on turn-ending helpers.
>
> The `[OUTCOME]` summary on `execute_turn` and `battle_wait` returns reports state changes for ALL units that changed during the call (including enemies whose Steel/Defending self-buffs landed during the player's move animation), with no clear separation between "what YOUR action did" vs "what other actions did during your sub-step". Example: after `execute_turn 6 8 Attack 6 9` (move Ramza, attack Knight), I got `[OUTCOME] Archer +163 HP +Defending -Charging,Haste PA +15,MA -9` — that's an enemy's Steel buff, not my Knight attack. I had to `scan_move` again to discover the Knight's HP was unchanged, meaning my attack silently no-op'd.

## 2. Slow
> The "NOT CONFIRMED (timeout, lastScreen=BattleMoving)" failure on `execute_turn` and `battle_move` consistently took ~14.5 SECONDS per failure (`t=14534ms`, `t=14540ms`, `t=14167ms` — three separate occurrences). It hit ~3 of my ~5 actual move attempts in this session. The cursor visually arrives at the destination tile but the bridge fails to detect the confirm-press completing, so it idles for the full 14.5s timeout window before returning failure. Combined with subsequent recovery steps (`battle_move` to push, `execute_action Cancel`, re-`scan_move`), each failed move cost me 25-40s of wall-clock and consumed the unit's move-slot anyway. This is the dominant friction in the session.
>
> `battle_wait` on `execute_turn` is also surprisingly slow on enemy-heavy turns: a single battle_wait that polled through 4 enemy actions took 18.5s and another took 21.7s. Not unreasonable per se (enemies have to animate), but the variance is high (single-step waits range 4-22s) which makes context-pressure budgeting hard.

## 3. Missing
> No facility to recover a stuck-in-BattleMoving turn cleanly. I ended up in BattleMoving with the cursor on a tile, the unit's MOVED slot consumed (per scan_move), and no obvious way to confirm or re-enter targeting. `execute_action ConfirmMove` appeared to no-op silently (returned same screen, no state change). Strict mode blocks `enter`. The only escape was `Cancel` → which somehow landed me in `BattleAttacking` once. A documented "force-confirm-move" or "abandon-stuck-state" recovery helper would have saved 30+ seconds twice.
>
> No way to express ability targeting that includes a level/potency selector. The scan dump shows `Aim (+1 to +20) R:5 {Potency: +1}` — the canonical name appears to be "Aim" but `battle_ability "Aim" 4 8` returns `failed: Ability 'Aim' not found in available skillsets: Attack, Iaido, Aim` (the error itself proves "Aim" IS in the skillset). Tried "Aim +1", "Aim (+1 to +20)", "Aim (+1)" — all rejected with different errors. I gave up and wasted Wilham's turn. Either the docs need a "ability name lookup conventions" section or the helper needs to handle the level-bracketed form.
>
> Phoenix Down phantom-success: `battle_ability "Phoenix Down" 7 9` returned `Used Phoenix Down on (7,9) → (6,8) HP=449/719`, looked perfectly fine. Next scan: Wilham still HP=0/528 [Dead]. The bridge claimed success without verifying. Either the targeting got swapped (cursor mismatch, hit empty tile) or the item didn't actually fire. Whichever — the post-action verification needs to compare target's lifeState before/after.

## 4. Wrong
> The enemy-turn narrator dump misattributes movements. After `battle_wait Lloyd` I saw `> Lloyd moved (8,8) → (3,5)` even though Lloyd is still at (8,8) — that movement event belongs to an enemy (Time Mage moved to similar coordinates). Another instance: `> Lloyd moved (8,8) → (6,8)` — that was actually Ramza's prior move. The narrator appears to be associating movement events with the most-recently-acted player rather than the actual moving unit.
>
> Enemy class labels can flip mid-battle without explanation. The "Time Mage" at (4,5) showed up in scan #1 (HP 353/353), got hit for 32, then in subsequent scans appeared as "Archer" at (4,6) HP 321/355, then back to (4,7) HP 289/355 with [Charging,Haste]. Not clear if this is a new unit, a reclassification, or a bug. Enemy reinforcements join lines suggested some new units do spawn (`> Archer (ENEMY) joined at (4,7)`) but the timing didn't align with the scans I saw.
>
> `[BattleVictory]` false-positive screen flicker on otherwise-fine attack returns. `execute_turn '' '' Attack 4 5` returned with screen=BattleVictory and `[FAILED] Cannot battle_wait from screen (current: BattleVictory)` — but the battle was clearly not over (4 enemies alive, scan immediately after showed BattleMyTurn intact). The Commands.md gotcha section warns about this generally, but it shouldn't fail the helper — it should retry the next sub-step or return a soft "screen-flicker, please re-issue" rather than aborting.

## 5. Surprises
> The `>rear` annotation in scan ability targets ("(6,6)<Summoner >rear>") immediately telegraphs back-attack opportunities — I noticed and used it on Ramza's first turn. Crisp UX win.
>
> The `[REVIVE-ENEMY!]` warning on Phoenix Down targets (when an enemy summon's corpse is in range) is a beautiful piece of defensive scan UX — I would have happily revived the dead Summoner without that label.
>
> Kenrick crystallized mid-fight (3-turn deathCounter expired). The narrator dump caught it (`> Kenrick gained Treasure / > Kenrick lost Dead`) but it was buried in a 30-line dump of enemy movements. This is a major story event (a unit becomes permanently unrecoverable for this battle) and deserves the same loud-banner treatment as TURN HANDOFF.
>
> The `[CHAIN INFO] 2 key-sending fft calls in one invocation. Bridge auto-delays if needed.` warning when I chained two helpers via `&&` was a pleasant surprise — clear diagnostic, not a hard error.

## 6. Wins
> The opening `screen` dump on a fresh BattleMyTurn was extraordinary: 19 abilities with rendered targets, 22 reachable move tiles with heights, threat timeline, recommended facing with arc breakdown, full unit roster with statuses and equipment — all in under 1s. I had everything I needed to plan Ramza's first turn from a single command.
> 
> Inline attack outcomes when `battle_attack` works cleanly: `Attacked (6,9) from (9,8) — KO'd! (48→0/531) → (9,8) HP=396/432`. Damage delta + KO flag + post-action position + active-unit HP all in one line. This is the model the rest of the helpers should aspire to.
>
> The `Throw Stone R:4 → (6,6)<Summoner >rear>` notation: range, target tile, target identity, AND facing-relative direction in one compact glyph. Same for `^strengthen` and `<Wilham SELF>`. Information density is excellent.
>
> `screen` is genuinely cheap (~200-600ms) and the response always echoes valid actions for the current screen. Recovering from confusion is one `screen` call away.

## 7. One change
> If I could change ONE thing: make `execute_turn` and `battle_move` survive the confirm-detection timeout WITHOUT losing the unit's move-slot. Right now a 14.5s NOT-CONFIRMED timeout consumes the move while leaving the unit physically in place AND poisons subsequent ability nav. Either (a) detect confirm via the post-move screen state change rather than a key-event ack so the timeout is rare, (b) auto-retry the confirm on timeout, or (c) on detected timeout, roll back the move-slot consumption so the player can replan. This single fix would have rescued ~4 of my 6 player-unit turns this session.

## Anything else
> Strict mode is documented as required for play sessions and that worked well — the `enter` → `advance_dialogue` rebinding caught me once but the helper's behavior was reasonable.
>
> The `play.md` append-only log discipline worked great as a real-time scratchpad and helped me build this feedback. Recommend the driver-prompt continue including "append a beat at T+5/10/15/20" — the time-pressure forcing-function genuinely changed my behavior toward writing-as-I-go vs writing-from-memory.
>
> No friction with sourcing fft.sh or with the `./fft <helper>` per-call form. Both worked equivalently. The doc on "two ways to use them" was clear.

---
