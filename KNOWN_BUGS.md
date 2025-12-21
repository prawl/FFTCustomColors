# Known Bugs

## Critical Issues

### 1. Black Enemy Units (Monks, Knights, and Others)
**Status**: 🔴 Active
**Severity**: High
**Reported**: December 2024
**Updated**: December 2024 - Confirmed affecting Knights as well

#### Description
Enemy units (confirmed with Monks and Knights) appear as pure black sprites during battles. This occurs because the .bin files become corrupted when the mod modifies them.

#### Technical Details
- FFT sprite files contain 8 sprite palettes (0-7) and 8 portrait palettes (8-15)
- Palettes 0-4 contain valid color data for different team variations
- **Palettes 5-7 are completely black (all zeros) in original game files**
- The game likely uses these palettes for enemy units
- When enemies try to use palette 5, 6, or 7, they appear pure black

#### Root Cause
1. The mod intercepts ALL sprite requests (both player and enemy)
2. Custom themes preserve the black palettes 5-7 from original files
3. Enemy units attempting to use these palettes appear black
4. The game normally applies enemy colors dynamically at runtime, but the mod prevents this

#### Proposed Solution
Populate palettes 5-7 with appropriate enemy colors when creating themes:
- **Palette 5**: Dark red enemy variant (based on palette 2)
- **Palette 6**: Dark purple enemy variant (based on palette 4)
- **Palette 7**: Dark gray/black enemy variant

---

### 2. Enemy Knights Color Changes
**Status**: 🔴 Active
**Severity**: High
**Reported**: December 2024

#### Description
When setting a custom color theme for the Knight job class, enemy knights also change to the selected color. The mod should ONLY affect player-controlled units, never enemy units.

#### Expected Behavior
- Player knights: Should use the custom color theme selected in configuration
- Enemy knights: Should always use their default enemy colors (red, purple, etc.)

#### Actual Behavior
- Both player AND enemy knights use the same custom color theme
- This breaks the visual distinction between player and enemy units

#### Root Cause
The mod intercepts sprite file loading at the filesystem level without distinguishing between player and enemy unit requests. When the game requests `battle_knight_m_spr.bin` for any knight (player or enemy), the mod provides the same modified sprite.

#### Proposed Solutions

**Option 1: Selective Interception**
- Hook into higher-level game functions to detect team/faction
- Only replace sprites for player team (Team ID 0)
- Return original sprites for enemy teams

**Option 2: Preserve Enemy Palettes**
- Keep original enemy color palettes in modified sprites
- Let the game's team detection choose the appropriate palette

**Option 3: Separate Enemy Sprite Files**
- Create separate enemy sprite variants
- Detect context and serve appropriate version

---

## Moderate Issues

### 3. Configuration Changes Not Applying Until Reloaded-II Restart
**Status**: 🔴 Active
**Severity**: Moderate
**Reported**: December 2024

#### Description
User reports that after changing settings in the F1 configuration menu and saving, the sprite changes don't appear in-game even after triggering a sprite reload (by opening formation menu). The changes only take effect after closing and reopening Reloaded-II.

#### Steps to Reproduce
1. Launch game through Reloaded-II
2. Press F1 to open configuration menu
3. Change job colors/themes
4. Save configuration
5. Open formation menu to trigger sprite reload
6. Observe: No visual changes applied
7. Close and reopen Reloaded-II
8. Observe: Changes now visible

#### Expected Behavior
Configuration changes should apply immediately or after triggering a sprite reload without requiring Reloaded-II restart.

#### Workaround
Close and reopen Reloaded-II after making configuration changes.

#### Possible Causes
- Configuration might be cached at Reloaded-II level
- Sprite interception might not be refreshing with new configuration
- File handles might be locked until Reloaded-II restarts
- Configuration path might not be updating properly during runtime

#### Proposed Solutions
1. Force configuration reload when changes are saved
2. Clear any cached sprite paths when configuration updates
3. Implement a "hot reload" mechanism for configuration changes
4. Add a "Reload Configuration" button that properly refreshes everything

### 4. General Enemy Color Modification
**Status**: 🔴 Active
**Severity**: Moderate
**Reported**: December 2024

#### Description
The mod is changing ALL unit sprites regardless of team affiliation. This affects the entire tactical experience as enemies no longer have distinct colors.

#### Affected Job Classes
- All generic job classes (Knight, Monk, Archer, etc.)
- Possibly story characters when they appear as enemies

#### Impact
- Loss of tactical clarity (can't distinguish teams by color)
- Immersion breaking (all units look like player units)
- Potential gameplay issues in large battles

---

## Testing Notes

### How to Reproduce Black Enemy Units
1. Enable any custom theme (not "original")
2. Enter a battle with enemy monks or knights (Mandalia Plains, Dorter, etc.)
3. Observe enemy units appearing as solid black
4. **Confirmed affected**: Monks, Knights (likely affects all job classes)

### How to Reproduce Enemy Color Changes
1. Set Knight job to any non-original theme (e.g., "Corpse Brigade")
2. Enter a battle with enemy knights
3. Observe enemy knights using the player-selected theme instead of enemy colors

---

## Technical Investigation Notes

### Palette Structure (from analysis)
```
Palettes 0-7: Unit sprite palettes
  0: Player default (brown/beige)
  1: Blue variant
  2: Red/orange variant (often used for enemies)
  3: Green variant
  4: Purple variant (often used for enemies)
  5-7: BLACK in original files (should be enemy palettes)

Palettes 8-15: Portrait palettes (working correctly)
```

### File Interception Point
The mod intercepts at: `ColorMod\Core\ModComponents\ThemeCoordinator.cs`
- Method: `InterceptFilePath()`
- No team detection currently implemented
- All sprite requests get the same modified file

---

## Update History
- **2024-12-21**: Initial documentation of black enemy monks and enemy color change bugs
- **2024-12-21**: Added configuration not applying until Reloaded-II restart bug