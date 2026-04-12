# Session Handoff — 2026-04-12 (Session 4)

Delete this file after reading.

## What Happened This Session

7 commits, 1660 tests passing (up from 1649). Major focus: enemy passive ability reverse engineering, documentation updates, screen state additions, and bug bash.

## Current State

**Branch:** `auto-play-with-claude`
**Tests:** 1660 passing
**Game:** Loaded at The Siedge Weald (loc 26) in a random encounter. Ramza/Kenrick/Lloyd/Wilham vs 6 enemies (Gobbledygooks, Skeleton, Black Goblin, Wisenkin, Treant). Gobbledygook #1 damaged (362/498). Lloyd used Jump and landed at (4,10). C+Up scan is in a bad state after restart — positions unreliable.

### What We Built

**Enemy Passive Ability Reading (major feature):**
- Reverse-engineered IC remaster heap struct bitfields for equipped passives:
  - **Reaction:** 4 bytes at heap struct +0x74, base ID 166, MSB-first
  - **Support:** 5 bytes at heap struct +0x78, base ID 198, MSB-first
  - **Movement:** 3 bytes at +0x7D, base TBD (not yet verified)
- Player units read from roster (+0x08/+0x0A/+0x0C byte IDs)
- `PassiveAbilityDecoder.cs` — decodes bitfields to ability names
- Shell renders as `equip: R:Parry | S:Equip Swords | M:Movement +3`
- Verified: Knight(Parry, Equip Swords), Archer(Gil Snapper, Evasive Stance), Time Mage(Evasive Stance)
- 11 unit tests for the decoder

**Documentation Updates:**
- `BattleTurns.md` — Added full battle_ability section covering all 6 targeting types (point, AoE, line, self, self-radius, full-field) with shell output examples
- `Commands.md` — Added battle_ability to command table, updated typical flow
- `BATTLE_MEMORY_MAP.md` — Added section 16 "Passive Ability Bitfields" with offsets, bases, and decoding formula

**Screen States:**
- `Battle_Dialogue` — New state for mid-battle character dialogue (inBattle + eventId > 0 + battleMode=0). Needs live calibration — detection logic didn't trigger correctly during testing.
- `Battle_Formation` — Added validPaths (place units, commence)
- `Battle_Abilities` — Added validPaths (submenu navigation)
- `Battle_<Skillset>` — Fallback validPaths for any ability list screen

**Bug Fixes:**
- JS crash when AoE ability has no validTargetTiles (Ultima out of range)
- Stale active unit name in `screen` — HP comparison suppresses wrong names after turn changes
- `battle_ability` timeout increased to 15s

### Known Bugs Found During Bug Bash

1. **C+Up scan positions vs condensed struct positions are DIFFERENT COORDINATE SYSTEMS** — NOT A BUG. C+Up reads grid cursor at 0x140C64A54/0x140C6496C (grid coords, correct for BFS). Condensed struct at 0x14077D360/0x14077D362 uses world coords (different system). The `battle_move` rejections during bug bash were legitimate — the party was clustered at (9-10, 8-10) blocking each other, leaving only 4 valid tiles from (10,9). Map terrain confirmed: h5 surrounded by allies on 3 sides + no_walk tile at (9,10).

2. **C+Up scan fails to detect units after restart** — Sometimes only finds 1 unit (the active) instead of all 10+. Investigation: game window handle refreshes every Execute() call (line 231). C key sent via SendInputKeyDown (global SendInput) + PostMessage + SendKeyDownToWindow — if game isn't foreground, SendInput goes to wrong window. Successful scans log "Full cycle after N presses, M unique units"; failed scans don't log this at all, suggesting C+Up key combos aren't reaching the game. Next step: check if game is foreground after restart, or switch to PostMessage-only approach for C key.

3. **Location address stale after restart** — `0x14077D208` reads 255 after restart even when on a valid battle map. `last_location.txt` sometimes missing. Causes wrong map auto-detection (MAP085 Mandalia instead of MAP074 Siedge Weald). Logged in TODO.

4. **Jump ends turn immediately** — After `battle_ability "Jump"`, Claude tries to `battle_wait` but the turn already ended. Need to detect Jump as turn-ending and skip Wait. Logged in TODO.

