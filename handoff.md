# Session Handoff — 2026-04-16 (Session 20)

Delete this file after reading.

## TL;DR

**Screen state detection overhauled with SM-first architecture.** Key
press responses now return the state machine's state directly for
party-tree transitions (PartyMenu ↔ CS ↔ EqA ↔ pickers ↔ JobSelection
↔ Chronicle/Options sub-screens), eliminating every "first read is
stale" bug within that tree. Detection only runs as a fallback for
world-side transitions (WorldMap ↔ LocationMenu ↔ shops) where the SM
doesn't have full coverage.

**Also landed:** EqA cursor row resolver via unequip-diff trick,
`remove_equipment` atomic action, Inventory tab flag, and compact
output cleanup.

**5 commits** on `auto-play-with-claude`, all pushed:

```
aaadbc4 EqA row resolver: unequip-diff trick + remove_equipment action
5da81c2 Inventory tab flag + EqA promotion guard for PartyMenu drift
6112afe Screen detection overhaul: SM-first for party tree, detection fallback for world
b8d156b Add TravelList + LocationMenu to SM, disable EncounterDialog (sticky noise)
d24dc3e Add Chronicle/Options sub-screens to SM party-tree gate
```

**Tests: 2044 passing** (up from 2038 at session start). 6 new
detection tests added.

## What landed, grouped by theme

### 1. Screen detection overhaul (the headline)

The fundamental architecture changed. Before: every key press ran
full memory-based detection to determine the screen, which read stale
bytes 200-500ms after transitions and returned wrong states. Now:

- **Party-tree key responses**: `BuildScreenFromSM()` returns the
  state machine's state directly. No memory reads. The SM processes
  each key synchronously and always knows the correct screen.
- **World-side key responses**: Fall back to `DetectScreen()` with
  a 350ms settling delay. SM-sync corrects the SM when detection
  returns a different screen than the SM expected.
- **Observational `screen` reads** (no key press): Full detection
  runs as before — correction layer for drift.

Guards that prevent false state:
- Tab flags (0x140D3A41E, 0x140D3A38E) gate detection's PartyMenu
  return — only fires when `party==0` (stale byte)
- EqA mirror promotion blocked by: tab flags active, detection says
  world-side, SM already in party tree
- Stale-SM recovery gated on `LastSetScreenFromKey` flag
- All tab flag reads use a single cached read per cycle (TOCTOU fix)

### 2. SM coverage expanded

New `GameScreen` enum values: `TravelList`, `LocationMenu`.

| SM Transition | Handler |
|---|---|
| WorldMap + Escape → PartyMenu | existing |
| WorldMap + T → TravelList | **new** |
| TravelList + Escape → WorldMap | **new** |
| LocationMenu + Escape → WorldMap | **new** |
| Chronicle sub-screens (all 10) | **added to party-tree gate** |
| OptionsSettings | **added to party-tree gate** |

WorldMap + Enter does NOT transition in SM (can't distinguish
settlement from battle location) — falls through to detection.

### 3. EqA cursor row resolver

Cursor row lives in UE4 widget heap that reallocates per keypress —
confirmed nonexistent in stable memory across 4 diff test shapes
(row sweep, optimize equipment, V-page cycle, R-effects toggle).

Instead: unequip-diff trick. `DoEqaRowResolve(restore)` opens the
picker (Enter), toggles the equipped item (Enter), reads the mirror
diff to find which slot changed, then restores or keeps-empty.

Two strict-mode actions:
- `resolve_eqa_row` — resolves and restores (4 keys, ~1.5s)
- `remove_equipment_at_cursor` — resolves and leaves slot empty (3 keys)

Both paths verified: populated slot (Beowulf Shield→row 1) and
empty slot (Lloyd Right Hand→row 0 via inverse 0→X detection).

### 4. Inventory tab flag

`0x140D3A38E` = 1 when Inventory tab active, 0 otherwise.
Cross-session stable. Sibling to Units-bit at `0x140D3A41E`
(0x90 bytes apart in the same bitfield struct). Wired into
both detection logic and drift-correction.

