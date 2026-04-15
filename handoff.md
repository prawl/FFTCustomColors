# Session Handoff — 2026-04-15 (Session 18)

Delete this file after reading.

## TL;DR

**The big one: the player inventory store is cracked.** After 5+
sessions of failed hunts, I found it via a 2-snapshot diff on a
single Dagger purchase. It's a flat u8 array at `0x1411A17C0`
(272 bytes, one byte per FFTPatcher item ID), sitting exactly
272 bytes before the roster base. Stable, session-safe, trivial
to read.

**4 commits landed on `auto-play-with-claude`:**

```
93b5579 Sell-price overrides, OutfitterFitting inventory, SkillsetItemLookup
9287e5e OutfitterSell: full sellable-items listing with estimated prices
3087140 Normalize screen state names: drop underscores, use CamelCase
0438aca Inventory store cracked + desync fixes — live-verified
```

**Tests: 2000 passing** (was 1956 at start of session 18; +44 across
this session — +15 inventory, +9 ItemPrices, +13 SkillsetItemLookup,
+several desync / rename). Every change live-verified in-game.

Game is still running on PartyMenu Units tab (cursor on Ramza)
if you want to pick up mid-investigation.

## What landed, grouped by commit

### 1. Commit `0438aca` — Inventory crack + 6 desync fixes

**Inventory store (the headline):**
- New `InventoryReader.cs` wraps `0x1411A17C0` with `ReadRaw` /
  `ReadAll` / `ReadByType` / `GetCount` / `DecodeRaw`. 10 unit tests
  cover empty/null/unmapped-id/ordering.
- `screen.inventory` populated on every PartyMenu tab (state-machine
  can misname the tab, so we gate on `onPartyMenuAnyTab` = any of 4
  names rather than the exact one).
- Each entry: `{id, count, name, type}` with ItemData-resolved
  metadata. 184 unique items / 2189 total owned on the test save,
  including 159 katana stockpile and 1072 chemist consumables.
- `fft.sh`: compact `inventory=N unique / M total owned` on any
  screen where payload is populated. Verbose (`screen -v`) groups
  by item type with per-group totals.
- Memory note: `project_inventory_store_CRACKED.md` with the full
  investigation narrative + **the "single-byte diff" breakthrough
  technique** (key insight: if you can trigger a one-byte change
  via a minimal in-game action, the diff becomes trivially small).

**Six desync/drift fixes (first half of session, before the
inventory pivot):**
1. **PartyMenu tab delays 300→500ms.** The game's tab-switch
   animation eats the second key on multi-press jumps
   (OpenChronicle/OpenOptions/OpenUnits/OpenInventory). Bumped
   DelayBetweenMs on all 4 paths.
2. **CursorRow/Col reset on Q/E back to Units tab.** New
   `ResetUnitsCursorToOrigin()` in `ScreenStateMachine` runs
   whenever Q/E wraps/advances into the Units tab. Stops the
   "state says Orlandeau, game shows Ramza" drift from session 16
   repro.
3. **Upward drift recovery via `MenuDepth==0` gate.** New
   symmetric fix that snaps SM to WorldMap when raw detection
   says WorldMap/TravelList AND `MenuDepth==0` AND SM reports
   any party-tree screen for 3 consecutive reads. The MenuDepth
   gate is what makes this safe — session 16's earlier symmetric
   attempt was reverted because `party=0 && ui=1` also matches
   EquipmentAndAbilities; MenuDepth==0 is true only on outer
   screens so it distinguishes real WorldMap returns from nested
   residual.
4. **`ui=Move` leak on non-battle screens.** Was pre-populating
   UI from the action-menu cursor mapping, producing misleading
   labels like `[WorldMap] ui=Move`. Non-battle screens now get
   null UI unless their own per-screen logic populates something.
5. **CharacterDialog.ReturnToWorldMap leads with Enter.** Escape
   is a no-op on flavor dialogs — was silently failing. Now sends
   Enter (dismiss dialog) + 2 Escapes. New dedicated test.
6. **Three new ScreenStateMachine tests** covering Units-cursor
   reset on Q-wrap and E-wrap tab cycles.

### 2. Commit `3087140` — CamelCase state name normalization

