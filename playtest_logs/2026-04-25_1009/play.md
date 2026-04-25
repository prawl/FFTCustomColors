# Playtest play log — 2026-04-25_1009

Append timestamped beats as you play. One or two lines each. Capture
friction in the moment — confusing output, slow commands, surprises,
wins. Don't worry about prose; a fragment is fine.

Example beats:
- `T+0:30 — Reading docs. Commands.md is dense; needed two passes.`
- `T+3:15 — First scan_move. Attack tiles empty? Confused.`
- `T+8:00 — Aurablast → "HIT (damage unread)". What's "damage unread" mean?`

---

- T+0:00 (10:37:38) — Start. Read CLAUDE.md, Rules.md, Commands.md, BattleTurns.md, AbilitiesAndJobs.md (skim).
- T+0:30 — `screen` works. Ramza Gallant Knight at (8,10) HP 719/719 with Chaos Blade. 4 enemies far west, closest d=7. None in Attack range. Mettle abilities all visible (Focus, Tailwind, Chant, Shout, etc.). Items dump is loud — 18 ability lines for self-target items I'd never use this turn. Could collapse.
- T+0:45 — `execute_turn 4 10` moved Ramza west. 15.6s round-trip. Bridge response is just `[BattleMyTurn] ui=Move ... t=15621ms[execute_turn]!!` — the trailing `!!` is unexplained anywhere I read. Interpreted as "completed with notes" but had to scan again for the actual delta.
- T+1:00 — On rescan at (4,10), HP=601/719 — took 118 dmg between turns (no narrator line in screen output, only inferred from HP delta). Enemies named `(?)` because mod-forced battle (Siedge Weald). Attack tile shows "Up→(3,10) enemy HP=671" — that worked great.
- T+1:10 — `battle_ability "Attack" 3 10` → MISSED. Response: `Attacked (3,10) from (4,10) — MISSED! → (3,10) HP=601/719`. The "→ (3,10) HP=601/719" suffix is confusing — that's Ramza's HP (601/719), not the target's HP at (3,10). Misleading.
- T+1:20 — `battle_wait` returned with NARRATOR lines: `> Skeleton moved (5,10) → (3,11)` and `> Ramza took 287 damage`. THIS is great — the play-by-play between turns is exactly what I needed. Wish the same line appeared after the move I made at T+0:45 (no narrator there).
- T+1:25 — Wrap. Ramza at HP 314/719. Have not seen the battle end.

