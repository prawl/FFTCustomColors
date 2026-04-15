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

## Design Principle: What Goes In Compact vs Verbose vs Nowhere

Every field on the `screen` response has to earn its spot by changing a decision Claude actually makes. Three tests:

1. **Would a human consult this on this screen?** If yes → strong candidate. If no → don't surface.
2. **Does Claude need it to act HERE, or could they navigate to it?** Need it here → surface. Could navigate → don't pre-populate.
3. **Would not having it cause a worse decision OR wasted round-trips?** Yes → surface. No → drop.

**Plus a noise penalty.** Claude greps past dense responses — every field in the compact one-liner makes other fields harder to find. There's a budget. Anything that doesn't strongly pass the three tests pays rent against that budget.

**Prefer decision aids over data dumps.** `jobCellState: "Visible"` (one word, decision is obvious) beats dumping 19 grid cells of raw JP that Claude has to interpret. Surface the *conclusion*, not the inputs.

**Where things go:**

| Compact one-liner | Verbose JSON only | Nowhere |
|---|---|---|
| Things Claude reads on every turn — state name, `ui=`, `viewedUnit=`, location, status. Tight budget; add only when a missing field would cost decisions on the next action. | Things Claude reads when planning — full loadouts, ability lists, grid dumps, per-unit detail. Liberal budget; if it could plausibly inform a decision, surface it here. | Anything that mirrors what hovering already reveals in-game. Per-cell stats Claude can read by moving the cursor. Anything the game shows clearly that isn't load-bearing for a *programmatic* decision. |

**Before adding a new field, write one sentence answering "what decision changes if Claude has this?"** If you can't, drop it. If the answer is "Claude could plan a turn ahead with this," verbose. If it's "Claude needs this to pick the next action," compact. If it's "it's nice to have," nowhere.

This rule killed AC5 (per-class Lv/JP on JobSelection grid) — Claude doesn't need 19 JP values to decide a job change; the cell they're hovering shows it in-game already.

---

## Status Key
- [ ] Not started
- [~] Partially done

---

## Priority Order

Organized by "what blocks Claude from playing a full session end-to-end" — most blocking first.

---

## 0. Urgent Bugs

- [x] **Ability picker state machine desync: Enter equipped an ability but state machine incorrectly returned to EquipmentAndAbilities** — FIXED 2026-04-14 session 13. Root cause: `ScreenStateMachine.HandleAbilityPicker` treated both `VK_RETURN` and `VK_ESCAPE` as picker-close events, but in the real game `Enter` only equips (picker stays open, shows checkmark); only `Escape` actually closes the picker. Fix: removed Enter from the close-transitions in HandleAbilityPicker; the picker now stays open on Enter and consumers (fft.sh helpers, Claude navigation) must send Escape to close. Verified live by running `change_reaction_ability_to` helper which cleanly equips + Escape-closes back to EquipmentAndAbilities.

- [ ] **CharacterStatus / EquipmentAndAbilities header missing `Next: N` (JP to next ability)** — logged 2026-04-14 session 13. The game's info bar shows four values: `Lv. N` (job level), `Next: N` (JP needed to learn the next cheapest unlearned ability in the current job), `JP N` (accumulated JP in current job). We surface Lv and JP but omit Next. Screenshot on Cloud shows `Lv. 2 Next: 116 JP 44`. Two options: (a) compute from the learned-action-abilities bitfield at roster +0x32+jobIdx*3 (bytes 0-1) cross-referenced with each skillset's ability cost table (`ABILITY_COSTS.md`) — find the cheapest unlearned ability in the unit's current skillset, return its cost; (b) find where the game stores the value (likely a widget cache we haven't located yet, same UE4 heap churn that defeated prior hunts). (a) is deterministic and session-safe; (b) would be faster to render but more fragile. Add `nextJp` to RosterSlot (or to the loadout payload) and render in fft.sh header after `Lv` and before `JP`.

- [ ] **EquipmentAndAbilities Abilities column surfaces `ui=(none)` on slots with no equipped ability** — logged 2026-04-14 session 13. Repro: open Cloud's EquipmentAndAbilities (Cloud's Primary is blank because "Soldier" isn't in `GetPrimarySkillsetByJobName`), cursor Right into the Abilities column. `ui=(none)` surfaces — bare and uninformative. Better: (a) populate the missing story-class primaries (Soldier=Limit, Dragonkin=Dragon, Steel Giant=Work, Machinist=Snipe, Skyseer/Netherseer=Sky/Nether Mantra, Divine Knight=Unyielding Blade, Templar=Spellblade, Thunder God=All Swordskills, Sky Pirate=Sky Pirating, Game Hunter=Hunting — verify each in-game before adding). (b) Change the `(none)` fallback in `CommandWatcher` EquipmentAndAbilities ability-cursor branch to `Primary (none)` / `Secondary (none)` / `Reaction (empty)` / `Support (empty)` / `Movement (empty)` so the row intent is at least visible. (c) Consider: Primary row should never surface `(none)` anyway — it's job-locked, so we should always know the primary skillset name from the job; a blank means our job-name map is incomplete.

- [ ] **Extend `ItemInfo` with attributeBonus / equipmentEffects / attackEffects / dualWield / twoHanded fields, then populate** — added 2026-04-14. The game's item info panel has 3 pages (verified live in Outfitter Try-then-Buy on Ragnarok): page 1 = WP/evade/range (already in ItemInfo), page 2 = Attribute Bonuses (e.g. PA+1, MA+2), Equipment Effects (e.g. "Permanent Shell"), Standard Attack Effects (e.g. on-hit Petrify), page 3 = Weapon Type flags (Can Dual Wield / Can Wield Two-handed) and Eligible Jobs. Without these fields the `uiDetail` description is incomplete for many items and Claude can't tell that Ragnarok grants permanent Shell etc. Strategy: extend `ItemInfo` record with the new fields, populate the ~30 most-used hero items by hand from the FFHacktics wiki (Ragnarok, Excalibur, Chaos Blade, Maximillian, Crystal Mail, Bracer, Chantage, etc.), then bulk-populate the rest from the game's NXD item table in a follow-up. Skip Eligible Jobs for now (low value, lots of data). Surface the new fields in `UiDetail` and render in fft.sh below the existing stats line.

- [ ] **`screen -v` doesn't include the new EquipmentAndAbilities/picker payloads** — added 2026-04-14. Compact `screen` shows the three-column Equipment/Abilities/Detail layout + cursor marker on EquipmentAndAbilities, and the `Available skillsets/reactions/supports/movement (N):` list on pickers. Verbose mode (`screen -v`) currently only changes PartyMenu output (full roster grid). It should ALSO surface fuller detail when -v is set on EquipmentAndAbilities / pickers — e.g. show the full long-form description (we currently wrap at 40 chars in compact, could be 80+ in verbose), expand all picker entries with their stats (Job + Description preview per row), maybe show all three pages of the in-game item info panel (Attribute Bonuses, Equipment Effects, Standard Attack Effects, Eligible Jobs) once `ItemInfo` carries that data. Implementation: add `if (verbose)` branches in fft.sh's EquipmentAndAbilities and picker rendering blocks.

- [ ] **PartyMenu tab desync — multi-press jump paths race the game's tab-switch animation** (regression introduced 2026-04-14 alongside Chronicle/Options shipping).
  - Repro: from any PartyMenu tab, run `execute_action OpenChronicle` (or any path with 2+ Q/E key presses). Live-verified: state machine reports `PartyMenuChronicle` but the game visually shows the previous tab (Inventory). Same desync for `OpenOptions`, `OpenInventory` from Chronicle/Options, etc.
  - Root cause: the game's tab-switch animation eats the second key if pressed too fast. State machine ticks `Tab` per OnKeyPressed call (synchronous, in-process), so it advances even when the actual UI doesn't.
  - First fix attempt: added `DelayBetweenMs = 300` on the multi-press tab paths in `NavigationPaths.cs` (OpenChronicle from Units, OpenOptions from Inventory, OpenUnits from Chronicle, OpenInventory from Options). 300ms didn't help — the game's animation may be longer, OR `WaitForScreen` short-circuits because it polls the OWN state-machine-derived screen name (which already says we arrived).
  - Real fix likely needs ONE of:
    1. **Bigger delay** (try 500ms, 750ms — empirical cap before tests of "feels slow"). Quick patch.
    2. **Per-key wait-for-game-confirm** instead of one wait at the end. Path engine change. Robust but invasive.
    3. **Find a memory address that reflects the actual rendered tab.** The 2026-04-14 hunt failed (UE4 widget churn — see `project_shop_stock_array.md`), but a fresh attempt could try the 0x141870xxx region where `shopListCursorIndex` lives — sister UI cursors might cluster there.
    4. **Stop using path-collapsed multi-press.** Force consumers to call `NextTab` repeatedly and verify with `screen` between each — slower but provably correct.
  - Workaround for now: use `NextTab`/`PrevTab` (single key) and call `screen` to verify. The single-press paths work; only the multi-press jumps desync.
  - Inner Chronicle/Options nav (CursorUp/Down/Left/Right + Select) should be unaffected because each is a single key press.