5. **False MISS detection** — Gun attack on Gobbledygook reported "MISSED (no HP change detected)" but actually hit (HP went 498→362). The condensed struct HP check reads stale data during animation. Logged in TODO.

6. **eventId=401 fires after battle actions** — After Shout and Jump, screen detects as `Cutscene` with eventId=401 instead of `Battle_Dialogue`. The detection logic excludes acted=1/moved=1 to avoid post-battle false positives, but mid-battle dialogue CAN happen after acting. Needs a different distinguisher.

7. **Duplicate/phantom unit in scan** — After Lloyd Jumped, an unnamed "(Archer)" appeared at (8,10) with identical HP to Kenrick. Likely the Jump landing position creating a ghost entry.

8. **Scan shows wrong abilities for unit** — Kenrick (Archer) scan showed Monk abilities (Chakra) from a previous scan's cached data.

9. **Battle_Dialogue detection not working in practice** — Agrias dialogue before Orbonne battle detected as `TravelList` (settlement false positive), not `Battle_Dialogue` or `Cutscene`. The `uiFlag=1` + valid location triggers TravelList detection. Known issue from TODO.

### TODOs Added This Session

- C+Up scan fails after restart
- Jump ends turn immediately
- `screen` shows wrong active unit (fixed with HP comparison)
- Flesh out validPaths (done)
- Enemy reaction abilities (done)
- Map auto-detection broken after restart (updated existing TODO)
- False MISS detection (updated existing TODO)

## Suggested Next 4 Tasks

1. **Fix C+Up scan position mismatch** — The #1 blocker. Without correct positions, `battle_move` can't work. Investigate why C+Up positions diverge from condensed struct after restart. May need to re-read positions from the condensed struct instead of C+Up, or recalibrate the coordinate system.

2. **Fix location persistence across restarts** — Write last_location.txt more reliably, or find a better address for current location. The map auto-detection chain breaks when location is unknown.

3. **Jump turn-ending detection** — After `battle_ability "Jump"`, skip `battle_wait`. Check if the ability's cast speed or a specific flag indicates "turn ends immediately." May also apply to other charge-time abilities.

4. **Battle_Dialogue detection calibration** — Capture actual memory values during a mid-battle dialogue scene (Agrias pre-battle, death speeches, etc.) and tune the detection logic. Current theory (eventId > 0 + battleMode=0 + inBattle) was close but the acted/moved exclusion is wrong.

## Files Changed This Session

| File | Changes |
|------|---------|
| `ColorMod/GameBridge/PassiveAbilityDecoder.cs` | NEW — Decodes reaction/support bitfields from heap struct |
| `ColorMod/GameBridge/NavigationActions.cs` | Read passives from heap (enemies) and roster (players) during scan |
| `ColorMod/GameBridge/BattleTracker.cs` | Updated passive ability field comments |
| `ColorMod/GameBridge/ScreenDetectionLogic.cs` | Added Battle_Dialogue detection |
| `ColorMod/GameBridge/NavigationPaths.cs` | Added paths for Battle_Dialogue, Battle_Formation, Battle_Abilities, Battle_<Skillset> |
| `ColorMod/Utilities/CommandWatcher.cs` | HP comparison to suppress stale active unit name |
| `Tests/GameBridge/PassiveAbilityDecoderTests.cs` | NEW — 11 tests for bitfield decoding |
| `Tests/GameBridge/ScreenDetectionTests.cs` | Added Battle_Dialogue tests |
| `Tests/GameBridge/CutsceneDetectionTests.cs` | Updated to expect Battle_Dialogue |
| `FFTHandsFree/BATTLE_MEMORY_MAP.md` | Added Passive Ability Bitfields section |
| `FFTHandsFree/Instructions/BattleTurns.md` | Added battle_ability docs with all targeting types |
| `FFTHandsFree/Instructions/Commands.md` | Added battle_ability to command table |
| `FFTHandsFree/TODO.md` | Multiple items marked done, 4 new bugs logged |
| `fft.sh` | Fixed AoE JS crash, increased battle_ability timeout |
