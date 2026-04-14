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
- See the whole battlefield at a glance → `screen` (unified battle state)
- Move a cursor to any tile → `move_grid x y`
- Read the menu options → `validPaths`
- Check a unit's stats by hovering → static battle array reads
- Press buttons quickly and accurately → key commands with settling

**What a human player does NOT have (neither should Claude):**
- A computer telling them the optimal move
- Auto-pathfinding around obstacles
- Pre-calculated damage numbers
- Filtered "only valid" tile lists that remove bad options
- Auto-targeting the weakest enemy

**The rule:** If it removes a decision from Claude, it's too much automation. If it makes Claude fumble with the controller instead of thinking about strategy, it's not enough.

---

## Status Key
- [ ] Not started
- [~] Partially done

---

## Priority Order

Organized by "what blocks Claude from playing a full session end-to-end" — most blocking first.

---

## 1. Battle Execution (P0, BLOCKING)

Basic turn cycle works: `screen` → `battle_attack` → `battle_wait`. First battle WON autonomously.

### NEXT 5 — Do these first (identified 2026-04-12 battle testing)

### Tier 0 — Critical (BFS broken, need game-truth tiles)

- [~] **Read valid movement tiles from game memory instead of BFS** [Movement] — Extensive search 2026-04-13 (3 agents + manual). Tile list at 0x140C66315 is perimeter outline (world coords), not valid tile set. Rendering struct at 0x140C6F400 is volatile per-frame. Heap search found movement calc struct with tileCount but couldn't decode tile indices. **Partial fix applied:** ally traversal penalty (+1 cost, can't stop) confirmed via TDD — perfect match for Kenrick's 10 tiles. Remaining issue: Wilham's tiles still have 9 extras (steep cliff transitions not handled). Next: investigate slope-direction-dependent height checks.



- [ ] **Inventory quantity for Items, Throw, and Iaido** [Abilities] — Three skillsets depend on a per-character "Held" count:
  - **Items** (Chemist): each potion/ether/remedy/phoenix down has a held count. In-game shows `Potion=3, High Potion=0, X-Potion=93`.
  - **Throw** (Ninja): one entry per weapon type with the held count (`Dagger=1, Mythril Knife=2`). Each throw consumes one.
  - **Iaido** (Samurai): draws power from held katana. Each use has ~1/8 chance to break the drawn katana, so the held count of each katana type directly gates which Iaido abilities are usable.

  Our scan currently lists every ability in the skillset as if unlimited. Need to find the per-character inventory array and surface each item's held count alongside the ability entry. Emit as a `heldCount` field per ability, and optionally mark `unusable: true` when `heldCount == 0`.
- [ ] **Cone abilities — Abyssal Blade** [AoE] — Deferred. 3-row falling-damage cone with stepped power bands. Low value (only 1 ability uses this shape) and requires map-relative orientation. Skip for now.

### Tier 2 — Core tactical depth

- [ ] **Projected damage preview** [State] — When you hover a target in-game, the game shows a projected damage number. Two approaches:
  - **Option A (fast):** Read the game's own damage preview value from memory while in Battle_Attacking/Battle_Casting.
  - **Option B (full):** Compute damage ourselves from the FFT formula: `PA × WP × multipliers` for physical, `MA × PWR × (Faith/100) × (TargetFaith/100)` for magick.

### Tier 2.5 — Navigation completeness

- [ ] **Chocobo riding** — Units can ride chocobos in battle, which changes their movement range and possibly their action menu. Need to detect when a unit is mounted, adjust Move stat, and handle any chocobo-specific abilities or movement restrictions.

### Tier 3 — Robustness

Turn-state recovery, edge case handlers, multi-unit battle reliability.

- [ ] **Add PreToolUse hook to block `| node` in bash commands** [Enforcement] — Claude should never pipe command output through node for parsing. All shell helpers (screen, execute_action, battle_attack, etc.) handle formatting internally. A Claude Code PreToolUse hook on Bash can detect `| node` in the command string and block it with a reminder to use the formatted helpers. Pending testing the unified screen command first.

