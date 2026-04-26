# Playtest play log — 2026-04-26_0921

Append timestamped beats as you play. One or two lines each. Capture
friction in the moment — confusing output, slow commands, surprises,
wins. Don't worry about prose; a fragment is fine.

---

T+0 09:23 — Read CLAUDE.md, Rules, Commands, BattleTurns, AbilitiesAndJobs. About to source fft.sh and screen.

T+1 09:24 — `screen` (492ms scan_move). Lloyd 105/432 active at (9,8). Three KO'd allies: Ramza 0/719 (DEAD, not yet crystal), Kenrick 0/467 (Treasure! already crystallized), Wilham 0/528 (Dead). Live enemies: Time Mage 225/355 at (9,9) d=1, Archer 389/447 (6,10), Archer 484/484 (5,11). Knight (6,9) and Summoner (6,6) already neutralized. So Lloyd is genuinely the last man standing, and Ramza is on a deathCounter. Pressure-cooker.

T+1 09:24 — Plan: gun says `[TOO CLOSE]` on adjacent Time Mage (Blaze Gun has min range >1). Attack on (6,10) Archer is `!blocked` LoS from current tile. Move 1N to (9,7) so Time Mage at (9,9) is d=2 (clear of min range), still hits rear since Time Mage faces S. Then end turn — pray no enemy crit ends Lloyd at 105 HP.

T+2 09:25 — `execute_turn 9 7 Attack 9 9` returned `[GameOver]` after 20.4s. Battle is lost. Not retrying per driver scope. The bundled turn went through Move → Attack → Wait, and during Wait the enemy turns ran and either: (a) an Archer killed Lloyd at 105 HP, or (b) Ramza's deathCounter ticked to crystallize and triggered Desertion → mistagged GameOver. The single-line GameOver tag swallows ALL the per-step info — I have no idea which sub-step succeeded, no damage tally, no Info field, nothing. Friction logged.

T+4 09:27 — Post-mortem: jsonl session log shows `execute_turn` row with `error: "Game Over"`, `latencyMs: 20239`, sourceScreen BattleMyTurn → targetScreen GameOver. The wrapper printed only `[GameOver]` — the error message "Game Over" was swallowed. live_events.log has 3 NARRATE lines: Archer (5,11)→(8,11), Lloyd (9,7)→(9,8), Archer (8,11)→(9,7). Reads like Lloyd moved to 9,7, then enemies ran two Archers up — last one ended on (9,7) which seems to imply Lloyd died and the Archer occupied his tile (or Lloyd snapped back to 9,8 on KO). Time Mage at (9,9) and an Archer crit Lloyd at 105 HP — totally plausible.

T+5 09:28 — Tried `screen -v`, `scan_units`, `scan_move`, `screen` on GameOver. All return the same one-line `[GameOver] curLoc=The Siedge Weald obj=Orbonne Monastery`. No way to introspect WHY I lost — was Lloyd KO'd, did Ramza crystallize, did the Time Mage counter on my Attack? scan_units printed `[USE screen] scan_units is deprecated. Use: screen` even though it's documented in BattleTurns.md and Commands.md as the canonical scan. Two friction items in one call.

T+6 09:28 — Game still running per `running`. Per scope rules, no retry/load. Beginning feedback writeup with ~22 min left.