### 5. Compact output cleanup

- `ui=` moved to second position (right after `[StateName]`)
- `status=completed` suppressed (always completed, no signal)
- `loc=`/`obj=`/`gil=` on same line as state name (was multi-line)
- Location names shortened to just the name (drop numeric ID)
- `inventory=` moved to verbose only, renamed to `items=N types, M total`
- `return 0` added to `fft()` (was leaking exit code 1 from chain-warning check)

### 6. Disabled auto-fire resolvers

- `ResolvePartyMenuCursor` no longer auto-fires on first PartyMenu
  `screen` read. It sent 8+ keypresses (Right/Left/Down/Up oscillation)
  that visibly bounced the cursor and never found a usable byte.
  Still available as explicit `resolve_party_menu_cursor` action.

### 7. EncounterDialog investigation

encA/encB counters are fundamentally unusable:
- Drift 0-2 during normal screen transitions (noise)
- Sticky from prior encounters (gap persists indefinitely)
- Gap threshold of 4+ still false-triggered during navigation

Session 20 diff at TheSiedgeWeald found candidate `0x140D87830`:
reads 10 during encounter, 0 on WorldMap, reverts after flee.
TODO added to wire as dedicated encounter flag. Needs cross-session
verification.

### 8. Story-class Primary fallback

`GetSkillsetName(pIdx)` returns null for story-class encodings
(Construct 8 Work=171). Falls back to
`GetPrimarySkillsetByJobName(matchedSlot.JobName)` when null.

## Live-verified states (session 20)

Every non-battle state reachable via the bridge was verified with
screenshot ground truth:

| State | Method | Result |
|---|---|---|
| WorldMap | boot/Escape | ✅ |
| TravelList | T from WorldMap | ✅ |
| LocationMenu | Enter at settlement | ✅ |
| Outfitter | Enter from LocationMenu | ✅ |
| OutfitterBuy | Enter from Outfitter | ✅ |
| OutfitterSell | Down+Enter | ✅ |
| OutfitterFitting | Down×2+Enter | ✅ |
| PartyMenu (Units) | Escape from WorldMap | ✅ |
| PartyMenuInventory | E from Units | ✅ |
| PartyMenuChronicle | E from Inventory | ✅ |
| PartyMenuOptions | E from Chronicle | ✅ |
| CharacterStatus | Enter from PartyMenu | ✅ |
| EquipmentAndAbilities | Enter from CS | ✅ |
| JobSelection | Down+Enter from CS | ✅ |
| SecondaryAbilities | Right+Down+Enter from EqA | ✅ |
| ReactionAbilities | Right+Down×2+Enter | ✅ |
| SupportAbilities | Right+Down×3+Enter | ✅ |
| MovementAbilities | Right+Down×4+Enter | ✅ |
| ChronicleEncyclopedia | Enter on Chronicle grid | ✅ |
| ChronicleStateOfRealm | Right+Enter | ✅ |
| ChronicleEvents | Right×2+Enter | ✅ |
| ChronicleAuracite | Down+Enter | ✅ |
| ChronicleReadingMaterials | Down+Right+Enter | ✅ |
| ChronicleCollection | Down+Right×2+Enter | ✅ |
| ChronicleErrands | Down+Right×3+Enter | ✅ |
| ChronicleStratagems | Down×2+Enter | ✅ |
| ChronicleLessons | Down×2+Left+Enter | ✅ |
| ChronicleAkademicReport | Down×2+Right×2+Enter | ✅ |
| OptionsSettings | Down×2+Enter from Options | ✅ |

## What's NOT done (top priorities for next session)

### 1. EncounterDialog detection

Disabled — needs `0x140D87830` wired as a dedicated encounter flag.
Cross-session verification required. See TODO in ScreenDetectionLogic.

### 2. Battle state verification

None of the 13+ battle states were tested this session. The SM-first
architecture only handles party-tree and world-side; battle states
still use detection exclusively. These need verification:
BattleMyTurn, BattleMoving, BattleAttacking, BattleAbilities,
BattleActing, BattlePaused, BattleWaiting, BattleEnemiesTurn,
BattleAlliesTurn, BattleVictory, BattleDesertion, BattleDialogue,
BattleFormation.

