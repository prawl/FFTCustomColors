# Playtest feedback — 2026-04-25_1540

Fill out between T+25 and T+30. Be specific — quote actual command
output where relevant. Friction is the point; "wins" matter too so we
don't accidentally remove what's working.

---

## 1. Confusing
Which commands or response fields were unclear or surprised you? What
did you have to re-read the docs to figure out?

> **`scan_move` deprecation banner conflicts with the docs.** Calling
> `scan_move` prints `[USE screen] scan_move is deprecated. Use: screen`
> but every doc (Commands.md, BattleTurns.md, the typical-flow examples,
> the gotcha section) tells me to call `scan_move`. As a fresh agent I
> followed the docs and got a polite scolding. Either kill the alias or
> update the docs.
>
> **`BattleAttacking` after `battle_ability` looked like the action had
> been queued, not committed.** The response said `Used Phoenix Down on
> (4,6) → (4,10) HP=719/719` (success-shaped) but `screen` afterward
> stayed in `[BattleAttacking] ui=Phoenix Down`, the targeted Skeleton's
> HP did not change, and only `battle_wait` unstuck me. I tried Phoenix
> Down twice (different tiles) before realizing it was a no-op. The
> phrasing "Used X on (x,y)" promised a resolved hit; the screen showed
> the targeting cursor still hovering. Suggest: only emit "Used …" when
> the post-animation classifier confirms a hit/miss; otherwise emit
> something like "Targeted X — confirm pending" or fail with a clear
> reason.
>
> **Self-target syntax differed from what scan_move suggested.** Scan
> output was `Steel → (4,6)<Ramza SELF>`, suggesting a coord works. The
> command reference says self-targets take no coords (`battle_ability
> "Shout"`). I tried `battle_ability "Steel"` first → error "requires a
> target (locationId=x, unitIndex=y)". The error vocabulary
> (`locationId`, `unitIndex`) didn't match the helper signature
> `<x> <y>` documented in Commands.md. Adding `4 6` then failed with
> "Cursor miss" (see #4). Steel is a self-buff Mettle ability — the
> error suggests the bridge thinks it's not self-targetable.
>
> **`battle_attack` "out of range" with a target Throw Stone reported
> as in range.** scan said `Throw Stone → (4,7)<Skeleton >BACK>` from
> (4,10). I tried `battle_attack 4 7` and got "Tile (4,7) is not in
> basic-Attack range." Reading more carefully I see scan's
> `Attack → (no targets in range)` is the authoritative basic-attack
> filter, but I parsed `Throw Stone` having a target as "I can hit it."
> A one-liner header summary like `BasicAttack: out of range —
> closest enemy d=3 (need 1)` would have prevented me wasting a call.

---

## 2. Slow
Where did the bridge feel laggy or fight you? (Compare against the
`t=Nms[action]` suffix on screen output, or just gut feel.)

> **`execute_turn` is consistently 11–15s with `!!`** (over the red
> warn). `t=10974ms`, `t=11488ms`, `t=11922ms`, `t=15149ms`,
> `t=13249ms` across this session. `battle_wait` similar
> (~12s). Individual `screen` calls are ~200-400ms which is fine. The
> bridge spends most of the wall-clock on Ctrl-fast-forward through
> enemy turns and Move/Wait menu nav. If it's irreducible, fine — but
> consider hiding the `!!` since it's expected, or only flagging when
> wall-clock exceeds e.g. 18s.
>
> **`BattleEnemiesTurn` poll was a 25-second blackout.** After Steel
> failed the bridge dropped me to BattleEnemiesTurn and stayed there
> across 4 sleep+screen calls totaling ~30s. The screen output gives
> me no signal — same one-liner each call. Some progress hint
> ("enemy 2/3 acting", or even a CT counter) would tell me whether to
> keep waiting or whether the bridge is wedged.

---

## 3. Missing
What info did you wish `screen` / `scan_move` surfaced that you had to
hunt for, infer, or do without?

> **Skeleton died and I never learned why.** Between two of my turns
> the Skeleton went from 344/680 → DEAD with no kill-feed entry. I'd
> like a `battle_wait` follow-up dump section ("Damage events while
> you waited:") that lists everything that happened: who acted, what
> ability, who took damage, who died. The current `> Bomb moved (1,7)
> → (1,9)` lines are a movement log only. Damage and KO are missing.
>
> **No HP/MP for myself in execute_turn responses.** After
> `execute_turn 4 10`, the response was `[BattleMyTurn] ui=Move
> curLoc=… t=10974ms[execute_turn]!!` — no active-unit field. I had
> to follow up with `screen` to learn my new HP. Helper output should
> include the activeUnit summary line that `screen` does.
>
> **No `[ACTED]/[MOVED]` flags shown when I expected them.** Commands.md
> documents these but I only saw `acted` once (and got `[ACTED]` in the
> Cancel error). On a fresh turn after acting via Throw Stone I didn't
> see the flag in the header. Hard to tell what I had left.
>
> **Counter-Attack and reactive damage are silent.** Ramza countered
> the Bomb for 380 (the Bomb died) — the inline `> Ramza countered
> Bomb for 380 dmg — Bomb died` line during `battle_wait` showed up
> nicely. But Steel-induced PA boosts, Regen ticks, and the
> Skeleton's mysterious death weren't surfaced anywhere. Regen was
> visible only as HP delta on next `screen`.
>
> **No "Can I close to attack range in N turns?" hint.** I had to
> mentally compute "Mv=4, target d=5, so I need 2 turns to close" for
> every move. With buffs/Move-+N this gets tedious. A `closingMoves`
> field on each enemy in scan would help.

---

## 4. Wrong
Any responses that felt incorrect, stale, or misleading? Bridge said one
thing and the game showed another?

> **Phoenix Down "Used … on (target)" was a phantom success.** Twice.
> `Used Phoenix Down on (4,6) → (4,10) HP=719/719` — clean success
> shape, no error code, but the targeted Skeleton's HP and lifeState
> were unchanged on the next scan. The bridge swallowed the failure.
> This is the worst kind of bug because it makes me feel I've made
> progress when I haven't. (Possible explanation: the Phoenix Down
> targeting cursor needed a confirm Enter that the helper didn't
> issue, and the cursor sat on the target until I cancelled out.)
>
> **Cursor staleness on `battle_ability "Steel" 4 6`.** Error: `Cursor
> miss: at (4,10) expected (4,6)`. I had moved to (4,6) successfully
> seconds earlier (`battle_move 4 6` returned `(4,8)->(4,6)
> CONFIRMED`). The bridge's cursor cache says (4,10). This looks like a
> stale-byte read with no invalidation after Move.
>
> **`battle_ability` `requires a target (locationId=x, unitIndex=y)`
> error message uses fields I cannot supply.** The helper signature is
> `<name> [<x> <y>]`. Fields named `locationId` and `unitIndex` aren't
> in the docs and aren't accepted. Suggest: rephrase as "missing
> coordinates — try `battle_ability \"Steel\" <x> <y>` (self-target:
> use your current tile)".
>
> **execute_turn entered Move mode despite ui=Abilities.** First
> `execute_turn 4 6 Steel` from `ui=Abilities` failed "Not in Move
> mode (current: BattleMyTurn)". Then `battle_move 4 6` from the same
> state worked. The two helpers should agree on whether to nav to Move
> first. (battle_move's behavior is the right one — handle the nav.)

---

## 5. Surprises
What game behaviors did the docs not prepare you for? What did you
discover the hard way?

> **`BattleDesertion` ended my run with no warning.** I assumed the
> battle ends only on Victory or GameOver. The instruction prompt
> mentions both. Apparently if Ramza dies and crystallizes, the screen
> goes to `BattleDesertion` (= unit crystallized → forced retreat). I
> only learned this by grepping the source after the fact. Players
> seeing this for the first time would benefit from one line in
> BattleTurns.md's screen-states table: `BattleDesertion — your party
> wiped or the protagonist crystallized; battle abandoned`.
>
> **`battle_wait` sometimes does NOT wait through enemies.** The
> earlier `battle_wait` returned in 3.7s with only one line: `> Ramza
> moved (4,8) → (5,8)`. The next state was BattleMyTurn (mine again),
> not BattleEnemiesTurn → BattleMyTurn. So either nobody had a CT
> ready, or the helper exited early. The 3.7s short-circuit fooled me
> into thinking the battle was paused.
>
> **The bridge response shape varies a lot.** Some calls return the
> rich "Abilities/Move tiles/Units" dump (e.g. `screen` and
> `scan_move`); others return a single line (e.g. `execute_turn`,
> `battle_ability`). The single-line responses don't include the
> `Helpers:` ValidPaths hint either, so when something fails
> mid-sequence (`Pause` opened during a stuck cursor, etc.) I have no
> action menu to recover from. Suggest: every error response includes
> the canonical Helpers/ValidPaths block.

---

## 6. Wins
What worked great and we should preserve? Specific helper or response
field that made the next decision obvious?

> **`./fft <helper>` per-call wrapper is excellent.** No source/per-shell
> overhead, just one command per Bash call. Fast, ergonomic, no state
> drift between calls. The agent UX is exactly right.
>
> **`screen`'s rich first response was a great onboarding gift.**
> Header line + Abilities + Move tiles + Recommend Wait + Timeline +
> Heights + Units — that's everything I needed to plan the first turn.
> The compact "Attack → (no targets in range)" line is exactly the
> right shape: it tells me the action exists but isn't useful right
> now, in 6 chars. The `*(x,y)«Name»` notation for marked targets is
> readable.
>
> **Inline HP-delta on `Used Throw Stone on (2,6) (344→335/680) →
> (4,8) HP=635/719`.** This is the gold standard. Both the target's
> delta AND my own HP after, in one line. If every `battle_ability`
> response looked like this, half the friction in #1 and #4 evaporates.
>
> **`>BACK` annotation on targets** told me the target's facing
> exposed its back without me having to do geometry. Useful for
> directional damage planning.
>
> **`[REVIVE-ENEMY!]` warning on Phoenix Down** caught the dead-Bomb
> tile. I'd have wasted a turn reviving an enemy without it.
>
> **Verbose error on `execute_action Cancel` in wrong state**: it
> listed all available actions with one-line descriptions. That's
> exactly the right recovery affordance.
>
> **`Recommend Wait: Face West — 2 front, 1 side, 0 back`** tells me
> the optimal facing AND the math behind it (arc counts). Trustable.

---

## 7. One change
If we could improve ONE thing before the next session, what?

> **Make `battle_ability` actually wait for the action to resolve, OR
> fail loudly when it doesn't.** The Phoenix Down phantom-success
> bug is the single biggest source of wrong-but-confident behavior I
> hit. If the cursor sits on the target tile and the action never
> committed, the helper should detect that (screen still in
> BattleAttacking after N polls → ERROR: action did not commit).
> Right now the helper exits as soon as it sends keypresses without
> verifying the resolution, and the response message implies success.

---

## Anything else
Open mic — anything that didn't fit above.

> **The pre-stubbed `[ACTED]/[MOVED]` documentation is good but the
> behavior is inconsistent.** Sometimes the flag appears in the bracket
> tag (`[BattleMyTurn] [ACTED]`), sometimes as a lowercase suffix in
> the body (`acted` after the curLoc), and sometimes not at all when
> docs say it should be. Either reliably surface it or remove the doc
> claim.
>
> **Battle ended at T+10 in a single bad turn** (Phoenix Down phantom
> failure → wasted turns → enemies closed → Ramza died during a
> 13s `execute_turn 4 7 Attack 4 8`). I had Ramza buffed to 719 HP
> with Regen/Protect/Shell and lost in 4 turns. The death itself isn't
> a bridge bug — that's me playing badly — but the inability to see
> WHAT killed me (no kill-feed for the death event in any response)
> made the post-mortem impossible. The `[BattleDesertion]` response
> contained literally nothing beyond the screen tag.
>
> **`logs` was helpful for diagnosis.** `[BattleAbility] WARN: cursor
> at (4,10), expected (4,6)` confirmed the cursor-stale theory in
> seconds. Keep that diagnostic surface — even better, surface that
> WARN line into the helper's stderr response so I don't have to
> tail logs separately.
>
> **`-v` flag on `screen` was a no-op in BattlePaused** — same output
> as plain `screen`. Docs say verbose mode expands details on certain
> screens; not on this one. Fine, but a "verbose mode not implemented
> for this screen" hint would save the curiosity tax.

---
