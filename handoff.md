# Session Handoff — 2026-04-14 (Session 12)

Delete this file after reading.

## TL;DR

PartyMenu → CharacterStatus → EquipmentAndAbilities is now a **first-class decision surface** for Claude. Cursor position, three-column layout matching the in-game UI, full learned-ability lists for all four pickers, hover detail panel with descriptions and usage conditions. The entire "Claude has to memorize item stats out-of-band" problem went away.

Biggest individual win: **byte 2 of the per-job roster bitfield is now fully decoded.** The `project_roster_learned_abilities.md` note from a past session flagged byte 2 as "fuzzy — possibly reaction/support/movement/mastery flags, several bits appear to always be set". It's neither fuzzy nor always-set — it's MSB-first over each job's ID-sorted passive list. Verified 100% live against Ramza's 19-reaction picker.

## Commits this session (on `auto-play-with-claude`, 5 commits, not yet pushed)

```
0425407 EquipmentAndAbilities: hover detail panel + cursor row marker
9f6fa7f docs: document byte-2 passive decode + per-job passive tables
dd1eca4 PartyMenu pickers: decode all learned passives via roster byte 2
6caa642 PartyMenu: ability pickers availableAbilities + two-column loadout view
1a44eed PartyMenu: Chronicle/Options nav + ui=<ability> + abilities loadout
```

## What To Work On Next

**Top of the queue — picker cursor row tracking** (the last piece of the PartyMenu picker UX). Current state: `screen.availableAbilities` lists all 19 reactions for Ramza in canonical order with the equipped one flagged, but `ui=<highlighted>` on a picker screen doesn't update as the player scrolls — it stays pinned to whatever was equipped when the picker opened. Picker-cursor row is what's missing.

Known-difficult: the existing `memory/project_submenu_cursor.md` note says the battle-time abilities submenu cursor has no static address — we use the counter delta at `0x140C0EB20`. Same technique may apply to PartyMenu pickers OR the fresh investigation approach from this session (snap + diff on scroll) could surface a stable address that the earlier session missed.

**Also unblocked by this session:**

1. **Picker cursor → detail in real time** — once cursor row is tracked, `screen.uiDetail` naturally updates to the hovered ability. The pipeline already exists; just needs the right row.
2. **Full picker UX for Secondary** — currently Secondary picker only shows skillsets Ramza has learned; doesn't include Dancer/Onion Knight/etc. The bitfield bytes 0-1 decode (action abilities) already handles it at the ability level; we'd need to check whether the unit has the DC job unlocked without having learned any action abilities. Edge case, low priority.
3. **Viewed-unit detection** — every "Ramza-only" limitation (the loadout, the ability list, the picker lists) snaps into place for any unit the instant we solve this. Attempts this session (3-way intersect on doubled brave/faith) produced a false positive we caught mid-session. Approach is still viable, just needs a tighter anchor.

**Known bugs / TODOs to be aware of:**

- **Tab-switch desync** — `OpenChronicle` / `OpenOptions` style multi-press paths still race the game's tab-switch animation. State machine ticks per-key even if the UI doesn't consume the second key. Documented in TODO §0. Worked around in practice by using `NextTab` / `PrevTab` (single key) and verifying with `screen` between.
- **Heap-picker-list reader in `PickerListReader.cs` is dead code** — we left the file in the tree after the byte-2 decode replaced it. It's unreferenced but still compiles. Safe to delete in a cleanup pass; leaving for now so the pattern is documented in case a future session wants to revive heap scanning.

## Session 12 Recap

### Major wins

1. **Full PartyMenu tree navigation shipped.** Chronicle (10 sub-screens via 3-4-3 grid nav) + Options (5-item vertical list) detected with `ui=<tile name>` on root and state-machine tracking through every transition. Tests cover every grid edge (Encyc→Auracite, Events→Collection, Errands→Akademic wrap, Akademic→Collection up, etc.).

2. **EquipmentAndAbilities first-class.** Every cursor position surfaces:
   - `ui=<item or ability name>` — e.g. `ui=Ragnarok`, `ui=Magick Defense Boost` (with spaces preserved — fft.sh fix to not strip them)
   - `screen.loadout` — 6 equipment slots (unit name + R Hand / L Hand / Shield / Helm / Chest / Accessory)
   - `screen.abilities` — Primary/Secondary/Reaction/Support/Movement
   - `screen.uiDetail` — name, type, source job, WP/evade/range/HP/MP/pev/mev, description, usageCondition (auto-extracted from the tail of ability descriptions that contain "Usage condition:")
   - `screen.cursorRow` / `screen.cursorCol` — authoritative cursor location
   - Header line with name / job / level / JP matching the game's info bar

