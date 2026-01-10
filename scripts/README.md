# FFT Color Customizer Scripts

This directory contains Python scripts for creating and managing color themes for Final Fantasy Tactics sprites.

## CRITICAL: Story Character Theme Initialization

**IMPORTANT**: When testing story character themes (like Orlandeau), the mod must apply the initial theme on startup. Without this, themes won't show until F2 is pressed. The mod's `ApplyInitialOrlandeauTheme()` method handles this by:
1. Copying the themed sprite from `sprites_orlandeau_[theme]/` to the main unit directory
2. Applying to all variants (battle_oru_spr.bin, battle_goru_spr.bin, battle_voru_spr.bin)
3. This happens in the mod's constructor, before the game fully loads

## Adding a Single New Theme to a Story Character - Complete Guide

**Example: Adding Twilight Blend theme to Rapha (2024-12-15)**

This section documents the complete process to add ONE new theme to an existing story character.

### Prerequisites
- The character's original sprite file (e.g., `battle_h79_spr.bin` for Rapha)
- Python environment with required libraries
- Access to the mod's source code

### Step-by-Step Implementation

#### Step 1: Generate the Theme
Create a Python script to generate the themed .bin file:

```python
# Example: generate_rapha_themes.py
# Key components:
- rgb_to_fft_color() function to convert RGB to FFT's 16-bit format
- darken_color() and lighten_color() for shading variations
- Apply colors to specific palette indices (3-10 for main colors)
- Save as .bin file with descriptive name
```

Run the script to generate:
- Themed .bin file (e.g., `rapha_twilight_blend.bin`)
- Preview PNG using `convert_sprite_sw.py --preview`

#### Step 2: Prepare Theme Files
1. Create theme folder in mod directory:
   ```bash
   mkdir ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_[character]_[theme]
   ```

2. Copy themed .bin with correct sprite name:
   ```bash
   cp themed_file.bin sprites_[character]_[theme]/battle_[sprite]_spr.bin
   # Example: cp rapha_twilight_blend.bin sprites_rapha_twilight_blend/battle_h79_spr.bin
   ```

3. Ensure original sprite exists:
   ```bash
   mkdir sprites_[character]_original
   cp original_sprite.bin sprites_[character]_original/battle_[sprite]_spr.bin
   ```

#### Step 3: Generate Preview Images
Create 64x64 preview PNGs for Config UI:
```bash
python scripts/convert_sprite_sw.py input.bin output.png --preview
# Creates 64x64 PNG with transparent background
```

Place previews in:
- `ColorMod/Resources/Previews/[character]_original.png`
- `ColorMod/Resources/Previews/[character]_[theme].png`

#### Step 4: Update Character Enum
Edit or create `ColorMod/Configuration/[Character]ColorScheme.cs`:

```csharp
using System.ComponentModel;
using ColorMod.Registry;

namespace FFTColorCustomizer.Configuration
{
    [StoryCharacter(SpriteNames = new[] { "sprite_name" }, DefaultTheme = "original")]
    public enum CharacterColorScheme
    {
        [Description("Original")]
        original,

        [Description("Theme Display Name")]
        theme_name,
    }
}
```

**IMPORTANT**:
- Enum name must match pattern: `[Character]ColorScheme`
- Theme names use underscores, descriptions use spaces
- SpriteNames array contains the sprite identifier (e.g., "h79" for Rapha)

#### Step 5: Ensure Config.cs Property Exists
Verify `ColorMod/Configuration/Config.cs` has the character property:

```csharp
public RaphaColorScheme Rapha { get; set; } = RaphaColorScheme.original;
```

**Note**: With the reflection-based system, this is usually auto-discovered if the enum exists.

#### Step 6: Deploy the Mod
Run the build script to compile and deploy:
```bash
powershell -ExecutionPolicy Bypass -File BuildLinked.ps1
```

This will:
- Compile the C# mod
- Copy all theme folders
- Copy preview images
- Deploy to Reloaded-II mods folder

#### Step 7: Test in Config UI
1. Launch Reloaded-II
2. Open mod configuration
3. Find the character in Story Characters section
4. Verify new theme appears in dropdown
5. Check preview image updates when theme is selected
6. Save configuration and test in-game

### File Structure After Implementation
```
ColorMod/
├── Configuration/
│   └── [Character]ColorScheme.cs (updated enum)
├── FFTIVC/data/enhanced/fftpack/unit/
│   ├── sprites_[character]_original/
│   │   └── battle_[sprite]_spr.bin
│   └── sprites_[character]_[theme]/
│       └── battle_[sprite]_spr.bin
└── Resources/Previews/
    ├── [character]_original.png
    └── [character]_[theme].png
```

### Common Issues and Solutions

**Theme not appearing in dropdown:**
- Check enum is properly formatted with Description attribute
- Ensure character property exists in Config.cs
- Rebuild and redeploy the mod

**Preview image not showing:**
- Verify PNG is 64x64 pixels
- Check filename matches pattern: `[character]_[theme].png`
- Ensure PNG has transparent background

**Theme not applying in game:**
- Verify .bin file is in correct folder structure
- Check sprite filename matches expected pattern
- Ensure theme folder follows naming convention

### Example: Rapha Twilight Blend Implementation
1. Generated theme with gradient style (indigo to deep pink)
2. Created `sprites_rapha_twilight_blend/` folder
3. Placed `battle_h79_spr.bin` in theme folder
4. Generated `rapha_twilight_blend.png` preview
5. Updated `RaphaColorScheme.cs` enum
6. Deployed and tested successfully

---

## Story Character Themes Status

**Completed Story Characters with Full Themes (2024-12-15)**:
- ✅ **Orlandeau** - Thunder God theme
- ✅ **Beowulf** - Temple Knight theme, Test theme
- ✅ **Agrias** - 25 custom themes (Holy Knight + variants)
- ✅ **Cloud** - Knights Round theme, Sephiroth Black theme
- ✅ **Mustadio** - 25 custom themes (removed from precision work)
- ✅ **Reis** - 25 custom themes
- ✅ **Malak** - 25 custom themes
- ✅ **Rapha** - Twilight Blend theme (gradient style with indigo to deep pink)
- ✅ **Delita** - 22 custom themes (3 removed)
- ✅ **Alma** - 25 custom themes
- ✅ **Wiegraf** - 25 custom themes (removed for precision work)
- ✅ **Celia** - 25 custom themes
- ✅ **Lettie** - 23 custom themes (2 removed)

**Total Themes Implemented**: 170 story character themes across 7 active characters

## Complete Story Character Implementation Process (December 2024)

### Overview
Successfully added 9 new story characters with 170 custom color themes to the FFT Color Customizer system.

### Implementation Steps

#### 1. Theme Generation Phase
**Script**: `generate_all_story_themes.py`
- Generated 25 color themes for each of the 9 new story characters
- Created 225 total sprite files (.bin format)
- Converted all sprites to PNG for user review
- Themes saved to: `C:/Users/ptyRa/OneDrive/Desktop/FFT_Story_Themes/`