- [ ] **PartyMenu cursor state-machine drift after Escape transitions** — observed 2026-04-15 session 15. Repro: from PartyMenu cursor on Ramza (0,0), Down to Agrias (1,0), Enter to her CharacterStatus, navigate around (Down sidebar → Enter JobSelection → cursor moves → Escape × N back out). Final state: state machine reports `ui=Agrias` on PartyMenu, but in-game cursor is back on Ramza (0,0). Several state-machine handlers (Escape transitions, drift recovery, etc.) clear/reset CursorRow/Col on the way back — but in-game the engine restores the cursor to its "pre-entry" position which is where the player came from, not the saved position. Effect: every nested-screen interaction after the first round-trip surfaces the wrong viewed unit and the wrong layout for downstream screens (JobSelection ui= shows the wrong character's class, change_job_to acts on the wrong unit). Workaround: explicitly Down-then-Up to re-sync state-machine cursor with in-game position before nesting again. Fix candidates: (a) read the actual PartyMenu grid cursor from memory (UE4 widget heap — same kind of byte we hunt for JobSelection); (b) restore the saved cursor on screen-Escape transitions; (c) snap the state machine to grid-pos-0 (Ramza) on every WorldMap-from-PartyMenu Escape since that's the game's real reset behavior. (b) is most accurate; (c) is cheapest.

- [ ] **JobSelection cell state (Locked / Visible / Unlocked) per unit** — observed 2026-04-15 session 15 (Agrias on Dancer cell). The grid ALWAYS has all 19 physical cells; cursor walks every cell regardless of state. Our flat-index layout math is correct. What's missing is per-cell state:
  1. **Locked** — NO unit in the party has unlocked this class. Cell renders as a blacked-out shadow silhouette. Hovering gives zero info. ui= should surface something like `(locked)` or omit the class name.
  2. **Visible** — SOME unit in the party has unlocked this class but the VIEWED unit hasn't. Cell renders normally. Hovering shows the unlock requirements (e.g. "Squire Lv. 2, Chemist Lv. 3"). Enter on Change Job is refused.
  3. **Unlocked** — viewed unit meets all prerequisites. Normal selectable cell. change_job_to works.
  - Sources:
    - Party-wide "any unit unlocked" flag: can be derived from roster by scanning each unit's per-job learned-ability bitfield (+0x32+jobIdx*3 bytes 0-1 — see `project_roster_learned_abilities.md`) → if any bit set for job J across any party member, cell J is at least Visible.
    - Per-unit "unlocked for me" flag: same bitfield but only the viewed unit's row. If any bit set, they've made progress in job J. Distinct from "met prerequisites to take the job" though — a unit CAN be able to take job J without ever having played it. Need a separate prerequisite check.
    - Prerequisites (e.g. Knight needs Squire Lv. 2): not yet in data. Source of truth is the game's FFT job tree — could hard-code a `JobPrereqs` map (~20 entries) in CharacterData. Verifiable live by checking the "Visible" cell's info panel text.
  - Surface on `screen`:
    - `screen.jobCellState: "Locked" | "Visible" | "Unlocked"` for the currently-hovered cell.
    - When Visible, add `screen.jobUnlockRequirements: "Squire Lv. 2, Chemist Lv. 3"` (scrape from info panel — needs a memory address for the widget text, OR reconstruct from the prereq map).
    - When Locked, omit the class name from ui= or surface `ui=(locked)`.
  - `change_job_to` should refuse with a clear error when target state is Locked ("class is not visible to any party member") or Visible ("unit missing prerequisites: Squire Lv. 2").
  - This explains the Agrias/Dancer test hiccup — Dancer is probably Locked or Visible for Agrias; our `ui=Dancer` claim is wrong because we don't know the cell's state. The cursor navigation itself works fine (19 cells, all walkable), but the label we emit for (2,4) is misleading without state context.
  - **Locked-state live verification deferred** — the current save has at least one party member who unlocked everything, so NO cell will render as a shadow silhouette for any other unit. Need a fresh-game save to verify the Locked branch end-to-end. Log a reminder to test it when a new-game save exists, or when we can temporarily dismiss all units except one under-leveled generic.
  - **JobSelection cursor row-cross desync FIXED 2026-04-15 session 15.** The widget heap reallocates per row cross — confirmed live: address `0x11EC34D3C` shuffled to `0x1370CF4A0` after a single Down. Fix: `InvalidateJobCursorOnRowCross` clears `_resolvedJobCursorAddr` + `_jobCursorResolveAttempted` on every Up/Down key while on JobSelection, forcing re-resolve on the next screen call. Horizontal movement (Left/Right) doesn't trigger it — those reads stay reliable.

- [x] **State machine drifts from reality on PartyMenu entry** [Detection] — PARTIALLY FIXED 2026-04-14 session 13 via memory-backed drift recovery. Memory byte `0x14077CB67` (menuDepth) cleanly distinguishes outer party-menu-tree screens (WorldMap/PartyMenu/CharacterStatus = 0) from inner panels (EquipmentAndAbilities/ability picker = 2). CommandWatcher.DetectScreen now runs a debounced check — if the state machine thinks we're on an inner panel but menuDepth reads 0 for 3 consecutive reads, snaps back to CharacterStatus (with `MarkKeyProcessed()` to prevent cascade into the older PartyMenu stale-state recovery). Live-verified in session 13 — helpers that used to desync now self-correct. Still outstanding: the inner-panel mid-restart case (restart happens while player is already on EqA or a picker) isn't directly covered — the byte reads 2 correctly, but the state machine has no way to know WHICH inner panel (EqA vs which picker). Lower priority because the common case (restart on PartyMenu/CharacterStatus) is now fixed.

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

- [ ] **Block `world_travel_to` to current location** — Calling world_travel_to with the location ID of the current standing node opens the travel list with the cursor on the current node, and the blind "press Enter to confirm" flow selects it. The game then gets stuck in an undefined state (travel modal opens, input routing goes to a subwindow, subsequent Enter presses are swallowed). Detect and refuse: if `locationId == currentLocationId` (where currentLocationId is the WorldMap cursor hover OR the last-arrived location), return `{status: "rejected", error: "Already at <name>. Use execute_action EnterLocation to enter the location menu."}`. 2026-04-14 — observed breaking the Dorter shop run.
- [ ] **Pre-press `C` (or middle mouse button) to recenter cursor before `EnterLocation`** — User discovered 2026-04-14: the game binds `C` / middle-mouse to "recenter WorldMap cursor on current node". This is the clean fix for the "Enter does nothing because cursor drifted" problem. Implementation: in the `EnterLocation` ValidPath handler (NavigationPaths.cs → GetWorldMapPaths), prepend a `C` key press before the Enter. Single key, deterministic, no memory-reading needed. This supersedes the "Block EnterLocation when cursor isn't on the current settlement" TODO below — just always recenter first and the edge case disappears.
- [x] ~~**Block `EnterLocation` when WorldMap cursor isn't on the current settlement**~~ — Superseded 2026-04-14 by the `C`-key recenter fix above. Leaving the strikethrough for history: the original symptom was `EnterLocation` silently no-oping because the cursor had drifted off the node. Instead of refusing the action, we just recenter before pressing Enter. Keeping the TODO marked done so the fix implementation is tracked in one place.
- [ ] **Locked/unrevealed locations** — Read unlock bitmask at 0x1411A10B0 and skip locked locations.
- [ ] **Encounter polling reliability** — Encounters sometimes trigger before polling starts.
- [ ] **Ctrl fast-forward during travel** — Not working.
- [ ] **Resume polling after flee** — Character continues traveling after fleeing. Need to re-enter poll loop.
- [ ] **Location address unreliable** — 0x14077D208 stores last-passed-through node, not standing position.

---

## 4. Instruction Guides (P1)

- [x] **PartyManagement.md** — Written 2026-04-13.
- [x] **Shopping.md** — Written 2026-04-14. See `FFTHandsFree/Instructions/Shopping.md`.
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

### Detection — what's mapped

- [x] LocationMenu detection (locationMenuFlag at 0x140D43481) — mapped 2026-04-14
- [x] LocationMenu shop type (Outfitter/Tavern/WarriorsGuild/PoachersDen) via shopTypeIndex at 0x140D435F0 — mapped 2026-04-14
- [x] ShopInterior detection (insideShopFlag at 0x141844DD0) — mapped 2026-04-14, partially reliable (doesn't always fire on a fresh process)
- [x] Outfitter sub-actions: Outfitter_Buy / Outfitter_Sell / Outfitter_Fitting via shopSubMenuIndex at 0x14184276C (values 1/4/6) — mapped 2026-04-14

### Detection — TODO

- [x] **Rename `ShopInterior` → `SettlementMenu`** — DONE 2026-04-14. ❗ **But done WRONG.** The rename was applied to the WRONG layer — see follow-up below.
- [ ] **Fix misaligned shop-state naming** — the 2026-04-14 rename chose the wrong layer. Current state:
  - `LocationMenu` — fires when hovering a shop in the settlement's shop list (correct)
  - `SettlementMenu` — fires when inside a specific shop, at the Buy/Sell/Fitting selector (misnamed — this IS the interior)
  - `Outfitter_Buy` / `Outfitter_Sell` / `Outfitter_Fitting` — inside the Buy/Sell/Fitting list (correct)

  User expectation is:
  - `LocationMenu` stays as-is (settlement's shop-type selector — you're choosing Outfitter vs Tavern vs ...)
  - The CURRENT `SettlementMenu` should be named **after the shop itself** — `Outfitter` / `Tavern` / `WarriorsGuild` / `PoachersDen`. The `ui=` field shows the sub-action cursor: `[Outfitter] ui=Buy`, `[Outfitter] ui=Sell`, `[Outfitter] ui=Fitting`.
  - Sub-action lists stay as `Outfitter_Buy` / `Outfitter_Sell` / `Outfitter_Fitting`.

  Fix: change detection to return `Outfitter` (or the appropriate shop name) when `insideShopFlag==1` and `shopSubMenuIndex==0`, using `shopTypeIndex` to pick the name. Drop `SettlementMenu` as a state name. Rename `GetSettlementMenuPaths` → `GetOutfitterMenuPaths` (and duplicate for Tavern / WG / PD once their interiors are distinguishable — but currently the layout is the same for all four, so one helper reused via per-shop state names works).

  Also needs: a memory scan for the sub-action cursor (Buy/Sell/Fitting hover) so `ui=Buy` can actually populate — currently only known AFTER Select has been pressed (shopSubMenuIndex jumps to 1/4/6). See also the "ui label at ShopInterior" TODO entry (which is about this same gap).
- [ ] **(NEXT) ValidPaths for the whole shop flow** [P0] — the automation unlock. Every shop screen needs a ValidPaths entry so Claude can drive the UI without knowing individual key sequences. Required entries:
  - **LocationMenu**: `EnterOutfitter`, `EnterTavern`, `EnterWarriorsGuild`, `EnterPoachersDen`, `Leave` (back to world map)
  - **ShopInterior** (shop menu): `Buy`, `Sell`, `Fitting`, `Leave`
  - **Outfitter_Buy**: `SelectItem <name>` (navigates to named item), `SetQuantity <n>`, `Purchase`, `Back`
  - **Outfitter_Sell**: `SelectItem <name>`, `SetQuantity <n>`, `Sell`, `Back`
  - **Outfitter_Fitting**: `SelectCharacter <name>`, `SelectSlot <slot>`, `SelectItem <name>`, `Equip`, `Back`
  - **Tavern / WarriorsGuild / PoachersDen**: fill in once sub-actions mapped
  - **Confirm dialogs**: `Confirm`, `Cancel` (requires the confirm-modal scan from below)
- [x] **Gil in state** [P0 quick win] — DONE 2026-04-14. Gil at 0x140D39CD0 surfaces on shop-adjacent screens (WorldMap, PartyMenu, LocationMenu, ShopInterior, Outfitter_Buy/Sell/Fitting) via ShopGilPolicy.
- [x] **Format gil with thousands separators** — DONE 2026-04-14. `_fmt_gil` helper in fft.sh renders via `printf "%'d"` under `LC_ALL=en_US.UTF-8`. JSON unchanged.
- [ ] **Suppress `ui=Move` outside battle** — `screen.UI` reads "Move" on WorldMap, LocationMenu, PartyMenu, etc. because the action-menu cursor stays at index 0 (which labels as "Move" per the battle menu mapping). Outside battle the label is meaningless noise. Fix: only populate `screen.UI` with the action-menu label while in `Battle_MyTurn` / `Battle_Acting`. On all other screens, either omit it or surface only context-appropriate labels (shop name on LocationMenu/SettlementMenu, etc.). Observed 2026-04-14.
- [ ] **ui label at ShopInterior** — when hovering Buy/Sell/Fitting inside a shop without having entered, `screen.UI` should read `Buy`/`Sell`/`Fitting`. Needs a cursor-index memory scan (current shopSubMenuIndex is 0 at all three hovers). Once ui is populated, Claude can pre-check which sub-action it's about to enter.
- [x] **Shop list cursor row index** — DONE 2026-04-14. `0x141870704` (u32) tracks the currently-highlighted row inside Outfitter_Buy/Sell/Fitting. Row 0 = top item, increments per ScrollDown. Persists across sub-action cycling. Found via 4-way module_snap diff at Dorter Outfitter (rows Oak→White→Serpent→Oak).
- [ ] **Decode row index to item name** — with the cursor index known, resolve `ui=<item name>` by looking up the row's position in the shop's stock list. Partial data found 2026-04-14:
  - **Master item name list (weapon tab)** at `0x6007BE4` — UTF-16LE strings in a flat array (`Oak Staff\x00\x17White Staff\x00\x1BHealing Staff\x00...`). Contains ALL weapons in the game, not just any shop's stock. Length/delimiter byte between entries (0x13, 0x17, 0x1B, etc. — proportional to NEXT string length).
  - **Master item DB (fixed-stride array)** around `0x5A1000` — 4000 u32 (a common item price) appears at 100+ addresses with exact 0x48 (72-byte) stride. This is likely the per-item record table with price, stats, name-pointer at fixed offsets inside each 72-byte slot.
  - **Shop record candidate** around `0x8CB9FB0-0x8CBA000` — two consecutive 72-byte records observed with id-like u32 (243, 244) and price-like u32 (6990, 7000) at offsets +24 and +28 (or +8 and +12). Not yet confirmed as shop-specific.
  - **Prices NOT stored contiguously** — direct search for sequenced u16 prices `78 00 20 03 98 08 A0 0F` (Oak/White/Serpent/Mage's Staff) found 0 matches. Each item record has gaps, so decoding requires knowing the stride.
  - **Gariland vs Dorter diff** — 80K changed bytes, too noisy from travel encounter counters. Need a tighter methodology: either (a) take TWO Dorter snapshots with the same shop state to filter noise, intersect with Gariland diff; or (b) focus scan on the 0x5A1xxx and 0x8CBAxxx regions and look for addresses where the value changes between Gariland and Dorter while staying stable within one session.
  - **Scan state available:** `gari_buy` and `dorter_buy` module snapshots exist in the current mod process. `diff_d_gari_dorter.txt` has the full pairwise diff (80K entries). Use as input if picking this up soon.
  - **Followup session 2026-04-14:** Row-to-row diff (Outfitter_Buy cursor step) is only 131 addresses and includes NO price-like transitions. Confirms that scrolling doesn't re-read prices — they're all pre-loaded into the widget. Per-item UE4 FString allocations (e.g. Oak Staff at `0x15BC16868` one moment, `0x1604B0BF0` the next) confirm the shop list lives in UE4-managed heap that moves every frame/allocation — static addresses won't work for shop-specific data. Byte-pattern searches for price sequences (`78 00 20 03` u16 adjacent, `78 00 00 00 20 03 00 00` u32 adjacent) return 0 matches in PAGE_READWRITE PRIVATE, so prices are NOT stored contiguously per-shop.
  - **Hardcoded stopgap considered and rejected 2026-04-14:** a per-(location, shopType, subAction) table maps row→item works for the two shops we've mapped but **shop stock changes per chapter** (confirmed by user). Would need per-chapter tables AND a chapter-detection scan. Brittle, silent-breakage risk when story advances. Removed.
  - **In-process scraper approach (partially implemented, NOT YET WORKING):** `scrape_shop_items` action wired up in `ColorMod/GameBridge/ShopItemScraper.cs`. Discovers the session-specific FString vtable by finding the live "Weapons" header widget and reading the 16-byte offset BEFORE it. Then runs a vtable broad-search (capped at 20K candidates) and decodes each candidate's length + UTF-16 text from the context bytes captured during the search. Current behaviour: finds 1-3 strings per run but NOT the shop item names. Root causes observed 2026-04-14:
    - UE4 widget allocations shift between search and decode steps (confirmed by dumping an "Oak Staff" match address moments after the search — bytes are completely different)
    - Walking entire memory regions as `byte[]` in C# crashed the game (GC pressure / OOM in the mod process)
    - Vtable discovery flickers per session — sometimes the "Weapons" live widget is in heap, sometimes it's been freed before my search reaches it
    - 20K vtable candidates hit the cap before scanner reaches the 0x15Axxxxxxx range where shop widgets live
  - **Next approach to try:** scrape-on-demand by scrolling. Instead of one-shot extracting everything, loop: scroll cursor down, read the ONE currently-highlighted item's FString (search narrowed by short-string pattern + filter by row=N memory state), advance. Trades a scan for ~10 scrolls × ~200ms each = ~2s. The "currently highlighted row" widget might be more stable than enumerating all visible rows.
  - **What we verified works today:** 1) master item name pool at `0x6007BE4` contains all weapon names statically. 2) when a shop is open, each currently-visible item name has exactly ONE live FString in PAGE_READWRITE that matches the master name. 3) When the list scrolls, some items get reallocated (addresses shift) — but SearchBytesAllRegions picks up current live copies reliably if queried once (second query may find it freed).
  - **Chapter detection missing:** before shop stock or chapter-gated features work, we need a stable u8/u16 read that reports the current story chapter. Memory scan needed — likely near the story objective field at `0x1411A0FB6`.
