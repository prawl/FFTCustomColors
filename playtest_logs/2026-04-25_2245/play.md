# Playtest play log — 2026-04-25_2245

Append timestamped beats as you play. One or two lines each. Capture
friction in the moment — confusing output, slow commands, surprises,
wins. Don't worry about prose; a fragment is fine.

---
T+0 (1777171698): screen → BattleMyTurn Kenrick(Thief). 4 player on cliff h=5, 5 enemy at d=10-15 below. Cast Hasteja AoE (ct=15) self-target — clear `<SELF>` markers helped.
T+2 (~1777171780): battle_wait → TURN HANDOFF banner clear and useful. Outcome log shows enemy moves but contains apparent A→B→A artifacts ("Knight moved (2,5) → (8,10) → (2,5)") which look like snapshot/diff bug, not real moves.
T+3: Lloyd(Orator) up. Has Geomancy abilities but every one shows ALL FOUR allies as targets and no enemies — confusing whether range is just short or output is mis-rendered. Skipped, moved Lloyd forward 8,8.
T+4: execute_turn 8 9 Shout for Ramza → returned `BattleVictory` exit-code-1 BUT actual screen was BattleMoving stuck at (8,9). False-positive Victory mid-turn is alarming friction. Recovered with execute_action ConfirmMove.
T+5: After ConfirmMove → BattleAttacking targeter open (Shout shouldn't open targeter — it's R:Self). Cancel → BattleMyTurn `acted` flag set. battle_ability Shout responded "Used Shout (self-target) → (10,10) HP=528/528" naming Wilham's tile — output tile coords misleading for self-only ability. Net effect: action consumed but no Shout buff visible (only Hasteja landed).
T+10 (~1777172310): Several friction discoveries: (1) Slowja/Meteor showed only 4 ally tiles in scan output but errored with "27 valid tiles cached" — render hides most cached targets. (2) battle_attack returned `[BattleVictory] Attacked (2,6) — HIT (318→274/318)` — claimed hit + damage but enemy HP unchanged in next scan (whiff). False-positive Victory state AND fabricated damage in success message. (3) Lloyd's position bounced (7,9)→(8,8) inexplicably between scans. (4) Recurring scary `[BattleVictory]` flashes from Lloyd's actions when no enemy died.
T+15: Wilham Iaido cycle worked cleanly via execute_turn. Kenrick attack on Knight whiffed (HIT msg with no damage info — but DID land 63 damage as shown in next outcome log). Cumulative friction: ConfirmMove timeouts on Ramza turns (multiple), abilities listing only allies for offensive ranged spells (Ultima/Slowja/Meteor), false-positive `[BattleVictory]` after move-only execute_turn at least 3 times.
T+22: Kenrick KO'd by Summoner spell. Multi-unit handoff banner consistently good. Knight took 63 damage. Combat slow because Defending+Evasive Stance enemies are evade-walls; Lloyd's gun repeatedly missed.
T+24: Wilham Iaido attempt — execute_turn returned BattleVictory false-positive but real state BattleMoving. Stopping play to write feedback at T+25.
