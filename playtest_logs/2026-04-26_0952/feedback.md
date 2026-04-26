# Playtest feedback — 2026-04-26_0952

Fill out between T+25 and T+30. Be specific — quote actual command
output where relevant. Friction is the point; "wins" matter too so we
don't accidentally remove what's working.

---

## 1. Confusing
> The opening `screen` dumped 13 Geomancy spells in near-identical lines (same R:5 AoE:2, same target list, same `best:` ranking). They differ only in element/status-rider, but visually they're a wall of repetition that buries everything else. I had to scroll past them to find the unit list — which is where the *actually critical* info lived (Ramza DEAD, Wilham DEAD, Kenrick TREASURE). Either collapse identical-shape skillset entries ("13 Geomancy variants — same targets, see X for full list") or move the Units block above the Abilities block in the renderer.

> Also confusing: `primary=Speechcraft` was listed in the header but no Speechcraft abilities appeared. Took me a second to realize "no listed abilities" means "none learned." A `(0 learned)` annotation on the primary= header line would have made it explicit.

## 2. Slow
> The 5-minute "read the manuals" pre-roll. I had to read CLAUDE.md + Rules.md + Commands.md + BattleTurns.md + AbilitiesAndJobs.md + skim WorldMapNav before the first action. For a fresh-eyes agent every spawn, that's a full 5+ minutes of reading on a 30-min budget — 17% of my time gone before I see the game. A `FFTHandsFree/Instructions/Cheatsheet.md` (one page: top 10 commands, 5 gotchas, screen-state cheat) would let a fresh agent be useful in 60s, then defer to full docs only on demand.

> The 24-second `execute_turn` for move+attack+wait. Header correctly red-flagged it (`!!`) so the tooling knows it's slow, but most of that was probably enemy-turn animation between sub-steps, which is unavoidable. NOT a bug, just noting "this is the floor for a turn that involves enemies acting."

## 3. Missing
> No surfaced **death timer / crystallize countdown** for KO'd allies. Ramza was DEAD when I opened, and the 3-turn → crystallize timer is decisive info ("Do I have time to revive him?"). I'd expect the unit row to show ` DEAD(2)` meaning 2 turns left. BattleTurns.md mentions the 3-turn deathCounter but it's not in the scan output.

> No **danger summary** at the top of the response. With 1 alive / 2 dead / 1 treasure / 0 ally, I should have seen a banner like `=== 1 PLAYER ALIVE — 2 KO'd, 1 LOST ===`. The current header only shows the active unit. A fresh-eyes player has to count corpses in the unit list to realize how cooked they are.

> No **revive-availability** flag in the scan when an ally is dead. If `lifeState=dead` exists on the field but the active unit's kit has no Phoenix Down / Raise / etc., a `[no revive in your kit]` flag would help. Right now you have to know that Speechcraft and Geomancy don't include revive — that's prior knowledge a fresh agent doesn't have.

## 4. Wrong
> Not a bug per se, but the `!blocked` flag was on the Archer at (6,10) from my position (9,8). After moving to (10,11) — actually FARTHER from that Archer (still distance 5) — the attack landed cleanly. Suggests `!blocked` from (9,8) was either correct (some terrain on that line) or false-positive. Either way, the docs say "treat as a hint" so this isn't wrong, but it would help to know which case it was. A `!blocked(reason: terrain at (8,10) h=5)` annotation would let me reason about it.

## 5. Surprises
> The `lifeState=treasure` on Kenrick — I'd never read about Treasure Pots/Chests in the docs (or the wiki sections I read). BattleTurns.md mentions it but I had to grep to confirm "permanently gone." A new-player surprise that's well-handled by the data layer; surfacing a one-line "Kenrick became a treasure chest — permanent loss" event when it happened would be friendlier.

> `execute_turn` aborted at the wait sub-step with `[turn-interrupt] step 'battle_wait' landed on GameOver (BattleEnded)`. The bundle's atomicity caveat is documented (move commits even if ability fails) but the GameOver-mid-bundle case is its own genre — my Move and Attack landed; somewhere during the auto-Wait the battle ended. The message handled it gracefully, which is a win — but I'd love a one-line "Battle ended during your turn — no further play possible" rather than just "aborting bundle."

## 6. Wins
> `screen` as the one-stop opener is excellent. The header `[BattleMyTurn] ui=Move Lloyd(Orator) (9,8) HP=105/432 MP=73/73 curLoc=The Siedge Weald t=380ms[scan_move]` told me 95% of what I needed at a glance. The `[scan_move]` action tag made it clear `screen` runs the full scan under the hood — I never had to think "do I need scan_move OR screen".

> The ability renderer's per-tile annotations (`>rear`, `^strengthen`, `[TOO CLOSE]`, `!blocked`, `<Lloyd SELF>`) are dense but readable once you parse one. `[TOO CLOSE]` on the gun-vs-adjacent immediately told me my Blaze Gun couldn't shoot the Time Mage at (9,9) — saved a wasted action.

> `execute_turn` aborting cleanly when the battle ended mid-bundle and reporting WHICH sub-step failed (`step 'battle_wait' landed on GameOver`) was excellent error handling. No phantom state, no need to recover.

> The `RecommendedFacing` line told me which way to face; auto-face on `battle_wait` would have done it for me. Nice that the agent doesn't have to manually pick a facing.

## 7. One change
> Add a **"battle situation summary"** banner at the top of `screen` output when the field is lopsided. Something like:
> ```
> === SITUATION: 1 PLAYER ALIVE / 2 KO'd / 1 LOST | 3 ENEMIES ALIVE ===
> Ramza DEAD → crystallize in 2 turns (no revive in your kit)
> Wilham DEAD → crystallize in 3 turns
> ```
> Right now this info is reconstructable from the unit list, but a fresh-eyes agent spends 30+ seconds counting corpses and inferring the death-counter. A summary would let me triage in 2 seconds and immediately know "I cannot win this; play conservatively / accept GameOver."

## Anything else
> Drove total commands: 4 bridge calls (`screen`, `execute_turn`, `screen`, `screen -v`). Battle ended in turn 1. Honest assessment: the seed state was probably already a loss — Lloyd at 105 HP with a non-revive kit, two dead allies, three live enemies. A future fresh-eyes seed with a slightly less catastrophic opener would let the agent exercise more of the bridge surface (multi-turn play, return_to_my_turn from sub-menus, scan_move overrides, ability cancellation, etc.). That said: getting a near-impossible state on turn 1 surfaced friction I wouldn't have noticed in a winning run — namely the "no danger summary" and "no death-timer surfaced" gaps.

> Re-reading the manuals took ~5 minutes of a 30-min budget. The driver prompt is right that fresh-eyes is the point, but a one-page TLDR.md (commands list + screen state table + 3 most common gotchas) would cut that to 60s without sacrificing the fresh-eyes goal. The full instruction files are well-structured but each is 200+ lines — a fresh agent reads all of them defensively rather than knowing which subset matters for "I'm in a battle right now."

---
