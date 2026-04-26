# Playtest feedback — 2026-04-26_0905

Fill out between T+25 and T+30. Be specific — quote actual command
output where relevant. Friction is the point; "wins" matter too so we
don't accidentally remove what's working.

---

## 1. Confusing
> The biggest confusion of this short session was the **Attack tiles vs. Attack-ability disagreement**. The same `screen` dump simultaneously claimed:
>
> ```
> Attack tiles: Left→(9,9) enemy (Time Mage) HP=225 >rear
> ```
>
> AND
>
> ```
> Attack R:8 → (6,10)<Archer !blocked> (5,11)<Archer !blocked> (+67 empty tiles)
> ```
>
> (9,9) does not appear in the `Attack R:8` list at all — it's silently filtered out, presumably because Blaze Gun has MinRange>1. So I trusted the more visible `Attack tiles: Left→(9,9)` cardinal panel, called `battle_attack 9 9`, and got `failed: Tile (9,9) is not in basic-Attack range`. The docs (BattleTurns.md) describe a `[TOO CLOSE]` annotation for exactly this case — but it didn't render here. Either the bug is that the cardinal `Attack tiles:` panel doesn't apply MinRange filtering at all, or `[TOO CLOSE]` regressed for guns. Either way, two parts of the same scan output disagree.
>
> Second confusion: the `BattleVictory` flicker. `battle_ability "Magma Surge" 9 9` returned with `[BattleVictory]` as the screen tag, but two enemies (Archers) were clearly still alive at d=5/d=7. I had to follow up with `screen` to confirm it was a flicker (Commands.md does warn about this, in fairness). Worth annotating in the response message itself — something like "(transient — re-call screen to confirm)."

## 2. Slow
> `battle_wait` took 9.7s — `t=9731ms[battle_wait]`. Two Archers each took a turn during enemy phase, so 9.7s for two enemy turns plus a GameOver transition is roughly OK, but the user feels it. No other commands felt slow; `screen` was 173-451ms, `battle_ability` resolved fast. The Magma Surge response time wasn't logged separately but the round-trip felt instant.

## 3. Missing
> **Speechcraft skills are invisible.** Lloyd is an Orator with `primary=Speechcraft secondary=Geomancy`, but the scan dump only listed `Attack` + the 12 Geomancy abilities. None of the Speechcraft abilities (Insult, Preach, Solution, etc. — whatever Orator's primary kit is) appeared anywhere. If they're learned, I should be able to see them. If they have no valid targets right now, I'd still want to know they exist so I can plan a turn that brings them into range. As a first-time agent, I had no idea what Lloyd's primary skillset can even DO — and the secondary was the only thing I could ever reach.
>
> **Per-unit lethality readout.** I knew Lloyd had HP=105/432 and Time Mage was adj at (9,9). I had no signal that the two Archers, even from d=5+, could one-shot me on their CT next turn. A hint like "incoming threat: Archer R=4-5 ranged, can hit (9,8) from current position" would have changed my play (would have moved off the death tile rather than firing in place).
>
> **The recap on GameOver.** The `> Archer moved (5,11) → (7,11)` / `> Archer moved (7,11) → (9,8)` lines are good — but they're missing the actual lethal action. Did Archer attack me to KO, then walk onto my tile? Did Lloyd get crystalized? "Lloyd KO'd by Archer (X dmg)" is the line I expected.

## 4. Wrong
> The cardinal `Attack tiles: Left→(9,9)` panel shouldn't list a tile that fails MinRange. That's the actionable bug from this session: the panel claims `Left→(9,9) enemy (Time Mage) HP=225 >rear` but `battle_attack 9 9` immediately rejects it. Either filter the panel by MinRange or attach a `[TOO CLOSE]` tag (the docs promise the latter but it didn't appear).
>
> Possibly wrong: the units[] HP didn't update after my Magma Surge. Pre-attack scan: `Time Mage (9,9) HP=225/355`. Magma Surge response: "(225→181/355)". Post-attack scan: `Time Mage (9,9) HP=225/355` again — unchanged. This may be a one-frame stale read, but it was the second `screen` call, so I'd expect it to be fresh.

## 5. Surprises
> Pleasant surprise: the abilities-and-targets dump on `screen` is genuinely beautiful — the per-ability `best:` placements with element affinity tags (`>rear`, `^strengthen`), AoE ranking, MP/range/proc-status all in one block, is exactly what I needed to plan. I picked Magma Surge in seconds because the row showed `(9,9) e:Time Mage` as the top center and the `>rear` tag on the per-tile entry confirmed the backstab arc. Without that I'd have been hunting through scan_units output for tile coordinates.
>
> Unpleasant surprise: dropping into a battle where 3 of 4 allies are already dead and the active unit is at <25% HP is brutal. That's a starting-state choice, not a friction item — but I had effectively one good turn to make a difference, and there was no obvious safe option (move away → die to Archer ranged anyway; attack the rear-arc Time Mage → die to Archer ranged anyway). Live and learn.

## 6. Wins
> - `screen` as a single command renders activeUnit + all abilities + valid targets + best AoE centers + recommended facing + heights + full unit list. One call replaced what would otherwise be 4-5 separate commands.
> - `battle_ability "Magma Surge" 9 9` was buttery — found the ability by name, validated range, dispatched cursor + confirm, returned the damage delta. No raw key presses, strict mode happy.
> - `./fft <helper>` per-call wrapper meant I never lost helper functions across Bash calls. The wrapper is invisible infrastructure when it works, which is a sign it works well.
> - The element-affinity / arc-tag system (`>rear`, `^strengthen`) is a great compression of three pieces of info into two characters per target. Reading `(6,10)<Archer ^strengthen>` told me instantly: archer at this tile has fire/ice/wind affinity that strengthens its OWN damage if I hit it with that element. Tight signal.

## 7. One change
> Fix the cardinal Attack panel / `[TOO CLOSE]` tag for ranged weapons. Today the cardinal panel says `Left→(9,9) enemy (Time Mage) HP=225 >rear` for a Blaze Gun user where (9,9) is NOT a legal target. Either (a) filter the panel by the active unit's weapon MinRange, or (b) reliably attach `[TOO CLOSE]` to the cardinal entries when MinRange>1 excludes them. The Attack ability list already filters those tiles out — the cardinal panel should at least flag them.

## Anything else
> Session was very short (~2 minutes wall clock, ~5 bridge calls) because the battle was already terminal when I joined and I lost on my second action. Volume of data is small, but the friction items above are real and reproducible.
>
> Doc accuracy: Commands.md and BattleTurns.md were both excellent. The "screen state can flicker" gotcha at the bottom of Commands.md is exactly the warning I needed when I saw `[BattleVictory]` mid-fight. The `[TOO CLOSE]` annotation IS documented in BattleTurns.md (line 134) — the implementation just didn't fire here. So this is a code bug, not a doc bug.
>
> Strict mode (`strict 1`) was in force the whole time and never blocked anything I wanted to do. Good shape.

---
