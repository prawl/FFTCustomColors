<!-- This file is exempt from the 200 line limit. -->
# FFT Hands-Free — Claude Plays Final Fantasy Tactics

## Project Goal

Make FFT fully hands-free by giving Claude Code a platform to play the game as if it were a human player. Claude sends commands to the game through a file-based bridge, reads game state from memory, and makes intelligent decisions during gameplay.

**Core principles:**
- **Speed** — Claude's interactions with the game should be as fast as possible. Every round-trip matters. Batch operations, embed state in responses, minimize tool calls.
- **Intelligence** — Claude should make smart tactical decisions in battle, manage party builds, navigate the world map, and plan ahead like an experienced player.
- **Engagement** — This should be fun to watch. Claude experiences the story as a new player — reading dialogue, reacting to plot twists, commenting on characters, sharing facts and observations as it learns. It should feel like watching a friend play for the first time.
- **Autonomy** — Claude should be able to play extended sessions with minimal human intervention. Scan the battlefield, pick a strategy, execute moves, handle unexpected situations, and recover from mistakes.

The ultimate vision: you say "play FFT" and Claude boots the game, loads a save, navigates the world, enters battles, makes tactical decisions, enjoys the story, and keeps you entertained along the way.

## Design Principle: Automate the Interface, Not the Intelligence

Give Claude the same tools a human player has, just digitized. The bridge should make it easy to *see* and *act* — but never make decisions for Claude.

**What a human player can do (Claude should too):**
- See the whole battlefield at a glance → `scan_units` / `battle_status`
- Move a cursor to any tile → `move_grid x y`
- Read the menu options → `validPaths`
- Check a unit's stats by hovering → condensed struct reads
- Press buttons quickly and accurately → key commands with settling

**What a human player does NOT have (neither should Claude):**
- A computer telling them the optimal move
- Auto-pathfinding around obstacles
- Pre-calculated damage numbers
- Filtered "only valid" tile lists that remove bad options
- Auto-targeting the weakest enemy

**The rule:** If it removes a decision from Claude, it's too much automation. If it makes Claude fumble with the controller instead of thinking about strategy, it's not enough.

