# Playtest play log — 2026-04-26_0124

Append timestamped beats as you play. One or two lines each. Capture
friction in the moment — confusing output, slow commands, surprises,
wins. Don't worry about prose; a fragment is fine.

---

T+0 (1777181271) — start. screen on entry: Lloyd(Orator) (9,8) HP=315/432 is the LAST player up. Ramza/Kenrick/Wilham all DEAD. 3 live enemies: Archer(3,10), Archer(4,7), Time Mage(5,9). All 3 live enemies show "!blocked" on Attack despite Blaze Gun R:8. Confused — does !blocked mean LoS blocked by terrain? Manual doesn't explain "!blocked" tag. Going to try Geomancy on the closest target (Time Mage at 5,9, d=5).

T+~1m — battle_attack 5 9 on Time Mage worked despite "!blocked" tag. 42 dmg (289→247). BUT response screen=BattleVictory — that's a misleading override; immediate next screen call returned BattleMyTurn [ACTED]. Two false signals at once.

T+~1.5m — Tried battle_move 9 9 to set up auto-facing. Got "[BattlePaused] failed: Not in Move mode (current: BattlePaused)". The pause menu opened spontaneously between my attack and my move call — no input from me. Mystery friction. execute_action Resume returned [BattleAbilities] ui=Attack — landed me in the Attack submenu instead of the action root menu. Couldn't easily get back to a Move-able state.

T+~2m — battle_wait from BattleAbilities worked (took 4525ms, slow). The response footer said "Lloyd moved (9,8) → (8,8)" — I never asked to move. Cancel/back navigation through the menu stack must have triggered Move mode. Net result: Lloyd ended at (8,8), unintentionally repositioned, [ACTED] flag CLEARED. Good outcome (better attack angles) but unpredictable.

T+~2.5m — On Lloyd's next turn, scan showed 2 of 3 enemies attackable (no "!blocked"). So !blocked is genuinely LoS-based and changes with caster position. Confirms !blocked semantics — but it lied earlier (battle_attack 5 9 succeeded despite !blocked).

T+~3m — Tried battle_ability "Tanglevine" 5 8 (between Time Mage & Archer). Failed: "Cursor miss: at (6,6) expected (5,8)". Cursor nav landed 1 tile NW of intended. No retry, no recovery. Action consumed? Screen flipped to BattleEnemiesTurn — enemies started acting.

T+~4m — Enemy turn ran. Lloyd died. [GameOver]. Last man fell. Battle over. Per driver rules, no retry/load. Writing feedback.