- [ ] **Full stock list inline at Outfitter_Buy** — instead of forcing Claude to scroll through items one at a time (ui=Oak Staff → down → ui=White Staff → down...), surface the entire shop stock in the screen response. Each entry: `{name, price, type, stats}`. Stats tier by type — weapons: `wp, range, element, statMods` (e.g. `WP=5 MA+1`); armor: `hp, def, evade, statMods`; consumables: `effect` (e.g. `Restores 30 HP`, `Removes KO`). Claude picks by name, one round-trip. Matches scan_move's "see everything at once" philosophy.
- [ ] **Full sell inventory inline at Outfitter_Sell** — same shape as Buy but for player-owned items. Each entry: `{name, heldCount, sellPrice, type, stats}`. Held count is required so Claude knows "I have 12 Potions" without separate reads.
- [ ] **Full equipment picker inline at Outfitter_Fitting** — when picking a slot's replacement item, show all items the player owns that fit the slot, with stats, so Claude can compare current vs candidate in one look.
- [ ] **Tavern interior** — captured 2026-04-14 (screenshots in session). State machine:
  - `Tavern` — inside the Tavern, two sub-actions: `ui=Rumors` and `ui=Errands`. Currently reports as `SettlementMenu ui=Tavern` (mis-named outer layer — see "Fix misaligned shop-state naming" above).
  - `Rumors` — opens a scrollable list of rumor titles (e.g. "The Legend of the Zodiac Braves", "Zodiac Stones", "The Horror of Riovanes", "At Battle's End"). Right pane renders the body text of the highlighted rumor. `ui=<rumor title>`. Claude should be able to READ the body — likely via a new action `read_rumor` that scrapes the UE4 widget like `read_dialogue` does for cutscenes.
  - `Errands` — list of available errands with metadata columns: `Days`, `Finder's Fee`. Right pane shows the errand description and quester (e.g. "Minimas the Mournful" — 12-14 days, 600 gil). `ui=<errand title>`. Claude should read the description, the party-size requirement, the days, the fee. Accepting an errand requires party-menu navigation (assign chosen units) — defer until party menu is complete.
  - ValidPaths needed: `Tavern` → Rumors / Errands / Leave / CursorUp/Down; `Rumors` → ScrollUp/Down / Read / Back; `Errands` → ScrollUp/Down / Select / Back (with Select opening the party-menu for dispatch, once that flow is built).
  - Memory scans needed: (a) Rumors vs Errands state discriminator (likely a new shopSubMenuIndex value for Tavern). (b) Rumor/errand cursor row index (probably 0x141870704 like Outfitter, to verify). (c) Scraping the body-text pane for the currently-highlighted entry.
