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

- [ ] **Remove scan_move caching** — Scan is now ~15ms (pure memory reads). The cache causes: (a) "Run scan_move first" errors after failed attacks, (b) stale data between turns, (c) forced `screen` calls before every `battle_attack`. Remove `HasCachedScan`, `CacheScanResponse`, `MarkScanned` — every `battle_attack`/`battle_ability` should scan fresh inline.
- [ ] **Format battle_attack/battle_ability responses** — Currently returns raw JSON walls. Should return compact formatted output like `screen` does. Example: `[Attack] Lloyd hit Treant at (7,10) for 378 damage (183/561)` or `[Attack] Kenrick shot Skeleton at (10,6) — evaded!`. Use BattleTracker events to detect damage/miss.
- [ ] **Remove false MISSED detection** — `battle_attack` reports "MISSED (no HP change detected)" on every attack because the HP check reads stale memory before the damage resolves. BattleTracker already detects real damage at 100ms polling. Remove the immediate HP comparison and rely on BattleTracker events instead.

### Screen Output Polish (identified 2026-04-12 end of session)

- [ ] **Move ability filtering and collapsing to C#** [Performance] — The hide-empty-enemy-target and Aim+N collapsing logic currently runs in fft.sh after the full JSON crosses the bridge. Move it server-side so the JSON payload never includes abilities Claude can't use. Wilham's 30+ abilities would shrink to ~10 entries. Also consider compact tile format: `[9,4,"e","Skeleton"]` instead of full object keys.

### Tier 1 — Unblockers (do first)

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

