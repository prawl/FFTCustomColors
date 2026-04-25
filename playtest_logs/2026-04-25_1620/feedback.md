# Playtest feedback — 2026-04-25_1620

Fill out between T+25 and T+30. Be specific — quote actual command
output where relevant. Friction is the point; "wins" matter too so we
don't accidentally remove what's working.

---

## 1. Confusing
Which commands or response fields were unclear or surprised you? What
did you have to re-read the docs to figure out?

> **Self-target ability syntax disagreement.** scan_move renders
> `Tailwind → (8,10)<Ramza SELF>` (with the "SELF" marker), and Commands.md
> documents `battle_ability "Shout"` (no coords) for self-targets. So I
> tried `execute_turn 5 10 Tailwind`. Got back:
> `[FAILED] Ability 'Tailwind' requires a target tile. Usage: battle_ability "Tailwind" <x> <y>`.
> So Tailwind is not actually self-target despite the scan label —
> it's a buff that lands on a tile. The "SELF" annotation is misleading
> because it actually means "this is one tile you could pick (yourself)",
> not "this is a self-only ability". The docs describing `Shout` and
> `Chakra` as "no coordinates" classes don't mention how to tell which
> abilities qualify. After the failure I had to re-call with explicit
> coords. **Suggested fix:** scan_move should distinguish "Self-only
> (no coords needed)" from "Self-targetable (coords required, target
> tile is yourself)". Maybe `<SELF-ONLY>` vs `<self-targetable>`.
>
> **`Attack → (no targets in range)` vs error message disagreement.**
> Scan said `Attack → (no targets in range)` while AttackTiles section
> said `Right→(5,9) enemy HP=137`. I tried `battle_attack 5 9` and got:
> `Tile (5,9) is not in basic-Attack range. Run scan_move first and pick from the active unit's Attack ValidTargetTiles (2 valid tiles).`
> So Attack reported zero targets in one place and "2 valid" in the
> error. Eventually I figured Throw Stone worked because it listed (5,9)
> with `>BACK` annotation. Two different range checks running with
> different results.
>
> **Damage / hit / miss not in response header for `battle_ability`.**
> `Used Throw Stone on (5,9)` told me it ran but not the result. Had to
> re-call `screen` to see [ACTED] tag and verify HP didn't change.
> `battle_attack` is better — it surfaced "MISSED!" inline. Make
> battle_ability match.

---

## 2. Slow
Where did the bridge feel laggy or fight you? (Compare against the
`t=Nms[action]` suffix on screen output, or just gut feel.)

> Most calls were fast (~200-600ms). Slow ones, with t= readings:
> - `execute_turn 5 10 Tailwind` — t=3801ms (failed at ability step)
> - `execute_turn 8 10 "X-Potion" 8 10` — t=9349ms (failed at wait step,
>   spurious BattleVictory)
> - `battle_wait` — typically 10-19s, the longest a single round-trip.
>   Not unreasonable since it polls through enemy turns, but noticeable.
> - `battle_wait` after the X-Potion: 19598ms — three seconds of which
>   the screen flickered Victory and made me lose confidence in the
>   bridge state.
>
> The state-flicker problem (screen reports `BattleVictory` mid-turn)
> wasted multiple commands re-checking. If `execute_turn`'s wait step
> sees a transient `BattleVictory`, it should retry once or wait a
> beat rather than aborting. Right now any spurious flicker breaks the
> bundled-turn helper.

---

## 3. Missing
What info did you wish `screen` / `scan_move` surfaced that you had to
hunt for, infer, or do without?

