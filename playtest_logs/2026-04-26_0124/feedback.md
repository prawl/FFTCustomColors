# Playtest feedback — 2026-04-26_0124

Fill out between T+25 and T+30. Be specific — quote actual command
output where relevant. Friction is the point; "wins" matter too so we
don't accidentally remove what's working.

Session ended early — Lloyd (last man standing) died on his second
turn after a cursor-miss on a Geomancy AoE consumed his action. Total
wall-clock ~4 minutes, ~6 bridge calls. Feedback below from a
fresh-eyes read of those 6 interactions.

---

## 1. Confusing
> **The `!blocked` tag in `scan_move` lied on its first use.** Initial
> scan showed all three enemies as `Attack R:8 → (4,7)<Archer !blocked>
> (5,9)<Time Mage !blocked> (3,10)<Archer !blocked>`. I assumed every
> attack would fail. I tested anyway by calling `battle_attack 5 9` —
> it HIT for 42 dmg (289→247). So `!blocked` was a false negative from
> position (9,8). On the next turn from (8,8), only Time Mage was
> tagged `!blocked` and attacking the others worked — so the tag IS
> position-sensitive (LoS-ish) but it is unreliable enough that I
> can't trust it. Combined with no documentation of what `!blocked`
> means in `BattleTurns.md` or `Commands.md`, this is the single most
> confusing thing I encountered.
>
> **Geomancy AoE entries don't show enemy hit-counts.** `Sinkhole R:5
> AoE:2 → (4,7)<Archer> (8,8)<Lloyd SELF> (5,9)<Time Mage> (+44 empty
> tiles)` lists three centers but doesn't tell me which center hits
> the most enemies. The docs promise a `best:` line for radius-AoE
> abilities ("`centers=N` valid aim points. `best:` ranks top
> placements by `(enemies - allies)`") but I never saw a `best:` line
> for any of the 13 Geomancy variants — only the bare `→` enumeration.
> So I had to mentally compute Chebyshev radii to figure out whether
> centering between two enemies would catch both. (My guess of (5,8)
> failed cursor-miss before I could find out.)

## 2. Slow
> **`battle_wait` took 4525ms** with the warn-suffix `t=4525ms[battle_wait]`
> (no `!`/`!!` color marker but it's well over the 800ms warn floor
> per docs). For a single command that's a noticeable pause. Not a
> blocker but it's the slowest single call I made.
>
> **Enemy turn took >5 seconds.** After my failed Tanglevine, screen
> went `BattleEnemiesTurn` and stayed there through `sleep 3` and into
> `sleep 5` before flipping to `GameOver`. There's no helper to
> "wait until my turn or the battle ends" — the recommended polling
> pattern in `BattleTurns.md` is `screen` until `BattleMyTurn`, but
> that doesn't anticipate the GameOver branch. A `wait_for_turn`
> helper that returns whichever of `{BattleMyTurn, BattleVictory,
> GameOver, BattleDesertion}` lands first would be cleaner.

## 3. Missing
> **No "what does !blocked mean" doc.** Search `BattleTurns.md` and
> `Commands.md` — the symbol isn't explained. I learned it's
> position-dependent only by experimenting (and even then it lied).
>
> **No documented response field for "did the action succeed?".** The
> `battle_attack` response said `[BattleVictory]` in the screen tag
> AND `HIT (289→247/355)`. The screen tag was wrong (it was actually
> still BattleMyTurn — confirmed via the next `screen` call returning
> `[BattleMyTurn] ui=Move [ACTED]`). I had to do a second roundtrip
> to confirm what really happened. The `BattleVictory` flicker is
> documented in `Commands.md` "Known Gotchas" as a transient state on
> battle endings — but it surfaced on a normal mid-battle attack
> with two enemies still alive. Either the gotcha is broader than
> documented, or the battle-end-flicker was incorrectly applied to a
> non-end action.
>
> **No "abort current sub-menu and go back to BattleMyTurn root"
> helper.** When the pause menu opened spontaneously and `Resume`
> dropped me into `BattleAbilities ui=Attack`, I had no clean way to
> say "I just want the action menu open at Move". Calling
> `battle_wait` from there worked but with the side effect of an
> unintended Move (see #4).
>
> **No way to "preview a target" before committing.** I wanted to
> test whether (5,8) was a valid Tanglevine center before spending
> the action. The cursor-miss failure consumed the turn. A dry-run
> mode for `battle_ability` (validate target tile, return
> AoE-affected units, but don't press Enter) would have saved me.

## 4. Wrong
> **`battle_wait` from `BattleAbilities` triggered an unintended
> move.** I called `battle_wait` after spontaneous-pause + Resume
> landed me in the Attack submenu. The response footer said
> `> Lloyd moved (9,8) → (8,8)`. I never asked to move. The cancel
> stack to get back to root must have re-entered Move mode at some
> point. Side effects:
> - Repositioned without my consent (luckily improved my attack
>   angles, but could have been catastrophic on a different map).
> - The `[ACTED]` flag from my prior attack was CLEARED — so on the
>   next "BattleMyTurn" I had Move and Act both available. Either
>   that was a different turn entirely (CT cycled?) or the flag was
>   lost in the navigation. Either way, the response didn't tell me
>   which case it was — no `=== TURN HANDOFF: ... ===` banner, no
>   timeline tick info.
>
> **`battle_attack` reported `[BattleVictory]` mid-battle.** Tag was
> wrong; the next `screen` came back `[BattleMyTurn] [ACTED]` with
> two enemies still alive. False positive on the screen-state
> override.
>
> **Cursor-miss on AoE targeting consumed the action.** Tanglevine at
> (5,8) failed with `Cursor miss: at (6,6) expected (5,8)`. Cursor
> ended at (6,6) — three tiles off (4 if Chebyshev). Then the screen
> flipped to BattleEnemiesTurn, meaning the action committed at the
> wrong tile (or did some other half-state). I'd hoped a cursor miss
> would simply abort and return to BattleMyTurn for a retry.

## 5. Surprises
> **The pause menu opened on its own.** Between my successful attack
> and my next `battle_move` call, BattlePaused appeared with no input
> from me. Possibilities I can think of: a stray Escape from a previous
> command's nav stack, a key getting captured by another window, a
> bridge-side bug. Whatever the cause it was startling and I had no
> idea how to recover other than `execute_action Resume` (a guess that
> happened to work).
>
> **Geomancy works as a self-radius cast that doesn't hurt me.** The
> `(8,8)<Lloyd SELF>` markers were initially scary — looks like
> "centered on me also hits me." But the entries clarify Lloyd is
> just a *valid center*, not a guaranteed hit (Geomancy presumably
> only hits enemies in radius). Took a re-read of `BattleTurns.md`
> "About the `<Caster SELF>` marker" to make sense of it.
>
> **Distance from (8,8) to (5,9) feels like 3 in Chebyshev** but the
> game/bridge clearly treats it differently in some places — the
> cursor-miss hint at "expected (5,8)" landing at "(6,6)" suggests
> the cursor walked along axes, not diagonals, and may have hit a
> step-cost wall. AoE targeting math feels less stable than the docs
> imply.
>
> **`Heights: caster h=5 vs enemies h=2`** is a great line — it
> tells me at a glance I have terrain advantage. Loved seeing that
> in scan output.

## 6. Wins
> **The opening `screen` (auto-decorated with abilities, units,
> timeline, recommended facing) is gorgeous.** Single command, all
> the situational awareness I needed: who's alive, who's dead, my
> stats, my full ability list with status-effect hint tags, AoE
> radii, ranges, valid move tiles, recommended Wait facing, timeline
> CT. That packed first response let me orient in seconds.
>
> **`(+38 empty tiles)` and `(+66 empty tiles)` summary counts** —
> the response collapses noise gracefully. Without that I'd have
> been wading through a wall of coordinates.
>
> **The `R:Parry S:Attack Boost M:Movement +2` shorthand for
> equipped passives** is compact and readable. Same for status tags
> like `[Haste]`, `[Defending]`, `[Treasure]`. Excellent visual
> density.
>
> **Status-effect hints in the AoE label** —
> `Magma Surge R:5 AoE:2 [Fire] {Instant KO (25%)}` told me the
> chance and the element without making me look anything up. Same
> for the `^strengthen` annotation on Archer (4,7) — I immediately
> knew Magma Surge would heal that target.
>
> **Bridge round-trip was usually fast** (`t=435ms`, `t=391ms`,
> `t=187ms`). Snappy enough to feel interactive.
>
> **`screen -h` style header is well-designed** — `[BattleMyTurn]
> ui=Move [ACTED] Lloyd(Orator) (9,8) HP=315/432 MP=73/73
> curLoc=...` packs the eight things I need to know to decide my
> next move into one line.

## 7. One change
> **Rework the `!blocked` tag.** Either:
> 1. Make it accurate (don't tag a tile blocked unless the bridge
>    has actually verified the attack would fail), or
> 2. Remove it (the `→` filter already lists only valid targets, so
>    `!blocked` is implying a contradictory truth), or
> 3. Document it precisely in `BattleTurns.md` so I know what it
>    represents (e.g. "LoS may be obstructed by Z-axis terrain;
>    attempt at your own risk").
>
> Today the tag is a confidence-eroder: it falsely scared me away
> from a viable attack on turn 1, and on turn 2 I just ignored it
> entirely. A tag that gets ignored is worse than no tag.

## Anything else
> **The TodoWrite tool reminder fires repeatedly even when ignored**
> (showed up 3+ times during this short session). Distracting noise
> in tool responses for short linear tasks.
>
> **The CLAUDE.md at repo root is for a sprite-customizer mod**, not
> the FFT-Hands-Free bridge — completely irrelevant to playing FFT.
> The driver prompt told me to read it first; I did and learned
> about Reloaded-II sprite themes. The `FFTHandsFree/Instructions/`
> docs are the actual player manual. Worth either splitting the
> repo or putting a top-of-file pointer in CLAUDE.md saying "for
> playing FFT, see FFTHandsFree/Instructions/".
>
> **Spontaneous BattlePaused** is the scariest unexplained event of
> the session. Worth instrumenting: log every screen-state change
> with cause (user input vs game-side trigger vs bridge side
> effect). If it was bridge side-effect, that's a real bug.

---
