# Playtest play log — 2026-04-26_0905

Append timestamped beats as you play. One or two lines each. Capture
friction in the moment — confusing output, slow commands, surprises,
wins. Don't worry about prose; a fragment is fine.

---

T+0   1777208833  start. read CLAUDE.md + 7 Instructions/*.md, sourced ./fft.sh.
T+0   `screen` returned a beautiful ability dump. Lloyd at HP=105/432, last man standing — Ramza, Kenrick, Wilham all DEAD. 3 live enemies: Time Mage adj at (9,9) >rear, two Archers at (5,11) and (6,10) — too far for Geomancy AoE.
T+0   FRICTION: scan dump shows primary=Speechcraft secondary=Geomancy, but only Geomancy abilities + basic Attack are listed. Speechcraft skills don't appear at all. I assume "no valid targets in range" hides the skillset, but it should still be visible somewhere — I have no idea whether Lloyd has Insult / Preach / Solution / etc. learned.
T+0   FRICTION: Attack tiles section says `Left→(9,9) enemy (Time Mage) HP=225 >rear` — clearly in range. But the Attack ability's per-tile entry on (9,9) is `<Archer !blocked>` style at distance, while (9,9) entry says (...wait, let me re-read)... actually (9,9) doesn't appear in the `Attack R:8 → ...` list at all. Only (6,10), (5,11) appear as `!blocked`. So I tried `battle_attack 9 9`.
T+1m  `battle_attack 9 9` → "Tile (9,9) is not in basic-Attack range. (69 valid tiles)." This contradicts the `Attack tiles: Left→(9,9)` line in the same scan output. Blaze Gun must have MinRange>1 (gun MinRange typically). Docs say `[TOO CLOSE]` should appear, but I see neither `[TOO CLOSE]` nor any indication that (9,9) is excluded.
T+1m  pivot to `battle_ability "Magma Surge" 9 9` — works! Response tagged `[BattleVictory]` (!). Damage 225→181, "cursor was already on target". I assumed the battle ended and was confused (Archers still alive at d=5+).
T+1m  `screen` again → returns to `[BattleMyTurn] [ACTED]`. The BattleVictory tag was a flicker / stale-override per Commands.md "Screen state can flicker." But the scan_move STILL shows Time Mage HP=225/355 — same as before the attack. So either the master HP store hasn't refreshed (first-turn stale-cached scan?), or the units[] block lags. Confusing — what's the truth?
T+2m  `battle_wait` → `[GameOver]`, narrator banner `> Archer moved (5,11) → (7,11)` then `> Archer moved (7,11) → (9,8)`. The Archers chain-moved and finished Lloyd. No KO line was reported for Lloyd in the recap — recap only shows "Archer moved → (9,8)" which IS Lloyd's tile but the narrator doesn't say "Lloyd KO'd" or "you took N damage."
T+2m  Per driver prompt: GameOver = stop, no retry. Wrote feedback at T+2m (very early — battle was already terminal when I joined, only one turn fit).

---
