# Playtest play log — 2026-04-26_0157

Append timestamped beats as you play. One or two lines each. Capture
friction in the moment — confusing output, slow commands, surprises,
wins. Don't worry about prose; a fragment is fine.

---
T+0 (01:59:50). Sourced fft.sh, screen check. Lloyd Orator (9,8) HP=315/432, last man standing. 3 live enemies: Archer (3,10) d=8, Archer (4,7) d=6, Time Mage (5,9) d=5. Allies all dead. Basic Attack with Blaze Gun shows !blocked on every cardinal. Geomancy R:5 AoE:2 — only (9,8)SELF and (5,9)Time Mage in valid intent list, though best: lines reference (5,7)(5,8). Slight friction: 13 Geomancy abilities all render same valid-target line; I have to read all 13 lines just to compare status effects. Plan: Magma Surge at (5,9) — Instant KO 25% on Haste Time Mage, [Fire] dmg too. Skip moving — preserves option to retreat.

T+5 region. Sequence: (1) Tanglevine on Time Mage 32 dmg, no Stop (25%). [BattleVictory] flicker after — fix #1 not actually working. (2) Wait, enemies moved. Counter-attacked TM. Lost 81 HP from Archer ranged shot. (3) Tried !blocked-tagged basic Attack on TM at (7,10); landed for 32 dmg — !blocked was wrong (doc warned). Another [BattleVictory] flicker. (4) execute_turn 8 8 — header said success "(8,8) acted moved", but turn-state after the next battle_wait suggested move actually didn't commit (no [ACTED], same timeline as pre-wait). (5) Tried Magma Surge 5 10 — bridge cursor mismatch "(8,7) instead of Magma Surge". return_to_my_turn worked cleanly. (6) Re-scanned, retried Magma Surge at 7 10 — cursor-miss to (5,11), action committed at wrong tile. Bridge says "may have committed at wrong tile" — non-recoverable. Screen flipped to BattleEnemiesTurn. (7) battle_wait → GameOver. Lloyd dead.

Major friction: BattleVictory flicker still triggers on damage actions despite alleged fix. Cursor-miss "action committed at wrong tile" path is genuinely catastrophic — turn lost AND wrong action fired, can't recover. battle_wait sometimes returns no narration (no OUTCOME line), making it unclear if turn ended or just polled. execute_turn position-confirmation in header may not match actual game state — got "(8,8) acted moved" then later screen showed (9,8) untouched.

T+~7 (02:06:13). GameOver confirmed. Per rules: no retry/load. Writing feedback.md and exiting early. Total bridge calls: ~13 (well under 80 cap). Total wall-clock: ~7 min of 30. Lloyd died after botched Magma Surge cursor-miss flipped me to BattleEnemiesTurn with no Wait queued; Archer ranged shot finished him.
