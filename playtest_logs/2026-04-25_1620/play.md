# Playtest play log — 2026-04-25_1620

Append timestamped beats as you play. One or two lines each. Capture
friction in the moment — confusing output, slow commands, surprises,
wins. Don't worry about prose; a fragment is fine.

Example beats:
- `T+0:30 — Reading docs. Commands.md is dense; needed two passes.`
- `T+3:15 — First scan_move. Attack tiles empty? Confused.`
- `T+8:00 — Aurablast → "HIT (damage unread)". What's "damage unread" mean?`

---

T+1:00 — Read CLAUDE.md + Rules/Commands/BattleTurns/AbilitiesAndJobs. First `screen` (407ms) clean: Ramza Gallant Knight @ (8,10) full HP, Chaos Blade + Maximillian + Regen/Protect/Shell. 5 enemies at distance 6-13, all out of Attack range. Plan: close distance toward (4,6) cluster, save Ultima for a clumped target.

T+2:00 — `execute_turn 5 10 Tailwind` — move worked, Tailwind FAILED with "Tailwind requires a target tile". But scan showed `Tailwind → (8,10)<Ramza SELF>` with "SELF" marker. Friction: SELF marker in scan suggests no-coords usage, but helper requires coords. Retried with `battle_ability "Tailwind" 5 10` and it worked.

T+2:30 — Bridge response after Tailwind said `[BattleVictory]` for 1-2s — state flicker as documented. screen 2s later showed `BattleMoving`. This is the doc'd "state can flicker" gotcha but it's spooky to see "Victory" mid-turn.

T+3:30 — scan_move in `BattleMoving` returned only a header line, no scan body. Docs say scan IS allowed in BattleMoving but in practice the state was stuck post-cast. battle_wait recovered cleanly and showed full event log.

T+4:00 — battle_attack 5 9 → "Tile (5,9) is not in basic-Attack range. Run scan_move ... 2 valid tiles". But scan ALSO said `Attack → (no targets in range)`. Confusing: Attack shows none, error says 2 valid. Different code paths disagree.

T+4:30 — `Throw Stone` worked and showed `Used Throw Stone on (5,9)` but no damage info in response, no [ACTED] in same response header. Had to re-call screen to confirm action consumed. HP didn't change → presumably missed/0 dmg, but bridge didn't say.

T+5:30 — battle_wait after Throw Stone: dropped Ramza 548→36 HP. Multiple enemies in range, gained Oil + Critical. Counter killed an adjacent enemy. Big info dump in event log was helpful.

T+6:30 — execute_turn 8 10 "X-Potion" 8 10 — execute_turn aborted at the wait step because screen flickered to "BattleVictory". Recovered with manual battle_wait. Then events showed Ramza only got 99 HP (Regen) — X-Potion appears to have either failed silently or its 150 HP heal was nullified. Response showed "275→719/719" — that looked like a planned/predicted heal, not actual.

T+7:30 — Re-engaged at (8,10) full of enemies (8,11) crit-low, attacked → MISSED. battle_wait. NEXT scan showed active unit "Tietra" at (3,10), Ramza at (4,12) HP 355. Tietra never appeared in earlier scans — she's a story-added guest? Or the scan is corrupt: cursor "*" was on an ENEMY at (3,10), not Tietra.

T+9:00 — Next battle_wait: GAME OVER. Battle ended. Doc says "Don't restart, write feedback." Stopping play and drafting feedback.md.

T+11:30 — feedback.md written. Approx 15 bridge calls total. Game ended fast because X-Potion silent failure + 5 enemies converging on Move=4 + Oil status. Returning to parent.