#### 2. User Theme Selection
- User reviewed all 225 generated themes as PNGs
- Selected preferred themes for each character:
  - Alma: 25/25 themes kept
  - Celia: 25/25 themes kept
  - Delita: 22/25 themes kept (removed bronze_armor, copper_shine, coral)
  - Lettie: 23/25 themes kept (removed bronze_armor, copper_shine)
  - Malak: 25/25 themes kept
  - Rafa: 25/25 themes kept
  - Reis: 25/25 themes kept
- Total: 170 themes selected for implementation

#### 3. Enum Creation Phase
Created individual ColorScheme enum files for each character:
```csharp
// Example: ColorMod/Configuration/AlmaColorScheme.cs
[StoryCharacter(SpriteNames = new[] { "aruma" }, DefaultTheme = "original")]
public enum AlmaColorScheme
{
    [Description("Original")]
    original,
    [Description("Crimson Red")]
    crimson_red,
    // ... 23 more themes
}
```

#### 4. Configuration Integration
**Updated Config.cs**:
```csharp
public AlmaColorScheme Alma { get; set; } = AlmaColorScheme.original;
public CeliaColorScheme Celia { get; set; } = CeliaColorScheme.original;
// ... etc for all 9 characters
```

#### 5. Theme Deployment
**Script**: `update_character_themes.py`
- Updated all 7 character enum files with selected themes
- Copied 170 sprite files to: `ColorMod/FFTIVC/data/enhanced/fftpack/unit/`
- Generated 170 preview PNGs (64x64) for Config UI
- Copied previews to: `ColorMod/Resources/Previews/`

#### 6. JSON Serialization Fix (Critical)
**Problem**: New characters weren't being saved to Config.json

**Solution**: Updated both JSON converters
- `ConfigJsonConverter.cs`: Added serialization/deserialization for all 9 characters
- `ConfigSystemTextJsonConverter.cs`: Added System.Text.Json compatibility

#### 7. Theme Initialization Fix (Critical)
**Problem**: Story character themes reverted to "original" on game startup

**Solution**: Updated initialization systems
- `Mod.cs`: Updated `InitializeStoryCharacterThemes()` to apply all 9 new character themes
- `StoryCharacterThemeManager.cs`: Added setter methods for all new characters

#### 8. UI Integration
**ConfigurationForm.cs**:
- All 9 new characters appear in dropdowns
- Theme selections properly persist
- Preview images display correctly at 64x64

### File Structure After Implementation
```
ColorMod/
├── Configuration/
│   ├── AlmaColorScheme.cs
│   ├── CeliaColorScheme.cs
│   ├── DelitaColorScheme.cs
│   ├── LettieColorScheme.cs
│   ├── MalakColorScheme.cs
│   ├── RafaColorScheme.cs
│   └── ReisColorScheme.cs
├── FFTIVC/data/enhanced/fftpack/unit/
│   ├── battle_aruma_[theme]_spr.bin (25 files)
│   ├── battle_seria_[theme]_spr.bin (25 files)
│   ├── battle_dily_[theme]_spr.bin (22 files)
│   ├── battle_ledy_[theme]_spr.bin (23 files)
│   ├── battle_mara_[theme]_spr.bin (25 files)
│   ├── battle_rafa_[theme]_spr.bin (25 files)
│   └── battle_reze_[theme]_spr.bin (25 files)
└── Resources/Previews/
    ├── alma_[theme].png (25 files)
    ├── celia_[theme].png (25 files)
    ├── delita_[theme].png (22 files)
    ├── lettie_[theme].png (23 files)
    ├── malak_[theme].png (25 files)
    ├── rafa_[theme].png (25 files)
    └── reis_[theme].png (25 files)
```

### Technical Challenges Solved

#### 1. Handle Creation Issue
**Problem**: WinForms ComboBox throws exception when accessing SelectedIndex before Handle exists
**Solution**: Force Handle creation after adding control to panel, then set selection

#### 2. JSON Persistence
**Problem**: Config.json only saved original 3 story characters
**Solution**: Extended both JSON converters to handle all character properties

#### 3. Runtime Initialization
**Problem**: Themes not applying at game startup
**Solution**: Added initialization methods for all new characters in StoryCharacterThemeManager

### Testing Checklist
- ✅ Config UI shows all new characters
- ✅ Dropdown lists show all theme options
- ✅ Preview images display correctly (64x64)
- ✅ Theme selections save to Config.json
- ✅ Themes no longer revert on game startup
- ⏳ In-game theme application (ready for testing)

### Key Scripts Created
1. **generate_all_story_themes.py**: Batch theme generation
2. **update_character_themes.py**: Enum update and file deployment
3. **analyze_kept_themes.py**: Theme selection analysis

## DynamicSpriteLoader Theme Preservation

**The DynamicSpriteLoader now automatically preserves story character themes**. It detects and preserves any theme directory matching the pattern `sprites_[character]_*`.

**How it Works**:
```csharp
// Automatically preserves all story character themes
if (dirName.StartsWith("sprites_orlandeau_") ||
    dirName.StartsWith("sprites_beowulf_") ||
    dirName.StartsWith("sprites_agrias_") ||
    dirName.StartsWith("sprites_cloud_"))
{
    ModLogger.Log($"DynamicSpriteLoader: Preserving {character} theme: {dirName}");
    continue;
}
```

**No Manual Updates Required**: The system now automatically detects and preserves story character themes, eliminating the need for manual exclusion patterns.

## How Orlandeau Story Character Themes Work

**Implementation Overview:**
1. **Character-specific enum** (`OrlandeauColorScheme`) with themes: `original` and `thunder_god`
2. **Theme manager** (`StoryCharacterThemeManager`) tracks and cycles the current theme
3. **F2 hotkey** cycles through themes by copying sprite files to the main directory
4. **Initial theme** applied on mod startup via `ApplyInitialOrlandeauTheme()`
5. **Deployment** via BuildLinked.ps1 copies all theme variants with proper sprite counts

**Key Implementation Details:**

1. **OrlandeauColorScheme.cs** - Character-specific enum:
```csharp
public enum OrlandeauColorScheme
{
    [Description("Original")]
    original,
    [Description("Thunder God")]
    thunder_god,
}
```

2. **StoryCharacterThemeManager.cs** - Theme management:
```csharp
private OrlandeauColorScheme _currentOrlandeauTheme = OrlandeauColorScheme.thunder_god;

public OrlandeauColorScheme CycleOrlandeauTheme()
{
    var values = Enum.GetValues<OrlandeauColorScheme>();
    var currentIndex = Array.IndexOf(values, _currentOrlandeauTheme);
    var nextIndex = (currentIndex + 1) % values.Length;
    _currentOrlandeauTheme = values[nextIndex];
    return _currentOrlandeauTheme;
}
```