3. **Three-column layout in `screen` output** matching the game's UI:
   ```
   Ramza  Gallant Knight  Lv 99  JP 9999:
                  Equipment                                 Abilities                           Detail
                  R Hand: Ragnarok                          Primary:    Mettle                  Parry (Reaction)
                  Shield: Escutcheon (strong)               Secondary:  Items                     from Knight
                  Helm:   Grand Helm             cursor --> Reaction:   Parry                     Block physical attacks with the
                  Chest:  Maximillian                       Support:    Magick Defense Boost      equipped weapon.
                  Access: Bracer                            Movement:   Movement +3
   ```
   `cursor -->` lives in one of two gutters (left of Equipment column when col=0, left of Abilities column when col=1) so it visually points at the right column. Detail wraps at 40 chars.

4. **All four ability pickers working.** SecondaryAbilities, ReactionAbilities, SupportAbilities, MovementAbilities each surface `screen.availableAbilities` with every ability the unit has learned in canonical order. For Ramza: 15+ unlocked skillsets (Secondary), 19 reactions, 23 supports, 12 movements. Currently-equipped ability is flagged via `isEquipped:true`. Secondary puts the equipped skillset FIRST (matches game's default cursor); passive pickers keep canonical ID-sorted order with the equipped entry marked in place.

5. **🔑 Byte 2 of the roster bitfield DECODED.** Per-job at `+0x32 + jobIdx*3 + 2`. MSB-first over each job's ID-sorted passive list. Full per-job lists documented in `ABILITY_IDS.md` "Per-Job Passive Ability Order" and coded in `RosterReader.ReadLearnedPassives`. Verified 100% on Ramza: all 19 reactions he sees in-game decode cleanly from the bitfield. This closes out a "still fuzzy" note that was hanging in memory for multiple sessions.

6. **Bonus during investigation:** decoded the `0x5D9B52C0` global master-item list (228 purchasable items). Proved it's a GAME-WIDE list, not per-shop — after traveling to a different town it read identical bytes. Saved to `memory/project_shop_stock_array.md` in case it's useful for a future shop-stock pass.

### State machine additions

| Field | Type | Set by | Read by |
|---|---|---|---|
| `ChronicleIndex` | int (0-9) | Q/E tab switch resets; Up/Down/Left/Right via ChronicleUp/Down/Left/Right helpers | `screen.UI` on PartyMenuChronicle |
| `OptionsIndex` | int (0-4) | Up/Down wraps both directions | `screen.UI` on PartyMenuOptions |
| 10 new `GameScreen` enum values | — | Enter on ChronicleIndex cases | drift recovery + detector |
| `OptionsSettings` | `GameScreen` | Enter on Options idx=2 | (boundary only) |

### Memory addresses decoded this session

| Address | Stride | Purpose | Status |
|---|---|---|---|
| Roster `+0x32+jobIdx*3+2` | 1 byte per job | Learned passive bitfield (reaction + support + movement mixed, MSB-first over ID-sorted passive list) | **NEW: fully decoded** |
| `0x5D9B52C0` | u8, ~228 bytes | Global purchasable-item master list | Confirmed global, not per-shop |
| `0x1187E3124` | u32 | Tried at first as Try-then-Buy item ID | **False positive** — byte didn't re-track on follow-up |
| `0x7DB0xxxx` region | — | Shop widget cache with 24-byte records | Confirmed as render-time UI cache, shared refresh tags, not master storage |

### New files this session

| File | Purpose |
|------|---------|
| `ColorMod/GameBridge/PickerListReader.cs` | Heap AoB scanner for the picker widget's master list. **Dead code** after byte-2 decode — kept for pattern reference. Safe to delete. |

### Files changed this session

| File | Summary |
|---|---|
| `ColorMod/GameBridge/ScreenStateMachine.cs` | Chronicle 3-4-3 grid nav (ChronicleIndex), Options vertical list (OptionsIndex), tab-change resets, Up/Down/Left/Right handlers with explicit row-transition maps |
| `ColorMod/GameBridge/ScreenStateModels.cs` | 10 new Chronicle screens + OptionsSettings in `GameScreen` enum |
| `ColorMod/GameBridge/NavigationPaths.cs` | Chronicle/Options paths + DelayBetweenMs=300 on multi-press tab jumps (partial fix for race) |
| `ColorMod/GameBridge/RosterReader.cs` | `ReadEquippedAbilities` (Primary/Secondary/Reaction/Support/Movement from roster +0x07..+0x0D), `ReadUnlockedSkillsets` (20-job bitfield → unlocked skillsets), `ReadLearnedPassives` (byte-2 decode across all 20 jobs) |
| `ColorMod/Utilities/CommandBridgeModels.cs` | `Loadout`, `AbilityLoadoutPayload`, `AvailableAbility`, `UiDetail` types. New fields on `DetectedScreen`: `loadout`, `abilities`, `availableAbilities`, `uiDetail`, `cursorRow`, `cursorCol` |
| `ColorMod/Utilities/CommandWatcher.cs` | EquipmentAndAbilities screen population (loadout, abilities, ui= cursor resolve, uiDetail, cursor pos), picker screen population (availableAbilities + picker uiDetail), `BuildUiDetail` helper, `SplitUsageCondition` helper, `PrettyItemType` helper, `SkillsetOwnerJob` helper. Drift recovery + nested-screen detector expanded to include Chronicle/Options screens. |
| `Tests/GameBridge/ScreenStateMachineTests.cs` | 13 new tests for Chronicle 3-4-3 nav, Options vertical list, index-to-name mappings |
| `fft.sh` | Three-column EquipmentAndAbilities layout, `cursor -->` gutter marker, detail panel with 40-char wrap, `ui` label parse via node (to preserve spaces), abilities summary line, new compact/verbose equipment labels (Right Hand / Left Hand / Shield / Helm / Chest / Accessory) |
| `FFTHandsFree/TODO.md` | §0 new urgent-bug entry (tab desync). §10.6 Chronicle/Options marked DONE. §10.7 NEW ("Chronicle Sub-Screen Inner States"). "Ability list with learned/unlearned" marked DONE. |
| `FFTHandsFree/ABILITY_IDS.md` | New "Per-Job Passive Ability Order" table (19 jobs, ~78 passive IDs total) |
| `FFTHandsFree/UNIT_DATA_STRUCTURE.md` | Roster offset table extended with +0x32+jobIdx*3 learned bitfield and +0x80 currentJobJp |

### Memory notes added/updated

- `memory/project_roster_learned_abilities.md` — byte 2 fully decoded, experiments section added
- `memory/project_shop_stock_array.md` — master item list confirmed global, false-positive documented
- `memory/project_viewed_unit_tracking.md` — NEW, documents why state-machine cycling (not heap AoB) is the right fix for PartyMenu viewed-unit tracking
- `memory/project_inventory_widget_buffer.md` — NEW, inventory widget cache at 0x7DB0xxxx decoded (24-byte records, shared refresh tags, not master store)
- `MEMORY.md` — three new index entries

### Things that DIDN'T work (don't repeat)

- **Heap AoB for viewed-unit cursor** — three diff attempts this session, all dead-ends. UE4 widget churn defeats byte-diff. See `project_viewed_unit_tracking.md`.
- **Heap AoB for player inventory** — fourth attempt across multiple sessions. Persistent inventory is behind UE4 pointer chains. See `project_inventory_widget_buffer.md`.
- **Heap AoB for picker cursor row** — ran into the same wall. But we escaped via `RosterReader.ReadLearnedPassives` which uses static roster data, zero heap scan.
- **Heap AoB for Try-then-Buy item ID** — `0x1187E3124` false positive. Even 3-way intersects can mislead when heap churn provides enough random candidates.

### Environment gotchas

- **fft.sh `$R` variable strips whitespace** (`tr -d '\r\n '`) before grep parsing, which smashed "Magick Defense Boost" into "MagickDefenseBoost" in `ui=`. Fixed by reading the `ui` field via `node` from the unstripped file. Watch for other grep-derived fields that might have the same issue.
- **`cursor -->` with em-dashes and backticks in node `-e "..."` comments** broke bash parsing — had to use plain quotes in JS comments. Don't use backticks inside double-quoted heredoc-style bash blocks.
- **Game UI tab animations take ~300ms**. Multi-key paths that press Q/E more than once need `DelayBetweenMs >= 300`, or they race the animation and the game eats the second key. Even with delays, the state machine still desyncs when the game doesn't advance — real fix is per-key wait-for-confirmation, documented in TODO §0.

## Quick Start Next Session

```bash
# Catch up
git log --oneline -10       # 5 new session-12 commits on top of 46bf727
cat FFTHandsFree/TODO.md    # §10.6 / §10.7 for PartyMenu status, §0 for tab desync

# Baseline
./RunTests.sh               # 1833 passing

# Live smoke test
source ./fft.sh
boot                        # or `restart` if game state suspect
esc                         # → PartyMenu
screen                      # 16/50 units
execute_action SelectUnit   # → CharacterStatus on Ramza
enter                       # → EquipmentAndAbilities — should show three columns with cursor --> on R Hand

# Try the pickers
execute_action CursorRight  # to Abilities column
execute_action CursorDown; execute_action CursorDown   # to Parry (Reaction slot)
enter                       # → ReactionAbilities, lists 19 learned reactions with Parry [equipped]
esc; esc; esc               # back out

# If cursor-row picker work resumes, the recommended first test:
#   snap while on ReactionAbilities picker, scroll once, snap, diff.
#   Look for a byte that went N → N+1. Compare to battle-side counter
#   at 0x140C0EB20 — same technique may apply here.
```

## One More Thing

This session closed out a year-long-feeling puzzle (byte 2) with 10 minutes of careful bitfield analysis. The approach: collect one complete known-set (Ramza's 19 reactions from the picker), pull the 60 bytes of bitfield data (20 jobs × 3 bytes), then brute-force the decoding rule. First pass (by-type ordering) failed on Bard — Ramza had Movement+3 but the decoded bit said Fly. Second pass (by-ID ordering) matched 100%.

Lesson: when a byte's pattern "feels fuzzy", try structural hypotheses (order by type, order by ID, order by learn-cost, etc.) systematically against a known-good ground truth. Don't assume it's context-dependent until you've ruled out structural ordering.