### 3. Compact output gaps

- WorldMap `ui=` should show hovered location name
- EqA `ui=` shows stale cursor row (SM key-tracking drifts)
- EqA compact format: two-column grid unreadable in narrow windows
- Outfitter `ui=` doesn't show Buy/Sell/Fitting hover

### 4. Chronicle vs Options discriminator

Still no memory byte distinguishes these tabs. Both flags at 0
during transitions caused spurious PartyMenuChronicle detection
(now disabled — SM doesn't guess when both flags are 0).

## Things that DIDN'T work (don't repeat)

1. **Byte-level poll-until-stable** — Stale bytes stabilize at wrong
   values before the game updates them. Polling raw bytes detects
   "stable" and exits, but the values are stale-stable.

2. **Detection-level poll-until-stable** — Same problem at a higher
   level. Detection stabilizes on a wrong result (TravelList instead
   of WorldMap) because the underlying bytes are stale-stable.

3. **encA/encB with any threshold** — Counters are sticky from prior
   encounters. Gap of 4+ from a previous fight persists indefinitely
   and causes false EncounterDialog triggers during normal navigation.

4. **Tab flags as unconditional PartyMenu override** — When `party==1`
   AND tab flag fires, the early return preempts the normal detection
   flow. This broke CharacterStatus/EqA detection because the
   stale-SM recovery then stomped the correct sub-screen back to
   PartyMenu.

5. **`LastSetScreenFromKey` set in SetScreen** — OnKeyPressed sets
   the flag, then key handlers assign `CurrentScreen` directly (not
   through SetScreen). Setting it in SetScreen was pointless.

## Things that DID work (repeat)

1. **SM-first for party-tree transitions** — The SM processes keys
   synchronously and is always correct for transitions it models.
   Returning its state directly eliminates all stale-byte issues.
   This is the right architecture.

2. **Detection as fallback, not primary** — Detection reads stale
   memory. Using it only when the SM can't handle the transition
   (world-side, shops, battle) limits the blast radius of stale bytes.

3. **SM-sync after detection** — When detection runs as fallback,
   syncing the SM to the detection result prevents the SM from
   carrying stale state into the next key press.

4. **Cached tab flag reads** — Reading the flags once and reusing
   across detection + correction blocks eliminates TOCTOU races
   where flags flicker during transitions.

5. **Screenshot ground truth** — `screenshot.ps1` as definitive
   proof of game state. Every fix was verified against screenshots.

## Memory notes saved this session

- **`project_inventory_tab_flag.md`** — `0x140D3A38E` = 1 on
  Inventory tab, 0 elsewhere. Cross-session stable.
- Updated **`project_eqa_equipment_mirror.md`** description: cursor
  row now via unequip-resolver (not "still unknown").

## Quick start next session

```bash
# Baseline check
./RunTests.sh                # 2044 passing

# Live smoke — test the SM-first architecture
source ./fft.sh
boot                          # if game isn't running

# Full navigation cycle (should be 8/8 correct):
key 27 Escape                 # [PartyMenu]
key 13 Enter                  # [CharacterStatus]
key 13 Enter                  # [EquipmentAndAbilities]
key 27 Escape                 # [CharacterStatus]
key 27 Escape                 # [PartyMenu]
key 27 Escape                 # [WorldMap]

# Tab cycling:
key 27 Escape                 # [PartyMenu]
fft '{"id":"e","keys":[{"vk":69,"name":"E"}],"delayBetweenMs":200}'
# Should cycle: Inventory → Chronicle → Options → Units
```

## Active TODO top of queue (next-session priority)

1. **EncounterDialog: wire 0x140D87830** — cross-session verify, add
   to ScreenAddresses, re-enable EncounterDialog rule
2. **Battle state verification** — enter a battle, verify all 13 states
3. **WorldMap ui= location name** — surface hover location as ui field
4. **EqA compact format** — narrow-friendly single-line layout
5. **Chronicle vs Options discriminator** — deferred memory hunt