3. **Mod.cs - F2 Handler** - Cycles and applies theme by copying files:
```csharp
// In ProcessHotkeyPress for F2:
var nextOrlandeauTheme = _storyCharacterManager.CycleOrlandeauTheme();
string orlandeauThemeDir = $"sprites_orlandeau_{nextOrlandeauTheme.ToString().ToLower()}";
var sourceFile = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", orlandeauThemeDir, "battle_oru_spr.bin");
var destFile = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "battle_oru_spr.bin");
File.Copy(sourceFile, destFile, true);
// Also copy variants: battle_goru_spr.bin, battle_voru_spr.bin
```

4. **BuildLinked.ps1** - Deployment with correct sprite counts:
- Orlandeau themes get 124 sprites (121 generic + 3 Orlandeau: oru, goru, voru)
- Filter pattern: `$_.Name -like "sprites_orlandeau_*"`
- Verification: Expects exactly 124 sprites per Orlandeau theme directory

**Important Directory Structure:**
```
ColorMod/FFTIVC/data/enhanced/fftpack/unit/
├── sprites_orlandeau_thunder_god/
│   ├── battle_oru_spr.bin     # Main Orlandeau sprite
│   ├── battle_goru_spr.bin    # Guest Orlandeau
│   └── battle_voru_spr.bin    # Variant Orlandeau
└── battle_oru_spr.bin          # Active sprite (copied from theme dir)
```

**Key Point:** The F2 handler copies sprites from theme directories to the main unit/ directory. This is a file-swapping approach, NOT path interception.

## Beowulf Story Character Theme Implementation (Complete Example)

**Implementation completed using TDD approach:**

1. **Created BeowulfColorScheme.cs enum**:
```csharp
public enum BeowulfColorScheme
{
    [Description("Original")]
    original,
    [Description("Test")]
    test,
    [Description("Temple Knight")]
    temple_knight,
}
```

2. **Updated StoryCharacterThemeManager.cs**:
```csharp
private BeowulfColorScheme _currentBeowulfTheme = BeowulfColorScheme.test;

public BeowulfColorScheme CycleBeowulfTheme()
{
    var values = Enum.GetValues<BeowulfColorScheme>();
    var currentIndex = Array.IndexOf(values, _currentBeowulfTheme);
    var nextIndex = (currentIndex + 1) % values.Length;
    _currentBeowulfTheme = values[nextIndex];
    return _currentBeowulfTheme;
}
```

3. **Added to Mod.cs**:
- `ApplyInitialBeowulfTheme()` - Applies test theme on startup
- F2 handler cycles Beowulf themes and copies sprite files
- Beowulf has only 1 sprite variant (battle_beio_spr.bin)

4. **Updated BuildLinked.ps1**:
- Added Beowulf theme detection and deployment
- Beowulf themes get 122 sprites (121 generic + 1 Beowulf)
- Verification expects exactly 122 sprites per Beowulf theme

5. **Theme Files Created**:
```
ColorMod/FFTIVC/data/enhanced/fftpack/unit/
├── sprites_beowulf_test/
│   └── battle_beio_spr.bin
└── sprites_beowulf_temple_knight/
    └── battle_beio_spr.bin
```

**Key Differences from Orlandeau:**
- Beowulf has only 1 sprite file (beio) vs Orlandeau's 3 (oru, goru, voru)
- Beowulf themes contain 122 sprites total vs Orlandeau's 124
- Default theme is "test" for Beowulf vs "thunder_god" for Orlandeau

## Cloud Story Character Theme Implementation (Complete Example)

**Implementation completed using efficient PNG preview workflow:**

1. **Created CloudColorScheme.cs enum**:
```csharp
public enum CloudColorScheme
{
    [Description("Original")]
    original,
    [Description("Knights Round")]
    knights_round,
    [Description("Sephiroth Black")]
    sephiroth_black,
}
```

2. **Updated StoryCharacterThemeManager.cs**:
```csharp
private CloudColorScheme _currentCloudTheme = CloudColorScheme.sephiroth_black;

public CloudColorScheme CycleCloudTheme()
{
    var values = Enum.GetValues<CloudColorScheme>();
    var currentIndex = Array.IndexOf(values, _currentCloudTheme);
    var nextIndex = (currentIndex + 1) % values.Length;
    _currentCloudTheme = values[nextIndex];
    return _currentCloudTheme;
}
```

3. **Cloud's Simplified Color Palette**:
Unlike generic sprites, Cloud uses only 2 color zones:
- **Indices 3-5**: RED zone - Shoulder pads, wrists, shirt/boot outlines
- **Indices 6-9**: GREEN zone - Main clothing (pants, shoes, shirt)
- Higher indices (10+) are not used by Cloud's sprite

4. **Theme Files Created**:
```
ColorMod/FFTIVC/data/enhanced/fftpack/unit/
├── sprites_cloud_knights_round/
│   └── battle_cloud_spr.bin
└── sprites_cloud_sephiroth_black/
    └── battle_cloud_spr.bin
```

5. **Efficient PNG Preview Workflow**:
- Generated 50 Cloud theme previews as PNGs for rapid evaluation
- Selected best themes without launching the game
- Converted selected PNGs back to .bin format for implementation

**Key Differences from Other Story Characters:**
- Cloud has only 1 sprite file (cloud) vs 3 for Orlandeau
- Simpler 2-zone color mapping vs complex multi-zone for generic sprites
- Default theme is "sephiroth_black" for Cloud
- PNG preview workflow enabled rapid theme selection without game testing

## Adding Story Characters to Config UI (Display-Only)

**To add story characters that appear in the Config UI with just their original sprite:**

### Step 1: Create the Enum Class
Create a new file `ColorMod/Configuration/[CharacterName]ColorScheme.cs`:

```csharp
using System.ComponentModel;
using ColorMod.Registry;

namespace FFTColorCustomizer.Configuration
{
    [StoryCharacter(SpriteNames = new[] { "sprite_name" }, DefaultTheme = "original")]
    public enum MustadioColorScheme
    {
        [Description("Original")]
        original,
    }
}
```

### Step 2: Add to Config.cs
Add a property for the new character:
```csharp
public MustadioColorScheme Mustadio { get; set; } = MustadioColorScheme.original;
```

### Step 3: Update ConfigurationForm.cs
Add the character to the UI in two places:

1. Add to the story character rows:
```csharp
AddStoryCharacterRow(row++, "Mustadio", _config.Mustadio);
```

2. Add handling in AddStoryCharacterRow method (follow the pattern of existing characters)

### Step 4: Generate Preview Image
Use the sprite conversion script to create a preview:
```bash
python scripts/convert_sprite_sw.py ColorMod/FFTIVC/data/enhanced/fftpack/unit/battle_musu_spr.bin ColorMod/Resources/Previews/mustadio_original.png --preview
```

**IMPORTANT:** You MUST include the `--preview` flag to generate a 64x64 config menu preview. Without this flag, the script will generate a large 256x320 (8x scaled) image that won't display correctly in the Config UI.

### Step 5: Deploy
Run `BuildLinked.ps1` to deploy the mod with the new characters.

---

## Adding New Story Character Themes - SIMPLIFIED PROCESS (2 Steps!)

**NEW: With the reflection-based converter system, adding story characters requires only 2 steps!**