- [ ] **Warriors' Guild interior** — captured 2026-04-14 (screenshot in session). State machine:
  - `Warriors_Guild` — inside the guild, two sub-actions: `ui=Recruit` and `ui=Rename`. The guildmaster has an idle dialog line ("Is there aught else?") visible but it's UI chrome, not gameplay state.
  - `Recruit` — requires party-menu integration (new hire joins the roster). Defer until party menu is complete. The recruit flow involves picking a job/class and naming the unit.
  - `Rename` — requires party-menu navigation to pick a unit, then text input. Also defer until party menu + text input are solved.
  - ValidPaths needed: `Warriors_Guild` → Recruit / Rename / Leave / CursorUp/Down. Sub-action flows deferred.
  - Memory scans needed: state discriminators for `Recruit`/`Rename` (new shopSubMenuIndex values for WG).
- [ ] **Poachers' Den interior** — captured 2026-04-14 (screenshot in session). State machine:
  - `Poachers_Den` — inside the den, two sub-actions: `ui=Process Carcasses` and `ui=Sell Carcasses`.
  - `Process Carcasses` — list of monster carcasses you own; picking one trades it for a rare item. `ui=<carcass name>` while hovering a row. If the player has zero carcasses the panel renders "No carcasses to process." and the ui is empty.
  - `Sell_Carcasses` — list of carcasses with `Held` and `Sale Price` columns, same empty-state ("No carcasses to sell."). `ui=<carcass name>` while hovering.
  - ValidPaths needed: `Poachers_Den` → Process / Sell / Leave / CursorUp/Down; `Process_Carcasses` and `Sell_Carcasses` → ScrollUp/Down / Select / Cancel.
  - Memory scans needed: shopSubMenuIndex values for Process/Sell sub-actions; cursor row index (probably reuses 0x141870704); carcass-name widget scraping (same problem as Outfitter items).
  - Bonus: surface `heldCount=<n>` and `salePrice=<gil>` per row in the state response for Sell_Carcasses.
- [ ] **Save Game menu** — encountered at Warjilis (Dorter has 4 shops, no Save). Needs its own scan; verify if it shows up as a 5th shopTypeIndex value or a distinct flag. Add the index to the shop name mapping in CommandWatcher.cs.
- [ ] **Midlight's Deep stage selector** [LOW PRIORITY] — Midlight's Deep (location ID 22) is a special late-game dungeon. When you press Enter on the node, a vertical list of named stages appears (captured 2026-04-14: NOISSIM, TERMINATION, DELTA, VALKYRIES, YROTCIV, TIGER, BRIDGE, VOYAGE, HORROR, ...). The right pane renders a flavor-text description of the highlighted stage. This UI is structurally similar to Rumors/Errands but with its own screen name: `Midlight's_Deep` with `ui=<stage name>`. ValidPaths needed: ScrollUp/Down / Enter (commits to that stage → battle) / Back. Memory scans needed: the stage-name list (probably UE4 heap like shop items), the cursor row index (probably 0x141870704 reused), and a state discriminator for "inside Midlight's Deep node selector" vs just-standing-on-the-node. Defer until main story shopping/party/battle loops are stable — this only matters for end-game content.
- [ ] **Cursor item label inside Outfitter_Buy** — the `ui` field should show the currently-hovered item name (e.g. `ui=Oak Staff`). Memory scan needed for the item-cursor-index, then map index → item name via the shop's stock list. Same for Outfitter_Sell (your inventory) and Outfitter_Fitting (slot picker → item picker).
- [ ] **Cursor character label inside Outfitter_Fitting** — when picking which character to equip, ui should show `ui=Ramza` etc.
- [ ] **Confirm dialog detection** — most sub-actions have a "Buy 3 Potions for 60 gil?" yes/no modal. Memory scan for a flag that distinguishes confirm-modal-open vs item-list state, so input doesn't cascade into accidental purchase.

### Action helpers — TODO

- [ ] **`buy_item <name> [qty]` action** — current `buy` helper exists but probably needs updating once Outfitter_Buy item-cursor is mapped. Should: enter Buy submenu if not already, locate item by name in stock, navigate cursor, set quantity, confirm.
- [ ] **`sell_item <name> [qty]` action** — same, but for Sell submenu.
- [ ] **`equip_item <unit> <slot> <name>` action** — Outfitter_Fitting flow: pick character, pick slot, pick item, confirm.
- [ ] **`hire_unit [job]` action** — Warriors' Guild Recruit. May need to surface the random recruit's stats so Claude can decide.
- [ ] **`dismiss_unit <name>` action** — Warriors' Guild Dismiss.
- [ ] **`rename_unit <old> <new>` action** — Warriors' Guild Rename. Text input is hard; deferred until we have a key-to-character mapping helper.
- [ ] **`process_carcass <name>` action** — Poachers' Den Process Carcasses (turns monster carcass into rare item).
- [ ] **`sell_carcass <name>` action** — Poachers' Den Sell Carcasses.
- [ ] **`read_rumors` / `read_errands` action** — Tavern dialogue scrape. Returns the current rumor/errand text so Claude can react and decide.
- [ ] **`save_game [slot]` / `load_game [slot]` actions** — wraps the Save Game flow once detected.