Examples of good automation:
- Self-calibrating rotation (a human doesn't think about rotation tables — they just see and press)
- `move_grid 4 9` (a human just moves the cursor to a tile — they don't count arrow presses)
- `scan_units` returning all positions (a human can see the whole board instantly)

Examples of bad automation:
- `move_to_best_tile` (the *choice* of tile is the game)
- Pre-filtering invalid tiles from the response (a human can try to walk into a wall too)
- `attack_weakest_enemy` (targeting is a tactical decision)

---

## Status Key
- [x] Done
- [ ] Not started
- [~] Partially done

---

## Priority Order

Organized by "what blocks Claude from playing a full session end-to-end" — most blocking first.

---

## 1. Battle Execution (P0, BLOCKING)

Basic turn cycle works: `scan_move` → `move_grid` → `battle_attack` → `battle_wait`. First battle WON autonomously.

Organized by priority tier. Each item is tagged with its original category:
- **[Identity]** — Know WHO is on the field
- **[State]** — Know WHAT each unit can do (HP, stats, status)
- **[Abilities]** — Know HOW to act beyond basic Attack
- **[AoE]** — Target abilities that hit areas
- **[EnemyIntel]** — Know what the enemy will do back
- **[Execution]** — Turn action speed and reliability
- **[Movement]** — Grid navigation and scanning infrastructure

### Tier 1 — Unblockers (do first)

These are correctness bugs or foundational features that block everything downstream. Fix these before adding new capabilities.

- [ ] **Read ability range and AoE shape** [AoE] — From the ability's secondary data: range, effect area, vertical tolerance. So Claude knows Fire has range 4 + 2-tile AoE diamond, vs Attack which is range 1 + single tile. **Foundational — blocks AoE targeting, charge time spells, spell Unit/Tile dialog, and enemy threat assessment.**
- [ ] **Multiple friendly unit support** [Movement] — Handle turns for units other than Ramza. Currently scan_move assumes first team=0 unit is active. Blocks every battle with more than one player unit.

### Tier 2 — Core tactical depth

Unlocks Chemist play, healing, mage targeting, and safe melee decisions.

- [ ] **Use Items** [Abilities] — Navigate to Item in the ability menu → select Potion/Phoenix Down/etc → target ally → confirm. Critical for healing and raising downed units.
- [ ] **Heal targeting allies** [Abilities] — battle_attack only targets enemies. Healing abilities and items need to target allies. The targeting cursor and confirmation work the same way, but the target selection logic needs to allow friendly tiles.
- [ ] **Raise downed units** [Abilities] — Phoenix Down or Raise spell on a KO'd unit. Requires knowing which tiles have dead units and being able to target them.
- [ ] **AoE targeting** [AoE] — For abilities with effect areas, Claude needs to position the AoE to maximize enemies hit and minimize allies hit. This is a placement decision Claude makes, not automation — but it needs the AoE shape info to decide. Depends on "Read ability range and AoE shape".
- [ ] **Enemy reaction abilities** [EnemyIntel] — Read equipped reaction ability per unit (Counter Tackle, First Strike, Blade Grasp, etc.). Claude needs this to assess risk: "if I melee this Knight, he has Counter Tackle and will hit me back."
- [~] **Ability list filtering** [Abilities] — Mettle abilities filtered correctly by learned IDs from scan. Items list shows full skillset (unfiltered) — need to read Chemist JP learned abilities from roster bitfield (PSX offset 0x99-0xD1) to filter. Item order may not match in-game order.
- [~] **`battle_ability <name> <x> <y>`** [Abilities] — Navigate Abilities menu → select a specific ability → target a tile → confirm. Self-target (Shout, Focus) and targeted (Pummel) verified working. Menu navigation uses learned ability list for correct index. Known issue: BattleMenuTracker gets out of sync after battle_ability because NavigationActions sends keys directly without updating tracker.
- [ ] **battle_ability spell targeting: Unit/Tile dialog** [Abilities] — Spells have an extra confirmation dialog: "Choose to target either the unit or the current tile" with Unit/Tile/Cancel options. Currently battle_ability doesn't handle this. Should press Enter (selects "Unit" default) for the extra confirmation. "Tile" is for edge cases like pre-casting on a tile a friendly will move to.

### Tier 3 — Robustness

Turn-state recovery, edge case handlers, multi-unit battle reliability.

- [ ] **Verify attack landed** [Execution] — Check enemy HP decreased after attack animation.
- [ ] **Scan cache doesn't invalidate between player turns** [Movement] — `battleUnitId` at `0x14077D2A4` reads the same value for multiple units, so the unit-change detection in TurnAutoScanner doesn't fire. Need a more reliable signal (e.g. compare `battleUnitHp` at `0x14077D2AC`, or track unit position changes). Critical blocker for multi-unit battles.
- [ ] **battle_move reports NOT CONFIRMED for valid moves** [Movement] — Navigation succeeds (cursor reaches target) but F key confirmation doesn't transition to Battle_MyTurn within 3s timeout. May need longer timeout for distant moves, or the F key confirmation flow changed. The move DID apply in-game.
- [ ] **Post-attack facing/move selection** [Movement] — After Attack without prior Move, game enters move+facing selection. Currently misdetected as Battle_Moving. Need to detect this state and handle it (confirm facing or escape).
- [ ] **Battle_Victory screen detection** [Movement] — Victory screen misdetected as TravelList/EncounterDialog. Need to capture memory values during victory screen to find a reliable signal. When detected, should auto-transition gracefully (press Enter to advance through rewards, then return to world map).
- [ ] **Detect charging/casting units** [Abilities] — Units charging a spell (e.g. Haste) show in the Combat Timeline with the spell name. Need to read charging state, which spell, and remaining CT from memory. Important for: not issuing commands to charging allies, knowing when spells will fire, and interrupting enemy casters.

### Tier 4 — Known hard problems, park until unblocked

Revisit only when you have a new approach. Don't spin on these.

- [ ] **Unit names — enemies** [Identity] — The game displays names for enemy units when hovered (e.g. a random encounter Bonesnatch might be "Sithon", a Grenade might be "Justitia"). Critical for story battles where the objective is "defeat Joe Schmo" and Claude needs to identify which specific unit is the target. `NameTableLookup` currently only covers the player roster table. Attempted to find enemy names via byte-pattern search — **failed**. Tested "Sithon", "Justitia", "Telephassa" in both UTF-8 and UTF-16 LE, with and without the enemy actively hovered in-game. Zero matches in PAGE_READWRITE memory. "Ramza" still finds 5 matches so the search itself works. Enemy names may be in PAGE_READONLY data sections, rendered via glyph lookup without ever forming a contiguous string, or loaded on-demand from a data file. See `memory/project_unit_name_table.md` for full investigation and 4 possible next approaches (extract from `battle_bin.en.bin`, find a name-index byte on the enemy unit struct, cautiously use SearchBytesAllRegions, or trigger name load via Status screen).
- [ ] **PA/MA/Brave/Faith for all units** [State] — Currently only the active unit's stats are read from UI buffer. Need stats for enemies too (to estimate damage) and allies (to choose healing targets).
- [ ] **Zodiac sign per unit** [Identity] — Read from roster or battle struct. Needed to assess damage multipliers (e.g. Scorpio vs Pisces = Good compatibility = +25% damage). See Wiki/ZodiacAndElements.md.
- [ ] **Charge time spells** [AoE] — Some abilities take multiple turns to resolve. Claude needs to know the CT cost so it can decide if a slow powerful spell or a fast weak one is better. Depends on "Read ability range and AoE shape".
- [ ] **Fix Move/Jump stat reading** [Movement] — UI buffer shows base stats, not effective (equipment bonuses missing). Cosmetic until you hit a unit that can't reach a tile it should.
- [ ] **Neutral unit handling (team=2)** [Movement] — Don't block pathing for NPCs/guests. Rare, only matters in guest battles.

### Tier 5 — Speed optimization

After correctness. 5s vs 30s per turn is huge, but only once the individual pieces are reliable.

- [ ] **`execute_turn` action** [Execution] — Claude sends full intent in one command: move target, ability, wait
  - `{"action": "execute_turn", "move_to": [4,9], "ability": "Attack", "target": [4,10], "wait": true}`
  - Mod handles internally: Move→navigate→confirm→Abilities→select→target→confirm→Wait
  - One round-trip instead of 6+ = **~5s instead of ~30s**
- [ ] **Support partial turns** [Execution] — move only, ability only, move+wait, etc.
- [ ] **Return full post-turn state** [Execution] — where everyone ended up, damage dealt, kills.

### Done (for reference)

- [x] **Unit names — story characters** [Identity] — Story characters identified via roster nameId lookup (match by level+origBrave+origFaith from static battle array at 0x140893E0C). UnitNameLookup maps nameIds to names (Ramza, Agrias, Orlandeau, etc.).
- [x] **Unit names — generic player recruits** [Identity] — Generic recruits (Warriors' Guild hires) now display their real names (Kenrick, Lloyd, Wilham, etc.) via `NameTableLookup`. Finds a heap table with 0x280-byte per-roster-slot records at a heap base discovered via the anchor signature `Ramza\0Delita\0Argath\0Zalbaag\0Dycedarg\0Larg\0Goltanna\0Ovelia\0Orland\0`. Each record has the chosen display name as the first null-terminated string at +0x10 inside the record. Walks at 0x280 stride. `RosterMatcher.RosterMatchResult` now carries `SlotIndex` so the name lookup keys off the matched roster slot.
- [x] **CT/Speed/Turn Order for all units** [State] — CT and Speed read from condensed struct (+0x0A and +0x06). Turn order derived from C+Up scan order (which traverses the game's Combat Timeline). turnOrder array in response includes name, team, level, hp/maxHp, position, ct.
- [x] **Status effects** [State] — Read active status flags from static battle array at 0x140893E45 + slot*0x200. 5-byte PSX bitfield decoded into named statuses (Poison, Haste, Protect, etc.). Matched to scanned units by HP+MaxHP. All 40 statuses supported.
- [x] **Dead/KO vs crystalized vs alive** [State] — lifeState field: "dead" (can be raised), "crystal"/"treasure" (permanently gone). HP=0 fallback when status bytes unavailable.
- [x] **Read available abilities** [Abilities] — Learned ability IDs read from condensed struct FFFF-terminated list at +0x28. Mapped to names, MP cost, range (horizontal/vertical/AoE/height), target, effect, cast speed, element, added effects via ActionAbilityLookup. Only shown for active unit (list doesn't update during C+Up cycling). Mettle abilities fully verified in-game with exact descriptions and range values.
- [x] **Battle_Abilities screen state** [Abilities] — Abilities submenu (Attack/Mettle/Items) tracked via BattleMenuTracker state machine. `0x140D3A10C` = submenu active flag. ui= shows current submenu item. Cursor persists within turn (Esc→re-Enter stays on same item), resets on new turn.
- [x] **Battle_Mettle/Battle_Items screen states** [Abilities] — When selecting a skillset from Abilities submenu, screen transitions to Battle_<Skillset> (e.g. Battle_Mettle, Battle_Items). ui= shows current ability name within the list (e.g. ui=Focus, ui=Shout).
- [x] **Filter scan abilities by equipped skillsets** [Abilities] — FilterBySkillsets with Fundaments/Mettle aliases. Only shows abilities from primary + secondary skillsets.
- [x] **BattleMenuTracker desync after battle_ability** [Abilities] — Fixed via SyncForScreen(), HasActedThisTurn flag, and NavigateToMove() (press Up 4x instead of trusting stale menuCursor).
- [x] **battle_ability should validate target range** [Abilities] — Checks ability range before confirming and detects when the game rejects the target.
- [x] **Show active unit name/job in screen state** [Abilities] — Screen output shows whose turn it is (e.g. "Ramza (Gallant Knight)" or "Lloyd (Archer)") so Claude doesn't have to scan to know.
- [x] **Active unit job fallback shows wrong job** [Abilities] — Fixed. Pre-scan job display now uses a reliable source instead of the stale UI buffer at 0x1407AC7EA.
- [x] **Block scan_move during animations** [Abilities] — scan_move returns `status=blocked` during Battle_Acting / Battle_AlliesTurn / Battle_EnemiesTurn. Allowed states: Battle_MyTurn, Battle_Moving, Battle_Attacking, Battle_Abilities, Battle_Waiting, Battle_Paused.
- [x] **Fix Ramza's job name** [Abilities] — Roster job=3 now maps to "Gallant Knight".
- [x] **Enemy equipped abilities** [EnemyIntel] — Monster enemies display their full fixed ability loadout via `MonsterAbilities.cs` + `MonsterAbilityLookup.cs` with range/AoE/target/element/effect metadata.
- [x] **`battle_attack` action** [Execution] — Opens Abilities → Attack → navigates target cursor to enemy → confirms.
- [x] **Read rotation DURING targeting mode** [Execution] — Uses empirical detection (press Right, read delta).
- [x] **`AttackTiles` in scan_move response** [Execution] — 4 cardinal tiles with ENEMY/ALLY/empty occupancy.
- [~] **Auto-detect battle map** [Movement] — Location ID lookup + random encounter maps implemented, fingerprint fallback.
- [x] **last_location.txt persistence fixed** [Movement]
- [~] **Wait facing direction** [Movement] — Basic implementation done, needs tactical improvement.
- [x] **Menu cursor address fixed** [Movement] — 0x1407FC620 confirmed reliable.
- [x] **Enemy job names all show "Chemist"** [Movement] — Fixed via 11-byte class fingerprint at heap struct +0x69. `ClassFingerprintLookup` maps fingerprints to class names for ~50+ classes. Story chars use roster nameId lookup.
- [x] **Auto-scan double-fire** [Movement] — Fixed. BattleTurnTracker now marks turns as scanned so auto-scan doesn't re-fire after explicit scan_move.
- [x] **battle_wait facing uses F key** [Movement] — Fixed.
- [x] **C+Up scan position/unit data desync** [Movement] — Auto-scan removal eliminated the failure window. `scan_move` now only runs C+Up once per turn on explicit request (subsequent calls return the cached response), and scans are blocked during Battle_Acting / Battle_AlliesTurn / Battle_EnemiesTurn. Roster-match team correction handles remaining edge cases. Not observed in play since. Relog if it recurs.

---

## 2. Story Progression — Know Where to Go (P0, BLOCKING)

Claude needs to know where the story wants it to go. Without this, it wanders aimlessly.

- [x] **Story objective location** — Read yellow diamond marker from 0x1411A0FB6, include in WorldMap response so Claude knows the next story destination
- [ ] **Orbonne Monastery story encounter** — Loc 18 has a different encounter screen than random battles. Need to detect and handle it.
- [ ] **Story scene handling** — Define how Claude reads dialogue, reacts to cutscenes, never skips

---

## 3. Travel System — Polish (P1)

Core travel works. These items make it robust.

- [ ] **Locked/unrevealed locations** — Read unlock bitmask at 0x1411A10B0 (43 bytes) and skip locked locations when calculating travel list indices. Critical for early game when not all locations are available.
- [ ] **Encounter polling reliability** — Encounters sometimes trigger before polling starts. Reduce delay before poll loop, increase poll frequency.
- [ ] **Ctrl fast-forward during travel** — Not working. May need SCANCODE flag or different timing.
- [ ] **Resume polling after flee** — Character continues traveling automatically after fleeing. Need to re-enter poll loop instead of returning to caller.
- [ ] **Location address unreliable** — 0x14077D208 stores last-passed-through node, not standing position. Find the real current position address or rely on hover after travel list open.

---

## 4. Instruction Guides (P1)

Claude needs plain-language guides for each game system so future sessions can pick up where we left off. These go in `FFTHandsFree/Instructions/` and explain how to play, not how the code works.

- [x] **WorldMapNav.md** — How to navigate the world map, travel list, encounters, all location IDs
- [ ] **BattleBasics.md** — How a battle turn works: Move → Act → Wait. Menu layout, cursor controls, facing confirmation. How to read the battlefield (unit positions, terrain, elevation).
- [ ] **PartyManagement.md** — How to access the party menu, view unit stats, change equipment, change jobs, learn abilities. Tab layout and navigation.
- [ ] **Shopping.md** — How to enter a settlement, navigate the outfitter, buy/sell items. Category tabs, quantity selection, fitting room.
- [ ] **FormationScreen.md** — How to place units before battle. Blue tiles, character selection, commence dialog.
- [ ] **SaveLoad.md** — How to save and load the game via the Options tab.
- [ ] **StoryScenes.md** — How story cutscenes work, dialogue advancement, when choices appear.
- [ ] **AbilitiesAndJobs.md** — How the job system works, JP, learning abilities, equipping reaction/support/movement abilities.

---

## 5. Player Instructions & Rules (P1)

Claude needs clear instructions on how to behave as a player before it starts playing. See `FFTHandsFree/PLAYER_RULES.md`.

- [x] PLAYER_RULES.md created — core rules: no googling, no spoilers, play as new player, think out loud
- [ ] Integrate PLAYER_RULES.md into Claude's system prompt / CLAUDE.md when playing
- [ ] Add intelligence level support (Beginner/Normal/Expert context files) — see IDEAS.md
- [ ] Define how Claude should narrate: brief during action, summary after turns, debrief after battles
- [ ] Test that Claude actually follows the rules during gameplay (doesn't leak training knowledge)

---

## 6. Intelligence Modes — Claude's Knowledge Levels (P1)

Claude plays at different skill levels depending on what game knowledge is available.
The implementation is the same code — just different reference data loaded.

### Mode 1 — Blind Playthrough ("First Timer")
- Only knows what's on screen: positions, HP, basic stats from memory
- No damage formulas, no ability data, no enemy weakness tables
- Discovers mechanics by experience and saves to a learning journal
- The entertaining "watching a friend play" experience
- **Implementation:** No `mechanics/` folder loaded. Learns by observing HP changes, deaths, etc.

### Mode 2 — Experienced Player ("Wiki Open")
- Full game mechanics loaded: damage formulas, ability ranges, zodiac chart, elements
- Pre-computes damage before attacking
- Plays like someone who's beaten the game before and has the wiki bookmarked
- **Implementation:** `FFTHandsFree/mechanics/` folder with damage.md, abilities.json, jobs.json, etc.

### Mode 3 — Min-Maxer ("Speedrunner") [Future]
- Optimizes party builds, ability combos, equipment loadouts
- Plans multiple turns ahead
- Plays like a challenge runner or speedrunner

---

## 7. Read Game Data from Memory (P1)

Currently Claude uses hardcoded lists. Reading from memory is better: always accurate, auto-updates.

- [ ] **Investigate NXD table access** — The game stores all text strings in NXD database tables. Finding how to read these at runtime would unlock everything below at once.
- [ ] **Unit names** — Read from CharaName NXD table keyed by NameId
- [ ] **Job names** — Read from memory when hovering in job grid
- [ ] **Ability names** — Read from memory when browsing abilities
- [ ] **Equipment names** — Read equipped item names from memory
- [ ] **Shop items** — Read item names, prices, and stats when browsing the shop
- [ ] **World map location names** — Read from memory instead of hardcoded dictionary

---

## 8. Speed Optimizations (P1)

### Done
- [x] GameState, screen, battle data embedded in every response
- [x] Sequence commands with assertions
- [x] Screen state machine (17 screens)
- [x] Fixed rotation table (zero calibration)

### Remaining
- [x] **Keep scan_units as diagnostic fallback** — scan_units bypasses the scan cache and always does a fresh C+Up cycle. Useful when scan_move returns stale cached data.
- [ ] **Auto-scan on Battle_MyTurn** — Include unit scan results in response automatically
- [ ] **Background position tracking** — Poll positions during enemy turns so they're fresh when it's our turn
- [ ] **Pre-compute actionable data** — Distances, valid adjacent tiles, attack range in responses
- [ ] **Latency measurement** — Log round-trip times, flag >2s actions

---

## 9. Battle — Advanced (P2)

### Error Recovery
- [ ] Detect failed move (still in Battle_Moving after F press) — retry or cancel
- [ ] Detect failed attack (still in targeting mode) — cancel and re-evaluate
- [ ] Handle unexpected screen transitions during turn execution
- [ ] **Counter attack KO** — If the active unit is KO'd by a reaction ability (Counter Tackle, etc.) after attacking, battle_wait fails because the game skips to the next unit's turn without going through the normal Wait flow. Need to detect "active unit died" and recover gracefully.
- [x] **Auto-Wait after Move+Act** — Fixed. BattleWaitLogic detects Battle_Attacking/Battle_Moving states (auto-facing after Move+Act) and skips menu navigation, going straight to facing confirmation. Tested in-game: Move→Attack→Wait now works seamlessly.
- [x] **Dead units block movement** — BFS now excludes all occupied tiles (allies, enemies, dead units) via BattleFieldHelper.GetOccupiedPositions(). Also added game tile list validation at 0x140C66315 as second safety net.
- [x] **Friendly units block movement** — Fixed with BattleFieldHelper.GetOccupiedPositions() in BFS.

### Unit Facing Direction
- [x] Choose facing intelligently at end of turn — FacingStrategy computes optimal direction via arc-based threat scoring (front=1, side=2, back=3 weights with distance/HP decay). battle_wait uses empirical rotation from grid navigation to press the correct key. 11/11 confirmed across all 4 directions.
- [ ] Read unit facing direction from memory — Searched 0x14077C970 (drifts), 0x140C64900 (unreliable), full heap diffs (no static address). Likely on UE4 heap behind pointer chains.
- [ ] Use facing data for backstab targeting — Once facing is readable, Claude can plan attacks from behind enemies (back attacks bypass most evasion, see Wiki/BattleMechanics.md)

### Advanced Targeting
- [ ] Line AoE abilities (e.g. some Geomancy, certain summons)
- [ ] Self-centered AoE abilities
- [ ] Multi-hit abilities (Truth/Nether Mantra random targeting)
- [ ] Terrain-aware Geomancy (surface type determines ability, see Wiki/MapFormat.md)

---

## 10. Settlements & Shopping (P2)

- [ ] **Settlement shop/service info** — Read available services and item categories from memory when hovering over a settlement on the world map
- [ ] Settlement menu detection: Outfitter, Tavern, Warriors' Guild, Poachers' Den
- [ ] `buy_item` / `sell_item` actions
- [ ] `hire_unit` / `dismiss_unit` actions
- [ ] `read_tavern` — Read rumors/errands
- [ ] `save_game` / `load_game` actions
- [ ] Chronicle tab reading

---

## 11. ValidPaths — Complete Screen Coverage (P2)

### Done
- [x] TitleScreen, WorldMap, TravelList, PartyMenu, CharacterStatus
- [x] EquipmentScreen, JobScreen, JobActionMenu, JobChangeConfirmation
- [x] EncounterDialog, Battle_MyTurn, Battle_Moving, Battle_Attacking, Battle_Acting, Battle_Paused

### Remaining
- [ ] Settlement menu, Outfitter, Tavern, Warriors' Guild, Poachers' Den
- [ ] Save/Load screens
- [ ] Chronicle tab, Achievements screen

---

## 12. Known Issues / Blockers

### Screen Detection Edge Cases
- Battle_Paused false positives: pauseFlag=1 stale after facing confirmation
- Battle at valid world map location detected as TravelList/WorldMap (clearlyOnWorldMap false positive)
- Settlement/shop screens not detected yet
- Menu cursor unreliable after animations
- [FIXED] Ability list browsing caused false Cutscene detection (slot0 changes from 0xFF inside submenus). Fixed by also checking slot9=0xFFFFFFFF + battleMode=2|3.
- [FIXED] 0x140D3A10C was labeled gameOverFlag — actually a submenu/mode active flag (1 when in Move mode, Abilities submenu, etc.)

### Coordinate System
- Grid coords and world coords are different systems
- Camera auto-rotates when entering Move mode or Attack targeting — always re-read rotation

### Turn Detection
- `acted` and `moved` flags at 0x14077CA8C/9C are unreliable
- Entering Move mode resets a previous move
- Active unit position from condensed struct is stale after moving

---

## 13. Shell Helpers (fft.sh)

- [x] `screen` — quick state check
- [x] `path <name>` — execute validPath
- [x] `enter/esc/up/down/left/right/space/tab/tkey/ekey` — key presses
- [x] `key_wait/key_leave/key_changed` — key + wait for condition
- [x] `rv/block/batch` — memory reads
- [x] `wv` — memory write
- [x] `scan_units` — C+Up unit scan with rich data
- [x] `scan_move` — scan units + compute valid movement tiles
- [x] `move_grid <x> <y>` — navigate cursor to grid position
- [x] `get_arrows [execute]` — compute/execute arrows to nearest enemy
- [x] `battle_wait` — end turn
- [x] `battle_flee` — quit battle to world map
- [x] `nav <screen>` — navigate to screen
- [x] `travel <id>` — travel to location (opens list, navigates, confirms, polls encounters)
- [x] `restart` — kill, build, deploy, relaunch, boot through title
- [x] Command chaining blocked — only one command per bash session

---

## 14. Key Memory Addresses

| Address | Size | Field | Notes |
|---------|------|-------|-------|
| 0x140C64A54 | byte | Grid cursor X | Absolute, doesn't change with rotation |
| 0x140C6496C | byte | Grid cursor Y | Absolute, doesn't change with rotation |
| 0x14077D2A0 | struct | Condensed battle struct | Cursor-selected unit data |
| 0x14077D2A2 | uint16 | Team (cursor unit) | 0=ally, 1+=enemy |
| 0x14077C970 | byte | Camera rotation | value % 4 = rotation 0-3 |
| 0x1407FC620 | byte | Action menu cursor | 0=Move,1=Abilities,2=Wait,3=Status,4=Auto |
| 0x140C66315 | 7*N | Movement tile list | X,Y,elev,flag per tile, flag=0 terminates |
| 0x140C64E7C | byte | Cursor tile index | Index into movement tile list |
| 0x14077CA30 | uint32*10 | Unit existence slots | 0xFF=exists, 0xFFFFFFFF=terminator |
| 0x14077CA8C | byte | Acted flag | Unreliable |
| 0x14077CA9C | byte | Moved flag | Unreliable |
| 0x14077D208 | byte | Location ID | Unreliable — stores last-passed node, not standing position |
| 0x140787A22 | byte | Hover location | World map cursor / travel list selection |
| 0x140D3A41E | byte | Party menu flag | 1=in party menu |
| 0x140D4A264 | byte | UI overlay flag | 1=UI present |
| 0x140900824/828 | byte | Encounter detection | Different values=Fight/Flee dialog |
| 0x140900650 | byte | Battle mode | 3=action menu, 2=move, 0=world map/cutscene |
| 0x1407AC7C0 | struct | UI display buffer | Level,NameId,HP,MP,PA,Move,Jump,Job,Brave,Faith |
| 0x140D39CD0 | uint32 | Gil | Party money |
| 0x1411A18D0 | 55*0x258 | Roster array | Persistent unit data |
| 0x1411A0FB6 | byte | Story objective | Yellow diamond location ID on world map |
| 0x1411A0FBC | uint32 | Travel list count | Number of reachable locations |
| 0x1411A0FC0 | uint32*N | Travel list entries | Reachable location IDs (adjacency list) |
| 0x1411A10B0 | byte*43 | Location unlock mask | 1=unlocked, 0=locked, one per location |
| 0x140D3A10C | byte | Game over flag | 1=game over, 0=normal |

---

## 15. DirectInput Key Simulation

FFT uses DirectInput for keyboard polling. Standard Win32 APIs work for single key presses but NOT for held-key detection.

**Working approach for held keys:**
1. `SetForegroundWindow(gameWindow)`
2. `SendInput` with `KEYEVENTF_SCANCODE` flag, `wScan = MapVirtualKey(vk, 0)`, `wVk = 0`
3. Also send via `keybd_event` and `PostMessage` as belt-and-suspenders
4. Re-assert the held key before each action key press
5. Release via all three methods when done

**Used for:** C+Up unit cycling scan, Ctrl fast-forward during battle/travel

---

## Sources & References

- [fft-map-json](https://github.com/rainbowbismuth/fft-map-json) — Pre-parsed map terrain data (122 MAP JSON files)
- [FFT Move-Find Item Guide by FFBeowulf](https://gamefaqs.gamespot.com/ps/197339-final-fantasy-tactics/faqs/32346) — ASCII height maps for MAP identification
- [FFHacktics Wiki](https://ffhacktics.com/wiki/) — PSX memory maps, terrain format, scenario tables