### Quick Start - Complete Implementation Steps

#### Step 1: Add Property to Config.cs
Add a property for your new character to the Config class:
```csharp
public YourCharacterColorScheme YourCharacter { get; set; } = YourCharacterColorScheme.original;
```

#### Step 2: Create the ColorScheme Enum File
Create a new file `ColorMod/Configuration/[CharacterName]ColorScheme.cs`:
```csharp
using System.ComponentModel;
using ColorMod.Registry;

namespace FFTColorCustomizer.Configuration
{
    [StoryCharacter(SpriteNames = new[] { "sprite_name" }, DefaultTheme = "original")]
    public enum YourCharacterColorScheme
    {
        [Description("Original")]
        original,

        [Description("Theme 1 Display Name")]
        theme1,

        [Description("Theme 2 Display Name")]
        theme2
    }
}
```

#### Step 3: Update ConfigBasedSpriteManager.cs
Add the character to the `ApplyStoryCharacterThemes` method:
```csharp
private void ApplyStoryCharacterThemes(Config config)
{
    // ... existing characters ...
    ApplyStoryCharacterTheme("yourcharacter", "sprite_name", config.YourCharacter);
}
```

#### Step 4: Update StoryCharacterConfig.cs
Fix the `GetSpritePathFormat` method to return the correct theme directory:
```csharp
case "YourCharacter":
    if (_config.YourCharacter == YourCharacterColorScheme.original)
        return "sprites_original";
    return $"sprites_yourcharacter_{_config.YourCharacter.ToString().ToLower()}";
```

#### Step 5: Create Theme Files
Place your theme sprite files in the correct directory structure:
```
ColorMod/FFTIVC/data/enhanced/fftpack/unit/
└── sprites_yourcharacter_theme1/
    └── battle_[sprite_name]_spr.bin
```

#### Step 6: Create Preview Images
Create and place preview images with the correct naming:
```
ColorMod/Resources/Previews/
└── yourcharacter_theme1.png  (64x64 preview image)
```

**Note:** Preview images must match the pattern `[charactername]_[theme].png` where charactername is lowercase.

#### Step 7: Deploy
Run `BuildLinked.ps1` to deploy the mod with the new character themes.

**The reflection-based system automatically handles:**
- ✅ JSON serialization/deserialization (both Newtonsoft.Json and System.Text.Json)
- ✅ Configuration merging
- ✅ UI integration
- ✅ Theme persistence
- ✅ Auto-discovery at startup
- ✅ Theme management and cycling
- ✅ Sprite-to-character mapping
- ✅ F1/F2 key integration

### What the Reflection System Eliminated (Previously 6+ Files)

The old manual process required updating:
1. ~~ConfigJsonConverter.cs (add hardcoded mapping)~~
2. ~~ConfigSystemTextJsonConverter.cs (add hardcoded mapping)~~
3. ~~ConfigMerger.cs (add merge logic)~~
4. ~~StoryCharacterRegistry.cs (add registration)~~
5. ~~Mod.cs (add initialization)~~
6. ~~BuildLinked.ps1 (update deployment)~~

Now ALL of these are handled automatically by reflection!

---

### Detailed Guide for Manual Customization (Optional)

Follow these steps when adding themes for a new story character (using Beowulf as example):

### Step 1: Create Theme Scripts
1. Create character directory: `scripts/[character_name]/`
2. Create three Python scripts:
   - `extract_original_colors.py` - Extract original palette
   - `create_simple_color_test.py` - Test palette mapping with distinct colors
   - `create_[theme_name].py` - Create the actual theme

### Step 2: Test Palette Mapping
```bash
# Extract original colors to understand base palette
cd scripts/beowulf
python extract_original_colors.py

# Create color test to identify which indices control what
python create_simple_color_test.py

# Deploy and test in-game to confirm mapping
# RED = armor, GREEN = secondary, BLUE = cape, etc.
```

### Step 3: Create C# Enum for Character Themes
Create `ColorMod/Configuration/[Character]ColorScheme.cs`:
```csharp
using System.ComponentModel;

namespace FFTColorCustomizer.Configuration
{
    public enum BeowulfColorScheme
    {
        [Description("Original")]
        original,

        [Description("Temple Knight")]
        temple_knight,

        [Description("Test")]
        test,
    }
}
```

### Step 4: Update Config.cs
1. Change the property type from `ColorScheme` to character-specific enum:
```csharp
// Before:
public ColorScheme Beowulf { get; set; } = ColorScheme.original;

// After:
public BeowulfColorScheme Beowulf { get; set; } = BeowulfColorScheme.original;
```

2. Update `GetColorSchemeForSprite` method to handle character themes:
```csharp
// Beowulf (beio = Beowulf)
if (spriteName.Contains("beio"))
{
    if (Beowulf == BeowulfColorScheme.original)
        return "sprites_original";
    if (Beowulf == BeowulfColorScheme.test)
        return "sprites_beowulf_test";
    return $"sprites_beowulf_{Beowulf.ToString().ToLower()}";
}
```

### Step 5: Update BuildLinked.ps1
Add support for the new character's themes in the deployment script:
```powershell
# Get all story character themes
$beowulfThemes = Get-ChildItem "ColorMod/FFTIVC/data/enhanced/fftpack/unit" -Directory -Filter "sprites_beowulf_*"

# Include in deployment
Where-Object { $_.Name -like "sprites_orlandeau_*" -or $_.Name -like "sprites_beowulf_*" }

# Update sprite exclusion patterns to include character sprite
Where-Object { $_.Name -notmatch "aguri|kanba|musu|dily|hime|aruma|rafa|mara|cloud|reze" -or $_.Name -match "oru|beio" }
```

### Step 6: Update Story Character Theme Manager
Update `ColorMod/Utilities/StoryCharacterThemeManager.cs`:
```csharp
// Add theme tracking for the character
private BeowulfColorScheme _currentBeowulfTheme = BeowulfColorScheme.test;

// Add cycling methods
public BeowulfColorScheme CycleBeowulfTheme()
{
    var values = Enum.GetValues<BeowulfColorScheme>();
    var currentIndex = Array.IndexOf(values, _currentBeowulfTheme);
    var nextIndex = (currentIndex + 1) % values.Length;
    _currentBeowulfTheme = values[nextIndex];
    return _currentBeowulfTheme;
}
```

### Step 7: Update Mod.cs for Path Interception
Add story character sprite handling to `InterceptFilePath`:
```csharp
// Check if it's a story character sprite
if (IsStoryCharacterSprite(fileName))
{
    return HandleStoryCharacterSprite(originalPath, fileName);
}

// Handle Beowulf in HandleStoryCharacterSprite
if (fileName.Contains("beio_spr"))
{
    var beowulfTheme = _storyCharacterManager.GetCurrentBeowulfTheme();
    if (beowulfTheme != BeowulfColorScheme.original)
    {
        var themeDir = $"sprites_beowulf_{beowulfTheme.ToString().ToLower()}";
        var themedPath = Path.Combine(_modPath, "data", "enhanced", "fftpack", "unit", themeDir, fileName);
        if (File.Exists(themedPath))
        {
            return themedPath;
        }
    }
}
```

