<!-- This file should not be longer than 200 lines, if so prune me. -->
# ColorMod/GameBridge — Small Pure-Class API Reference

This directory holds small pure-function classes that back the bridge actions.
They're testable without spinning up the mod or running the game.

This README is a jump table for discovering them. Full doc is in the xml-doc
comments in each file; this doc exists for the "what's here?" first question.

## Rumor system

- **`RumorCorpus`** — hardcoded 26-entry string array decoded from `world_wldmes_bin.en.bin`. Regenerated via `WorldMesDecoderIterationTests.EmitHardcodedRumorCorpus` when the decoder changes.
- **`RumorLookup`** — wraps `RumorCorpus`. API: `GetByIndex(int)`, `GetByTitle(string)`, `GetByBodySubstring(string)`, `GetPreview(int)`, `FirstSentence(string)`, `All`. Hardcoded title→index map for 3 known titles.
- **`RumorResolver`** — pure 4-tier resolution extracted from `CommandWatcher`. `Resolve(lookup, searchLabel, locationId, unitIndex) → {Ok, Rumor, Error}`. Priority: title → substring → city+row → raw index.
- **`CityRumors`** — `(cityId, row) → corpusIndex` map. Seeded live for Dorter/Gariland/Warjilis/Yardrow/Goug (all Chapter-1). API: `Lookup(city, row)`, `CitiesFor(corpusIdx)`, `AllMappings`, `CityId.NameFor(id)`, `CityId.IdFor(name)`.

## Screen detection

- **`ScreenDetectionLogic`** — pure screen classifier. Input: memory-read primitives (slot0, ui, party, battleMode, eventId, ...). Output: screen name string. Also exports `IsRealEvent(eventId)` / `IsEventIdUnset(eventId)` / `IsMidBattleEvent(eventId)` with named constants `EventIdRealMin / EventIdRealMaxExclusive / EventIdMidBattleMaxExclusive / EventIdUnsetAlt`.
- **`ScreenNamePredicates`** — null-safe string predicates on screen names. `IsBattleState`, `IsPartyMenuTab`, `IsPartyTree`. Replaces scattered `screenName.StartsWith("Battle")` checks.
- **`LocationSaveLogic`** — `ShouldSave(rawLocation, hover, screenName, lastSavedLocation)` + `GetEffectiveLocation(...)`. Used by the location-persist path.
- **`ShopTypeLabels`** — `LabelFor(shopTypeIndex)` returning "Outfitter" / "Tavern" / etc.

## Prices / items / abilities

- **`ItemData`** — master item-id → metadata lookup. Canonical name field is what `ItemPrices` keys against.
- **`ItemPrices`** — `GetBuyPrice(id)`, `GetSellPrice(id)`, `GetSellPriceWithEstimate(id) → (int, bool)?`, `IsSellPriceGroundTruth(id)`. `SellPriceOverrides` is the live-verified ground-truth table; falls back to `buy/2` estimate. Extend the override table whenever new sell prices are captured.
- **`AbilityJpCosts`** — `GetCost(name)`, `ComputeNextJpForSkillset(skillset, learnedIndices)`. Filters 0-cost sentinels (Zodiark) and unknown costs. Drives the "Next: N" header on CharacterStatus.
- **`AbilityCompactor`** — collapses numbered-family abilities (Aim +1..+20 with identical tiles → single "Aim (+1 to +20)" entry). Exposes `IsHidden(entry)` predicate for enemy-target abilities with no enemies in range.
- **`ActionAbilityLookup`** — skillset name → list of abilities (indexed 0-15). `GetSkillsetAbilities(name)` returns null for unknown names; case-sensitive.

## Battle mechanics

- **`BattleFieldHelper`** — `AllEnemiesDefeated(units)` etc. Treats Team=1 as enemy, Team=2 as neutral.
- **`BackstabArcCalculator`** — `ClassifyHit(attacker, target)` returns front/side/back given attacker position + target facing.
- **`LineOfSightCalculator`** — `IsBlocked(start, end, map)` via DDA walk + linear altitude interpolation.
- **`WeatherDamageModifier`** — `GetMultiplier(weather, element)` + `FormatMarker(...)`. Rain/Snow/Thunderstorm canonical multipliers.
- **`ElementAffinityDecoder`** — 8-bit mask → list of elements.
- **`ZodiacData`** — sign/gender → compatibility multiplier. Opposite-sign lookup, involution invariant.
- **`WeaponEquippability` / `ArmorEquippability`** — type → allowed jobs lookup.

## Data decoders

- **`WorldMesDecoder`** — PSX byte-sequence → UTF-16 text. Handles digraphs (DA 74 → ",", D1 1D → "-") and structural bytes (F8/FE/E3/F5 section markers).
- **`MesDecoder`** — simpler sibling for non-world `.mes` tables.
- **`CharacterData`** — story-character nameId → canonical name.
- **`ClassFingerprintLookup`** — 11-byte class fingerprint → job name.

## Testing patterns

These classes are designed for `[Theory]` + `[InlineData]` coverage. When adding
a new entry to any data table (e.g. `CityRumors.Table`, `ItemPrices.SellPriceOverrides`,
`AbilityJpCosts.CostByName`, `CharacterData.StoryCharacterName`) add a regression
test in the matching `Tests/GameBridge/*Tests.cs` file.

See `SkillsetNameReferenceTests.cs` for the pattern of pinning canonical name
values — if a name is typo'd in production, the meta-test fires at test time
rather than silently passing.
