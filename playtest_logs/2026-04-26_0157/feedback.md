# Playtest feedback — 2026-04-26_0157

Fill out between T+25 and T+30. Be specific — quote actual command
output where relevant. Friction is the point; "wins" matter too so we
don't accidentally remove what's working.

(Session ended early at ~T+7 with a GameOver — Lloyd died on enemy
turn after a botched Magma Surge cursor-miss flipped me into
BattleEnemiesTurn with no Wait queued. Per rules I do not retry or
load. Feedback below covers what I observed in the ~7 minutes I had.)

---

## 1. Confusing

> **The `best:` line tile coordinates are not in the listed valid intent
> targets, but they ARE legal aim points — and that's never spelled out
> in BattleTurns.md.** From (9,8) the scan rendered:
> ```
> Magma Surge R:5 AoE:2 [Fire] {Instant KO (25%)}
>     → (9,8)<Lloyd SELF> (5,9)<Time Mage> (+38 empty tiles)
>   best: (5,7) e:Archer  (5,8) e:Time Mage  (5,9) e:Time Mage
> ```
> The doc says "Pick from the ability's valid targets in scan_move" —
> implying I can ONLY target tiles printed in the `→` list (Lloyd SELF
> and (5,9)). But the `best:` line is (5,7), an "empty tile" you have
> to aim at. There's a paragraph at line 122–124 that hints "best:
> ranks top placements by (enemies - allies)" but doesn't say "yes,
> these tiles are legal targets you should call `battle_ability` with."
> A first-read agent will treat the `best:` as informational and try
> to use only the `→` list, which limits AoE play.

> **`!blocked` was wrong on every cardinal Attack tile, but the doc
> framing makes you hesitate.** From (9,8) all 3 enemies showed
> `!blocked`. Doc says "treat as a hint, not a verdict — if you really
> want to attack, try anyway." I tried Attack at (7,10) anyway and it
> landed for 32 dmg, no problem. So the LoS calculator was 100%
> wrong on this map. If `!blocked` is going to be wrong this often, it
> almost feels like noise — maybe surface a `!blocked-low-confidence`
> vs `!blocked-high-confidence` distinction, or only show the tag
> when the LoS check is actually trustworthy.

> **`battle_wait` response with no OUTCOME / TURN HANDOFF banner is
> ambiguous.** Two of my three battle_wait calls returned a header
> like `[BattleMyTurn] ui=Move curLoc=The Siedge Weald
> obj=Orbonne Monastery t=5930ms[battle_wait]` with NO active unit
> name, NO outcome lines, NO handoff banner. I had no idea whether
> the turn ended, whether I was the active unit, whether enemies
> moved. I had to call `screen` immediately to figure it out. Either
> always print the active unit in the header (even when it's the
> same unit returning) or print "no enemies acted, your turn again"
> explicitly.

## 2. Slow

> `battle_wait` round-trip averaged 6–12 seconds (saw 11697ms,
> 5930ms, 8368ms). On a single-player-unit battle that's mostly
> just enemy CT cycling, but each `battle_wait` is a hard wall I
> can't do anything during. With ~30 commands of total budget for a
> 30-min playtest, having 3 of them be 10-second blocks is a real
> tax. Maybe surface a "skipping N enemy turns, ETA X seconds"
> rough indicator so I know whether to plan another scan/think
> while it runs.

> `[BattleVictory]` flicker still happens on damage-only abilities
> (Tanglevine 32 dmg, basic Attack 32 dmg). Baseline.md said this
> was patched with 3×500ms recheck. It is NOT actually fixed — both
> my damage actions returned `[BattleVictory]` immediately. The
> good news: an immediate `screen` showed BattleMyTurn with [ACTED]
> correctly. So the false-positive is harmless if you always re-check,
> but the prompt UX is misleading and a less-cautious agent would
> stop playing.

## 3. Missing

> **No status-effect ailment indicator on enemy units after a
> probability-roll ability fires.** Tanglevine claims "Applies Stop
> (25%)". After it landed, the unit list still showed Time Mage
> with no status (Stop did NOT proc — fine). But no place in the
> response says "STOP NOT APPLIED (25% rolled NO)" or anything like
> it. It's tedious to diff statuses across two scans to figure out
> if a probability ability actually triggered. A line in the action
> response like `Used Tanglevine on (7,10) — DAMAGE 32, STOP DID
> NOT APPLY` would close that loop.

> **No `screen.dead` summary or "you are alone" warning.** I had to
> grep through the unit list to discover that all 3 of my allies
> were KO'd. A single line near the top of `screen` like
> `Players alive: 1/4 (Lloyd at 234/432)` would have made my
> situation clear immediately. With 4 players + 5 enemies in the
> unit list, the dead/alive math should be pre-computed.

> **No "in-range from move tile X" projection on Attack/abilities.**
> Doc explicitly says scan_move shows attack range from CURRENT
> position, not post-move. But for an agent trying to plan
> move-then-attack on a single turn, that means I have to mentally
> translate "Attack R:8 from (9,8) sees Time Mage at d=4" into
> "if I move to (8,8) it'd be d=3." Some `move-then-act` planner
> helper that takes a move tile + ability name and returns the
> would-be valid targets would save a TON of mental overhead. I
> moved to (8,8) on a hunch and it improved !blocked tags
> dramatically — but it could have been worse.