### Step 8: Add F2 Cycling Support
Update F2 handler in Mod.cs to cycle the character's themes:
```csharp
// Cycle Beowulf theme
var nextBeowulfTheme = _storyCharacterManager.CycleBeowulfTheme();
Console.WriteLine($"[FFT Color Customizer] Cycling Beowulf to {nextBeowulfTheme}");
```

### Step 9: Test and Verify
1. Run `BuildLinked.ps1` to deploy
2. Launch game through Reloaded-II
3. Test theme switching with F2 or config menu
4. Verify all character variants work (main, guest, variant sprites)

**Critical Implementation Notes:**
1. **Path Structure**: Theme sprites must be in `ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_[character]_[theme]/`
2. **Deployment Path**: BuildLinked.ps1 deploys from `ColorMod/FFTIVC/data/` to `$modPath/data/` (NOT `$modPath/FFTIVC/data/`)
3. **File Interception**: Use `InterceptFilePath` for path redirection, NOT file copying in F2 handler
4. **Story Character Detection**: Check sprite names (e.g., "beio_spr" for Beowulf) in `IsStoryCharacterSprite`
5. **Theme Directory Naming**: Must match pattern `sprites_[character]_[theme]` exactly
6. **Initial Theme Application**: Apply default theme on mod startup via `ApplyInitialStoryCharacterThemes`
7. **Sprite Count Verification**: Beowulf themes have 122 sprites (121 generic + 1 Beowulf), Orlandeau has 124

## Sprite Conversion Tools

### convert_sprite_sw.py - Extract Southwest-Facing Sprites and Generate Configuration Previews
Converts FFT .bin sprite files to PNG images, extracting the southwest-facing sprite for previews or detailed viewing.

**Purpose:**
- Generates standardized 64x64 preview images for the configuration menu
- Batch generates all job previews for a theme with consistent formatting
- Can also extract high-resolution (8x scaled) sprites for detailed viewing

**Usage:**

#### Single File Mode:
```bash
# Generate 64x64 config preview
python scripts/convert_sprite_sw.py battle_knight_m_spr.bin knight.png --preview

# Extract 8x scaled sprite (256x256) for detailed viewing
python scripts/convert_sprite_sw.py battle_oru_spr.bin orlandeau_sw.png

# Use different palette (0-15)
python scripts/convert_sprite_sw.py battle_oru_spr.bin output.png 2
```

#### Batch Mode - Generate All Previews for a Theme:
```bash
# Generate all job previews for a theme
python scripts/convert_sprite_sw.py --batch crimson_red

# Generate specific jobs only
python scripts/convert_sprite_sw.py --batch royal_purple knight_male,knight_female

# Generate previews for all custom themes
for theme in crimson_red royal_purple phoenix_flame frost_knight; do
    python scripts/convert_sprite_sw.py --batch $theme
done
```

**Preview Mode Features (--preview or --batch):**
- Generates standardized 64x64 PNG images for configuration menu
- Transparent background with proper alpha channel
- Consistent sprite positioning (y_offset=1, shifted up 2 pixels)
- 2x scaling from original 32x32 sprite
- Automatic output naming: `{job}_{theme}.png`

**Batch Processing:**
- Processes all job classes for a theme in one command
- Automatically finds sprite files in theme directories
- Supports partial processing with job list parameter
- Generates consistent previews across all sprites

**Technical Details:**
- FFT sprites are 256 pixels wide with 8 directional poses
- Southwest sprite position: x_offset=32 (2nd sprite), y_offset=1
- Captures 32x32 pixels, scales to 64x64 for previews
- Uses palette index 0 with transparent background (color 0)
- Sprite data uses 4-bit indexed color with BGR555 palette format

**Output Formats:**
- **Preview Mode**: 64x64 PNG with transparent background (config menu)
- **Standard Mode**: 256x256 PNG scaled 8x (detailed viewing)
- **Batch Mode**: Multiple 64x64 PNGs in Resources/Previews directory

## Scripts Overview

### 1. create_cohesive_theme.py - Recommended Theme Creator
Creates truly cohesive themes with correct cape edges and shadows.

**Usage:**
```bash
# Create a two-tone theme with proper cape shading
python create_cohesive_theme.py --name "cobalt_crusader" --primary "#0047AB" --accent "#FFD700"

# Single color theme with auto-generated darker shades
python create_cohesive_theme.py --name "crimson_knight" --primary "#DC143C"

# Test with single sprite first
python create_cohesive_theme.py --name "test_theme" --primary "#0047AB" --accent "#FFD700" --single "battle_knight_m_spr.bin"
```

**Features:**
- Properly handles main cape color at indices 6-10 in palette 0
- Cape edges at index 7 across palettes 0-3 (25% darker)
- Cape shadows at index 9 across palettes 0-3 (50% darker)
- Buckles/clasps at indices 3-5 with accent color

### 2. create_sprite_theme.py - Alternative Theme Creator
Create themes with primary and accent colors using a different approach.

**Usage:**
```bash
# Two-tone theme with primary and accent colors
python create_sprite_theme.py --source original --name royal_blue \
  --primary-color "#0047AB" --accent-color "#FFD700"

# Single color theme (no accent)
python create_sprite_theme.py --source original --name crimson \
  --primary-color "#DC143C"

# Custom indices for specific modifications
python create_sprite_theme.py --source original --name custom \
  --custom-indices "3,4,5" --custom-color "#FFD700"
```

### 3. Orlandeau Theme Scripts - Character-Specific Themes
See `orlandeau/` subdirectory for Orlandeau-specific theme creation scripts.

**Key Scripts:**
- `orlandeau/extract_original_colors.py` - Extract original palette
- `orlandeau/create_simple_color_test.py` - Test palette mapping
- `orlandeau/create_thunder_god_variants.py` - Create Thunder God themed variants

**Note:** Orlandeau has a different palette mapping than generic sprites. See `orlandeau/README.md` for details.

### 4. apply_story_characters.py - Story Character Management
Apply themes to story character sprites or revert them to original colors.

**Usage:**
```bash
# Copy story characters to all themes
python apply_story_characters.py

# Revert working characters to original colors
python apply_story_characters.py revert

# Show help
python apply_story_characters.py help
```

**Working Story Characters:**
- Orlandeau (`battle_oru_spr.bin`)
- Malak (`battle_mara_spr.bin`)
- Reis human (`battle_reze_spr.bin`)
- Reis dragon (`battle_reze_d_spr.bin`)
- Agrias (`battle_aguri_spr.bin`, `battle_kanba_spr.bin`)
- Beowulf (`battle_beio_spr.bin`)

## Critical Technical Information

### FFT Sprite Palette Structure

FFT sprites use a complex palette system discovered through extensive research:

