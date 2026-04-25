# Playtest play log — 2026-04-25_1740

Append timestamped beats as you play. One or two lines each. Capture
friction in the moment — confusing output, slow commands, surprises,
wins. Don't worry about prose; a fragment is fine.

---

T+3:30 — Started: Kenrick (Thief w/ Time Magicks 2nd) on (9,9), enemies far west (d=13-15). Surprised the abilities header showed only Time Magicks & not Steal — assumed `scan_move` only renders the active skillset, not basic Thief skills. Moved Kenrick forward + cast Hasteja. `execute_turn` with an ability seems to auto-Wait (returned BattleEnemiesTurn) — the docs say "move-only does NOT end your turn" but the bundled-ability variant clearly does. Could be clearer.
T+3:30 — Lloyd (Orator, Geomancy 2nd, Blaze Gun): Geomancy is melee-tile-based, all enemies far. "Attack → ally !blocked" is helpful — wish there was a hint that the gun has range but no LOS. Moved + waited.
T+3:30 — Wilham (Samurai): Iaido renders as "→ self" only — even though they're (I think) AoE around caster? Was confused whether using one would actually swing a sword. Moved + waited. Saw narrator beats "Time Mage moved (1,5)→(1,4)" — very nice touch.
