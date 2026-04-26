# Playtest feedback — 2026-04-25_1955

Fill out between T+25 and T+30. Be specific — quote actual command
output where relevant. Friction is the point; "wins" matter too so we
don't accidentally remove what's working.

---

## 1. Confusing
> **`execute_turn` requires a move-to-tile that differs from current pos.** Tried `execute_turn 7 9 "Phoenix Down" 6 9` while standing on (7,9) and got `[FAILED] Tile (7,9) is not in the valid move range. Run scan_move first and pick from ValidMoveTiles (22 valid tiles available)`. The tile **was** my current tile — but the validator only accepts tiles in the `Move tiles:` list, which excludes the origin. So if I'm already optimally positioned and just want to act, I have to either pick a wasteful adjacent tile (giving up Brave Face/back-protection) or split into `battle_ability` + `battle_wait`. The error message also misled me — it lists "22 valid tiles" but doesn't tell me my current tile is implicitly invalid, so I re-scanned thinking I'd misread.
>
> **Move-tile lists are wide, unsorted, and hard to scan.** Example from T+5: `Move tiles: (7,10 h=3) (7,8) (9,9 h=5) (8,10 h=5) (8,8 h=5) (6,10 h=2) (7,11 h=4) (6,8) (7,7) (10,9 h=5) (9,8 h=5) (8,11 h=5) (5,10 h=2) (6,11 h=3) (8,7) (4,9) (7,6) (10,10 h=5) (10,8 h=5) (9,7 h=3) (9,11 h=5) (5,11 h=2)`. To pick a tile near a specific enemy I need to mentally cross-ref against the unit list. Sorted-by-distance-to-nearest-enemy or grouped-by-row would help.
>
> **`>BACK` annotation on attack tiles is ambiguous.** It means "I'd be attacking their back" but I read it twice as "BACK = run away" the first time. `>back-hit` or `(rear)` would be clearer.

## 2. Slow
> **Every `execute_turn` takes 14-37s round-trip even on simple moves.** Concrete: T+0 `execute_turn 5 9 Hasteja` → 19234ms. T+1 move-only → 14074ms. T+10 move+ability that silently failed → 36992ms. Even successful single-action turns take 8-19s. After ~10 turns you've burned 3+ minutes on bridge wait time with nothing happening.
>
> **The "victory false-positive" path adds 8-15s recovery per occurrence.** I hit it 3 times in a row at T+7. Each time: bridge bails on `wait` sub-step → I call `screen` → call `battle_wait` → succeed. Adds a noticeable tax to every ranged attack.

## 3. Missing
> **No outcome recap after `execute_turn`.** When a TURN HANDOFF banner fires, I get the next-active-unit info but **nothing about what just happened**: did Hasteja hit anyone? did the Phoenix Down land? was the X-Potion +150 applied or wasted on a full-HP unit? I have to diff HPs from a prior `screen` mentally. A `Last action: Kenrick cast Hasteja → hit Kenrick(+Haste), Wilham(+Haste). Wilham damaged 467→528.` line would be huge.
>
> **No "stand still and act" command shape.** All `execute_turn` paths require a move tile. If I want to act-then-wait without moving, I have to call `battle_ability` + `battle_wait` separately and parse two responses. A `wait_in_place=true` flag or a `execute_turn_inplace` would be welcome.
>
> **No revive/heal helper that picks the best target.** With 3 allies KO'd I had to manually iterate Phoenix Down + tile + ally each turn. A `revive_nearest` or `heal_lowest_ally` shortcut would be clutch in emergencies.