**Sprite Format:**
- **256 colors total** (512 bytes) organized as 16 palettes of 16 colors each
- Palettes 0-7: Unit sprites
- Palettes 8-15: Portraits
- Each palette: 2 bytes per color (XBBBBBGGGGGRRRRR format)
- Color format: 1 bit for transparency + 5 bits each for BGR channels

### CONFIRMED WORKING PALETTE INDICES

After extensive testing (2024-12-09), these indices change visible armor WITHOUT affecting hair:

**ACCENT COLORS (clasp, buckle, trim):**
- **Indices 3-5**: Belt buckles, clasps, and trim

**PRIMARY COLORS (cape, clothing, armor):**
- **Indices 6-9**: Main clothing and cape colors
- **Indices 20-31**: Additional armor elements
- **Indices 35-47**: Extended armor pieces (skip 44)
- **Indices 51-62**: More armor palette

**NEVER MODIFY - Hair Indices:**
- **Range 10-19**: ALL indices in this range affect hair
- **Index 44**: Skip this one (between 43 and 45)

### Cape Color Mapping Breakthrough (2024-12-09)

After color-coded sprite testing, we definitively mapped the cape structure:

**Cape Components & Their Indices:**
- **Main Cape Body**: Indices 6-10 in palette 0 (primary cape color)
- **Cape Edge/Trim**: Index 7 in palettes 0-3 (the literal border/edge of the cape)
- **Cape Accent/Shadows**: Index 9 in palettes 0-3 (darker areas for depth/shadows)
- **Additional Details**: Indices 12-15 in palettes 0-3 (extra armor/cape details)
- **Buckles/Clasps**: Indices 3-5 in palette 0 (metal accents, trim pieces)

**Why Previous Attempts Failed:**
- Working themes (corpse_brigade, lucavi) actually **swap entire palettes** rather than modifying individual colors
- Cape edges/accents appear because they use colors from MULTIPLE palettes simultaneously
- **Solution**: Must modify index 7 and 9 across palettes 0-3 for complete cape coverage

**Proper Color Relationships for Cohesive Themes:**
- **Cape Edge** (index 7): Should be ~25% darker than main cape color
- **Cape Accent/Shadow** (index 9): Should be ~50% darker than main cape color
- **Buckles/Clasps** (indices 3-5): Use accent color for two-tone themes

## Creating Custom Themes

### Method 1: Using create_cohesive_theme.py (Recommended)

```python
# Example: Create a royal purple theme with gold accents
python create_cohesive_theme.py --name "royal_guard" --primary "#6B46C1" --accent "#FFD700"

# The script will:
# 1. Apply purple to main armor (indices 6-10, 20-31, 35-47, 51-62)
# 2. Create darker purple for cape edges (25% darker at index 7)
# 3. Create shadow purple for cape depth (50% darker at index 9)
# 4. Apply gold to buckles and clasps (indices 3-5)
```

### Method 2: Using create_sprite_theme.py

```python
# Example: Create two-tone themes with specific index control
accent_indices = "3,4,5"  # Gold trim, silver buckles, etc.
primary_indices = "6,7,8,9,20,21,22,23,24,25,26,27,28,29,30,31,35,36,37,38,39,40,41,42,43,45,46,47,51,52,53,54,55,56,57,58,59,60,61,62"

python create_sprite_theme.py --source original --name "knight_commander" \
  --custom-indices $primary_indices --custom-color "#1E3A5F" \
  --custom-indices $accent_indices --custom-color "#C0C0C0"
```

## Important Technical Limitations

### DirectX 12 Sprite Refresh Issue
FFT uses DirectX 12 for rendering, which fundamentally affects how sprites update:
- Textures are uploaded to GPU memory via command lists
- Once in VRAM, textures remain cached until explicitly released
- CPU-side memory modifications don't affect GPU-cached textures
- **Result**: File swapping works for new sprites but not already-loaded ones

**User Workarounds:**
1. Change scenes (enter/exit battle, change maps)
2. Restart the game
3. Wait for unloaded sprites to appear with new colors

### Ramza Sprite DLC Protection
- Ramza's sprite appears to be DLC-locked
- File modifications trigger DLC validation failure
- Other story characters work fine with file swapping
- This is a known limitation without a current solution

## Theme Creation Best Practices

1. **Test with Single Sprite First**: Use the `--single` flag to test on one sprite before batch processing
2. **Preserve Hair Colors**: Never modify indices 10-19 to keep original hair colors
3. **Use Color Relationships**: Make cape edges 25% darker and shadows 50% darker than base color
4. **Two-Tone Themes**: Use accent colors sparingly on buckles/clasps for best visual impact
5. **Backup Original Sprites**: Always keep backups before batch modifications

## File Structure

```
ColorMod/FFTIVC/data/enhanced/fftpack/unit/
├── sprites_original/          # Base sprites (never modify)
├── sprites_corpse_brigade/    # Blue armor theme
├── sprites_lucavi/           # Dark demon theme
├── sprites_northern_sky/     # Holy knight theme
├── sprites_[custom]/         # Your custom themes
└── *.bin                     # Active sprites (swapped by F1/F2)
```

## Mod Implementation Details

### Current Implementation (v0.5.0)
- **F1/F2 Cycling**: Hotkeys cycle through available color schemes
- **File Swapping**: Copies sprites to `unit/` directory for real-time changes
- **Preferences**: Saves/loads color choice between sessions
- **Auto-Detection**: Automatically finds all `sprites_*` directories
- **Dynamic Loading**: No hardcoded scheme list needed

### Key Components (C# Mod)
- `MonitorHotkeys()`: F1/F2 key detection
- `ProcessHotkeyPress()`: Cycles through schemes
- `SwitchPacFile()`: Sprite file swapping mechanism
- `SetColorScheme()`: Saves preference to config

### Adding New Color Schemes
1. Create new `sprites_[name]` folder in `ColorMod/FFTIVC/data/enhanced/fftpack/unit/`
2. Add all 38 required job sprites to the folder
3. Deploy using BuildLinked.ps1
4. The mod will auto-detect the new scheme on next launch

### Technical Details
- **Color Format**: BGR format, palette in first 512 bytes (256 colors)
- **First 288 bytes**: Contains the first 144 colors (legacy note, actually 512 bytes total)
- **File Swapping**: Direct file replacement approach for compatibility
- **Complete Set Required**: Each theme directory needs all 38 job sprites

## Troubleshooting

### Common Issues

**Theme not appearing in game:**
- Ensure sprite files are in correct directory structure
- Check that all 38 job sprites are present in theme folder
- Verify file names match exactly (case-sensitive)

**Colors look wrong:**
- Different armor pieces use different palettes within the same sprite
- Some sprites may need manual adjustment after batch processing
- Use single sprite testing to identify issues

**Story characters not changing:**
- Run `apply_story_characters.py` to copy story sprites to themes
- Some characters (like Mustadio) have known issues
- Ramza is DLC-protected and won't work

## Development Notes

### Discovery Method
The working palette indices were discovered by:
1. Analyzing existing working themes (corpse_brigade, lucavi, etc.)
2. Identifying which indices they modify
3. Testing with color-coded sprites to map exact armor components
4. Confirming that indices 10-19 affect hair and should be avoided