- [~] **execute_action responses missing ui= field** [State] — UI is set for all battle screens in DetectScreen. May be resolved by current code. Needs verification. Observed 2026-04-12.
- [~] **battle_ability selects wrong skillset for secondary abilities** [Execution] — Secondary now detected correctly (Martial Arts secondaryIdx=9 for Lloyd). First scan sometimes misses (null/null) but auto-scan catches it. Fallback to all-skillsets also works. Mostly resolved.
- [ ] **Show hit% per target in ability tiles** [State] — When hovering a target in-game, the game shows projected hit%. Read this from memory and include it per target tile so Claude can see `(10,6)<Skeleton 73%>` instead of just `(10,6)<Skeleton>`. Would help decide between a high-damage low-accuracy Aim+20 vs reliable Attack. Could also help detect LoS blocking (0% = blocked). Identified 2026-04-12.
- [ ] **Line-of-sight blocking for ranged attacks** [Abilities] — Archer attacked Treant at (7,11) from (10,9) but a tree blocked the projectile. FFT has LoS checks for ranged abilities (bows, thrown stones, guns). We need to detect blocked paths. Options: (A) read the game's projected hit% from memory during targeting mode, (B) compute LoS from map height data, (C) enter targeting, check if game rejects tile, cancel if blocked. Option A is most practical if the address can be found. Observed 2026-04-12.
- [ ] **Equipment IDs stale across battles** [State] — Roster equipment at `+0x0E` reads the save-state equipment, not the current in-battle loadout. Need to find the live equipment address.
- [ ] **Active unit name/job stale across battles** [State] — After restarting a battle with different equipment/jobs, the name/job display doesn't refresh between battles.
- [ ] **battle_move reports NOT CONFIRMED for valid moves** [Movement] — Navigation succeeds but F key confirmation doesn't transition. Timeout increased from 5s to 8s for long-distance moves.
- [ ] **Detect disabled/grayed action menu items** [Movement] — Need to find a memory flag or detect from cursor behavior.
- [~] **battle_retry doesn't work from GameOver screen** [Execution] — Code exists, GameOver detection fixed. Needs live testing.
- [ ] **Re-enable Ctrl fast-forward during enemy turns** [Execution] — Tested both continuous hold and pulse approaches. Neither visibly sped up animations. Low priority.
- [ ] **Populate new BattleUnitState fields from memory** [State] — deathCounter, elementAbsorb/Null/Half/Weak, chargingAbility/chargeCt, facing. All need IC remaster addresses discovered.
- [ ] **Read death counter for KO'd units** [State] — KO'd units have 3 turns before crystallizing. Need to find the IC equivalent of PSX offset ~0x58-0x59.
- [ ] **Detect charging/casting units** [Abilities] — Units charging a spell show in the Combat Timeline. Need to read charging state, which spell, and remaining CT from memory.

### Tier 4 — Known hard problems

- [ ] **Unit names — enemies** [Identity] — Enemy display names not found in memory. May need NXD table access or glyph-based lookup.
- [ ] **Zodiac sign per unit** [Identity] — Needed for damage multipliers.
- [ ] **Fix Move/Jump stat reading** [Movement] — UI buffer shows base stats, not effective (equipment bonuses missing).

### Tier 5 — Speed optimization

- [ ] **`execute_turn` action** [Execution] — Claude sends full intent in one command: move target, ability, wait. One round-trip instead of 6+.
- [ ] **Support partial turns** [Execution] — move only, ability only, move+wait, etc.
- [ ] **Return full post-turn state** [Execution] — where everyone ended up, damage dealt, kills.

---

## 2. Story Progression (P0, BLOCKING)

- [ ] **Orbonne Monastery story encounter** — Loc 18 has a different encounter screen. Need to detect and handle it.
- [ ] **Story scene handling** — Define how Claude reads dialogue, reacts to cutscenes, never skips