### Shop-stock data — TODO

- [ ] **Read shop stock from memory** — each shop has an inventory of items it sells, varying by location and story progress. Find the stock array in memory so `buy_item` can reference items by name without hardcoding per-shop tables.
- [ ] **Read player inventory for Sell** — for the Sell submenu, surface what the player owns and at what price.

### Documentation

- [x] **Shopping.md instruction guide** — DONE 2026-04-14. Initial version covers detection and ValidPaths flow; will need revisions as action helpers (`buy_item`, etc.) land.

---

## 10.5. State Naming Convention (P1)

- [ ] **Normalize screen state names to CamelCase (no underscores)** — the codebase currently mixes conventions: `Outfitter_Buy`, `Battle_MyTurn`, `SettlementMenu`, `PartyMenu`, `WorldMap`. Standardize on **CamelCase with no separators** (`OutfitterBuy`, `BattleMyTurn`, `SettlementMenu`, `PartyMenu`, `WorldMap`). This affects `ScreenDetectionLogic.cs` return values, `NavigationPaths.cs` dispatch keys, every test that asserts on screen names, and `ShopGilPolicy`. Rename in one batched commit so the churn is contained. Also update instruction docs (Shopping.md, BattleTurns.md, WorldMapNav.md) that reference the state names.

---

## 10.6. Party Menu (P1)

Captured 2026-04-14 from user screenshots. State machine already exists in `ScreenStateMachine.cs` but detection from memory only fires `PartyMenu` — all nested screens are inferred from key history and drift easily. Needs real memory-driven detection + ui labels + data surfacing.

### Party Menu hierarchy

```
PartyMenu ──Enter on unit──► CharacterStatus ──Enter on sidebar──► EquipmentAndAbilities / JobSelection / CombatSets
   │                              │                                     │
   │                              │                                     └──Enter on ability slot──► SecondaryAbilities / ReactionAbilities / SupportAbilities / MovementAbilities (Primary is job-locked, no-op)
   │                              │                                     └──Enter on equipment slot──► EquippableWeapons / EquippableShields / EquippableHeadware / EquippableCombatGarb / EquippableAccessories
   │                              └──Enter on Job──► JobSelection ──Enter──► JobActionMenu ──Right+Enter──► JobChangeConfirmation
   │                                                                    └──Left+Enter──► LearnAbilities (TBD)
   └──Tab change──► Inventory / Chronicle / Options
```

### Detection — TODO

- [x] **`PartyMenu` top-level tabs** — DONE 2026-04-14. Uses `ScreenStateMachine.Tab` (driven by Q/E key history, now wraps both directions) to resolve detection to `PartyMenu` / `PartyMenuInventory` / `PartyMenuChronicle` / `PartyMenuOptions`. Memory scan for a tab-index byte was inconclusive — heap diff found 2029 candidates with the right 0/1/2/3 shape but none survived re-verification (UE4 widget heap reallocates per keypress). State-machine-driven detection is the working answer. Each tab has its OWN screen name (not just `PartyMenu ui=<tab>`) because the content differs entirely per tab:
  - `PartyMenu` — Units tab (the roster grid; covered below)
  - `PartyMenuInventory` — Inventory tab (item catalog; covered below)
  - `PartyMenuChronicle` — Chronicle tab (lore/events browser; covered below)
  - `PartyMenuOptions` — Options tab (save/load/settings; covered below)
  Shared ValidPaths across all four: `NextTab` (E wraps), `PrevTab` (Q wraps), `WorldMap` (Escape back out).