### Memory Patching Results (December 2024)
Direct memory patching was attempted but causes game crashes due to:
- Memory access violations when scanning protected regions
- CLR errors when interacting with managed memory
- Process handle issues with OpenProcess permissions
- DirectX 12 architecture preventing runtime texture updates

File swapping remains the only reliable method for sprite color modification.

## Contributing

When creating new themes:
1. Use the provided scripts rather than manual editing
2. Test thoroughly with multiple job classes
3. Document any new discoveries about palette indices
4. Share successful theme configurations

## License

For personal use only. Final Fantasy Tactics © Square Enix.# FFT Complete Sprite Mapping - All 138 Sprites

## Overview
The FFT game files contain **138 total sprite files**, far more than just the 38 generic job sprites that the color mod currently supports. This document maps ALL sprites found in the game.

## WotL Jobs Support (Dark Knight & Onion Knight)

### Overview
FFT Color Customizer now supports theming for War of the Lions exclusive jobs:
- **Dark Knight** (Male & Female)
- **Onion Knight** (Male & Female)

### Requirements
These jobs require the **GenericJobs mod** to be installed:
- Mod ID: `ffttic.jobs.genericjobs` v0.0.7+
- The mod enables these PSP-exclusive jobs in the game

### How It Works
WotL job sprites are stored in a different location than regular generic jobs:
- **Regular jobs**: `fftpack/unit/` directory
- **WotL jobs**: `fftpack/unit_psp/` directory

The Color Customizer automatically detects WotL jobs and routes their sprites to the correct `unit_psp` directory.

### Sprite File Names
| Job | Sprite File |
|-----|-------------|
| Dark Knight (Male) | `spr_dst_bchr_ankoku_m_spr.bin` |
| Dark Knight (Female) | `spr_dst_bchr_ankoku_w_spr.bin` |
| Onion Knight (Male) | `spr_dst_bchr_tama_m_spr.bin` |
| Onion Knight (Female) | `spr_dst_bchr_tama_w_spr.bin` |

### Creating Themes for WotL Jobs
1. **Extract original sprites** from `unit_psp/` directory
2. **Create theme using same process** as generic jobs (see scripts below)
3. **Place themed sprites** in theme directory with correct filename
4. Theme files go in: `sprites_[theme]/spr_dst_bchr_[job]_[m/w]_spr.bin`

### Configuration UI
WotL Jobs appear in a dedicated **"WotL Jobs"** section in the configuration form, located after the Story Characters section.

---

## Sprite Categories Breakdown

### 1. Generic Job Sprites (38 sprites) - FULLY SUPPORTED BY COLOR MOD ✅
See `SPRITE_UNIT_MAPPING.md` for detailed mapping of these 38 sprites.

### 2. Story Characters & NPCs (45+ sprites)
These are named characters from the story. Some are partially supported by the color mod.

#### Main Story Characters
| Sprite File | Character | Status | Notes |
|------------|-----------|---------|--------|
| battle_ramuza_spr.bin | **Ramza (Chapter 1)** | ❌ DLC Protected | Main protagonist |
| battle_ramuza2_spr.bin | **Ramza (Chapter 2-3)** | ❌ DLC Protected | Mercenary outfit |
| battle_ramuza3_spr.bin | **Ramza (Chapter 4)** | ❌ DLC Protected | Heretic outfit |
| battle_aguri_spr.bin | **Agrias** | ✅ Working | Holy Knight |
| battle_kanba_spr.bin | **Agrias (Alt)** | ✅ Working | Alternative sprite |
| battle_dily_spr.bin | **Delita (Chapter 1)** | ❓ Untested | Young Delita |
| battle_dily2_spr.bin | **Delita (Black Armor)** | ❓ Untested | Arc Knight |
| battle_dily3_spr.bin | **Delita (Chapter 4)** | ❓ Untested | Holy Knight |
| battle_hime_spr.bin | **Ovelia** | ❓ Untested | Princess |
| battle_aruma_spr.bin | **Alma** | ❓ Untested | Ramza's sister |
| battle_simon_spr.bin | **Simon** | ❓ Untested | Scholar |

#### Playable Special Characters
| Sprite File | Character | Status | Notes |
|------------|-----------|---------|--------|
| battle_musu_spr.bin | **Mustadio** | ❌ Not Working | Engineer/Machinist |
| battle_oru_spr.bin | **Orlandeau** | ✅ Working | Thunder God |
| battle_beio_spr.bin | **Beowulf** | ✅ Working | Templar |
| battle_reze_spr.bin | **Reis (Human)** | ✅ Working | Beowulf's love |
| battle_reze_d_spr.bin | **Reis (Dragon)** | ✅ Working | Holy Dragon form |
| battle_mara_spr.bin | **Malak/Marach** | ✅ Working | Heaven Knight |
| battle_rafa_spr.bin | **Rafa/Rapha** | ❓ Untested | Heaven Knight |
| battle_cloud_spr.bin | **Cloud Strife** | ❓ Untested | FF7 Guest |
| battle_rudo_spr.bin | **Luso** | ❓ Untested | PSP exclusive |
| battle_baru_spr.bin | **Balthier** | ❓ Untested | PSP exclusive |

#### Enemy Leaders & Bosses
| Sprite File | Character | Notes |
|------------|-----------|--------|
| battle_wigu_spr.bin | **Wiegraf** | Corpse Brigade leader |
| battle_veri_spr.bin | **Wiegraf/Velius** | Lucavi form |
| battle_garu_spr.bin | **Gaffgarion** | Dark Knight |
| battle_zarumou_spr.bin | **Zalmo** | Heretic Examiner |
| battle_zaru_spr.bin | **Zalbag** | Ramza's brother |
| battle_zaru2_spr.bin | **Zalbag (Undead)** | Vampire |
| battle_arute_spr.bin | **Argath/Algus** | Chapter 1 antagonist |
| battle_dami_spr.bin | **Dycedarg** | Ramza's eldest brother |
| battle_adora_spr.bin | **Adrammelech** | Lucavi demon |
| battle_bariten_spr.bin | **Barinten** | Grand Duke |
| battle_eru_spr.bin | **Elmdore** | Marquis/Vampire |
| battle_seria_spr.bin | **Celia** | Assassin |
| battle_ledy_spr.bin | **Lettie** | Assassin |
| battle_voru_spr.bin | **Vormav/Folmarv** | Temple Knight |
| battle_kyuku_spr.bin | **Cuchulainn** | Lucavi demon |
| battle_kasanem_spr.bin | **Hashmal** | Lucavi demon |
| battle_kasanek_spr.bin | **Hashmal (Alt)** | Lucavi demon variant |
| battle_ajora_spr.bin | **Ultima/Altima** | Final boss |

### 3. Monsters & Creatures (50+ sprites)
These are enemy creatures and beasts encountered in battles.