---

## 3. Travel System — Polish (P1)

- [ ] **Locked/unrevealed locations** — Read unlock bitmask at 0x1411A10B0 and skip locked locations.
- [ ] **Encounter polling reliability** — Encounters sometimes trigger before polling starts.
- [ ] **Ctrl fast-forward during travel** — Not working.
- [ ] **Resume polling after flee** — Character continues traveling after fleeing. Need to re-enter poll loop.
- [ ] **Location address unreliable** — 0x14077D208 stores last-passed-through node, not standing position.

---

## 4. Instruction Guides (P1)

- [x] **PartyManagement.md** — Written 2026-04-13.
- [ ] **Shopping.md** — How to enter a settlement, navigate the outfitter, buy/sell items.
- [x] **FormationScreen.md** — Written 2026-04-13.
- [x] **SaveLoad.md** — Written 2026-04-13.
- [ ] **StoryScenes.md** — How story cutscenes work, dialogue advancement.
- [ ] **AbilitiesAndJobs.md** — How the job system works, JP, learning abilities.

---

## 5. Player Instructions & Rules (P1)

- [ ] Integrate PLAYER_RULES.md into Claude's system prompt / CLAUDE.md when playing
- [ ] Add intelligence level support (Beginner/Normal/Expert context files)
- [ ] Define how Claude should narrate: brief during action, summary after turns, debrief after battles
- [ ] Test that Claude actually follows the rules during gameplay

---

## 6. Intelligence Modes (P1)

### Mode 1 — Blind Playthrough ("First Timer")
- Only knows what's on screen. Discovers mechanics by experience.

### Mode 2 — Experienced Player ("Wiki Open")
- Full game mechanics loaded: damage formulas, ability ranges, zodiac chart, elements.

### Mode 3 — Min-Maxer ("Speedrunner") [Future]
- Optimizes party builds, ability combos, equipment loadouts.

---

## 7. Read Game Data from Memory (P1)

- [ ] **Investigate NXD table access** — The game stores all text strings in NXD database tables.
- [ ] **Unit names** — Read from CharaName NXD table keyed by NameId
- [ ] **Job/Ability/Equipment/Shop/Location names** — Read from memory instead of hardcoded lists

---

## 8. Speed Optimizations (P1)

- [ ] **Auto-scan on Battle_MyTurn** — Include unit scan results in response automatically
- [ ] **Background position tracking** — Poll positions during enemy turns so they're fresh when it's our turn
- [ ] **Pre-compute actionable data** — Distances, valid adjacent tiles, attack range in responses
- [ ] **Latency measurement** — Log round-trip times, flag >2s actions

---

## 9. Battle — Advanced (P2)

### Error Recovery
- [ ] Detect failed move/attack — retry or cancel
- [ ] Handle unexpected screen transitions during turn execution
- [ ] **Counter attack KO** — Active unit KO'd by reaction ability after attacking. Need to detect and recover.

### Unit Facing Direction
- [ ] Read unit facing direction from memory
- [ ] Use facing data for backstab targeting

### Advanced Targeting
- [ ] Line AoE abilities
- [ ] Self-centered AoE abilities
- [ ] Multi-hit abilities (random targeting)
- [ ] Terrain-aware Geomancy (surface type determines ability)

---

## 10. Settlements & Shopping (P2)

- [ ] Settlement menu detection: Outfitter, Tavern, Warriors' Guild, Poachers' Den
- [ ] `buy_item` / `sell_item` / `hire_unit` / `dismiss_unit` actions
- [ ] `save_game` / `load_game` actions

---

## 11. ValidPaths — Complete Screen Coverage (P2)

- [ ] Settlement menu, Outfitter, Tavern, Warriors' Guild, Poachers' Den
- [ ] Save/Load screens
- [ ] Chronicle tab, Achievements screen

---

## 12. Known Issues / Blockers