- [ ] **Full roster grid on `PartyMenu` (Units tab)** — surface the whole 5-column roster in the state response, NOT one unit at a time. Same principle as scan_move: one round-trip beats N cursor-move + re-read cycles. Design:
  - **Grid layout:** strictly 5 columns, rows flex based on roster count. Max roster = 50 units (16/50 shown in screenshot header — that's `current/max`). Omit empty slots; don't emit `(row, col) empty` placeholders.
  - **Cursor wrap:** horizontal per row. Left at (0,0) wraps to (0,4) if row 0 has 5 units, otherwise to the last unit in row 0. Right at last-in-row wraps to first-in-row. Up/Down navigate rows as expected (no vertical wrap per current ScreenStateMachine tests).
  - **Concise format per unit (default):** `(row, col) [*]Name Lv.N Job HP=h/maxh MP=m/maxm`. `*` marks current cursor. Example:
    ```
    (0,0) *Ramza Lv.99 Gallant Knight HP=719/719 MP=138/138
    (0,1) Kenrick Lv.99 Monk HP=477/477 MP=115/115
    ...
    ```
  - **Verbose format (add `-v` flag to screen command, matching scan_move's -v):** include Brave, Faith, CT, statuses, current equipment, reaction/support/movement abilities, learned job list. Deferred until memory scans for those fields land.
  - **Grid movement hints:** include a small `navHints` block showing where each arrow goes from the current cursor:
    ```
    From (0,0) Ramza:
      Right → (0,1) Kenrick
      Down → (1,0) Lavian
      Left → (0,4) Alicia [wraps]
      Up → no-op [top row]
    ```
    Let Claude plan multi-step navigation in one glance.
  - **Roster capacity:** show `16/50` so Claude can decide whether recruiting is viable.
  - **Screen real-estate:** match the game's own 5-wide grid so Claude's mental model aligns with what would render on screen.
- [ ] **`PartyMenuInventory` tab** — captured 2026-04-14 (SS3). Full item catalog the player owns across all categories (weapons, shields, helms, armor, accessories, consumables). Screenshot shows Weapons tab with columns `Item Name | Equipped/Held`. Right pane shows hover'd item's full description + WP/element/range/effect. State name: `PartyMenuInventory` with `ui=<item name>`. ValidPaths: ScrollUp/Down, ChangePage (cycles sub-tabs — Weapons, Shields, Helms, etc. per the `<V> Change Page` hint in bottom right), Back (Escape → WorldMap), NextTab/PrevTab (wraps to Chronicle/Units). Multi-page: SS3 shows "1/3" indicator — state should surface current page + total pages. Memory scans needed: active inventory category, cursor row, page number.
- [x] **`PartyMenuChronicle` tab** — DONE 2026-04-14. State machine tracks `ChronicleIndex` (0-9 flat) over the 3-4-3 grid (Encyclopedia/StateOfRealm/Events / Auracite/Reading/Collection/Errands / Stratagems/Lessons/AkademicReport). `screen.UI` surfaces tile name (`Encyclopedia`, `Auracite`, etc.). Verified row transitions live: Encyc→Auracite, SoR→Reading, Events→Collection, Errands→Akademic (last col wraps left), Akademic→Collection (up). Memory hunt for the cursor address failed (UE4 widget heap reallocates per keypress producing false positives — same wall as PartyMenuInventory — see `project_shop_stock_array.md`). Each tile opens its own sub-screen via Enter, surfaces as `ChronicleEncyclopedia`/`ChronicleStateOfRealm`/etc. Sub-screens currently model only the boundary (Escape back) — inner-state navigation (Encyclopedia tabs, scrollable lists, etc.) is deferred to §10.7 below.
- [x] **`PartyMenuOptions` tab** — DONE 2026-04-14. State machine tracks `OptionsIndex` (0-4 vertical, wraps both directions). `screen.UI` surfaces action name (`Save`, `Load`, `Settings`, `Return to Title`, `Exit Game`). Enter on Settings opens new `OptionsSettings` screen (boundary only). Save/Load/ReturnToTitle/ExitGame Enter actions don't open sub-screens via the state machine — those flows are handled by their own existing systems (`save`/`load` actions, title-screen/quit sequences not yet modelled).
- [x] **`CharacterStatus` sidebar** — DONE 2026-04-14. `screen.UI` populated from `ScreenStateMachine.SidebarIndex` (now wraps both directions). Reads "Equipment & Abilities" / "Job" / "Combat Sets". No memory scan needed — sidebar is purely keyboard-driven and the state machine tracks Up/Down reliably.
- [x] **Equipment Effects toggle (`R` key on `EquipmentAndAbilities`)** — DONE 2026-04-14. State machine tracks `EquipmentEffectsView` (toggled by `R`); CommandWatcher surfaces it as `equipmentEffectsView` boolean on the screen response. Resets when leaving the screen. Effects panel TEXT scrape (e.g. "Permanent Shell", "Immune Blindness") still TODO — needs a memory scan or widget hook. Sub-bullets below also done:
  - Default view: `ui=<highlighted item or ability name>` (current spec).
  - Effects view: new sub-state or a flag like `EquipmentAndAbilities ui=EquipmentEffects view=Effects`. The bottom-right hint reads `[R] Equipment Effects` in the default view and `[R] View Equipment` in the effects view — confirming it's a binary toggle on the same screen.
  - ValidPaths: add `ToggleEffectsView` action that wraps the `R` key. Detection: needs a memory scan for the view flag (binary). Scrape the effects panel text as its own payload field when the flag is on.
- [x] **Full stats panel toggle (`1` key on `CharacterStatus`)** — DONE 2026-04-14. State machine tracks `StatsExpanded` (toggled by `1`); CommandWatcher surfaces it as `statsExpanded` boolean. Resets when leaving CharacterStatus. The actual stat NUMBERS (Move/Jump/PA/MA/PE/ME/WP-R/WP-L/Parry/etc.) still TODO — needs a memory scan for each stat's address.
  - Model as a view flag on `CharacterStatus`: `statsExpanded: true/false`.
  - When expanded, surface the full stat block in the screen response (NOT just the cursor label). This supersedes the "Full stat panel on CharacterStatus" entry below — the data IS already rendered numbers, just needs scraping when the flag is on.
  - ValidPaths: add `ToggleStatsPanel` action that wraps the `1` key.
  - Detection: needs a memory scan for the stats-expanded flag (binary).
- [x] **Character dialog (spacebar on `CharacterStatus`)** — DONE 2026-04-14. New state `CharacterDialog` detects via state machine. Only Enter advances (Escape is a no-op on dialogs in this game). Detection live-verified.
- [x] **Dismiss Unit flow (hold B on `CharacterStatus`)** — DONE 2026-04-14. Added `hold_key <vk> <durationMs>` action in CommandWatcher and `dismiss_unit` shell helper in fft.sh. When VK_B is held ≥3s on CharacterStatus, the state machine transitions to DismissUnit. Cursor defaults to Back (safe). `ui=Back/Confirm` reflects the toggle. Live-verified on Kenrick. Action helper `dismiss_unit <name>` (find unit, navigate to status, hold B, confirm) still TODO — current `dismiss_unit` only fires the held key, doesn't navigate.
- [x] **Rename `Equipment_Screen` → `EquipmentAndAbilities`** — DONE 2026-04-14 (screen name only; `GameScreen.EquipmentScreen` enum still legacy, renamed in the ScreenDetectionLogic → CommandWatcher mapper). The `ui=<highlighted item name>` inner-cursor work (Ragnarok / Escutcheon / Mettle / etc.) is still TODO — requires decoding the game's cursor position inside the two-column panel.
- [x] **New states for ability slots** — DONE 2026-04-14. `SecondaryAbilities` / `ReactionAbilities` / `SupportAbilities` / `MovementAbilities` detected via state machine routing on EquipmentAndAbilities Enter (right column rows 1-4). Row 0 (Primary action) is intentionally a no-op — the primary skillset is job-locked. All 4 pickers live-verified. Inner `ui=<skillset/ability name>` cursor decoding still TODO — needs the picker's selected-row address. The slot details below are now historical context:
  - `SecondaryAbilities` — ui=<skillset name> (Items, Arts of War, Aim, Martial Arts, White Magicks, Black Magicks, Time Magicks, ...). Screenshot showed Mettle + Items + Arts of War + Aim + Martial Arts + White/Black/Time Magicks. Primary action (row 0) is job-locked — no picker opens; change job via JobSelection to change the primary skillset.
  - `ReactionAbilities` — ui=<ability name> (Parry, Counter, Auto-Potion, ...).
  - `SupportAbilities` — ui=<ability name> (Magick Defense Boost, Equip Heavy Armor, Concentration, ...).
  - `MovementAbilities` — ui=<ability name> (Movement +3, Move-Find Item, Teleport, ...).
- [x] **New states for equipment slots** — DONE 2026-04-14. `EquippableWeapons` / `EquippableShields` / `EquippableHeadware` / `EquippableCombatGarb` / `EquippableAccessories` detected via state machine routing on EquipmentAndAbilities Enter (left column rows 0-4). All 5 pickers live-verified. `EquipmentSlot` enum added to ScreenStateModels.cs; CommandWatcher uses `CurrentEquipmentSlot` (captured at Enter time) to surface the correct slot-specific screen name. Inner `ui=<item name>` decoding still TODO. Slot details below are historical context:
  - `EquippableWeapons` — ui=<weapon name> (Ragnarok, Materia Blade, Chaos Blade, Blood Sword, Excalibur, Save the Queen, ...). Columns show Equipped/Held counts.
  - `EquippableShields` — ui=<shield name> (Escutcheon, Aegis Shield, ...).
  - `EquippableHeadware` — ui=<helm name> (Grand Helm, Crystal Helm, ...).
  - `EquippableCombatGarb` — ui=<armor name> (Maximillian, Carabini Mail, ...).
  - `EquippableAccessories` — ui=<accessory name> (Bracers, Genji Gauntlet, Reflect Ring, ...).
- [x] **Rename `Job_Screen` → `JobSelection`** — DONE 2026-04-14 (screen name only; `GameScreen.JobScreen` enum still legacy). The `ui=<job name>` inner-cursor work remains TODO — requires decoding the grid cursor inside JobSelection.
- [ ] **Existing nested states** — these already have ValidPaths, need detection rules and `ui=`:
  - `JobActionMenu` — modal with Learn Abilities (left) / Change Job (right). `ui=Learn Abilities` or `ui=Change Job`.
  - `JobChangeConfirmation` — yes/no after selecting Change Job. `ui=Confirm` / `ui=Cancel`.
  - `EquippableItemList` (currently `EquipmentItemList`) — already has ValidPaths; add `ui=<item name>`.
- [x] **`CombatSets` state** — DONE 2026-04-14 (boundary detection only). Pressing Enter on the third sidebar item now transitions to `CombatSets` in the state machine; Escape returns to CharacterStatus. Inner navigation NOT modeled — user explicitly opted to defer (loadouts feature not in use). Add Up/Down/Enter handlers when needed.

### Data surfacing — TODO

- [x] **Full roster grid on `PartyMenu` (Units tab)** — 2026-04-14 landed slot-indexed list with slot, name, level, job, brave, faith. Empty-slot rule verified = `unitIndex != 0xFF && level > 0`. **Display order solved 2026-04-14 (session 13)**: roster byte `+0x122` (1 byte per slot) holds each unit's 0-indexed grid position under the game's current Sort option (default: Time Recruited). Discovered by dumping all 14 active slots' first 600 bytes and scanning for a strictly-monotonic ranking — offset 290 (0x122) was a perfect `[0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13]`. Verified live per-slot: Ramza s0=0, Kenrick s1=1, ..., Mustadio s11=4 (displays 5th in grid, before Reis s6 which has DisplayOrder=12). **NOW DELIVERED:** sorted list matches visible grid, cursor `(row, col)` tracked by state machine, `ui=<hovered name>`, drill-in to any unit surfaces that unit's real loadout/abilities/stats. fft.sh compact mode renders a 5-col grid matching the game's layout with `cursor->` gutter; `screen -v` dumps raw JSON with `gridCols`, `gridRows`, `cursorRow`, `cursorCol`, `hoveredName`, and `displayOrder` per unit. See `RosterReader.DisplayOrder`, `RosterReader.GetSlotByDisplayOrder`, `ScreenStateMachine.ViewedGridIndex`.
  - **HP/MP still not in roster.** Scanned Ramza's full 0x258 bytes for his displayed HP (719 = 0x02CF) and MP (138 = 0x008A): zero matches. Theory: runtime-computed from job base + equipment bonuses, OR stored in a separate per-slot live stats table in the UE4 heap. Partial answer via the hovered-unit heap array (BATTLE_MEMORY_MAP.md §19) — populated for a handful of units only. Future work: recompute from FFTPatcher formulas OR widget pointer-chain walk. Separate item below.
  - **Custom sort modes (Job / Level) — not yet tested.** The `+0x122` byte is re-written by the game when the player changes the Sort option, so display order stays accurate under any sort — but `IsRamza` in the state machine still assumes grid-pos-0 === Ramza, which breaks under Level sort (multiple lv99 units tie; game picks one deterministically). If non-default sort becomes a goal, resolve `IsRamza` via slot identity instead of grid position. Documented inline in `ScreenStateMachine.HandlePartyMenu`.
- [x] **Viewed-unit identification on EquipmentAndAbilities** — DONE 2026-04-14 (session 13). Resolved purely from the state machine: cursor (row, col) on PartyMenu → grid index (row × 5 + col) → roster slot whose `+0x122` byte equals that grid index (see `ScreenStateMachine.ViewedGridIndex`, `RosterReader.GetSlotByDisplayOrder`). Zero heap scan, zero AoB — the display-order byte lives in the stable roster array at `0x1411A18D0`. Previous plans (a/b/c above in the history) are no longer needed. The hovered-unit heap array from BATTLE_MEMORY_MAP.md §19 is still useful IF we want runtime HP/MP (not stored in the roster), but it's now a separate concern.
- [~] **Unit summary on `PartyMenu`** — name / level / job / brave / faith / JP surface correctly since 2026-04-14 session 13 (display-order + viewed-unit fix). Still missing: HP / maxHP / MP / maxMP / CT. HP/MP are NOT in the roster (see grid note above — runtime-computed). CT is a battle-only concept. Path forward: either (a) recompute HP/MP from the FFTPatcher job-base + equipment-bonus formulas using `ItemData.cs`, or (b) re-visit the hovered-unit heap array (BATTLE_MEMORY_MAP.md §19) which holds computed HP/MP for a handful of units at session-specific addresses. (a) is more robust (works for every unit) and no heap scan needed; (b) is quicker to ship for whichever units the game has populated.
- [ ] **Full stat panel on `CharacterStatus`** — the header shows far more numbers than the party grid. The small icons on the right (7 20 24, 3 16 50%, 11 10% 0%, 75%, etc.) are attack/defense/magick/evade/movement/jump/zodiac/element stats. Decode and label each.
- [ ] **Element resistance grid** — the colored symbols on the right side of CharacterStatus show elemental absorb/null/halve/weak. Decode from memory.
- [ ] **Equipped items with stat totals on `EquipmentAndAbilities`** — the "Equipment Effects" summary under the two columns aggregates stats from the current loadout. Surface as `equipmentStats: { hpBonus: X, paBonus: Y, ... }`.
- [x] **JP totals per job on `JobSelection`** — DROPPED 2026-04-15 session 15 per the "What Goes In Compact vs Verbose vs Nowhere" principle above. Claude doesn't need 19 JP values to make a job-change decision; hovering a cell already shows Lv + JP in-game (info panel). Reconsider only if a concrete decision flow emerges that needs the full grid in one round trip.
- [x] **Ability list with learned/unlearned inside picker screens** — DONE 2026-04-14. `screen.availableAbilities` surfaces the full learned list for SecondaryAbilities (unlocked skillsets), ReactionAbilities (19 for Ramza), SupportAbilities (23), MovementAbilities (12). SecondaryAbilities puts the equipped skillset first (matches game's default cursor); other pickers use canonical ID-sorted order with the equipped ability marked in place. Decoded via roster byte 2 of the per-job bitfield at +0x32+jobIdx*3+2 (MSB-first over each job's ID-sorted passive list — see `ABILITY_IDS.md` and `RosterReader.ReadLearnedPassives`). JP cost + "unlearned-but-could-be-learned" still TODO — requires a separate learnable-set, not just learned-set.

### ValidPaths — TODO

- [ ] **`PartyMenu` tab switch actions** — `OpenInventory`, `OpenChronicle`, `OpenOptions`, `OpenUnits` in addition to the existing CursorUp/Down/Left/Right/SelectUnit/WorldMap.
- [ ] **`EquipmentAndAbilities` directional semantics** — left column = equipment picker, right column = ability picker. Add named actions like `FocusEquipmentColumn` / `FocusAbilitiesColumn` that wrap Left/Right.
- [ ] **`Equippable_*` screens** — ScrollUp/Down / Select / Cancel, plus `ChangePage` (Tab key in game) to cycle item categories if that's how it's presented. Screenshot 3 shows `<V> Change Page` hint.
- [ ] **`JobSelection`** — grid nav (Up/Down/Left/Right), Select (opens JobActionMenu), Back.
- [~] **EquipmentAndAbilities action helpers** — declarative one-liners that wrap the full nav flow. All helpers are **locked to the EquipmentAndAbilities state** — they error out with a clear message anywhere else. All helpers are **idempotent**: if the target is already in the slot, they no-op with "already equipped". All helpers **validate**: the ability must be in the unit's learned list (surfaced via `screen.availableAbilities` on pickers); the equipment must be in inventory. Session 13 (2026-04-14) landed the ability helpers; equipment helpers are stubbed pending ItemInfo / inventory work.
  - [x] `change_reaction_ability_to <name>` — shipped session 13.
  - [x] `change_support_ability_to <name>` — shipped session 13.
  - [x] `change_movement_ability_to <name>` — shipped session 13.
  - [x] `change_secondary_ability_to <skillsetName>` — shipped session 13.
  - [x] `remove_ability <name>` — unequip a passive by re-pressing Enter on the already-equipped entry (the in-game unequip idiom).
  - [ ] `change_right_hand_to <itemName>` — stub ("Not implemented yet"), blocked on inventory reader.
  - [ ] `change_left_hand_to <itemName>` — stub.
  - [ ] `change_helm_to <itemName>` — stub.
  - [ ] `change_garb_to <itemName>` — stub. (The game calls this slot "Combat Garb" / "Chest".)
  - [ ] `change_accessory_to <itemName>` — stub.
  - [ ] `remove_equipment <slotName>` — stub.
  - [ ] `change_job_to <jobName>` — future, routes through JobSelection grid + JobActionMenu + JobChangeConfirmation.
  - [ ] `dual_wield_to <leftWeapon> <rightWeapon>` — future, requires Dual Wield support ability equipped.
  - [ ] `swap_unit_to <name>` — future, from any nested PartyMenu screen Q/E-cycles to the named unit.
  - [ ] `unequip_all` — future, clears every equipment slot.
  - [ ] `clear_abilities` — future, sets Secondary/Reaction/Support/Movement all to (none).

### Scan findings — what doesn't work for PartyMenu detection

Documented 2026-04-14 after spending ~45 min trying to find the tab-index and sidebar-index bytes via memory diff.

- **`module_snap` (main-module writable 0x140000000 range) does NOT contain PartyMenu UI state.** 4-way snapshots cycling through all 4 tabs produced only encounter-counter + camera-rotation false positives. Same result for CharacterStatus sidebar (Equipment→Job→CombatSets).
- **`heap_snapshot` (UE4 heap) contains the values but they're not stable.** Diffing gave 2029 candidates with the right shape (e.g. 0/1/2 for sidebar); strict filter narrowed to 36. Live re-verification showed ZERO of the 36 actually tracked the sidebar when cycled — the widget heap is reallocated per keypress, so addresses mean different things at different times.
- **What works:** `ScreenStateMachine` driven by key history (Q/E for tab, Up/Down for sidebar) reliably produces Tab + SidebarIndex. Use that to set `screen.Name` and `screen.UI` instead of scanning memory. This is what the current implementation does.
- **If you need this later (e.g. for robust recovery after state-machine drift):** consider (a) hooking the game's widget render function via DLL detour, (b) finding the PartyMenu widget's vtable and reading a stable field offset from each instance, or (c) parsing the `[FFT File Logger]` output which shows distinct `.uib` loads per screen (e.g. `ffto_bravestory_top.uib` loads when entering JobSelection). Naive byte-diff is the wrong tool.

### Known gaps

- [ ] **Rumors/Errands body text scrape depends on this** — Tavern Errands open the party menu for assigning units. Don't wire up errand acceptance until PartyMenu navigation is solid.
- [ ] **Recruit / Rename at Warriors' Guild depend on this** — both flows transition into the party menu for unit selection or text input. Same dependency.
- [ ] **Text input (for Rename)** — FFT lets you rename units letter-by-letter via an on-screen keyboard. Need a whole separate text-input state + action helper.

---

## 10.7. Chronicle Sub-Screen Inner States (P2)

Outer detection of the 10 Chronicle tile screens shipped 2026-04-14 (§10.6 above). Each sub-screen surfaces only the boundary (Escape back to PartyMenu Chronicle tab). Inner-state navigation is deferred; this section enumerates what each one needs.

- [ ] **`ChronicleEncyclopedia`** — 3-tab top header (Persons / Locales / Terms) + scrollable left list + right-pane description. ValidPaths needed: Q/E (cycle tabs), Up/Down (scroll list), Enter (no-op? or open detail), Escape (back). Inner state: `ui=<tab name> > <highlighted entry name>` (e.g. `Persons > Ramza Beoulve`). Needs entry text scrape from memory or widget cache.
- [ ] **`ChronicleStateOfRealm`** — political map showing faction control. Behaviour TBD on first visit. Probably static info display.
- [ ] **`ChronicleEvents`** — cutscene replay browser. Needs a scrollable list of unlocked events with `ui=<event title>`.
- [ ] **`ChronicleAuracite`** — auracite stone collection. Probably grid of stones; needs cursor + `ui=<stone name>`.
- [ ] **`ChronicleReadingMaterials`** — books + special lectures index. Likely a list with `ui=<book title>`.
- [ ] **`ChronicleCollection`** — bestiary / item encyclopedia. Tabbed (monsters / items / arts?) — TBD on first visit.
- [ ] **`ChronicleErrands`** — completed-errand log. Different from Tavern_Errands (which is accept-errand). List of past errands with `ui=<errand name>`.
- [ ] **`ChronicleStratagems`** — Master Daravon's Stratagems for Battle lecture. Probably a static text reader; needs Enter/Escape boundaries only.
- [ ] **`ChronicleLessons`** — Lessons in Leadership lecture. Same shape.
- [ ] **`ChronicleAkademicReport`** — Akademic Report lecture. Same shape.
- [ ] **`OptionsSettings`** — audio / video / input config. Multi-section settings menu. Low priority for Claude-playing — only matters if we want Claude to verify text speed / autosave settings.

For all of these, the same memory-hunt limitation applies: UE4 widget heap reallocates per keypress, so byte-diff approaches produce false positives (verified during the §10.6 Chronicle hunt — see `project_shop_stock_array.md`). State-machine cursor tracking + drift recovery is the working pattern. Inner-text scraping (e.g. Encyclopedia entry text) needs a different approach — possibly reading from the rendered UI widget while the menu is open.

**Update 2026-04-14:** The **ability pickers** inside EquipmentAndAbilities escaped this wall by using a static-table decode instead of heap hunting — the roster's per-job bitfield at +0x32+jobIdx*3 contains byte 2 tracking learned passives MSB-first over each job's ID-sorted passive list. Zero heap search needed. Applying the same technique to Chronicle sub-screens won't work directly (those surface story text, not per-unit state), but **when a sub-screen's data is driven by persistent save state**, look for a per-unit or global bitfield before reaching for widget scans.

---

## 11. ValidPaths — Complete Screen Coverage (P2)

- [ ] Settlement menu, Outfitter, Tavern, Warriors' Guild, Poachers' Den
- [ ] Save/Load screens
- [x] Chronicle tab + sub-tile detection — done 2026-04-14, see §10.6 / §10.7

---

## 12. Known Issues / Blockers

### Missing Screen States
- [ ] **Battle_Cutscene** — Mid-battle cutscenes. Need to distinguish from regular cutscenes.
- [ ] **SaveScreen / LoadScreen** — Indistinguishable from TitleScreen with static addresses.
- [ ] **Settlement** — Indistinguishable from TravelList with static addresses. Could use location-based heuristic.
- [ ] **`Battle_Objective_Choice`** [P0 — gameplay-affecting] — some story battles open with a pre-battle dialogue that forks the win condition. Examples recalled from prior playthroughs: "We must save Agrias, protect her at all cost" vs. "Focusing on defeating all enemies is priority". Picking the first changes the objective to `Protect Agrias — battle ends if she's KO'd`; picking the second leaves the standard `defeat all enemies` objective. New state distinct from `Battle_Dialogue` (which is advance-only): `Battle_Objective_Choice` with two Y/N-style options, `ui=<option A text>` / `ui=<option B text>` based on cursor. ValidPaths: `Confirm` (Enter), `CursorUp/Down` (or Left/Right — verify live). Memory scan needed: (a) discriminator for this modal vs. regular `Battle_Dialogue`, (b) cursor index, (c) option text scrape (same FString problem as shop items). Priority HIGH because picking blindly can permanently fail the battle — Claude needs to SEE the options and decide.
- [ ] **`Recruit_Offer` modal** — end-of-battle: a defeated/befriended enemy offers to join your party (e.g. "Orlandeau wants to join your party"). Accept adds them to the roster; decline loses them forever (story-character one-shot). Possibly uses the same detection as `Battle_Objective_Choice` if both are driven by the same underlying modal system — check during scanning. New state: `Recruit_Offer` with `ui=Accept` / `ui=Decline`, ValidPaths `Confirm` / `Cancel` / `CursorUp/Down`. Also HIGH priority: wrong choice loses a unit permanently.

### Screen Detection Edge Cases
- Battle_Paused false positives: pauseFlag=1 stale after facing confirmation
- Settlement/shop screens not detected yet
- Menu cursor unreliable after animations

### Screen Detection Rewrite (P0) — identified 2026-04-14 audit
Comprehensive 45-sample audit of `ScreenDetectionLogic.Detect` revealed the detection layer is the root cause of most UI navigation bugs ("Auto-Battle instead of Wait", cursor desync, broken world-side detection). Full data in `detection_audit.md` in repo root. Key findings in `BATTLE_MEMORY_MAP.md` §12.

**Root causes:**
- `menuCursor` is overloaded (different meaning per context: action menu vs submenu vs targeting vs pause)
- `battleMode` is overloaded (encodes cursor-tile-class, not screen submode)
- `encA/encB` are noise counters — every rule using them is a coincidence-detector
- `gameOverFlag` is sticky process-lifetime — rules requiring `gameOverFlag==0` fail after first GameOver
- `rawLocation==255 → TitleScreen` rule preempts valid world-side screens (WorldMap/TravelList/PartyMenu all fall through wrongly)
- Two distinct TitleScreen states exist (fresh process vs post-GameOver) with different memory fingerprints

**Fix tasks:**
- [ ] **Delete `Battle_AutoBattle` rule** — UI label on Battle_MyTurn already handles cursor=4 correctly. Fixes "Auto-Battle instead of Wait" bug (TODO #1 in handoff).
- [ ] **Collapse `Battle_Casting` into `Battle_Attacking`** — byte-identical in memory. Track cast-time via `_lastAbilityName` + ability ct lookup client-side. Fixes "Used vs Queued" response text bug.
- [ ] **Tighten `TitleScreen` rule** — require full uninit sentinels (`slot0==0xFFFFFFFF && battleMode==255 && eventId==0xFFFF && ui==0`). Current rule catches too much.
- [ ] **Reorder rules** — specific rules (PartyMenu via `party==1`, EncounterDialog, LoadGame, LocationMenu) must run BEFORE the TitleScreen catch-all.
- [ ] **Remove `encA/encB`-dependent rules** — replace Battle_Victory / Battle_Desertion / EncounterDialog discriminators with stable signals (`paused`, `submenuFlag`, `acted/moved` combos).
- [ ] **Remove `gameOverFlag==0` requirement from post-battle rules** — treat as sticky, use other signals.
- [ ] **Fix Battle_Dialogue / Cutscene `eventId` filter** — change from `< 200` to `< 400 && != 0xFFFF` (caught eventId=302 at Orbonne pre-battle).
- [ ] **Add `LoadGame` rule** — `gameOverFlag==1 && paused==0 && battleMode==0 && acted==0 && moved==0 && rawLocation==255`.
- [ ] **Add `LocationMenu` rule** — `rawLocation in 0-42 && !inBattle` for shops/services/sub-battles.
- [ ] **Add `Battle_ChooseLocation` discriminator** — requires location-type annotation (which location IDs are multi-battle campaign grounds vs villages). Add to `project_location_ids_verified.md`.
- [ ] **Scope `menuCursor` interpretation** — only treat as action-menu index when `submenuFlag==0 && team==0`. Inside submenus, rely on `_battleMenuTracker`.
- [ ] **Memory scan for WorldMap vs TravelList discriminator** — these are byte-identical in current 18 inputs. Need a menu-depth or focused-widget address.
- [x] **Memory scan for shop-type discriminator** — DONE 2026-04-14. shopTypeIndex at 0x140D435F0 distinguishes Outfitter/Tavern/WarriorsGuild/PoachersDen at LocationMenu. Outfitter sub-actions (Buy/Sell/Fitting) further split by shopSubMenuIndex at 0x14184276C. Save Game and other shop sub-actions still TODO — see Section 10.
- [ ] **Add `hover` to ScreenDetectionLogic inputs** — currently read in DetectScreen but not passed through. May help disambiguate world-side states.
- [ ] **Rename `clearlyOnWorldMap` to `atNamedLocation`** — the current name is actively misleading (it's TRUE when at a shop/village, not on the open world map).

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
