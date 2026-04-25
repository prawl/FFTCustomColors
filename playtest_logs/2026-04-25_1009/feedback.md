# Playtest feedback — 2026-04-25_1009

Fill out between T+25 and T+30. Be specific — quote actual command
output where relevant. Friction is the point; "wins" matter too so we
don't accidentally remove what's working.

---

## 1. Confusing
Which commands or response fields were unclear or surprised you? What
did you have to re-read the docs to figure out?

> The `battle_ability "Attack" 3 10` response was `Attacked (3,10) from (4,10) — MISSED! → (3,10) HP=601/719`. The trailing `→ (3,10) HP=601/719` reads as "target at (3,10) has HP=601" but it's actually Ramza's HP (the attacker). Either drop the suffix on a miss or label it `attacker HP=`.
>
> The trailing `!!` on bundled responses (e.g. `t=15621ms[execute_turn]!!`) isn't documented anywhere I could find — guessed it means "had nontrivial events" but unsure.

---

## 2. Slow
Where did the bridge feel laggy or fight you? (Compare against the
`t=Nms[action]` suffix on screen output, or just gut feel.)

> `execute_turn 4 10` was 15.6s and `battle_wait` was 15.95s. `screen` and `scan_move` were both ~370ms — instant. The slow ones felt fine because the game has to play move/wait animations, but in a 2-min budget two of those eats most of my time. For longer sessions probably fine.

---

## 3. Missing
What info did you wish `screen` / `scan_move` surfaced that you had to
hunt for, infer, or do without?

> Enemy class/job names. Every enemy was `(?)` in `<>` brackets, only the team `[ENEMY]` and HP shown. BattleTurns.md says this is a "mod-forced battle" gotcha for Siedge Weald — but as a fresh player I had no idea if these were skeletons, knights, or wizards until the post-turn narrator said "Skeleton moved". Surfacing the inferred class (even tentatively) before the first move would help planning.
>
> No HP delta or "I took N damage" line on the response from my own move (only on `battle_wait`). Had to compare HP across two scans to notice I'd taken 118 damage during my move.

---

## 4. Wrong
Any responses that felt incorrect, stale, or misleading? Bridge said one
thing and the game showed another?

> The miss-response HP trailer (above) is the main misleading bit. Otherwise nothing felt wrong. Initial scan showed enemy at (1,10), but after `execute_turn 4 10` the same enemy was at (3,10) — that's just normal game state advancing during my move animation, not a bug.

---

## 5. Surprises
What game behaviors did the docs not prepare you for? What did you
discover the hard way?

> `execute_turn 4 10` (move-only) returned to `BattleMyTurn` with new info but enemies had advanced and hit me for 118 between my move and my next scan. Expected: a "move-only" turn ends my turn cleanly. Actual: it leaves me back in MyTurn with my Act/Wait still available. Reading BattleTurns.md again, this is correct (Move and Act are independent, only Wait ends the turn) but a one-liner in Commands.md `execute_turn` table saying "move-only DOES NOT end the turn — you'll be back at MyTurn with Act available" would help.

---

## 6. Wins
What worked great and we should preserve? Specific helper or response
field that made the next decision obvious?

> The `Attack tiles: Up→(3,10) enemy HP=671` line is gold. One glance, I know the enemy is one tile up, has 671 HP, and which arrow key to press. Combined with `Recommend Wait: Face West — 3 front, 1 side, 0 back` it's a complete tactical readout in two lines.
>
> The post-`battle_wait` narrator (`> Skeleton moved (5,10) → (3,11)` and `> Ramza took 287 damage`) is exactly what I needed to know between turns. Preserve aggressively.
>
> `screen` and `scan_move` round-trips at ~370ms feel snappy.

---

## 7. One change
If we could improve ONE thing before the next session, what?

> Fix the miss-response HP trailer. `Attacked (3,10) from (4,10) — MISSED! → (3,10) HP=601/719` should either drop the HP suffix on a miss or label it clearly as attacker HP. As-is it implies the target's HP, which would mislead any agent that's tracking enemy health from response payloads.

---

## Anything else
Open mic — anything that didn't fit above.

> Spawn / record / return loop worked end-to-end in budget. Read 5 instruction docs, sourced fft.sh, executed `screen` / `execute_turn` / `battle_ability` / `battle_wait`, wrote both `play.md` and `feedback.md`. No crashes, no stuck states, no ambiguous failures. The 2-min budget is tight — most of it went to the two 15s execute_turn/battle_wait calls. A 30-min session is comfortably feasible.

---