### Missing Screen States
- [ ] **Battle_Cutscene** — Mid-battle cutscenes. Need to distinguish from regular cutscenes.
- [ ] **SaveScreen / LoadScreen** — Indistinguishable from TitleScreen with static addresses.
- [ ] **Settlement** — Indistinguishable from TravelList with static addresses. Could use location-based heuristic.

### Screen Detection Edge Cases
- Battle_Paused false positives: pauseFlag=1 stale after facing confirmation
- Settlement/shop screens not detected yet
- Menu cursor unreliable after animations

### Coordinate System
- Grid coords and world coords are different systems
- Camera auto-rotates when entering Move mode or Attack targeting — always re-read rotation

### Bugs Found 2026-04-12
- [ ] **Ability list navigation: use counter-delta instead of brute-force scroll** — Currently presses Up×N to reset then Down×index. Could use counter-delta approach.
- [ ] **Detect rain/weather on battle maps** — Rain boosts Lightning spells by 25%.
- [ ] **Post-battle memory values stuck at 255 after auto-battle** — All memory addresses stayed at 255/0xFFFFFFFF permanently. May require game restart.
- [~] **Auto-detect battle map** — Location ID lookup + random encounter maps implemented, fingerprint fallback. BUG: after restart, location address reads 255 causing wrong map auto-detection.

### Bugs Found 2026-04-12 Session 2
- [ ] **DetectScreen reports Battle_Casting when actually in Battle_Moving** [State] — battleMode flickers to 1 during move mode, causing DetectScreen to return Battle_Casting instead of Battle_Moving. This breaks `execute_action Cancel` and other commands that check screen state. The battle_move confirmation poll was fixed to ignore Battle_Casting (2026-04-13), but the general DetectScreen path still has this issue — any command that calls DetectScreen while in move mode can get the wrong state. Root cause: battleMode=1 (Casting) takes priority over battleMode=2 (Moving) in detection order, and the flicker isn't filtered. Observed 2026-04-12, 2026-04-13.
- [ ] **Static array at 0x140893C00 is stale mid-turn** [State] — HP AND positions don't update during/after moves or attacks within a turn. Only refreshes at turn boundaries. Killed a Skeleton (HP 535→0 on screen) but array still read 535. Moved Ramza but array still showed old position. Need to find the live data source the game UI reads from.
- [ ] **Damage/hit% preview during targeting** [State] — The game displays projected damage and hit% when hovering a target. Extensive investigation 2026-04-12:
  - **Found via probe_status:** In attacker's heap struct, hit% at statBase-62 (u16), damage at statBase-96 (u16). Verified across 3 targets (Kenrick 570/48%, Lloyd 342/50%, Wilham 364/95%). Offsets consistent for hit%, damage shifted by 4 bytes for one target.
  - **Two heap copies exist:** One in 0x416xxx range (found by `SearchBytesInAllMemory`, PAGE_READWRITE PRIVATE) — has HP/stats but NOT preview data. Another in 0x130xxx-0x15Axxx range (found by `SearchBytesAllRegions`) — this copy HAS preview data at the offsets above.
  - **Problem:** `SearchBytesInAllMemory` only scans PAGE_READWRITE PRIVATE memory, missing the copy with preview data. `SearchBytesAllRegions` finds it but is slow (scans from addr 0) and returns too many false matches.
  - **Approach needed:** Use `SearchBytesInAllMemory` with `broadSearch: true` flag (already added — scans all readable memory with address range filter). Search for HP+MaxHP of the attacker, verify level byte, read at statBase-62 and statBase-96. Must exclude the 0x416xxx copy (no preview data) — filter by checking hit% > 0.
  - **Also found at low static address** (0x60823C one session, different next) via `search_all` with unique 10-byte pattern. Address shifts between restarts. Reading from this address crashed the game — likely in a protected code segment.
  - **Code exists but disabled:** `ReadDamagePreview()` in NavigationActions.cs has the search + offset logic. Currently returns (0,0) because the broad search finds the wrong copy. Fix: add address range filter to skip 0x416xxx and target the 0x130-0x15A range.
