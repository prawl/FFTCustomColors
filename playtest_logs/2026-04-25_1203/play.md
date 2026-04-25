# Playtest play log — 2026-04-25_1203

Append timestamped beats as you play. One or two lines each. Capture
friction in the moment — confusing output, slow commands, surprises,
wins. Don't worry about prose; a fragment is fine.

Example beats:
- `T+0:30 — Reading docs. Commands.md is dense; needed two passes.`
- `T+3:15 — First scan_move. Attack tiles empty? Confused.`
- `T+8:00 — Aurablast → "HIT (damage unread)". What's "damage unread" mean?`

---

- T+0:00 — Sourced fft.sh, strict 1, screen. Battle at Siedge Weald, Ramza solo full HP 719, fancy Chaos Blade weapon, big item bag. 4 enemies (Black Goblin, 2 Dryad, Wisenkin). Lots of buffs already applied.
- T+1:00 — execute_turn 5,10 Attack 4,10 — output line was "[BattleMyTurn] ui=Move ... t=20247ms[execute_turn]!!". The trailing "!!" is unexplained — what does it mean? Re-screen showed move worked + did 336 dmg to Dryad (didn't kill), HP 719→601 (took damage), Dryad relocated to (2,11) and another Dryad now BEHIND me at (6,11). fft.sh helpers don't persist across Bash calls; need to re-source every command. Annoying friction.
- T+1:30 — Move (3,11) + Attack (2,11) Dryad → 2 enemies dead!? Dryad (intended target) and Black Goblin both at HP=0. Surprise — Chaos Blade onHit Stone proc plus reactions/regen worked out massively in my favor. HP 601→690 (Regen).
- T+2:00 — Move (4,11) + Attack Dryad (5,11) → Dryad PETRIFIED! HP 241/689 with Stone status. Chaos Blade is a beast. Ramza fully healed (719/719) by Regen. Only 2 live enemies left: petrified Dryad + Wisenkin.
- T+3:00 — Move (4,9) + Attack Wisenkin (4,8) → INSTANT GAME OVER. From 719/719 HP buffed Ramza to dead in one engagement. logs show Wisenkin has reaction=Counter; that alone shouldn't one-shot a 719/96-Brave Ramza with Protect. Surprised — felt unfair. Possibly Wisenkin's attack hit very hard (lv 91) AND counter back? Bigger friction: would love feedback in screen output explaining what happened: "Wisenkin counter-attacked for 800". Right now I just see GameOver and have to read logs.
- T+3:30 — screen reports GameOver, screen -v reports WorldMap, screen again reports WorldMap. State-detection inconsistency between calls + between -v variants. Looks like GameOver was a brief override during the post-death animation; underlying screen is WorldMap.
- T+4:00 — Battle ended fast. Per briefing, not retrying. Will spend remaining time on feedback.
- T+4:30 — Wrote feedback.md early since battle ended fast and parent's brief says don't restart/load. Plenty of slack to be thorough.
