# Playtest feedback — 2026-04-25_2245

Fill out between T+25 and T+30. Be specific — quote actual command
output where relevant. Friction is the point; "wins" matter too so we
don't accidentally remove what's working.

---

## 1. Confusing
> The **scan_move ability listing only shows allies as targets** even for offensive
> ranged spells. Every offensive ability (Slowja, Meteor, Ultima, all of
> Lloyd's Geomancy) rendered with only `<...ALLY>` and `<...SELF>` tiles, but
> the actual error message admitted "27 valid tiles cached" / "32 valid
> tiles cached" for Meteor and Ultima respectively. So the renderer is
> filtering most targets out of the rendered output but the bridge has
> them — I had no way to know what tiles were actually valid without
> guessing-then-erroring. For an offensive AoE this is the difference
> between "use the ability" and "give up and skip a turn."
>
> Lloyd's class label is `(Orator)` but his primary skillset is
> Geomancy. No hint anywhere that Geomancy is the secondary or what the
> primary is — I had to infer. Made me question whether class labels
> are reliable.

## 2. Slow
> `battle_wait` gets **stuck on a confirmation modal** during the
> facing-pick phase. Twice my unit's wait returned `[BattleActing] ui=Reset
> ... ValidPaths: Confirm, Cancel, OK, Yes` and never advanced —
> polling `screen` 9 times over 30s stayed `[BattleActing]`. Recovery
> required `execute_action Cancel` which actually then advanced the turn
> handoff cleanly. So the Wait helper is leaving an open modal in some
> code path, possibly when there's no real "best facing" delta to choose.
>
> `execute_turn 5 9 Ultima 4 5` regularly hit `(8,9)->(5,9) NOT
> CONFIRMED (timeout, lastScreen=BattleMoving)` after 10s on Ramza's
> turn — happened twice. Recovery (`execute_action ConfirmMove` then
> retry) didn't unstick it either; the move had to be abandoned. Move
> commit reliability degraded as battle progressed.
>
> Iaido Kiku-ichimonji `execute_turn` worked but took 17s — long enough
> I wasn't sure if it had hung. A heartbeat or "still working: <step>"
> would help.

## 3. Missing
> **A way to know an ability's actual range/AoE before targeting.**
> The output `Meteor ct=10 → (9,9)<Kenrick SELF> (10,9)<Lloyd ALLY>...`
> tells me nothing about R or AoE. AoEs are listed as e.g. `R:Self
> AoE:2` for self-radius but for offensive AoEs I got no R/AoE annotation
> at all and the target-list was incomplete. A `R:N AoE:M` tag on every
> ability line, plus showing both ally AND enemy occupants in the cached
> tiles, would let me plan instead of guess-and-error.
>
> A `revive` / `phoenix_down <unit>` shortcut that finds a living unit
> within range. Kenrick crystal-counted from (7,4) and the only options
> were "guess if Wilham can reach via Move + Phoenix Down" — too much
> manual planning for a time-pressure decision.

## 4. Wrong
> **False-positive `[BattleVictory]` flashes after `execute_turn` calls.**
> Hit at least 3 times over the session. Example:
>     `execute_turn 8 9 Shout` → `[BattleVictory] ... t=9143ms[execute_turn]`
>     `[FAILED] Cannot battle_wait from screen (current: BattleVictory)`
>     immediately after, `screen` returned `[BattleMoving] ui=(8,9)`.
> No enemies had died. The screen-flicker doc in Commands.md ("Trust
> the LATER read") is acknowledged but a `BattleVictory` mid-turn
> cascades into spurious failure messages and panicked recovery code.
> Worse: my agent's "the battle is over!" reasoning would have been
> totally wrong if I trusted the response.
>
> `battle_attack 2 6` returned `HIT (318→274/318)` but the next
> `scan_move` showed Summoner unchanged at 318/318. The damage was
> fabricated in the success line — a whiff. (A *later* attack DID
> register the 44 dmg, but the first attempt's "HIT (X→Y)" was a lie.)
> Worse than `MISSED!`: I planned next turn assuming the enemy was
> already softened.
>
> `Used Shout (self-target) → (10,10) HP=528/528` — Shout is R:Self
> on Ramza but the response printed Wilham's position as the target.
> The display tile is wrong for self-only abilities (or the ability
> didn't actually fire — Ramza never gained Shout's PA/MA buffs in the
> next scan, only Hasteja's Haste). Either way, the response message
> was misleading.
>
> Outcome diff log shows **A→B→A position artifacts** constantly:
>     `> Knight moved (2,5) → (8,10)`
>     `> Knight moved (8,10) → (2,5)`
>     `> Lloyd moved (8,8) → (10,9)`
>     `> Lloyd moved (10,9) → (8,8)`
> No way Knight teleported 8 tiles to my line and back in one turn.
> Looks like the snapshot-diff is reading stale + fresh positions and
> emitting both transitions. Filters out a single-turn "round trip"
> would clean this up.

## 5. Surprises
> Lloyd's class showing `(Orator)` while his abilities are pure Geomancy
> was confusing — see 1.
>
> The "Defending" + "Evasive Stance" combo on enemies absolutely walls
> Lloyd's gun. He missed every shot for 4-5 turns. Felt very PSX-FFT
> in a way I didn't expect from a remaster.
>
> `execute_turn` timing being non-atomic (move commits even if ability
> fails) is documented in Commands.md but I still got bitten — when an
> ability errored mid-bundle, my unit was advanced into the danger zone
> with no Action available. Bigger warning at the top of the helper
> output (e.g. `[PARTIAL] Moved (8,9)->(5,9) but Ultima failed range —
> unit out of cover, plan recovery`) would help me notice without
> re-reading docs every time.

## 6. Wins
> **TURN HANDOFF banner is excellent.** `=== TURN HANDOFF: Kenrick(Thief)
> → Lloyd(Orator) (10,9) HP=432/432 ===` is loud, scannable, contains
> exactly what I need to re-plan. Same for `[OUTCOME] Kenrick +Charging`
> — clean per-turn delta. Don't change either.
>
> The compact one-line header (`[BattleMyTurn] ui=Move
> Kenrick(Thief) (9,9) HP=467/467 MP=53/53 curLoc=The Siedge Weald
> t=531ms[scan_move]`) gives all the context in one line. Header rules.
>
> `scan_move` defaults are sensible: heights, recommended facing,
> timeline preview. The `Heights: caster h=5 vs enemies h=2-3` line
> told me at-a-glance I had high-ground advantage.
>
> `[ACTED]` / `[MOVED]` flags in header — clear, hard to miss.
>
> Outcome log when it works (`> Knight took 63 damage (HP 531→468)`)
> is the single most useful artifact for tracking what actually
> happened.

## 7. One change
> **Show the full set of cached valid target tiles in the rendered
> ability list, with both ally AND enemy occupants annotated.** Render
> a compact summary like `Meteor R:5 AoE:2 → 27 tiles, best: (4,5) hits
> 4 enemies` instead of "here are the 4 ally tiles in radius." Right now
> I have to memorize the Meteor radius and try targets one by one to
> find the valid set. This single change would unblock half the offensive
> abilities I tried.

## Anything else
> Strict mode is fine, no friction from it.
>
> The driver-prompt asking me to "ignore auto-memory" was easy to
> follow — I never opened it.
>
> Multi-unit turn cycling worked: I never lost track of who was active
> after a battle_wait once the TURN HANDOFF banner landed.
>
> One micro-bug: `execute_action Wait` from BattleMyTurn took me to
> `[BattleMoving] ui=(8,8)` once instead of opening Wait. Probably the
> action-name-to-key mapping confused Wait with Move at that menu state.
> battle_wait helper worked correctly.

---