> - **Ability success/damage in helper response.** `battle_ability`
>   should report HIT/MISS, damage dealt, and any status applied —
>   exactly like `battle_attack` does. Instead I got `Used Throw Stone
>   on (5,9)` with no outcome and had to scan to infer.
>
> - **Why is Attack out of range?** When `Attack → (no targets in
>   range)` is shown, it would help to know WHY — facing? height? Range
>   1 weapon and target is 2 tiles away? An enemy ADJACENT to me wasn't
>   in Attack range; I never figured out why. Some enemies have Float —
>   maybe height is involved? Adding a one-line reason like `Attack →
>   (no targets in range, h=2 vs target h=4 +2 over jump)` would unblock
>   the player.
>
> - **Active turn unit identity.** When I scanned after a battle_wait
>   and the active unit changed to "Tietra" (a name that never appeared
>   in earlier scans), I had no idea who Tietra was, where she came
>   from, or what team she's on. The `units[]` list also didn't include
>   her by name — the cursor `*` was on an enemy at her tile. Suggestion:
>   if the active unit is a guest/ally not previously seen, surface
>   `team=ALLY [GUEST]` and explain in the header.
>
> - **Roster/unit count change between turns.** A new unit (Tietra)
>   appeared on the field with no event log entry between turns. The
>   battle_wait event log showed enemy actions but no "Tietra joined".
>
> - **Action menu is opaque pre-action.** The `ui=Move` is the cursor
>   position, but I don't know which other actions are available on
>   action menu without re-scanning. A line like `Action menu: Move,
>   Abilities, Wait, Status, AutoBattle [Move acted, Abilities acted]`
>   would tell me what I have left.

---

## 4. Wrong
Any responses that felt incorrect, stale, or misleading? Bridge said one
thing and the game showed another?

> - **X-Potion claimed full heal but didn't deliver.** Response was
>   `Used X-Potion on (8,10) (275→719/719)`. Next turn's battle_wait
>   event log said `Ramza recovered 99 HP (HP 275→374)`. The X-Potion
>   apparently never hit, but the response stated 275→719/719 as if
>   it had. The 99 HP looks like Regen alone. **This is the most serious
>   issue I hit** — the response was confidently wrong about a
>   resource-spending action, and I made strategy decisions based on
>   "I'm at full HP" that were wrong.
>
> - **State flicker `[BattleVictory]` mid-turn.** Documented gotcha,
>   but execute_turn doesn't tolerate it. The flicker isn't just
>   cosmetic — it actually breaks helper composition.
>
> - **`Attack → (no targets in range)` while error path says 2 valid
>   tiles** (covered above).
>
> - **Tietra-but-not-Tietra cursor.** Header said active unit is
>   `Tietra (3,10) HP=639/639` but the units list has no Tietra entry,
>   only enemies and Ramza. The cursor `*` was on an `[ENEMY] (3,10)
>   HP=639/639`. Two interpretations possible — bridge crashed during
>   battle end or the unit identity got mismatched. Either way,
>   confusing right before GameOver.

---

## 5. Surprises
What game behaviors did the docs not prepare you for? What did you
discover the hard way?

> - **Move-then-act commits the move even if the act fails.** When my
>   `execute_turn 5 10 Tailwind` failed at the ability step, Ramza was
>   still at (5,10) — the move had already happened. I expected
>   atomic-or-rollback behavior given the bundled helper.
>
> - **Counter passive gets logged inline with the wait event log.**
>   Cool — `Ramza countered (unit@4,8) for 137 dmg` was clear.
>
> - **Massive single-turn damage.** Going from 548 HP to 36 HP in one
>   battle_wait cycle was a shock. I had no warning that 3+ enemies
>   could converge between my move and my next decision. The Timeline
>   section showed three enemies at ct=30, but I didn't read it as
>   "these will all act before you again". A heads-up like
>   `WARN: 3 enemies will act before you (combined potential ~500 dmg)`
>   would have changed my play.
>
> - **Guest characters appear without ceremony.** Tietra showed up on
>   the field unannounced. No `[ALLY joined]` event in the wait log.
>
> - **Enemies have Float status.** I didn't know what Float meant in
>   FFT (height-locked? immune to ground spells?). The status array
>   listed it but the docs don't explain status effects. (Probably out
>   of scope for the docs — a Wiki/Statuses.md reference link in the
>   header would help.)
>
> - **Mid-battle status changes for the active unit aren't called out.**
>   I had `Oil` for two turns and never got a notification it was on
>   me until I scanned and saw `[Oil,Regen,Protect,Shell]`. The
>   `gained Oil, Critical` line was buried in the wait event log.