## 4. Wrong

> **`execute_turn 8 8` returned a positively-toned success line
> ("Lloyd(Orator) (8,8) HP=234/432 ... acted moved") but the actual
> game state appears to have rejected the move.** The very next
> `battle_wait` returned no narration. Then a fresh scan showed
> Lloyd back at (9,8) with the original ability target patterns
> matching (9,8), and no [ACTED] tag. Either the move silently
> reverted, or the header lied about which tile Lloyd ended up on.
> Concerning because it caused me to plan an AoE based on
> stale-position assumptions.

> **`battle_ability` cursor-miss with "could NOT abort, action may
> have committed at wrong tile" is the worst outcome the bridge
> can deliver.** It cost me Lloyd's life. The error string itself is
> honest about what happened, but there's no way to recover —
> screen flipped straight into BattleEnemiesTurn before I could
> queue a Wait. The cursor-miss aborted-cleanly path (which I hit
> earlier on Magma Surge at (5,9) from (9,8)) was great; the
> committed-at-wrong-tile path is fatal. Suggestion: even if the
> action commits, the bridge should immediately try to send Wait
> behind it so the unit at least gets defensive facing.

## 5. Surprises

> **The Geomancy ability list is 13 lines of nearly-identical
> rendering** — same R:5 AoE:2, same valid-target list, same `best:`
> line, only the status effect / element differs. I have to read
> all 13 to compare side-effects. A condensed view like:
> ```
> Geomancy R:5 AoE:2 → SELF, (7,10)<Time Mage>, +38 empty
>   best: (7,10) e:Time Mage  (7,11) e:Time Mage
>   variants:
>     Sinkhole {Immobilize 25%}
>     Torrent [Water] {Toad 25%}
>     ...
>     Magma Surge [Fire] {Instant KO 25%}
> ```
> would compress 30+ lines to 8 and make the chooser obvious.

> **Lloyd's HP dropped 81 from a single Archer attack** even though
> both archers rendered as "Defending". I expected Defending to mean
> they wouldn't act offensively. The narrator correctly attributed
> the damage and the screenshot/log made clear what happened, but
> "Defending" as a status word was misleading to me as a fresh
> player.

## 6. Wins

> **The TURN HANDOFF banner format documented in BattleTurns.md is
> exactly what an agent needs.** I never actually saw one fire (I
> was the only living player), but the documented format is
> unambiguous and the doc explicitly tells me to "treat as a hard
> reset." Great DX even if I didn't get to use it.

> **The cursor-miss "aborted cleanly" recovery on my first Magma
> Surge attempt was a saving grace.** Bridge said
> `failed: Cursor miss: at (9,9) expected (5,9) — aborted cleanly`
> and `screen` confirmed I was still on BattleMyTurn with my action
> still available. I retried with Tanglevine and it worked. Without
> that recovery I'd have lost the turn for nothing.

> **`return_to_my_turn` after the second Magma Surge failure worked
> instantly** — sent escapes, returned me to BattleMyTurn cleanly.
> Universal escape hatches like this are exactly the safety net
> agents need.

> **The unit list lifeState rendering is great.** `[Dead] DEAD` and
> `[Treasure]` and `[Defending]` and `[Haste]` all communicate
> clearly. The `*` marker for active unit is helpful. Element/status
> sigils are well-designed even if Geomancy floods the screen.

> **`scan_move` is fast (~470-525ms) and structured.** Easy to grep,
> easy to plan from. The Move tiles list with `h=` heights helped me
> immediately spot the high-ground option.

## 7. One change

> **Fix the cursor-miss "may have committed at wrong tile" path.**
> If the bridge can detect that the cursor moved to a wrong tile,
> it should ALSO be able to detect what wrong tile the action
> committed at AND queue an immediate `Wait` behind it so the unit
> at least ends turn defensively. Right now this path is the only
> bridge failure mode that can directly cause unit death — every
> other failure (BattleVictory flicker, !blocked false-positive,
> stale ability list) is recoverable. This one isn't.

## Anything else

> **The cursor-miss happens often enough on Geomancy AoE targeting
> that there might be a systematic bridge bug.** Three Magma Surge
> attempts, two cursor-misses with different wrong-tile reads
> ((9,9), (5,11), and on the second pass (8,7) for the ability
> selection). All from a screen that scan_move just refreshed. If
> the cursor's coordinate read is racing with the previous menu's
> rendering, this would explain it. Worth instrumenting which sub-
> step times out and what tile-byte was actually read at miss-time.

> **The "wait, no OUTCOME line, was that even my turn?" pattern
> from battle_wait was the single most disorienting moment.** I
> could not tell if the wait completed or stalled. A single
> `[BattleMyTurn] ui=Move <UnitName>(Job) (X,Y) HP=A/B
> turn=N` header line at minimum (with optional outcome lines
> below) would solve this. The current "header missing the unit
> name" rendering is genuinely worse than rendering it always.

> **Auto-memory entries in my conversation prompt referenced a TON
> of FFT-internal stuff** (battle array offsets, project-state
> memory like "session 60 shipped fixes"). I followed the driver
> rule to ignore them, but it's worth noting they bled into prompt
> context and a less-disciplined agent would absolutely use them.
> Driver-prompt enforcement is good but not bulletproof.

---