## 4. Wrong
> **`battle_ability "Shout" 7 9` printed wrong target in summary.** From T+2: I cast Shout (self-only ability) standing on (7,9), and the summary line said `→ (10,10) HP=528/528` (Wilham's tile — a different unit). The actual cast was correct (Shout fired on Ramza, self-buff applied), but the printed target tile was wrong. This is the same phantom-success class of bug I'd seen reports about — auto-fill picked the wrong tile but didn't propagate to the actual game action.
>
> **Move-only `execute_turn` ends the turn (contra docs).** From T+1: `execute_turn 6 9` (move only, no ability) returned with TURN HANDOFF Lloyd→Ramza. CLAUDE.md / Commands.md hint that two-arg execute_turn returns to BattleMyTurn with Act/Wait still available. Either docs are stale or behavior changed.
>
> **`response.json` got deleted on a 37s execute_turn.** From T+10: `execute_turn 7 8 "Phoenix Down" 6 9` returned `[]` then `cat: response.json: No such file or directory`. The action partially completed (move + enemy turns) but the ability sub-step's response was clobbered. There's a known race in MEMORY (response.json overwritten by chained bridge calls) — but here I only made ONE call. So the bridge itself is racing internally during long execute_turn flows.

## 5. Surprises
> **3 of my 4 allies died while I was off-screen between turns.** I made an action at T+8, returned at T+10, and Kenrick / Lloyd / Wilham were all KO'd. The 5 enemies cycled multiple turns in between and obliterated my back line. The bridge gave me NO indication on the next BattleMyTurn that anything dramatic happened — no "3 allies KO'd this round" banner. I had to read the unit list and notice the `DEAD` tags myself.
>
> **`>BACK` damage triggered Counter / Chaos Blade onHit and petrified an enemy without me noticing.** Knight at (5,8) HP=531/531 at T+5 became Knight at (6,8) HP=139/531 [Petrify,Haste] STONE at T+10. Cool emergent behavior, but I learned about it by accident reading the unit list 3 minutes later.

## 6. Wins
> **`screen` output is genuinely good.** The Abilities + targets list with `<? >BACK>`, `<Ramza SELF>`, `<Lloyd ALLY [REVIVE]>` annotations told me exactly what each ability could hit and at what disposition. The `[REVIVE]` tag on Phoenix Down + dead-ally tile combo was instantly readable.
>
> **Inventory counts inline (`Phoenix Down [x98]`, `X-Potion [x92]`).** Saved a separate inventory query and made it obvious I had headroom.
>
> **Status flags on units (`[Charging]`, `[Haste]`, `[Petrify,Haste]`, `[Defending]`, `STONE`, `DEAD`).** Compact and informative.
>
> **TURN HANDOFF banner.** When it fired (e.g. `Kenrick → Lloyd (10,9)`), it correctly told me the new active unit + tile. That was the multi-unit-cycle bug from prior sessions — feels solved on the banner side.
>
> **Recommend Wait line.** `Recommend Wait: Face West — 1 front, 4 side, 0 back` is great prompt-engineering for an LLM driver: shows what the cost/benefit of each facing is.
>
> **Timeline preview.** `Timeline: E(E,ct=32) → E(E,ct=94) → ...` lets me anticipate enemy ordering before I commit.

## 7. One change
> **Add an "outcome line" to every `execute_turn` response, BEFORE the TURN HANDOFF banner.** Format suggestion:
>
> ```
> [TURN COMPLETE] Kenrick cast Hasteja from (5,9) → hit: Kenrick (+Haste), Wilham (+Haste, +Regen). 0 enemies in radius.
> [TURN HANDOFF] Kenrick → Lloyd (10,9) HP=432/432 ...
> ```
>
> This solves: (a) verifying actions actually landed, (b) catching the wrong-target-printed bug visually, (c) building a mental model of what happened during enemy turns I missed, (d) reduces "did Phoenix Down work?" `screen` round-trips. Combined with a "missed enemy turns" recap (`While you waited: 3 allies KO'd by enemy archers`), the bridge stops feeling like blind play.

## Anything else
> **The 37s execute_turn that lost response.json (T+10) is the worst single bridge experience I had.** The bridge said `[]` (empty success) but the file the helper tried to cat was gone. I had no way to tell if the ability ran. I had to call `screen`, see Lloyd was still DEAD with HP=0, and infer the Phoenix Down sub-step was skipped. If this happens mid-tournament-style play with no opportunity to observe, the agent is operating blind. The bridge should at minimum return a clear `[FAILED] response.json missing — sub-step did not complete` rather than `[]`.
>
> **Soft cap of ~80 commands felt comfortable but the per-command latency would have made it tight if I'd gotten through more turns.** I used ~25 bridge commands in 30 minutes; most of the wall clock was waiting on `execute_turn`.
>
> **Strict-mode helpers were complete enough.** No times I wished for a raw key. The doc-vocabulary was consistent (BattleMyTurn / BattleMoving / BattleAttacking matched what showed up).

---
