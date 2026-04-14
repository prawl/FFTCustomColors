<!-- This file should not be longer than 200 lines, if so prune me. -->
# Class Fingerprint Labeling — Enemy Class Discovery Loop

## Goal
Discover and label the class (job/monster type) of every enemy in FFT by looping through random encounters, scanning the battlefield, and adding new fingerprints to the lookup table.

## Background
The FFT IC remaster doesn't expose enemy job IDs reliably. Instead, each battle unit's heap struct has 11 bytes at offset `+0x69` that act as a **class fingerprint** — identical for instances of the same class. The system reads these during `scan_move`, looks them up in a dictionary, and renders the class name. Unknown fingerprints show as `(?)` and must be labeled by the user.

See `../CLASS_FINGERPRINTS.md` for the technical reference (memory layout, known pitfalls, family patterns, implementation files).

## The Loop (per battle)

```bash
source ./fft.sh

# 1. From Battle_MyTurn in any battle, flee back to world map.
battle_flee

# 2. Travel to an unexplored battleground (IDs 24-42, see WorldMapNav.md).
world_travel_to 28           # e.g. Zeklaus Desert

# If you get stopped en route at a settlement, travel again.
# If you get EncounterDialog, skip to step 4.
# If you arrive on WorldMap with no encounter, try EnterLocation.
execute_action EnterLocation  # Triggers encounter roll (probabilistic)

# 3. Accept the fight.
execute_action Fight

# 4. Formation screen. The screen detection lags (reports TravelList)
#    for 3-6s during the transition. Disable strict mode to send raw keys.
strict 0
sleep 3                       # Wait for formation scene to load
enter                         # Select default blue tile
enter                         # Place Ramza
space                         # Open "Commence battle?" dialog
enter                         # Confirm Yes (default)

# 5. Wait for battle load, re-enable strict, scan.
sleep 6
strict 1
scan_move

# 6. If the scan output has any `(?)` units, get their fingerprints:
logs grep "Unknown fingerprint"
```

## What to do with unknowns

1. Show the user the unknown units and their fingerprints:
   ```
   | Pos   | HP  | LV | Fingerprint                              |
   |-------|-----|----|------------------------------------------|
   | (5,11)| 541 | 95 | 06-63-1E-46-55-86-27-84-07-55            |
   ```
2. Ask: "What class are these enemies? (include position + HP + level in the question)"
3. User will reply with class names.
4. Add each new entry to `ColorMod/GameBridge/ClassFingerprintLookup.cs`, placing it under the right family comment. Use **bytes 1-10** (skip the first byte — it's per-unit variation):
   ```csharp
   // Panther family (tier 1 Red Panther, tier 2 Coeurl, tier 3 Vampire Cat):
   ["06-74-1E-32-55-74-27-62-07-5B"] = "Red Panther",
   ["06-63-1E-46-55-86-27-84-07-55"] = "Vampire Cat",
   ```
5. `restart` to rebuild/redeploy. This takes ~60s including boot-through-title.
6. Wait a turn (`battle_wait`) then scan again to verify the label took.
7. Continue the loop with a new destination.

## Gotchas

- **Screen detection lags during formation loading.** The bridge says `TravelList` for 3-6s after `execute_action Fight`. Don't trust it — just `sleep 3` and send the key sequence blindly.
- **Strict mode blocks raw keys.** `strict 0` for the formation sequence, `strict 1` after.
- **First turn of a newly loaded battle can return stale cached scan.** `battle_wait` through the first turn to force a fresh scan.
- **Encounters are probabilistic.** If `world_travel_to` arrives without an encounter, try `EnterLocation` then travel to an adjacent battleground and back.
- **Settlements (0-14) don't have encounters.** Only battlegrounds (24-42).
- **Mod-forced battles may have unit structs at non-standard heap addresses** (e.g. Grogh Heights had them at 0x430xxxx, outside the hardcoded 0x4160..0x4180 range). Every unit gets "No heap match". Skip those battles — don't label mod-only enemies.
- **Ramza's fingerprint varies per save** depending on his current job/equipment. Don't add Ramza as a fingerprint entry — his roster match (nameId=1) handles him.
- **Story characters use roster nameId-based lookup**, not fingerprint. Entries like Mustadio/Agrias/Marach/Beowulf/Rapha/etc. are in `CharacterData.StoryCharacterJob`.

## Strategy for efficient coverage

Check `memory/project_battle_loop.md` for the current "missing classes" list. Target battlegrounds you haven't visited yet — each typically has a different enemy pool. If a battleground keeps giving known classes, flee and move on; don't grind the same one.

## Where the code lives

- `ColorMod/GameBridge/ClassFingerprintLookup.cs` — the lookup table
- `ColorMod/GameBridge/NavigationActions.cs` — `CollectUnitPositionsFull()` uses `SearchBytesInAllMemory(hpPattern, 8, 0x4160000000L, 0x4180000000L)` to find unit structs
- `ColorMod/GameBridge/CharacterData.cs` — `StoryCharacterJob` dict for nameId→job
- `fft.sh` — `battle_flee`, `world_travel_to`, `execute_action`, `scan_move`, `strict`, `logs`, `restart`, `boot`

## Commit often

After adding 3-5 new entries, commit with a clear message listing what you added:
```
git add ColorMod/GameBridge/ClassFingerprintLookup.cs
git commit -m "Add N monster fingerprints: ClassA, ClassB, ClassC"
git push
```