- [ ] **BFS move tiles too permissive — terrain height not properly limiting range** [Movement] — BFS at Move=4 from (10,9) includes (8,7) (distance 4) but in-game the tile isn't reachable due to terrain. The BFS validation passes but the game rejects the move. Need to verify terrain height costs in BFS match FFT's rules. Observed 2026-04-12.
- [ ] **Screen detection shows Cutscene during ability targeting** [State] — While in targeting mode for Aurablast (selecting a target tile), screen detection reports "Cutscene" instead of "Battle_Attacking" or "Battle_Casting". This causes key commands to fail because they check screen state. Observed 2026-04-13.
- [ ] **Failed battle_move reports ui=Abilities instead of ui=Move** [State] — After battle_move fails validation, the response shows ui=Abilities but the in-game cursor is still on Move. The scan that runs before the move might be changing the reported ui state. Observed 2026-04-13.
- [ ] **battle_ability selects wrong ability from list** [Execution] — battle_ability "Aurablast" selected Pummel instead. The ability list navigation (Up×N to top, Down×index) is picking the wrong index. The learned ability list may not match the hardcoded index, or the scroll navigation is off-by-one. Observed 2026-04-13.
- [x] **scan_move disrupts targeting mode** [State] — Fixed 2026-04-13: removed Battle_Attacking and Battle_Casting from scan-safe screens.
- [ ] **Abilities submenu remembers cursor position** [Execution] — After battle_ability navigates to a skillset (e.g. Martial Arts for Revive), then escapes, the submenu cursor stays on that skillset. Next battle_attack enters Martial Arts instead of Attack. Need to verify/navigate to correct submenu item rather than assuming cursor is at index 0. Observed 2026-04-13.
- [ ] **BFS valid tiles don't match game** [Movement] — BFS computed (8,11) as valid for Kenrick at (10,9) with Move=4, but game rejected the move. Terrain height transitions or impassable tiles not properly accounted for. Observed 2026-04-13.
- [ ] **battle_ability response says "Used" for cast-time abilities** [State] — Abilities with ct>0 (Haste ct=50) are queued, not instant. Response says "Used Haste" but spell is only queued in Combat Timeline. Unit still needs to Wait. Response should say "Queued" for ct>0 abilities. Observed 2026-04-13.
- [ ] **Detect auto-end-turn abilities (Jump)** [Execution] — Jump auto-ends the turn (unit leaves the battlefield). battle_ability should detect this by checking if the screen transitioned past the active unit's turn after confirming. If so, report "turn ended automatically" instead of leaving Claude to issue a redundant Wait. Observed 2026-04-13.

---

## 13. Battle Statistics & Lifetime Tracking

### Per-battle stats
- [ ] Turns to complete, per-unit damage/healing/kills/KOs, MVP selection

### Lifetime stats (persisted to JSON across sessions)
- [ ] Per-unit career totals, ability usage breakdown, session aggregates

### Display
- [ ] Post-battle summary, `stats` command, milestone announcements

---

## 14. Mod Separation

- [ ] **Extract FFTHandsFree into its own Reloaded-II mod** — All the GameBridge code is piggybacked onto FFTColorCustomizer. Needs its own standalone mod project for public distribution.

---

## Low Priority / Deferred

- [ ] **Re-enable strict mode** [Execution] — Disabled. Re-enable once all gameplay commands are tested.

---

## Sources & References

- [fft-map-json](https://github.com/rainbowbismuth/fft-map-json) — Pre-parsed map terrain data (122 MAP JSON files)
- [FFT Move-Find Item Guide by FFBeowulf](https://gamefaqs.gamespot.com/ps/197339-final-fantasy-tactics/faqs/32346) — ASCII height maps for MAP identification
- [FFHacktics Wiki](https://ffhacktics.com/wiki/) — PSX memory maps, terrain format, scenario tables