- [ ] **"ERROR: Friendly turn after Xms" is not an error** [State] — `battle_wait` / `execute_action Wait` reports "Friendly turn after 903ms" in the ERROR field. This is informational (how long until next player turn), not an error. Should use `info` field instead, or just omit it. Confusing when reading responses. Observed 2026-04-12.
- [ ] **execute_action responses missing ui= field** [State] — Non-battle-MyTurn screens (Battle_Abilities, Battle_Attacking, etc.) no longer show `ui=Attack` or `ui=Jump` in the response header. The old `fft` formatter included it but `execute_action` lost it. Need to add ui= back to execute_action output for non-MyTurn battle screens. Observed 2026-04-12.
- [ ] **battle_ability selects wrong skillset for secondary abilities** [Execution] — Lloyd has Jump (primary) and Martial Arts (secondary). `battle_ability "Aurablast"` selected Attack instead of Martial Arts. The ability→skillset resolution is picking the wrong submenu item. Need to verify the skillset lookup maps Aurablast→Martial Arts correctly and that the submenu navigation scrolls to the right entry. Observed 2026-04-12.
- [x] **screen header should show [Battle_MyTurn] not [Battle]** [State] — Fixed 2026-04-12.
- [x] **battle_attack response shows [Cutscene] instead of battle state** [State] — Fixed 2026-04-12: filter nameIds >= 200 from eventId check.
- [ ] **battle_attack leaves game stuck in targeting mode on failure** [Execution] — When `battle_attack` fails (e.g. target out of range), it leaves the game in `Battle_Attacking` state instead of canceling back to `Battle_MyTurn`. A subsequent `battle_attack` then fails because the scan cache was invalidated. The failed attack should cancel out of targeting mode before returning the error. Observed 2026-04-12.
- [x] **Gun range calculation wrong — shows targets too close** [Abilities] — Fixed 2026-04-12: guns/bows MinRange=2, crossbows MinRange=3.
- [ ] **Show hit% per target in ability tiles** [State] — When hovering a target in-game, the game shows projected hit%. Read this from memory and include it per target tile so Claude can see `(10,6)<Skeleton 73%>` instead of just `(10,6)<Skeleton>`. Would help decide between a high-damage low-accuracy Aim+20 vs reliable Attack. Could also help detect LoS blocking (0% = blocked). Identified 2026-04-12.
- [ ] **Line-of-sight blocking for ranged attacks** [Abilities] — Archer attacked Treant at (7,11) from (10,9) but a tree blocked the projectile. FFT has LoS checks for ranged abilities (bows, thrown stones, guns). We need to detect blocked paths. Options: (A) read the game's projected hit% from memory during targeting mode, (B) compute LoS from map height data, (C) enter targeting, check if game rejects tile, cancel if blocked. Option A is most practical if the address can be found. Observed 2026-04-12.
- [x] **Filter dead units from ability target tiles** [State] — Fixed in session 5 (AbilityTargetCalculator.IsRevivalAbility).
- [ ] **battle_attack allows diagonal targets** [Execution] — `battle_attack x y` doesn't validate that the target is on a cardinal direction (same X or same Y) from the attacker. FFT only allows attacking in the 4 cardinal directions. Fix: validate `targetX == unitX || targetY == unitY` before entering targeting mode. Observed 2026-04-12.
- [ ] **Equipment IDs stale across battles** [State] — Roster equipment at `+0x0E` reads the save-state equipment, not the current in-battle loadout. Need to find the live equipment address.
- [ ] **Active unit name/job stale across battles** [State] — After restarting a battle with different equipment/jobs, the name/job display doesn't refresh between battles.
- [ ] **`screen` shows wrong active unit between scans** [State] — `screen` reads the active unit from a stale memory buffer. Fix: use the `IsActive` flag from static array scan.
- [ ] **Verify attack landed** [Execution] — Currently reports false "MISSED" when the attack landed but HP check read stale data. Consider removing MISS detection since BattleTracker now detects damage in real-time.
- [ ] **Scan cache doesn't invalidate between player turns** [Movement] — Will be fixed by removing caching entirely.
- [ ] **battle_move reports NOT CONFIRMED for valid moves** [Movement] — Navigation succeeds but F key confirmation doesn't transition within 3s timeout.
- [ ] **Detect disabled/grayed action menu items** [Movement] — Need to find a memory flag or detect from cursor behavior.
- [x] **C+Up scan sometimes fails after restart** [Movement] — OBSOLETE: C+Up eliminated in session 5.
- [ ] **Jump ends turn immediately — no Wait/facing step** [Execution] — After Jump, the turn ends immediately. Claude tries to `battle_wait` after Jump which fails. Need to detect Jump as a turn-ending ability.
- [ ] **Post-attack facing/move selection** [Movement] — After Act without prior Move, game returns to Battle_MyTurn with cursor on Move. battle_wait should handle this correctly.
- [~] **battle_retry doesn't work from GameOver screen** [Execution] — Code exists, GameOver detection fixed. Needs live testing.
- [ ] **Re-enable Ctrl fast-forward during enemy turns** [Execution] — Tested both continuous hold and pulse approaches. Neither visibly sped up animations. Low priority.
- [ ] **Populate new BattleUnitState fields from memory** [State] — deathCounter, elementAbsorb/Null/Half/Weak, chargingAbility/chargeCt, facing. All need IC remaster addresses discovered.
- [ ] **Read death counter for KO'd units** [State] — KO'd units have 3 turns before crystallizing. Need to find the IC equivalent of PSX offset ~0x58-0x59.
- [ ] **Detect charging/casting units** [Abilities] — Units charging a spell show in the Combat Timeline. Need to read charging state, which spell, and remaining CT from memory.

### Tier 4 — Known hard problems

- [ ] **Unit names — enemies** [Identity] — Enemy display names not found in memory. May need NXD table access or glyph-based lookup.
- [ ] **Zodiac sign per unit** [Identity] — Needed for damage multipliers.
- [ ] **Charge time spells** [AoE] — CT cost per ability for tactical planning.
- [ ] **Fix Move/Jump stat reading** [Movement] — UI buffer shows base stats, not effective (equipment bonuses missing).
- [ ] **Neutral unit handling (team=2)** [Movement] — Don't block pathing for NPCs/guests. Rare.

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

