# Adding New Abilities to Final Fantasy Tactics

## Overview

This document explains how mods add new abilities to Final Fantasy Tactics: The Ivalice Chronicles (FFTIVC), based on analysis of the "Unique Abilities - Chapter 4" mod by Thoradin.

## Key Insight

FFT mods don't actually create entirely new abilities from scratch. Instead, they work within the existing game framework by reassigning, combining, and unlocking abilities that already exist in the game's code but aren't normally accessible to players.

## Implementation Method

### 1. XML Table Override System

The game uses the **fftivc.utility.modloader** (created by Nenkai) which allows mods to override hardcoded game data tables without modifying the executable.

**Key Components:**
- Mod loader dependency in `ModConfig.json`
- Override tables placed in `FFTIVC/tables/enhanced/` directory
- XML format for defining job command data

### 2. JobCommandData.xml Structure

The primary file for ability modification is `JobCommandData.xml`. This file overrides the game's internal job command table.

**XML Structure:**
```xml
<?xml version="1.0" encoding="utf-8"?>
<JobCommandTable>
  <Version>1</Version>
  <Entries>
    <JobCommand>
      <Id>27</Id> <!-- Job Command/Skillset ID -->
      <AbilityId1>198</AbilityId1>
      <AbilityId2>199</AbilityId2>
      <!-- ... up to AbilityId16 -->
      <ReactionSupportMovementId1>436</ReactionSupportMovementId1>
      <!-- ... up to ReactionSupportMovementId6 -->
    </JobCommand>
  </Entries>
</JobCommandTable>
```

**Key Fields:**
- `Id` - The job command/skillset ID being modified
- `AbilityId1-16` - Up to 16 ability IDs that belong to this skillset
- `ReactionSupportMovementId1-6` - Support/reaction/movement abilities
- `ExtendAbilityIdFlagBits` - Extended ability flags (auto-managed by loader)

### 3. Ability Assignment Methods

Mods add "new" abilities through several techniques:

#### A. Reassigning Existing Abilities
Taking abilities from normally unplayable classes (enemy-only, boss-only) and adding them to playable job skillsets.

**Example:** Giving Ramza access to Zalbaag's "Powersap" ability

#### B. Activating Unused Ability Slots
The game contains ability IDs that exist but aren't used in vanilla gameplay.

**Example:** Ability ID 358 "Barrage" exists but isn't normally accessible

#### C. Mixing Abilities from Different Jobs
Creating new combinations by pulling abilities from various sources into a single skillset.

**Example:** Combining Holy Sword techniques with status-effect magicks

### 4. Documentation System

Modders typically maintain a spreadsheet (`Final Fantasy Tactics Information.xls`) that:
- Documents all ability IDs and their effects
- Maps ability assignments to jobs
- Tracks which abilities are vanilla vs modified
- Provides reference for other modders

### 5. Technical Limitations

#### Hardcoded Ability Slots
Certain ability slots cannot be modified:
- Slot 40
- Slot 184
- Slot 219
- Slot 220

These are hardcoded in the game engine and will not function properly if assigned.

#### Cannot Create New Effects
Mods are limited to:
- Reassigning existing abilities
- Modifying ability parameters (damage, range, cost)
- Combining existing effects
- Cannot create entirely new ability mechanics

#### Ability ID Range
- Standard abilities: IDs 0-512
- Extended abilities require special flag bits
- Total limit depends on game engine constraints

### 6. Best Practices

#### Incremental Changes Only
Only include modified entries in your XML file:
```xml
<!-- Good: Only modified entries -->
<JobCommand>
  <Id>27</Id>
  <AbilityId1>198</AbilityId1>
  <!-- Only changed abilities -->
</JobCommand>
```

#### Mod Compatibility
- Remove unmodified entries to avoid conflicts
- Consider load order when multiple mods affect same jobs
- Test compatibility with popular mod frameworks

#### Documentation
- Maintain clear documentation of ability ID mappings
- Note which vanilla abilities were replaced
- Document any dependency requirements

## Implementation Steps

### Step 1: Set Up Mod Structure
```
YourMod/
├── ModConfig.json
├── FFTIVC/
│   └── tables/
│       └── enhanced/
│           └── JobCommandData.xml
```

### Step 2: Configure Dependencies
In `ModConfig.json`:
```json
{
  "ModDependencies": [
    "fftivc.utility.modloader"
  ]
}
```

### Step 3: Create JobCommandData.xml
Start with the template and add only your modifications:
```xml
<?xml version="1.0" encoding="utf-8"?>
<JobCommandTable>
  <Version>1</Version>
  <Entries>
    <!-- Your job command modifications here -->
  </Entries>
</JobCommandTable>
```

### Step 4: Assign Abilities
Map existing ability IDs to job commands:
```xml
<JobCommand>
  <Id>1</Id> <!-- Squire job command -->
  <AbilityId1>155</AbilityId1> <!-- Assign Holy Sword ability -->
</JobCommand>
```

## Example: Adding Holy Sword to Squire

```xml
<JobCommand>
  <Id>1</Id> <!-- Squire Command Set -->
  <AbilityId1>155</AbilityId1> <!-- Judgment Blade -->
  <AbilityId2>156</AbilityId2> <!-- Cleansing Strike -->
  <AbilityId3>157</AbilityId3> <!-- Northswain's Strike -->
  <AbilityId4>158</AbilityId4> <!-- Hallowed Bolt -->
  <AbilityId5>159</AbilityId5> <!-- Divine Ruination -->
</JobCommand>
```

## Related Systems

### Sprite Modifications
Ability mods often include visual changes:
- Character sprites in `FFTIVC/data/enhanced/fftpack/unit/`
- Portrait modifications
- Faction-based recolors

### Other Table Overrides
Similar XML override system works for:
- Item data
- Job data
- Status effects
- Equipment stats

## Tools and Resources

### Required Tools
- **fftivc.utility.modloader** - Core mod loading framework
- **XML editor** - For editing table data
- **Spreadsheet software** - For tracking ability mappings

### References
- [FFHacktics Wiki - Skillsets](https://ffhacktics.com/wiki/Skillsets)
- Game's internal ability ID documentation
- Community modding forums

## Conclusion

Adding abilities to FFT is primarily about creative reassignment and combination of existing game mechanics rather than creating entirely new ones. The mod loader's table override system provides a clean, non-destructive way to modify game data while maintaining compatibility with other mods. Success comes from understanding the existing ability pool and finding creative ways to recombine them into new and interesting skillsets.