#### Beast/Animal Types
| Sprite File | Monster Type | Description |
|------------|--------------|-------------|
| battle_cyoko_spr.bin | **Chocobo** | Yellow chocobo |
| battle_gob_spr.bin | **Goblin** | Basic goblin |
| battle_bom_spr.bin | **Bomb** | Explosive creature |
| battle_tori_spr.bin | **Cockatrice** | Bird monster |
| battle_hebi_spr.bin | **Serpent/Snake** | Snake type |
| battle_ika_spr.bin | **Squid/Kraken** | Aquatic monster |
| battle_hyou_spr.bin | **Panther** | Cat family |
| battle_uri_spr.bin | **Boar/Pig** | Wild boar |
| battle_minota_spr.bin | **Minotaur** | Bull monster |
| battle_mol_spr.bin | **Morbol/Malboro** | Plant monster |
| battle_behi_spr.bin | **Behemoth** | King of beasts |
| battle_dora_spr.bin | **Dragon** | Basic dragon |
| battle_dora1_spr.bin | **Dragon (Variant 1)** | Different dragon |
| battle_dora2_spr.bin | **Dragon (Variant 2)** | Another dragon |

#### Demon/Undead Types
| Sprite File | Monster Type | Description |
|------------|--------------|-------------|
| battle_demon_spr.bin | **Demon** | Basic demon |
| battle_cyomon1_spr.bin | **Demon Type 1** | Demon variant |
| battle_cyomon2_spr.bin | **Demon Type 2** | Demon variant |
| battle_cyomon3_spr.bin | **Demon Type 3** | Demon variant |
| battle_cyomon4_spr.bin | **Demon Type 4** | Demon variant |
| battle_sukeru_spr.bin | **Skeleton** | Undead warrior |
| battle_yurei_spr.bin | **Ghost/Wraith** | Spirit type |
| battle_gyumu_spr.bin | **Reaper** | Death type |

### 4. Special/Unique Sprites (15+ sprites)
These appear to be special units, alternate forms, or cutscene-specific sprites.

#### Age/Status Variants
| Sprite File | Description | Usage |
|------------|-------------|-------|
| battle_10m_spr.bin | **Young Male** | Child sprite |
| battle_10w_spr.bin | **Young Female** | Child sprite |
| battle_20m_spr.bin | **Adult Male** | Civilian |
| battle_20w_spr.bin | **Adult Female** | Civilian |
| battle_40m_spr.bin | **Middle-aged Male** | Noble/merchant |
| battle_40w_spr.bin | **Middle-aged Female** | Noble/merchant |
| battle_60m_spr.bin | **Elder Male** | Elder/sage |
| battle_60w_spr.bin | **Elder Female** | Elder/sage |
| battle_souryo_spr.bin | **Priest/Cleric** | Religious figure |

#### Special Designations
| Sprite File | Possible Use |
|------------|--------------|
| battle_kanzen_spr.bin | Perfect/Complete form |
| battle_other_spr.bin | Miscellaneous/Other |
| battle_wep_spr.bin | Weapon sprite? |
| battle_ki_spr.bin | Tree/Wood element? |
| battle_tetsu_spr.bin | Iron/Metal element? |
| battle_h61_spr.bin through battle_h85_spr.bin | Unknown special units |

### 5. Previously Unknown Characters - NOW IDENTIFIED
Based on research, these sprites with Japanese or coded names have been identified:

| Sprite File | Character Identity | Role/Description |
|------------|-------------------|------------------|
| battle_aru_spr.bin | **Alphonse/Worker Unit** | Likely Alphonse Delacroix or generic worker |
| battle_arufu_spr.bin | **Cardinal Alphonse Delacroix** | Religious leader, Chapter 2 |
| battle_arli_spr.bin | **Alternate Argath/Algus** | Possibly different chapter version |
| battle_baruna_spr.bin | **Balk/Barich Fendsor** | Temple Knight Machinist, gun user |
| battle_daisu_spr.bin | **Darlavon/Daravon** | Tutorial professor at Gariland Academy |
| battle_fyune_spr.bin | **Unknown/Funeral Related** | Still unidentified |
| battle_gando_spr.bin | **Duke Goltanna** | Ruler of Gallionne, political figure |
| battle_goru_spr.bin | **Golagros/Gragoroth Levigne** | Corpse Brigade leader |
| battle_hasyu_spr.bin | **Worker 7/Construct 7** | Ancient automaton/robot |
| battle_ragu_spr.bin | **Ladd/Radd** | Mercenary with Gaffgarion |
| battle_furaia_spr.bin | **Byblos** | Monster/demon type (furaia = flyer) |
| battle_zarue_spr.bin | **Zalera the Death Seraph** | Lucavi demon/Esper |

## Statistics Summary

### Total Sprites: 138

### By Category:
- **Generic Jobs**: 38 sprites (27.5%)
- **Story Characters**: ~45 sprites (32.6%)
- **Monsters/Creatures**: ~50 sprites (36.2%)
- **Special/Other**: ~5 sprites (3.6%)

### Color Mod Support:
- **Fully Supported**: 38 generic jobs (27.5%)
- **Partially Working**: 5 story characters (3.6%)
- **Not Working/Untested**: 95 sprites (68.8%)

## Technical Notes

### Sprite File Format
- All sprites use `.bin` format
- Located in: `data/enhanced/fftpack/unit/`
- Size varies but typically 20-40KB per sprite
- Contains palette data in first 512 bytes

### Why Only 38 Are Modded
The color mod focuses on the 38 generic job sprites because:
1. These are the most commonly used sprites by player units
2. They share consistent palette structures
3. They're not DLC-protected like Ramza
4. They don't have unique palette requirements like monsters

### Future Expansion Possibilities
- Story character support (partially implemented)
- Monster recoloring for variety
- NPC/civilian sprite theming
- Boss sprite alternate colors

## Sprite Naming Conventions

### Japanese Terms Used
- **cyoko** (チョコボ) = Chocobo
- **kuro** (黒) = Black
- **siro** (白) = White
- **toki** (時) = Time
- **syou** (召) = Summon
- **onmyo** (陰陽) = Yin-yang (Oracle/Mystic)
- **san** (算) = Calculate
- **samu** (侍) = Samurai
- **ryu** (竜) = Dragon
- **fusui** (風水) = Feng Shui (Geomancer)
- **waju** (話術) = Speech art (Mediator)
- **gin** (吟) = Recite/sing (Bard)
- **odori** (踊) = Dance
- **mono** (物真似) = Imitate (Mime)
- **yumi** (弓) = Bow (Archer)
- **mina** (皆) = Everyone (Squire)
- **hebi** (蛇) = Snake
- **tori** (鳥) = Bird
- **ika** (烏賊) = Squid
- **hyou** (豹) = Panther
- **uri** (瓜) = Boar
- **behi** = Behemoth
- **dora** = Dragon
- **gob** = Goblin
- **bom** = Bomb
- **yurei** (幽霊) = Ghost
- **sukeru** = Skeleton
- **souryo** (僧侶) = Priest/Monk

---
*Last Updated: December 2024*
*Total Game Sprites: 138*
*Currently Moddable: 38 generic + 5 story characters*