- [ ] **PartyManagement.md** — How to access the party menu, view unit stats, change equipment, change jobs.
- [ ] **Shopping.md** — How to enter a settlement, navigate the outfitter, buy/sell items.
- [ ] **FormationScreen.md** — How to place units before battle.
- [ ] **SaveLoad.md** — How to save and load the game.
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
- [ ] **battle_move reports Battle_Casting instead of Battle_Moving** [State] — After `battle_move 8 7`, response header shows `[Battle_Casting]` but the actual screen state is Battle_Moving (battleMode=2). Battle_Casting (battleMode=1) is for cast-time magick targeting, not movement. The screen detection may be reading battleMode=1 during the move confirmation animation. Observed 2026-04-12.
- [ ] **battle_move doesn't validate target tile** [Execution] — `battle_move 8 7` accepted and moved to an invalid tile without returning an error. The move command should validate the target against the BFS-computed valid tile list before attempting navigation. If the tile isn't reachable, return an error immediately. Observed 2026-04-12.
- [x] **screen doesn't show ui= at turn start after Wait** [State] — Fixed 2026-04-12: added s.ui to screen header in fft.sh.
- [ ] **Static array at 0x140893C00 is stale mid-turn** [State] — HP AND positions don't update during/after moves or attacks within a turn. Only refreshes at turn boundaries. Killed a Skeleton (HP 535→0 on screen) but array still read 535. Moved Ramza but array still showed old position. Need to find the live data source the game UI reads from.
- [ ] **Damage/hit% preview during targeting** [State] — The game displays projected damage and hit% when hovering a target. Extensive investigation 2026-04-12:
  - **Found via probe_status:** In attacker's heap struct, hit% at statBase-62 (u16), damage at statBase-96 (u16). Verified across 3 targets (Kenrick 570/48%, Lloyd 342/50%, Wilham 364/95%). Offsets consistent for hit%, damage shifted by 4 bytes for one target.
  - **Two heap copies exist:** One in 0x416xxx range (found by `SearchBytesInAllMemory`, PAGE_READWRITE PRIVATE) — has HP/stats but NOT preview data. Another in 0x130xxx-0x15Axxx range (found by `SearchBytesAllRegions`) — this copy HAS preview data at the offsets above.
  - **Problem:** `SearchBytesInAllMemory` only scans PAGE_READWRITE PRIVATE memory, missing the copy with preview data. `SearchBytesAllRegions` finds it but is slow (scans from addr 0) and returns too many false matches.
  - **Approach needed:** Use `SearchBytesInAllMemory` with `broadSearch: true` flag (already added — scans all readable memory with address range filter). Search for HP+MaxHP of the attacker, verify level byte, read at statBase-62 and statBase-96. Must exclude the 0x416xxx copy (no preview data) — filter by checking hit% > 0.
  - **Also found at low static address** (0x60823C one session, different next) via `search_all` with unique 10-byte pattern. Address shifts between restarts. Reading from this address crashed the game — likely in a protected code segment.
  - **Code exists but disabled:** `ReadDamagePreview()` in NavigationActions.cs has the search + offset logic. Currently returns (0,0) because the broad search finds the wrong copy. Fix: add address range filter to skip 0x416xxx and target the 0x130-0x15A range.
- [ ] **BFS move tiles too permissive — terrain height not properly limiting range** [Movement] — BFS at Move=4 from (10,9) includes (8,7) (distance 4) but in-game the tile isn't reachable due to terrain. The BFS validation passes but the game rejects the move. Need to verify terrain height costs in BFS match FFT's rules. Observed 2026-04-12.
- [x] **Attack target tiles include diagonal targets** [Abilities] — INVALID: FFT Attack DOES include diagonals via Manhattan distance. (7,9)→(8,10) is a valid adjacent attack. Cardinal-only assumption was wrong.

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