**Renames per TODO §10.5.** All `Battle_*` and `Outfitter_*`
state names drop their underscores:

```
Battle_MyTurn       → BattleMyTurn
Battle_Acting       → BattleActing
Battle_Moving       → BattleMoving
Battle_Attacking    → BattleAttacking
Battle_Casting      → BattleCasting
Battle_Abilities    → BattleAbilities
Battle_Waiting      → BattleWaiting
Battle_Paused       → BattlePaused
Battle_Status       → BattleStatus
Battle_AutoBattle   → BattleAutoBattle
Battle_AlliesTurn   → BattleAlliesTurn
Battle_EnemiesTurn  → BattleEnemiesTurn
Battle_Dialogue     → BattleDialogue
Battle_Victory      → BattleVictory
Battle_Desertion    → BattleDesertion
Battle_Formation    → BattleFormation
Battle_Cutscene     → BattleCutscene
Battle_GameOver     → BattleGameOver
Battle_ChooseLocation → BattleChooseLocation
Battle_<Skillset>   → Battle<Skillset>  (dynamic via GetAbilityScreenName)
Outfitter_Buy       → OutfitterBuy
Outfitter_Sell      → OutfitterSell
Outfitter_Fitting   → OutfitterFitting
```

**Scope:** 30 files touched (19 code, 8 docs, 1 fft.sh, 2 DTO
comments). Symmetric diff — 334+/334-.

**Gotcha caught mid-flight:** `ScreenDetectionLogic.GetAbilityScreenName`
was still producing `Battle_{pascal}` for dynamic skillset states
(BattleBlackMagicks, BattleThrow, etc.). The sed missed it because
the underscore was in an interpolation template. Fixed in the same
commit. **If you ever do another rename like this, grep for
interpolation templates too.**

TODO.md historical references in the Completed Archive left
unchanged — they were accurate when written.

### 3. Commit `9287e5e` — OutfitterSell basic

**Ships Outfitter Sell inventory surface** using the inventory
store. Claude can view every sellable item in one read with
per-group gil subtotals.

**New: `ItemPrices.cs`** — a buy-price lookup sourced from
`FFTHandsFree/SHOP_ITEMS.md` (Sal Ghidos end-game stock). Keyed
by ItemData NAME not ID, so additions don't require ID guessing.
Static init resolves each name to an ItemData entry and builds
the id→price map; any name that fails to resolve lands in
`UnresolvedNames` and fails the suite via a dedicated test.

110 entries covering knives / swords / katanas / axes / rods /
staves / flails / books / poles / instruments / cloths / shields /
helms / body armor / robes / shoes / cloaks / bracelets / rings.
**Bags excluded** — IC remaster renamed them (Croakadile /
Fallingstar / Pantherskin / Hydrascale) from the PSX names
(Catskin / Proudhide / Hardscale); prices unverified for IC names.

**Sell price formula:** buy/2. **Live-verified NOT accurate** —
Dagger shows 50 in-game but we compute 100 (buy 200 / 2). No
consistent ratio across items. Surfaced to Claude via:
- Compact summary: `"~2,186,435 gil est (prices ~buy/2, not ground-truth)"`
- Verbose per-item: `sell~N gil` (tilde = estimated)

**InventoryReader extensions:** `InventoryEntry.SellPrice`
populated via `ItemPrices.GetSellPrice`. New `ReadSellable()`
filter returns only entries with known sell prices (for the
Sell screen, as opposed to `ReadAll` for PartyMenu).

**CommandWatcher:** `screen.inventory` now populates on
OutfitterSell in addition to PartyMenu tabs.

### 4. Commit `93b5579` — Sell overrides + OutfitterFitting + SkillsetItemLookup

**Three tasks ship in one commit:**

