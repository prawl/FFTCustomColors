# Playtest feedback — 2026-04-25_1203

Fresh-eyes 30-min playtest. Battle ended quickly (Ramza died in the
4th turn) so I have less in-game data than expected, but a lot of
feedback on the first impression.

---

## 1. Confusing
Which commands or response fields were unclear or surprised you? What
did you have to re-read the docs to figure out?

> - **`!!` suffix on `[BattleMyTurn] ... t=20247ms[execute_turn]!!`** —
>   what does the trailing `!!` mean? It appeared on every
>   `execute_turn` response. Commands.md doesn't mention it. I assumed
>   "completed" but couldn't find a doc. After a turn that arguably
>   went badly (instant GameOver) it would be helpful to have the
>   suffix tell me _what_ resolved (hit / miss / KO'd / countered).
>
> - **Attack-tile cardinal labels are relative to the cursor unit's
>   facing, not absolute compass directions.** After moving to (4,11)
>   the live target was at (5,11) — east of me on the map — but
>   `Attack tiles: Up→(5,11) petrified (Dryad)` rendered it as "Up".
>   I get that this matches in-game arrow-key prompts after rotation,
>   but it took me a second look to confirm I wasn't misreading the
>   map. Doc could explicitly say "labels are post-rotation arrow
>   keys, not absolute N/S/E/W."
>
> - **`(?)` for unknown enemies on the unit list mixed with named
>   enemies.** I had `(Black Goblin)`, `(Dryad)`, `(Wisenkin)` in turn
>   1 and after killing one Dryad the survivor's body showed as `(?)`
>   on subsequent scans even though same coordinates. Eventually I
>   realized `(?)` on the unit list = unknown-fingerprint, but on
>   first reading I thought "(?)" might mean "now hidden / fog of
>   war".
>
> - **`[ENEMY] (3,10) f=N HP=0/426` — no name on a dead unit at all.**
>   Combined with the `(?)` for live ones, I had to track which slot
>   was which by HP totals.
>
> - **`screen -v` and `screen` disagreed during the post-death
>   transition.** `screen` reported `GameOver`; `screen -v` returned
>   `WorldMap`. Both with same `curLoc`. Felt buggy until I read the
>   `[StateOverride]` log lines and realized GameOver is an override
>   on top of WorldMap. Either (a) verbose should honor the override
>   too, or (b) it should be documented.

---

## 2. Slow
Where did the bridge feel laggy or fight you? (Compare against the
`t=Nms[action]` suffix on screen output, or just gut feel.)

> - `execute_turn` calls were ~10–20 seconds each (10606ms, 16085ms,
>   17182ms, 20247ms). That's reasonable given they include enemy
>   turns + animations, but the only signal it's still working is
>   waiting on Bash. A "phase" indicator (move-done / attack-resolved
>   / waiting-on-enemy-turn) streamed during the wait would let me
>   know progress vs. hang.
>
> - **Re-sourcing `fft.sh` per-bash-call is meaningful friction.**
>   Each shell invocation drops shell state, so every command is
>   `source ./fft.sh && execute_turn ...`. Doc says "Source it once
>   per session" which is true _within_ a shell, but for an LLM agent
>   each Bash tool call is a fresh shell. Either: (a) ship a single
>   wrapper executable, (b) make each helper a standalone script in
>   $PATH, or (c) doc this gotcha explicitly. As written I burned
>   tokens echoing `source ./fft.sh &&` ~10 times.

---

## 3. Missing
What info did you wish `screen` / `scan_move` surfaced that you had
to hunt for, infer, or do without?

> - **Damage/hit results.** When `execute_turn` returns `[BattleMyTurn]
>   ui=Move`, I have no idea whether my Attack hit, missed, crit, or
>   triggered Stone. I had to call `screen` again and diff HP totals
>   across scans to figure out my Chaos Blade petrified the Dryad and
>   that the Black Goblin (3,10) ended up dead via some chain I still
>   don't understand. A `lastAction: { hit: true, dmg: 336, statusAdded:
>   ["Petrify"] }` summary on the response would be huge.
>
> - **What killed me.** GameOver came back on `execute_turn 4 9 Attack
>   4 8`. From 719/719 with Protect+Shell+Regen, Ramza died in one
>   action. Was it Counter? AoE? I had to grep logs and infer.
>   `lastAction.deaths: ["Ramza killed by Wisenkin Counter for 824"]`
>   would have made it diagnosable.
>
> - **Per-unit reaction/support visible during scan.** I learned the
>   Wisenkin has Counter only by reading mod log AFTER the GameOver.
>   `scan_move` shows enemy abilities but not their passives. Knowing
>   "this monster will counter-attack you if you melee it" would
>   change my play significantly. (See logs:
>   `Passives (4,8): reaction=Counter support=none`)
>
> - **Active unit equipment / reaction / support / movement.** Ramza's
>   own passives weren't displayed anywhere I saw — just the weapon
>   onHit `[Chaos Blade onHit:chance to add Stone]`. If I have a
>   reaction like Auto-Potion I'd want to know it's primed.
>
> - **Map height grid in `screen` (not just per-tile h on move tiles).**
>   I had no sense of the terrain — was I on a hill, in a swamp? The
>   per-tile h values on move tiles helped, but a quick "you're on h=5
>   tile, 3 enemies on h≤2" summary would orient me.
>
> - **CT / turn-order preview.** Docs say `battle.turnOrder[]` exists
>   but the shell didn't render it on `scan_move`. Knowing "Wisenkin
>   acts in 2 turns" vs. "Wisenkin acts before me" would have changed
>   whether I rushed in.

---

## 4. Wrong
Any responses that felt incorrect, stale, or misleading? Bridge said
one thing and the game showed another?

> - **State-override inconsistency on `screen` vs. `screen -v`** as
>   noted in §1.
>
> - **`[BattleMyTurn] ui=Move` after a fully-completed turn.** Every
>   `execute_turn` response showed `ui=Move`, even though the action
>   menu position before/after a wait+autoFace shouldn't be guaranteed
>   to be on Move. Could be cosmetic. Or it might mean the cursor
>   _is_ on Move at the moment the next turn opens, which I can't
>   verify without screenshots.
>
> - **`Attack tiles: Down→(2,11) dead  Right→(3,10) dead`** after I
>   killed both. Showing dead-body tiles as attack-tile entries is
>   accurate to game mechanics but visually confusing — they appear in
>   the same list as live targets. Could be filtered or marked
>   `(corpse, no target)` more clearly.
>
> - **Ability target listings include corpses too.** `Phoenix Down …
>   → (3,10)<? >BACK> (2,11)<?>` — Phoenix Down on a non-undead-aware
>   stone-dead enemy is presumably useless? It's listed as if a valid
>   target.

---

## 5. Surprises
What game behaviors did the docs not prepare you for? What did you
discover the hard way?

> - **Chaos Blade petrifies on hit.** Documented in the
>   `[Chaos Blade onHit:chance to add Stone]` tag on Ramza's status
>   line — that's _great_, the docs surfaced it. I just didn't think
>   the proc rate would be high enough to matter. Across 3 attacks I
>   killed-or-stoned 3 different units. Felt like I was cheating.
>
> - **Counter monster one-shotting full-HP buffed Ramza.** Did not
>   expect a 719-HP Protect+Shell+Regen unit to die from a single
>   melee engagement. Either Wisenkin hits like a truck or there's
>   something compounded I'm missing (counter + dragonheart proc
>   chain?). The mod log showed Counter on the Wisenkin but not the
>   damage. Result: I have very little post-mortem signal.
>
> - **Dragonheart on a dead Dryad** — the (2,11) corpse had
>   reaction=Dragonheart per logs. If it auto-revives during enemy
>   turn that materially changes the threat picture. Not visible in
>   `scan_move`.
>
> - **Move-and-attack vs. stay-and-attack range.** From (8,10) my
>   Attack list was empty (no targets in range), even though Throw
>   Stone reached (4,10). I had to remember that basic Attack uses
>   weapon range (1) and abilities use their own R: number. The
>   `Attack → (no targets in range)` line is fine but for a fresh
>   player it's a moment of "wait, why?". Doc tip in BattleTurns.md
>   that clarifies "Attack range = your weapon's R, listed on the
>   activeUnit?" would help. (And does it show weapon range
>   anywhere? I didn't see it.)
>
> - **Move-only `execute_turn` does not end the turn** — Commands.md
>   warns about this. I noted it but it still felt mildly trappy
>   that my first reflex would have been `battle_move 5 10` then
>   `battle_attack 4 10` and gotten clobbered between calls. The
>   bundled `execute_turn 5 10 Attack 4 10` form worked great. The
>   doc warning is appropriate; I'm just flagging it as a sharp
>   edge.

---

## 6. Wins
What worked great and we should preserve? Specific helper or response
field that made the next decision obvious?

> - **`scan_move` is genuinely excellent.** The combined view of
>   units, abilities, valid targets per ability, move tiles with `h`,
>   attack tiles with arrow + occupant, recommended facing — that's
>   most of the decision-making information for a turn in one
>   compact dump. The `Throw Stone → (4,10)<Dryad>` rendering with
>   coordinates AND occupant is exactly what I need.
>
> - **`execute_turn` bundling.** One call for move+attack+wait, even
>   if 20s, is way better than 4 separate calls each with race risk.
>   The fact that it aborts at first non-completed sub-step is the
>   right design.
>
> - **`Recommend Wait: Face West — 3 front, 1 side, 0 back`** — clear,
>   actionable, with the arc-count explanation. Loved it.
>
> - **Status badges on units like `[Regen,Protect,Shell]`** —
>   immediately useful, no need to parse a flag bitmask.
>
> - **`d=` (distance) on every unit** — saved me from coordinate
>   subtraction every scan.
>
> - **`[Petrify] STONE` rendering on the Dryad after Chaos Blade** —
>   cleanly told me that target was inert. Loved this.
>
> - **`>BACK` annotation on `(6,11)<Dryad >BACK>`** — telling me an
>   enemy is BEHIND me is exactly the kind of hint that matters for
>   facing decisions.
>
> - **Strict mode default ON** — I appreciated that wrong commands
>   would error fast rather than silently mash keys.

---

## 7. One change
If we could improve ONE thing before the next session, what?

> **Action-result summary on every battle-action response.** A line
> like:
>
>     [BattleMyTurn] ui=Move ... t=16085ms[execute_turn]
>     last: Attack(4,8) → MISS counter:Wisenkin→Ramza HIT 824 dmg → Ramza KO'd
>
> would replace the current "I have to grep logs to figure out what
> happened to me" loop with a single response that tells me I lost
> and why. Right now `execute_turn` is opaque about consequences,
> which is the difference between "I made a tactical mistake" and
> "the bridge is being weird."

---

## Anything else
Open mic — anything that didn't fit above.

> - **Re-sourcing fft.sh:** mentioned in §2 but worth restating —
>   for an LLM agent driving the bridge, every Bash tool call is a
>   fresh shell. The "source once per session" pattern doesn't
>   compose with that. Either expose the helpers as standalone
>   scripts on PATH, or document this clearly in Commands.md
>   (current "Source it once per session" wording is misleading
>   for agent use).
>
> - **`!!` suffix:** mentioned in §1 but worth flagging as a small
>   doc fix — Commands.md should explain the suffix semantics
>   alongside the `t=Nms[action]` format.
>
> - **First impression of Chaos Blade Stone-onHit was "this can't be
>   right"** — felt like a bug or cheat for ~30 seconds before I
>   accepted it. If this is intended behavior of the buff_ramza
>   weapon, maybe the screen header should expand the
>   `[Chaos Blade onHit:chance to add Stone]` to mention the proc
>   rate. Otherwise a fresh agent will assume their attacks shouldn't
>   stone enemies and get confused.
>
> - **The `(?)` enemy on (2,11) had Dragonheart and Equip Crossbows
>   per logs — but `scan_move` rendered it as "Dryad" until it died.**
>   Once dead, name reverted to `(?)`. The cache hit/miss for naming
>   makes sense mechanically but the user-facing inconsistency reads
>   weird.
>
> - **No friction with strict mode itself** — it was on, I never had
>   to fight it. Good default.
>
> - **Bridge was responsive and never appeared to crash** in this
>   session. Game crashed once during my read of post-GameOver state?
>   No — game did not crash. I saw `[StateOverride] WorldMap →
>   BattleEnemiesTurn (battle residue)` log entries that looked
>   alarming but were just the state-detection trying to settle.
>
> - **Briefing was clear and helpful.** Reading Rules / Commands /
>   BattleTurns / AbilitiesAndJobs in that order felt right. The
>   timestamped beat cadence requirement (T+5/10/15/20) didn't
>   apply because battle ended at T+~3min, but I logged beats as
>   they happened.

---
