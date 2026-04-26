# Playtest play log — 2026-04-25_1955

Append timestamped beats as you play. One or two lines each. Capture
friction in the moment — confusing output, slow commands, surprises,
wins. Don't worry about prose; a fragment is fine.

---

T+0  Started. screen shows Kenrick(Thief) (9,9) active, 5 enemies clustered SW (d=13-15). Player party (Ramza/Kenrick/Lloyd/Wilham) all on east side. Kenrick has Gastrophetes crossbow + Time Magicks secondary. Attack out of range; Time spells show only ALLY targets (range covers our cluster but enemies too far). Plan: move forward to (5,9), cast Hasteja on self for AoE haste on the cluster — but I won't land it, I moved so it'll only catch nearby allies if any.

T+0  execute_turn 5 9 Hasteja → 19234ms (slow!). Got a TURN HANDOFF banner: Kenrick → Lloyd (10,9). Banner duplicated (printed twice in Info). Kenrick presumably moved & cast successfully then waited. No outcome detail in the response — I cannot tell from the banner what tiles got hasted, or if Lloyd ended up affected. Just "your turn now, different unit." I'd want a "Kenrick: cast Hasteja, hit Wilham" recap.

T+1  Lloyd's turn. Attack shows ALLY tiles "!blocked" only — gun has range but LoS or distance blocks enemies. execute_turn 6 9 (move only) → 14074ms. TURN HANDOFF Lloyd→Ramza. Move-only DID end the turn here, despite docs saying "execute_turn with two args returns to BattleMyTurn with Act/Wait still available." Doc/behavior mismatch.

T+2  Ramza's turn. Tried execute_turn 7 9 Shout. Got [BattleVictory] flicker → bridge bailed on wait sub-step with FAILED. Took 8s. Recovery dance: screen showed BattleMoving still, scan_move on BattleMoving returns nothing useful, battle_move couldn't reuse the cursor tile, eventually execute_action ConfirmMove → slid into BattleAttacking, Cancel back to BattleMyTurn. Ramza is now at (7,9) — move did commit. But [MOVED] tag absent from header. Then battle_ability "Shout" without coords FAILED — auto-fill picked (10,10) instead of self. battle_ability "Shout" 7 9 worked but the success line printed "→ (10,10) HP=528/528" (Wilham's tile) — wrong target reported in summary even though Shout fired correctly. Then battle_wait failed: stuck on BattleAbilities submenu. Two Cancels later, screen jumped to Wilham's turn (Ramza presumably auto-waited). Lots of friction in this single turn.

T+3  Wilham's turn (Samurai/Iaido). All Iaido abilities show ONLY (10,10)<Wilham SELF> as target — no enemies in radius. Iaido is self-radius so this matches; enemies are still d=12-16 away.

T+5  TURN HANDOFF: Wilham→Kenrick HP=161/467. Kenrick took 306 damage during enemy turns (Time Mage cast probably hit). Tried `execute_turn 4 6 Attack 3 6` for Wilham; got "Tile (4,6) is not in the valid move range" — I misread the move tile list. Move tile lists are wide and unstructured; would benefit from sorted/grouped output (e.g. by row, or "tiles within R of enemy X"). Wilham fell back to (5,8) advance only.

T+7  CRITICAL BUG: every successful ranged attack triggers a [BattleVictory] false-positive flicker → execute_turn bails on the wait sub-step → must manually battle_wait. Hit it 3x in a row (Lloyd Attack on Summoner, Ramza X-Potion on Kenrick, etc). The wait recovery itself succeeds but doubles round-trip time and forces the agent to manually call screen→battle_wait. Suspect the enemy KO/HP-near-zero detector is triggering on Summoner-Charging or similar.

T+8  Lloyd dealt 44 dmg to Summoner (274→230). Ramza healed Kenrick 161→311 (X-Potion +150). Enemy report after wait listed Time Mage moves and gained Charging — that's GREAT info but only surfaced post-wait, not in the abilities list ("Time Mage [Charging]" status appeared in scan instead).

T+10  Came back to find Ramza alone, 3 allies KO'd. screen shows Phoenix Down x98 with [REVIVE] tag on dead allies — clear and helpful. Tried `execute_turn 7 9 "Phoenix Down" 6 9` — FAILED: "Tile (7,9) is not in the valid move range." So execute_turn REQUIRES a move tile distinct from current pos; you cannot stand still and act. That's a footgun — when you're already optimally positioned, you must move 1 tile away pointlessly.

T+10  Retried `execute_turn 7 8 "Phoenix Down" 6 9`. Ran 37s and bridge bailed: response.json was DELETED ("No such file"). Recovered via screen — turn DID complete: Ramza moved to (7,8), HP dropped 719→287 (got walloped by enemies), Knight got petrified (Chaos Blade onHit), and the next turn cycle began. So the action succeeded but the response was clobbered. Bad: I have no idea if Phoenix Down hit Lloyd, because Lloyd is still DEAD HP=0. Probably I never ran it — execute_turn moved + waited but skipped the ability sub-step on the silent failure.
