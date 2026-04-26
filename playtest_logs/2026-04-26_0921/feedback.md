# Playtest feedback — 2026-04-26_0921

Fill out between T+25 and T+30. Be specific — quote actual command
output where relevant. Friction is the point; "wins" matter too so we
don't accidentally remove what's working.

---

## 1. Confusing
> The starting situation was severely under-explained for a fresh agent.
> `screen` showed Ramza/Wilham `[Dead] DEAD` while Kenrick was `[Treasure]`
> with no DEAD tag, and the Summoner enemy at (6,6) was also `[Treasure]`.
> Same word, completely different stakes: Ramza is recoverable with a
> Phoenix Down within 3 turns, Kenrick is permanently lost. Nothing in
> the rendered output or in `BattleTurns.md` explains the `[Treasure]`
> bracket tag. The doc says `lifeState` returns `"dead"`, `"crystal"`,
> or `null` — but the visible label is `[Treasure]`, not `[Crystal]`.
> Two names for the same state, both surfaced.
>
> The `(9,8)<Lloyd SELF>` marker appeared in the target list of every
> single Geomancy AoE (13 abilities × 1 SELF entry each = 13 lines of
> noise). Per the doc, SELF means "your tile is a valid target", which
> is technically true for AoE-radius hits — but in this context, my
> tile being in the AoE is a *risk indicator* (I'd splash myself), not
> a "default target if no coords given." Treating it the same as
> Tailwind's true SELF target is misleading.
>
> The `!blocked` LoS tag on `(6,10)<Archer !blocked>` for basic Attack
> says "the bridge thinks LoS is blocked, but try anyway". From a
> fresh-agent perspective: do I commit a precious turn to test the
> bridge's pessimism? With 105/432 HP, no — I gave up on those tiles.
> A confidence score (or a "pre-flight" check helper) would help me
> decide whether to bet a turn on a `!blocked` shot.

## 2. Slow
> `execute_turn 9 7 Attack 9 9` took 20.4 seconds (per jsonl
> latencyMs=20239) before returning `[GameOver]`. That's a long
> blackout — no streaming progress, no "Move committed", no
> "Attack hit for X damage", no "enemy turn 1 of 3". Just twenty
> seconds of nothing then a one-line death notice. For a 30-min
> playtest budget, ~20s per `execute_turn` × ~80 commands soft-cap
> means a third of the session can disappear inside `execute_turn`
> wait loops. (The 20s here was inflated by the GameOver detection
> short-circuit, but enemy-turn waits are inherently long.)
>
> `scan_move` was 149ms (great), `screen` was 1ms (great). Those felt
> instant. The slowness is concentrated in the action-resolving
> commands.

## 3. Missing
> **Last-unit-standing / unwinnable-state warning.** A fresh agent
> looking at `screen` sees a wall of unit data and has to mentally
> tally "alive players: ...just Lloyd." The header could surface a
> `lastStanding=Lloyd` flag, or `players=1/4 alive (3 KO'd, Ramza
> deathCounter=2)`. With that I'd know to flee and would never have
> bothered with the move-and-shoot plan.
>
> **Ramza deathCounter visibility.** Ramza is `[Dead]` but his
> 3-turn crystallize timer isn't surfaced. If Ramza was on turn 2 of
> 3, fleeing was the only correct play; if he was on turn 1, I had
> a window to fight. The bridge could expose `crystallizeIn=N` on
> the dead unit line.
>
> **Per-step Info field on `execute_turn` failure.** The bundled-turn
> doc promises `info` aggregating each sub-step (`Moved (8,10)→(5,10)
> | Failed: ability ...`), but on GameOver the wrapper printed only
> `[GameOver] ... t=20426ms[execute_turn]!` with no Info. The jsonl
> session log has `error: "Game Over"` and BattleMyTurn → GameOver,
> but the user-facing message swallows it. I had to read
> `live_events.log` (3 NARRATE lines) and `session_*.jsonl` to piece
> together what happened. No fresh agent will know to look there.
>
> **Damage / outcome surfacing for the action that ended the battle.**
> Did Lloyd die from an Archer crit? Did Ramza crystallize? Did the
> Time Mage counter-attack the move? `execute_turn` should at least
> say "GameOver triggered during enemy phase: Lloyd KO'd by Archer
> at (9,7) for 132 damage" — that's the level of post-mortem a real
> player gets from the death animation.
>
> **`scan_units` and `scan_move` on GameOver return a silent
> one-liner.** A fresh agent might call `scan_move` to retry their
> plan and get `[GameOver] curLoc=...` back with no explanation that
> the call is now a no-op because the battle is over. Similar to how
> `battle_wait` says `[FAILED] Cannot battle_wait from screen (current:
> GameOver)`, `scan_move` should say "scan unavailable: battle ended
> in GameOver" instead of returning a 1-line `screen` and looking
> like a half-broken response.

## 4. Wrong
> `scan_units` is documented as a primary tool in
> `BattleTurns.md` (line 11) and `Commands.md` (line 31, "All unit
> positions, HP, teams, stats") but actually prints
> `[USE screen] scan_units is deprecated. Use: screen` and returns
> the screen one-liner. The docs and the implementation disagree.
> Either rip `scan_units` from the doc tables (and from the
> "Available Commands" block in BattleTurns.md), or unbreak it.
>
> The `screen` rendering of `[Treasure]` for the Summoner enemy at
> (6,6) HP 0/318 reads as a status (like Defending). It's actually a
> permanent terminal state. Mixing alive-statuses and lifeState into
> one bracketed list confuses the visual scan. Suggest splitting the
> render: alive-statuses in `[...]`, lifeState as a separate suffix
> (`DEAD`, `CRYSTAL`, `TREASURE-CHEST`).

## 5. Surprises
> **Player units showing `[Treasure]` while still alive on the
> field.** Kenrick at (7,4) HP=0/467 with `[Treasure]` — I had to
> infer that he'd already crystallized into a chest and is unrecoverable.
> A flat `CRYSTAL` or `LOST` tag would land harder.
>
> **`(6,8) [Dead,Regen,Protect,Shell]` on Ramza.** Buffs persisting
> on a corpse was unexpected. Useful (tells me he had Protect/Shell
> when he died), but no doc explains it.
>
> **The `Timeline:` line shows only enemies (`E(E,ct=62) → E(E,ct=48)
> → ...`).** That's actually a strong implicit signal that no
> player unit is queued in the next 5 actions — i.e., I'm alone.
> Took me a moment to read it that way. Could be louder.
>
> **`execute_turn` returning `[GameOver]` with `t=20426ms` and a `!`
> warn-suffix.** The `!` marker is documented as a speed-regression
> tag, not an error tag. Confusing to see speed-warn punctuation on
> a battle-ending response.
>
> **Win:** the `screen`-time scan of 492ms gave me 13 fully-formatted
> Geomancy abilities with element + status + best AoE centers + 25%
> chances annotated, plus per-Archer affinity tags (`^strengthen` for
> Fire on the bow Archer). That's *enormous* signal in one call. The
> hard part wasn't getting info, it was metabolizing it under
> last-stand pressure.

## 6. Wins
> - `screen` is fast (~1ms uncached, ~150-500ms with scan) and the
>   one-line header packs `[Screen] ui=<slot> Unit(Job) (X,Y) HP
>   MP curLoc t=Nms[action]`. Excellent density.
> - The `best:` ranking on AoE abilities (`(9,9) e:Time Mage
>   (10,9) e:Time Mage`) is exactly the kind of pre-digested
>   tactical advice a fresh agent needs.
> - `>rear` and `^strengthen` per-tile annotations meant I knew the
>   Time Mage was facing away from me and the Archer would
>   power-up Fire — without that I'd have had to look up facings.
> - `execute_action` with no arg dumping `ValidPaths:
>   RetryFromStart, RetryChangeFormation, Load, ReturnToWorldMap,
>   ReturnToTitle` on GameOver was a clean discoverability win.
> - Strict mode caught nothing (no typos), but the strict-mode
>   contract — "use named helpers, no raw keys" — kept me focused.
> - The `[FAILED] Cannot battle_wait from screen (current:
>   GameOver)` message after the death was a model error format:
>   action name + reason + current screen. `execute_turn` should
>   adopt the same on terminal-state aborts.

## 7. One change
> Add an Info / per-step summary to `execute_turn` responses,
> *especially* on terminal failures. The agent should always be able
> to tell which sub-step (Move / Ability / Wait) advanced, and what
> happened during the wait that triggered Victory / GameOver /
> Desertion. Right now `[GameOver]` with `t=20426ms` and no Info
> is the worst case: I burned my one play action, can't tell what
> went wrong, and no in-game recovery is possible. A one-line
> `info: "Move (9,8)→(9,7) OK | Attack (9,9) hit Time Mage for 87
> | Wait: KO'd by Archer (8,11)→(9,7) on enemy phase 2"` would
> close the gap.

## Anything else
> - Driver-prompt setup was clear; the manual-reading prelude felt
>   appropriate for a fresh agent (Rules + Commands + BattleTurns +
>   AbilitiesAndJobs covered everything I needed for battle-only
>   scope). Skimming WorldMapNav for screen-name vocabulary was a
>   nice tip.
> - The auto-memory MEMORY.md *was* loaded into my prompt context
>   despite the driver saying to ignore it; it's hard to "unsee"
>   index lines like "🎯 12 commits / 5 playtest runs / +13 tests" or
>   "🔁 Fresh-eyes Claude sub-agent 30-min playtests". Those entries
>   already half-spoiled what this session is *for*. If true
>   fresh-eyes is the goal, consider scrubbing the auto-memory
>   index in addition to telling the agent not to read the topic
>   files.
> - I exited at T+~6 (battle was unwinnable, GameOver triggered on
>   first action) rather than T+25. Wrote feedback during the
>   remaining time.

---