---

## 6. Wins
What worked great and we should preserve? Specific helper or response
field that made the next decision obvious?

> - **The `t=Nms[action]` header on every response.** Made it easy to
>   gut-check timing without diving into JSON.
>
> - **Equipment/buffs/passive line.** `Ramza(Gallant Knight) (8,10)
>   f=W HP=719/719 [Chaos Blade onHit:chance to add Stone] [Grand Helm,
>   Maximillian, Sortilege, Escutcheon (strong)] R:Counter S:Evasive
>   Stance M:Jump +2 [Regen,Protect,Shell] *` is dense and useful. I
>   had everything I needed for unit-level planning in one line.
>
> - **The `>BACK` annotation on attack tiles.** Knowing I'd hit an
>   enemy from behind (bonus damage) was great strategic info.
>
> - **`Recommend Wait: Face West — 3 front, 1 side, 0 back`.** Clear
>   reasoning for the recommendation. I trusted it.
>
> - **Event log on battle_wait.** The list of damage/buff/status events
>   that happened during the wait cycle was the single most useful
>   feature. Without it I'd have been guessing what changed each turn.
>
> - **Compact unit list with f= facing, d= distance, status list.**
>   Reading 6 enemies at a glance was easy.
>
> - **Heights summary line.** `Heights: caster h=5 vs enemies h=2-3`
>   gave me instant high-ground awareness.
>
> - **Bundled `execute_turn` concept.** When it works, it's exactly
>   what I want.

---

## 7. One change
If we could improve ONE thing before the next session, what?

> Make `battle_ability` report outcome (HIT/MISS, damage, status, KO)
> in its response header the way `battle_attack` already does. Right
> now `Used Throw Stone on (5,9)` is silent about whether anything
> happened, forcing a follow-up scan. Worse — `Used X-Potion on (8,10)
> (275→719/719)` showed a heal that never actually delivered. Aligning
> battle_ability's response with battle_attack's would: (a) eliminate
> the silent-failure mode, (b) cut a follow-up scan per turn, (c) let
> me trust resource-spending actions. Resource accounting (X-Potion
> count went 94→93 even though it didn't heal) is a separate but
> related bug I'd want to investigate as part of the same fix.

---

## Anything else
Open mic — anything that didn't fit above.

> - **Briefing nailed it.** The driver-prompt was clear, scoped, and
>   the reading list was short enough to actually read. Capturing start
>   time on first command and writing beats during play (not after) is
>   a good pattern.
>
> - **CLAUDE.md auto-memory section is huge.** I followed the briefing
>   and ignored it, but a fresh agent without that instruction would
>   probably "absorb" it and not notice. Worth flagging at top of
>   CLAUDE.md.
>
> - **TodoWrite reminders fired repeatedly during play.** Got 4-5 of
>   them during a 9-minute battle. They're harmless but noisy when the
>   task is fundamentally "play, capture friction" with no todo list.
>
> - **Battle ended in 4 turns** because of the X-Potion silent failure
>   + Oil + 5 enemies converging on a Move=4 unit. Game-balance-wise
>   this was a hard fight despite full buffs and great gear. The
>   buffs/gear DID matter (Counter killed an enemy, Regen kept me
>   ticking) but the bridge issue (X-Potion no-op) is what tipped me
>   into GameOver.
>
> - **Soft cap of 80 bridge calls felt very generous.** I used roughly
>   15 before GameOver. The first-time player rate is 1-2 calls per
>   turn, not the 5-10 that experienced agents seem to do.