**(a) Sell-price override table.** New `SellPricesByName` dict
in `ItemPrices.cs` that OVERRIDES the buy/2 estimate when
populated. Seed set of 7 weapon prices captured live 2026-04-15
(Dagger=50, Mythril Knife=250, Blind Knife=400, Mage Masher=750,
Assassin's Dagger=2500, Broadsword=100, Longsword=250).

**User will send more values later.** Paste them into
`SellPricesByName` and they flow through
`InventoryEntry.SellPrice` → `InventoryItem.SellPrice` →
fft.sh automatically. `sell=` (verified) replaces `sell~`
(estimated) on rendered output.

New `IsSellPriceGroundTruth(id)` exposes the verified flag.
`InventoryItem.SellPriceVerified` and fft.sh render the
`=` vs `~` operator accordingly.

**(b) OutfitterFitting inventory surface.** Same `screen.inventory`
path as PartyMenu + Sell, but with `ReadAll()` (full list, not
sellable-only). Filter by slot type is **deferred** — requires
state-machine tracking of the Fitting picker depth
(unit→slot→item), which has its own drift problems.

fft.sh verbose trigger extended to include `OutfitterFitting`.

**(c) SkillsetItemLookup infrastructure.** New static class
`SkillsetItemLookup.cs` maps consumable-backed ability names
to inventory items:
- `ItemsAbilityToItemId` (Chemist): 14 entries, ID 240-253
- `IaidoAbilityToItemId` (Samurai): 10 entries, ID 38-47
- `ThrowAbilityToItemType` (Ninja): 10 entries, ItemData type strings

`TryGetHeldCount(skillset, ability, inventoryBytes)` returns the
held count for Items/Iaido (single-item lookup) or Throw
(type-sum across all owned items of that type). Returns null
for non-inventory skillsets so callers know to omit the heldCount
field entirely.

`AbilityWithTiles` DTO gains `HeldCount` (int?) and `Unusable`
(bool) fields.

**Battle wiring NOT landed.** `NavigationActions.cs` is 4000+
lines and end-of-session is the wrong time to touch it.
Infrastructure is ready; a 10-line wire-up at the point where
`AbilityWithTiles` objects get built will do it next session.

13 SkillsetItemLookupTests cover Items/Iaido/Throw paths, plural
tolerance (Eye Drop / Eye Drops), null inventory, non-inventory
skillsets.

## What's NOT done (top priorities for next session)

### 1. **Row-byte hunt — 6 candidates found, none verified yet**

The single most impactful remaining work. The PartyMenu cursor drift
bug is unsolvable until we have a ground-truth row byte in memory.

**What happened this session (partial progress, not committed):**
- Snapshot at PartyMenu (r1, c0) Agrias, move Down to r2 Orlandeau,
  snapshot, diff. **~700 changed bytes** in a single keypress (way
  more than inventory's 175 — cursor animations are noisy).
- Filtered for `01 → 02` transitions. **6 candidates:**
  `0x1407AC7CD`, `0x1407AC7D1`, `0x1418708CB`, `0x1437436BD`,
  `0x1437436C1`, `0x14374377B`.
- Read `0x1407AC7CD` at (r0, c0) expecting 0 — got 02. **First
  candidate FAILS the wrap-test.** The byte went 1→2→1→2 as I
  scrolled which is why it showed up in the diff, but it doesn't
  represent the literal row index.
- **Other 5 candidates UNTESTED** — ran out of time.

**Next-session plan (documented in `project_partymenu_row_byte_hunt.md`):**
1. **3-snapshot multi-step filter**. Snapshot at r0, r1, r2.
   A real row byte must show EXACTLY `0, 1, 2` across the three.
   That eliminates ~99% of the ~700 false positives that merely
   transition 1→2 without hitting 0 at r0.
2. **Verify the other 5 candidates** at (r0, c0). One of them
   might still be correct — I stopped after the first failed.
3. **Column byte hunt**: same technique with Left/Right, once
   row is solved.

If row+col are both found, the entire PartyMenu drift cluster
(the "state says X, game shows Y" class) can be fixed by reading
them into `ScreenStateMachine.SetPartyMenuCursor(row, col)` which
already exists from session 16.

### 2. **Battle wiring for Items/Throw/Iaido held counts**

Infrastructure is ready (commit 93b5579). What's left:
- Find the point in `NavigationActions.cs` where `AbilityWithTiles`
  objects get built for the active unit's ability list. Around
  lines 1720-2100 based on grep results.
- When the ability's skillset is Items/Throw/Iaido, call
  `SkillsetItemLookup.TryGetHeldCount(skillset, ability.Name,
  inventoryBytes)` and populate `HeldCount` + `Unusable = (count == 0)`.
- Needs an `InventoryReader.ReadRaw()` call once per scan and
  pass the byte array through to the lookup (don't call
  `ReadRaw` 50 times).
- Live-verify on a unit with Items secondary. **User said Ramza
  might have Items as secondary — check that first.**
- Unit test the wiring point.

**Estimated 1-2 hours of careful work.** `NavigationActions.cs`
is the most complex file in the repo — take it slow.

### 3. **Real sell prices (user will send them)**

User is collecting actual sell prices from the in-game Outfitter
Sell screen. When you get them:
1. Paste into `SellPricesByName` in `ColorMod/GameBridge/ItemPrices.cs`
2. Run `./RunTests.sh` — `UnresolvedNames` test catches any name
   typos against `ItemData.Items`
3. Done. The values flow through automatically.

Current seed set (session 18 live-captured from Gariland Weapons tab):
Dagger=50, Mythril Knife=250, Blind Knife=400, Mage Masher=750,
Assassin's Dagger=2500, Broadsword=100, Longsword=250.

**Caveat on naming:** shop display names sometimes differ from
ItemData names. Session 18 had to fix 12 mismatches during
`ItemPrices` build-out (Bizen Osafune → Osafune, Ame-no-Murakumo
→ Ama-no-Murakumo, Gokuu's Pole → Gokuu Pole, Hi-Potion vs High
Potion, singular "Bracer" vs shop "Bracers", etc.). The
`UnresolvedNames` test will tell you which names didn't match.

### 4. **State-machine detection drift on picker screens**

Untouched this session but re-observed: game visually on
EquippableWeapons picker but state machine reports
EquipmentAndAbilities. This happens after Enter from EqA doesn't
cleanly advance the SM. Root cause is probably the same
"Enter that doesn't register because animation is mid-flight"
pattern as the tab-switch delays we fixed.

Would benefit from the row byte first — if we have ground-truth
cursor data for PartyMenu, the SM can self-correct from memory
and stop compounding drift.

### 5. **Q/E tab-switch count bug**

Also re-observed this session: single E keypress sometimes
advances 2 tabs (Units → Chronicle, skipping Inventory). Might
be a double-fire from key-event handling OR a state-machine bug
where E is counted twice. Need to log each key fired and each
SM transition to catch it in the act. Not pursued this session.

## What's SHIPPED and production-ready

- **`screen.inventory` on PartyMenu (any tab), OutfitterSell,
  OutfitterFitting.** 184 items × 4 fields each. Compact summary
  + verbose grouped-by-type rendering.
- **`sell=N gil` vs `sell~N gil` distinction** on OutfitterSell.
  7 items verified, 103 estimated.
- **Ground-truth price override infrastructure** ready for paste.
- **CamelCase state names everywhere.** If you're writing new
  code, no more `Battle_XYZ` — just `BattleXYZ`.
- **6 desync fixes** eliminating common drift classes.

## Things that DIDN'T work (don't repeat)

### Session 18 failed approaches:

1. **Memory scan for shop sell prices (several attempts).** Found
   the buy-price table at heap `0x15B1B3300` but it's transient
   widget data, not persistent. Static buy prices exist somewhere
   but weren't located. Real fix was to just hardcode from
   SHOP_ITEMS.md.

2. **Hunting sell prices via u32 LE pattern `50, 250`.** The
   pattern shows up all over memory — too generic. `0x32 00 00 00
   FA 00 00 00` found 1 match but it was unstable heap data.

3. **Chasing vtable `0x7FFDCE029280` for picker TArray decoding.**
   Spent 45 min on this before pivoting. Turned out to be a UE5
   `UFunction` object, not a TArray vtable. The widget chain led
   through UE5 internals that were genuinely hard to walk. The
   inventory hunt via snapshot-diff was **10x faster** and found
   a completely different answer.

4. **Row-byte hunt with just 2 snapshots.** 700+ false positives
   is too much signal to scrub by hand. Need 3-snapshot filter.

5. **Direct `read_block` on guessed addresses.** Crashed the game
   once during the shop price hunt. Memory regions that look
   readable in the diff dump aren't always committed when you
   read them later.

### Session 18 successful techniques (do repeat):

1. **Single-byte-transition snapshot diff** for minimal-state
   changes (inventory count: 3→4 Dagger). 175 total changed
   bytes in the diff = trivial scan.

2. **Name-keyed lookup tables with ID resolution at static init.**
   `ItemPrices.BuyPricesByName` is pattern-worthy. Adding new
   entries can't cause ID drift.

3. **Diagnostic tests that surface integration gaps.** The
   `UnresolvedNames` test turned an invisible "typo in static
   table" into a build failure. Reused-pattern-worthy.

4. **Live-verify with screenshots BEFORE trusting state machine.**
   State-machine drift happens silently. Screenshots catch it.
   Session 18 used screenshots to verify OutfitterSell and
   OutfitterBuy state was actually live before committing.

## Memory notes saved this session

- **`project_inventory_store_CRACKED.md`** — full inventory hunt
  narrative + 2-snapshot technique + unblock paths
- **`project_partymenu_row_byte_hunt.md`** — session 18 row-byte
  investigation with the 6 candidate addresses and 3-snapshot
  next-session plan

## Updated MEMORY.md index

Top entry is now `project_inventory_store_CRACKED.md` marked with
the 🎯 breakthrough emoji.

## Quick start next session

```bash
# Baseline check
./RunTests.sh                # 2000 passing

# Live smoke — test what session 18 added
source ./fft.sh
# Game is still running from session 18 on PartyMenu Units tab.
# If it's dead: `boot`
screen                       # should show [PartyMenu] + inventory=184 unique

# Verify inventory write path
# Navigate to PartyMenu Inventory tab:
fft '{"id":"e","keys":[{"vk":69,"name":"E"}],"delayBetweenMs":300}'
screen -v                    # dumps 184-item grouped listing

# Verify OutfitterSell:
# Escape to WorldMap, EnterLocation, Enter Outfitter, Down to Sell, Enter:
fft '{"id":"x","keys":[{"vk":27,"name":"Esc"},{"vk":13,"name":"Enter"},{"vk":13,"name":"Enter"},{"vk":40,"name":"Down"},{"vk":13,"name":"Enter"}],"delayBetweenMs":500}'
screen                       # should show [OutfitterSell] + sellable count

# Check the 7 seeded verified prices
screen -v | head -30         # Dagger should show sell=50 gil (NO ~)
```

## Active TODO top of queue (next-session priority)

1. **Row-byte 3-snapshot hunt** — unblocks the entire PartyMenu
   drift class. 15-30 min with the right filter.
2. **Battle wiring for Items/Throw/Iaido held counts** — 1-2 hours
   in NavigationActions.cs. Infrastructure ready.
3. **Real sell prices paste** — user sending. 1 min of work when
   they arrive.
4. **State-machine detection drift on picker screens** — benefits
   from row byte first.
5. **Q/E tab-switch count bug** — needs investigation to root-cause.

## Three principles from this session

1. **Dumber is often smarter for memory hunts.** The inventory
   store was found by snapshot-diffing a single Dagger purchase.
   5+ prior sessions assumed sophisticated structures (TArray,
   FString, TSparseArray) and all failed. The actual layout was
   the simplest possible (flat u8 array, one byte per item ID).
   **Single-byte diffs beat clever decoders.**

2. **Name-keyed tables with static ID resolution beat
   hand-curated ID tables.** The `ItemPrices.BuyPricesByName`
   pattern means you can never drift out of sync with ItemData,
   because the resolver will surface any mismatch as a build
   failure. Reuse this pattern anywhere you'd otherwise hand-write
   ID lookup tables.

3. **Know when to stop probing and commit.** I lost 45 min chasing
   the picker TArray vtable before pivoting to the inventory hunt
   that worked. I almost lost another hour on the row-byte hunt
   before timeboxing myself out. **Set a timebox, hit it, commit
   partial progress.** The row byte will fall in the next session
   with the 3-snapshot approach; the partial progress is in the
   memory note, not lost.

Good session. Inventory store cracked after 5+ failed sessions.
30-file rename without breaking tests. 44 new tests. Four solid
commits. Row-byte and battle wiring are the top priorities — both
well-scoped with next-session instructions in this handoff.
