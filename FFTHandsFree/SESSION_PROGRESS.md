# Session Progress — 2026-04-03

## What We Built

### ValidPaths Navigation System
- Every bridge response includes `validPaths` — a map of action names to exact commands
- `path` action: Claude sends one key (e.g. `path Flee`, `path PartyMenu`) and C# executes it
- No docs needed — Claude reads the state and picks from available actions

### Screen Detection (17 screens)
| Screen | Detection Method |
|--------|-----------------|
| TitleScreen | location=255, not in battle |
| WorldMap | party=0, ui=0, valid location |
| PartyMenu | party=1, state machine not in sub-screen |
| CharacterStatus | party sub-screen via ScreenStateMachine |
| EquipmentScreen | party sub-screen via ScreenStateMachine |
| EquipmentItemList | party sub-screen via ScreenStateMachine |
| JobScreen | party sub-screen via ScreenStateMachine |
| JobActionMenu | party sub-screen via ScreenStateMachine |
| JobChangeConfirmation | party sub-screen via ScreenStateMachine |
| TravelList | party=0, ui=1 |
| EncounterDialog | encA != encB |
| Battle_MyTurn | inBattle, moveMode=0, acted=0, moved=0 |
| Battle_Moving | inBattle, moveMode=255, acted=0 |
| Battle_Targeting | inBattle, moveMode=255, acted=1 |
| Battle_Acting | inBattle, acted=1 or moved=1 |
| Battle_Paused | inBattle, pauseFlag=1 |
| GameOver | inBattle, pauseFlag=1, battleMode=0 |
| Battle | inBattle, enemy turn |

### Settled State Detection
- Every response polls until screen reads the same 3 consecutive times (~150ms)
- `waitUntilScreenNot` settles for 10 consecutive reads at 100ms (1s stable)
- Eliminates stale/transient state in responses

### High-Level Actions
| Action | What It Does |
|--------|-------------|
| `battle_wait` | Navigates to Wait, confirms, handles facing, polls for terminal state |
| `confirm_attack` | Double-F (select + confirm), polls through attack animation for terminal state |
| `travel` | Opens travel list, cycles tabs, selects location by hover ID |
| `navigate` | Multi-step screen navigation (e.g. from anywhere to PartyMenu) |
| `move_to` | Enters Move mode, finds closest tile to enemy, navigates cursor, confirms |

### Shell Helpers (fft.sh)
- `path <name>` — execute validPath, shows next available paths
- `travel <id>` — move world map cursor to location
- `restart` — kill game, build, deploy, relaunch, wait for bridge
- `boot` — press Enter until leaving TitleScreen
- `screen` — quick state check

### Key Memory Addresses Found
| Address | Purpose |
|---------|---------|
| 0x1407FCCA8 | Battle action menu cursor (0=Move, 1=Abilities, 2=Wait, 3=Status, 4=AutoBattle) |
| 0x14077CA5C | moveMode flag (255=in tile selection, 0=not) |
| 0x140C64E7C | Cursor index into movement tile list |
| 0x140C66315 | Movement tile list (7 bytes per tile: X, Y, elev, flag, 0, 0, 0) |
| 0x140900650 | battleMode (0=GameOver, 2=Move, 3=ActionMenu, 4=Targeting — unreliable) |

## What Works End-to-End
- **World map navigation**: travel to any location, enter, flee encounters
- **Party menu**: navigate all sub-screens (character status, equipment, jobs)
- **Battle action menu**: Move, Abilities, Wait, Pause — all via `path` command
- **Battle Move**: enter Move mode, navigate cursor, confirm with F
- **Battle Attack**: Abilities → Attack → target cursor → double-F confirm with animation wait
- **Battle Wait**: full turn-end sequence with facing confirmation
- **Pause menu**: all options including Return to World Map
- **Game Over**: detected and all options available

## Known Issues / Blockers

### 1. MoveToEnemy Moves Wrong Direction (CRITICAL)
The `move_to` action picks the tile with the smallest Manhattan distance to the enemy, but it consistently moves AWAY from the enemy. The tile list coordinates and enemy positions appear to be in the same coordinate system (verified: cursor start position = Ramza's tile), but the distance calculation picks the wrong tile.

**Possible cause**: The enemy position from the turn queue (5,1) may be stale or in a different scale. When Ramza is at tile (9,12) and the tile list goes (6,9) to (14,15), tile (6,9) has the smallest distance to (5,1) — but on screen, moving to (6,9) goes AWAY from the enemy. This suggests the coordinate systems DO differ despite appearing similar.

**Next step**: Need to empirically verify by having the user confirm which tile is closest to the enemy visually, then check what coordinates that tile has.

### 2. Turn Queue Positions Are Stale
- Active unit position at 0x14077D360/62 doesn't update after moving
- The `moved` and `acted` flags at 0x14077CA8C/9C are unreliable
- Positions only update when a unit rotates to turn queue slot 0
- Heap scanning (which found live positions) was disabled due to game crashes

### 3. Battle_Paused False Positives
- The `battle_wait` action leaves pauseFlag=1 stale after facing confirmation
- Fixed by adding Escape in the Wait polling loop, but root cause not addressed
- The pause flag at 0x140C64A5C doesn't clear reliably after animations

### 4. Menu Cursor Unreliability After Animations
- 0x1407FCCA8 is correct for reading cursor during action menu
- But after MoveToEnemy or attacks, the cursor may be on a different position than expected
- Move option becomes "Reset Move" after already moving — pressing Enter on it resets instead of entering Move mode

### 5. No "Has Moved" / "Has Acted" Detection
- Can't reliably detect if the unit has already moved or acted this turn
- The memory flags 0x14077CA8C (acted) and 0x14077CA9C (moved) don't reflect reality
- Need to find the real flags to show correct validPaths (e.g. hide Move when already moved)

## Architecture

```
Claude → command.json → CommandWatcher → {
  "path" action → ExecuteValidPath → looks up NavigationPaths → executes keys
  "move_to" action → NavigationActions.MoveTo → enters Move, navigates, confirms
  "battle_wait" → NavigationActions.BattleWait → menu nav + confirm + poll
  "confirm_attack" → NavigationActions.ConfirmAttack → double-F + poll
  "travel" → NavigationActions.Travel → tab cycling + select-and-check
}
→ DetectScreenSettled → attach ValidPaths → response.json → Claude
```

Every response includes: screen name, screen-specific data (cursor, tiles, battle units), and validPaths for the next action.
