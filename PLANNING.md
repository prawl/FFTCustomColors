# FFT Color Mod - Technical Planning & Research
<!-- KEEP UNDER 100 LINES -->

## 🚨 CRITICAL DISCOVERY #1: StartEx vs Start - THE BUG!
**Our `Start()` method NEVER gets called by Reloaded-II!**

### The Fix (From FFTGenericJobs):
```csharp
// Template/Startup.cs - REQUIRED PATTERN
public class Startup : IMod {
    public void StartEx(IModLoaderV1 loaderApi, IModConfigV1 modConfig) {
        // This DOES get called!
        _mod = new Mod(new ModContext {
            Hooks = _hooks,
            Logger = _logger,
            ModLoader = _modLoader
        });
    }
}

// Mod.cs - Receives context in constructor
public class Mod : ModBase {
    public Mod(ModContext context) {
        // Everything available immediately!
        _hooks = context.Hooks;
        Initialize(); // Can hook right away!
    }
}
```

### Required Dependencies:
```json
"ModDependencies": [
    "Reloaded.Memory.SigScan.ReloadedII",  // CRITICAL!
    "reloaded.sharedlib.hooks"
]
```

## 🚨 CRITICAL DISCOVERY #2: Two Successful Modding Approaches

### Path A: Simple File Override (Proven by 4 mods)
- Place modified files in `FFTIVC/data/enhanced/` structure
- FFT automatically loads replacements
- **NO CODE NEEDED** for basic asset replacement
- Works for: Black Boco, better_palettes, WotL Characters

### Path B: Memory Hooking (FFTGenericJobs)
```csharp
// Hook sprite loading to modify palettes AS they load:
private nint LoadSpriteHook(nint spriteData, int size) {
    var result = _loadSpriteHook.OriginalFunction(spriteData, size);

    // Apply PaletteDetector during load!
    if (_paletteDetector.DetectChapterOutfit(spriteData)) {
        _paletteDetector.ReplacePaletteColors(spriteData, _currentScheme);
    }

    return result;
}
```

## 🚨 CRITICAL DISCOVERY #3: File Format Clarification

**Sprites are `.bin` files, NOT `.spr`!**
- Location: `FFTIVC/data/enhanced/fftpack/unit/`
- Format: `battle_[name]_spr.bin`
- Example: `battle_ramza_spr.bin`

## Implementation Paths

### Immediate Fix (StartEx):
1. Create Template/Startup.cs with StartEx
2. Update Mod.cs to accept ModContext
3. Add dependencies to ModConfig.json

### Option A: File Override (1-2 days):
1. Extract `battle_ramza_spr.bin`
2. Generate color variants with PaletteDetector
3. Place in FFTIVC structure
4. Test hotkey switching

### Option B: Memory Hooks (3-4 days):
1. Add Reloaded.Memory dependencies
2. Find sprite loading signatures
3. Hook and modify during load
4. Prevent palette reloading

### Option C: Hybrid (Recommended):
Start with file override, add hooks for real-time switching

## Why Our Approach Failed

**Root Cause**: FFT continuously reloads palettes from source files
- Memory modifications get overwritten
- Need to intercept at load time, not after
- StartEx bug prevented initialization entirely

## Next Steps (Priority Order)

1. **Fix StartEx bug** - Mod doesn't initialize without this!
2. **Update for .bin files** - Wrong extension blocked extraction
3. **Choose implementation** - File override vs memory hooks
4. **Test with existing sprites** - We have sample files from other mods
5. **Deploy and verify** - Hotkeys should work immediately after StartEx fix

## 🔍 Mod Repository Analysis - WotL Characters

### Repository Overview
**Location**: `C:\Users\ptyRa\Dev\WotL Characters`
**Description**: Adds Balthier and Luso characters to FFT
**Author**: Dana Crysalis
**Version**: 1.0.1

### Directory Structure
```
WotL Characters/
├── FFTIVC/                              # Main mod content directory
│   └── data/
│       └── enhanced/
│           ├── nxd/                     # Game data files (NXD format)
│           │   ├── ability.en.nxd       # Abilities
│           │   ├── charaname.en.nxd     # Character names
│           │   ├── charshape.nxd        # Character appearance data
│           │   ├── job.en.nxd           # Job definitions
│           │   ├── jobcommand.en.nxd    # Job commands
│           │   └── uijobabilityhelp.en.nxd
│           └── ui/                      # User interface assets
│               └── ffto/
│                   └── common/
│                       └── face/        # Character face portraits
│                           ├── texture/
│                           │   ├── wldface_163_08_uitx.tex  # Balthier face
│                           │   └── wldface_164_08_uitx.tex  # Luso face
│                           └── textureparts/
│                               ├── wldface_163_08_uitx.utexpt
│                               └── wldface_164_08_uitx.utexpt
├── ModConfig.json                       # Reloaded-II mod configuration
├── WotLCharacters.dll                   # Compiled mod assembly (.NET 9.0)
├── WotLCharacters.deps.json             # .NET dependencies
├── WotLCharacters.pdb                   # Debug symbols
├── Reloaded.Hooks.Definitions.dll      # Hook framework
├── Reloaded.Hooks.ReloadedII.Interfaces.dll
└── Preview.png                          # Mod preview image
```

### Key Insights for Character Sprite Modification

#### 1. **File-Based Asset Replacement Approach**
WotL Characters uses **pure file replacement** - no memory manipulation or function hooking. The mod works by:
- Placing replacement files in the exact same directory structure as the original game
- FFT automatically loads the replacement files instead of originals
- No runtime code execution required for basic asset replacement

#### 2. **Character Data Components**
Character modifications involve multiple file types:
- **NXD files**: Core character data (names, abilities, appearance parameters)
- **TEX files**: Face portrait textures
- **UTEXPT files**: Texture part definitions
- **Character shape data**: Physical appearance and sprite references

#### 3. **FFT File Override System**
The mod demonstrates FFT's **hierarchical file loading**:
```
Game searches for: data/enhanced/ui/ffto/common/face/texture/wldface_163_08_uitx.tex
1. Checks mod directories first (FFTIVC/data/enhanced/...)
2. Falls back to original game files if not found
3. No special hooking required - just file placement
```

#### 4. **Character Face Portrait System**
- **File naming pattern**: `wldface_[ID]_08_uitx.tex`
- **ID mapping**: 163 = Balthier, 164 = Luso
- **Format**: TEX format (custom texture format)
- **Size**: ~14KB per face texture
- **Companion files**: Corresponding .utexpt texture part files

#### 5. **Texture File Format Analysis**
From hexdump of `wldface_163_08_uitx.tex`:
- **Header**: "TEX " signature (0x54455820)
- **Format**: Custom FFT texture format, not standard image format
- **Size**: 14,520 bytes for face textures
- **Structure**: Binary format with embedded texture data

#### 6. **Configuration and Dependencies**
```json
// ModConfig.json reveals dependency pattern
"ModDependencies": [
    "fftivc.utility.modloader",    # FFT-specific mod utilities
    "reloaded.sharedlib.hooks"     # Hook framework (unused for pure asset replacement)
]
```

#### 7. **Character Definition System**
The `charshape.nxd` file contains character appearance definitions:
- Binary format with structured character data
- Links character IDs to sprite references
- Defines visual parameters and animations

### Implications for FFT Color Mod

#### ✅ **Validation of File-Based Approach**
- WotL Characters proves **file replacement works** for FFT modifications
- No memory manipulation needed for basic asset changes
- Simpler implementation than function hooking

#### 🎯 **Key Learnings for Sprite Colors**
1. **File structure is critical** - exact path matching required
2. **Multiple file types** may be involved for complete character changes
3. **TEX format** is used for textures, may apply to sprite files too
4. **ID-based naming** suggests systematic file organization

#### 🔄 **Revised Implementation Strategy**
Based on WotL Characters success with file replacement:
1. **Extract original sprite files** to proper directory structure
2. **Generate color variants** using our existing PaletteDetector
3. **Place variants in mod directory** following FFT's path conventions
4. **Use file redirection** instead of memory hooks for hotkey switching

#### ⚠️ **Differences from Our Use Case**
- WotL Characters adds **new** characters (face portraits)
- We need to **modify existing** character sprites (Ramza)
- Our changes are **runtime/hotkey based**, theirs are permanent
- May need to combine file replacement with selective loading logic

### Technical Architecture Comparison

| Aspect | WotL Characters | FFT Color Mod (Planned) |
|--------|----------------|--------------------------|
| **Approach** | Static file replacement | Dynamic color switching |
| **Files Modified** | Face portraits + character data | Sprite palettes |
| **Runtime Logic** | None (pure asset swap) | Hotkey handling + file redirection |
| **Complexity** | Low (file placement) | Medium (selective loading) |
| **Memory Usage** | Minimal | Multiple color variants |

This analysis confirms that **file-based asset replacement is a proven approach** for FFT modifications and validates our pivot away from complex memory manipulation toward simpler file override systems.

## 🔍 Mod Repository Analysis - Black Boco

After analyzing the **Black Boco** FFT mod repository located at `~/Dev/'Black Boco'`, here are the key findings that reveal a **completely different approach** to FFT modding:

### Repository Structure Analysis
```
Black Boco/
├── ModConfig.json              # Mod configuration and dependencies
├── icon.png                    # Mod icon (200x200 PNG)
└── FFTIVC/
    └── data/
        └── enhanced/
            └── nxd/
                └── overrideentrydata.nxd  # Binary data file (97KB)
```

### Critical Discovery: Two Completely Different Modding Approaches

#### 1. **Black Boco Approach: File Replacement via fftivc.utility.modloader**
- **Method**: Data file replacement using `.nxd` format
- **Dependencies**: `fftivc.utility.modloader` by Nenkai
- **File Format**: `overrideentrydata.nxd` - Custom binary format with "NXDF" header
- **Approach**: Replace game data files directly, no code execution
- **Size**: Single 97KB binary data file
- **Complexity**: Simple file replacement system

#### 2. **FFTGenericJobs Approach: Function Hooking & Memory Manipulation**
- **Method**: Dynamic function hooking and memory patching
- **Dependencies**: `reloaded.sharedlib.hooks`, `Reloaded.Memory.SigScan.ReloadedII`
- **File Format**: Compiled .NET assembly (.dll) with source code
- **Approach**: Hook game functions, patch memory at runtime
- **Size**: Full C# source code project with complex initialization
- **Complexity**: Advanced reverse engineering with signature scanning

### Technical Analysis of .nxd Format

#### File Structure Discovery
```
Header: "NXDF" (4 bytes) + Version/Metadata (28 bytes)
Data:   Binary entries with offsets and sizes
        Contains palette/texture override data
        Uses structured entry system with pointers
```

#### Key Insights
1. **NXDF Format**: Custom FFT data override format
2. **Entry-Based**: Contains multiple data entries with offsets
3. **Palette Data**: Contains color/texture replacement information
4. **Game Integration**: Loaded directly by FFT without code execution

### Comparison: File Replacement vs Function Hooking

| Aspect | Black Boco (File Replacement) | FFTGenericJobs (Function Hooking) |
|--------|-------------------------------|----------------------------------|
| **Complexity** | Low - Single data file | High - Complex C# hooking system |
| **Dependencies** | fftivc.utility.modloader only | Multiple memory/hooking libraries |
| **Runtime** | No code execution | Active memory manipulation |
| **Approach** | Replace source data | Intercept game functions |
| **Maintenance** | Simple file updates | Complex signature maintenance |
| **Flexibility** | Limited to data changes | Unlimited game behavior changes |

### Implications for Our Color Mod

#### Option 1: Adopt .nxd File Format (SIMPLE)
**Pros:**
- Much simpler than function hooking
- No complex signature scanning needed
- Direct game integration via fftivc.utility.modloader
- Proven to work with color changes (Black Boco changes Boco's colors)

**Cons:**
- Need to reverse engineer .nxd format
- Limited to predefined data changes
- No real-time hotkey switching
- Less flexible than hook approach

**Implementation:**
1. Analyze Black Boco's .nxd file structure
2. Find Ramza sprite entries in the data
3. Create color variant .nxd files
4. Use fftivc.utility.modloader for file switching

#### Option 2: Continue with Function Hooking (COMPLEX)
**Pros:**
- Full control over game behavior
- Real-time hotkey switching possible
- Reuse all existing PaletteDetector code
- More powerful and flexible

**Cons:**
- Much more complex implementation
- Requires extensive reverse engineering
- Signature scanning maintenance needed
- Higher chance of breaking with updates

#### Option 3: Hybrid Approach (RECOMMENDED)
**Phase 1:** Implement .nxd approach for quick proof-of-concept
**Phase 2:** Add function hooking for real-time switching

### File Format Research Needed

To adopt the .nxd approach, we need to:
1. **Hex analyze** the `overrideentrydata.nxd` file structure
2. **Identify** sprite/palette entry formats
3. **Locate** Ramza-specific data entries
4. **Extract** the data layout and offset calculations
5. **Create** our own .nxd files with modified palettes

### Immediate Next Steps

1. **Analyze .nxd structure** using hex editor and pattern analysis
2. **Study fftivc.utility.modloader** documentation/source if available
3. **Test file replacement** approach with simple color changes
4. **Compare complexity** vs function hooking approach
5. **Choose primary implementation** path based on analysis

### Strategic Decision Point

The discovery of Black Boco reveals that **file replacement might be significantly simpler** than our current function hooking approach. We should evaluate:

- **Time to Implementation**: .nxd approach likely days vs weeks for hooking
- **Feature Requirements**: Do we need real-time switching or is mod loading sufficient?
- **Maintenance Burden**: File format changes vs signature maintenance
- **User Experience**: Mod loading vs hotkey switching

This analysis provides a **crucial alternative path** that could dramatically simplify our implementation while still achieving the core color modification goals.

## 🔍 Mod Repository Analysis - better_palettes

### Repository Overview
The **better_palettes** mod by Daytona (v2.0.7) is a comprehensive palette replacement mod that provides "cooler palettes" for generic jobs and includes a real black chocobo. The mod represents a mature implementation of asset replacement using file override methodology.

### Key Technical Findings

#### 1. **Implementation Approach: File Asset Replacement**
- **Method**: Complete file replacement, not memory patching
- **Architecture**: Creates FFTIVC folder structure to override game assets
- **Persistence**: Changes are permanent once files are copied to game directory
- **Performance**: No runtime overhead - game loads pre-modified assets directly

#### 2. **Dependencies & Configuration**
```json
// ModConfig.json - Critical Dependencies
"ModDependencies": ["fftivc.utility.modloader"]  // Required for FFT-specific functionality

// GitHubDependencies include:
- fftivc.utility.modloader (Nenkai) - FFT-specific mod loader
- Reloaded.Memory.SigScan.ReloadedII - Memory scanning capabilities
- reloaded.sharedlib.hooks - Function hooking support
```

#### 3. **File Structure & Asset Organization**
```
FFTIVC/data/enhanced/
├── fftpack/unit/           # Battle sprites (.bin files)
├── system/ffto/g2d/        # UI sprites (.bin files)
└── ui/ffto/common/face/texture/  # Portrait files (.tex files)
```

**Asset Types Discovered:**
- **Battle Sprites**: `battle_[job]_[gender]_spr.bin` (42-46 KB each)
- **Portraits**: `wldface_[id]_[variant]_uitx.tex` (26-73 KB each)
- **UI Elements**: `tex_[id].bin` (116-128 KB each)

#### 4. **Palette Variant System**
The mod uses a sophisticated variant system with multiple palette themes:

**Available Themes:**
- Smoke (dark/gray tones)
- Azure (blue/cyan tones)
- Lucavi (demonic/red tones)
- Festive (holiday colors)
- Gold_with_Blue_Cape
- Southern_Sky / Northern_Sky
- Corpse_Brigade (faction colors)
- Ginger (warm tones)
- Maid (specific for Chemist Female)
- Red_Bard (specific variant)

**Configuration Structure:**
```
config_files/units/
├── male/[JobClass]/[Variant]/
├── female/[JobClass]/[Variant]/
└── unique/[CharacterName]/[Variant]/
```

#### 5. **Asset Processing & Deployment**
From the log file analysis, the mod:
1. **Processes 50+ job variants** across male/female/unique characters
2. **Copies sprites and portraits** to game directory at runtime
3. **Handles multiple file sizes** - sprites (42-46KB), portraits (26-73KB)
4. **Supports unique characters** like Agrias with special variants
5. **Processes UI elements** for mobile and standard variants

#### 6. **Binary Asset Format Analysis**
**Sprite Files (.bin)**:
- Size range: 42-46 KB for character sprites
- Header analysis shows structured binary data (not plain bitmap)
- Likely contains palette data + compressed sprite frames
- Format appears to be FFT-specific sprite containers

**Portrait Files (.tex)**:
- Size range: 26-73 KB for face textures
- Various numbering system (096-133 range observed)
- Multiple variants per character (08, 09, 10, 11, 12 suffixes)
- Binary texture format with embedded palette data

#### 7. **Deployment Strategy**
The mod uses **runtime file copying** rather than permanent installation:
1. Maintains original assets in `config_files/`
2. Copies selected variants to `FFTIVC/data/enhanced/` at mod load
3. Game loads modified assets transparently
4. Allows variant switching by reloading mod with different configs

### Critical Insights for FFT_Color_Mod

#### 1. **File Override is Proven Effective**
- The better_palettes mod successfully overrides FFT assets using file replacement
- No memory hooking or patching required for basic palette changes
- FFTIVC folder structure is the standard for asset override

#### 2. **Asset Format Compatibility**
- Our existing `.spr.bin` approach aligns with their `.bin` sprite format
- Portrait modification (`wldface_*.tex`) could extend our scope beyond sprites
- Binary formats suggest embedded palette data that our PaletteDetector can process

#### 3. **Scalability Model**
- Pre-generated palette variants (like our color schemes) work effectively
- Runtime variant switching achievable through file management
- Multiple job classes and unique characters can be handled systematically

#### 4. **Implementation Parallels**
```csharp
// better_palettes approach (conceptual)
CopyAssetFiles(selectedVariant, destinationPath);
// Translates to our approach:
FileRedirector.RedirectPath(originalFile, GenerateColorVariant(originalFile, colorScheme));
```

#### 5. **Dependency Learning**
- `fftivc.utility.modloader` appears critical for FFT-specific functionality
- Memory.SigScan + SharedLib.Hooks suggest they may use hybrid approach
- Our Universal Redirector approach could be simpler and more direct

### Recommended Integration Strategy

#### Phase 1: Asset Format Validation
1. Test our PaletteDetector on their `.bin` files from config_files
2. Verify our color transformation works on their asset format
3. Validate FFTIVC folder structure compatibility

#### Phase 2: Hybrid Implementation
1. Use our Universal Redirector for real-time switching
2. Adopt their FFTIVC folder organization for compatibility
3. Support both sprite (.bin) and portrait (.tex) modification

#### Phase 3: Extended Coverage
1. Add support for unique characters like Agrias
2. Implement multiple palette themes beyond our 4 color schemes
3. Consider UI element modification (chocobo, etc.)

### Key Technical Advantages Identified

**For Memory Approach Advocates:**
- better_palettes proves asset replacement works reliably
- No complex signature scanning or hooking required
- Compatible with existing FFT mod ecosystem

**For Our Hotkey System:**
- Runtime file copying enables dynamic switching
- Our approach could be more responsive (no file I/O during switches)
- Hybrid model could combine both benefits

**For Performance:**
- Pre-generated assets = zero runtime processing overhead
- File-based approach = no memory scanning loops
- Compatible with existing mod infrastructure

This analysis confirms that **file-based asset replacement is a mature, proven approach** for FFT modding, while also revealing that sophisticated variants and runtime management are achievable within this paradigm.

## 🔍 Mod Repository Analysis - FFT Texture Pack

### Repository Structure Overview
The Final Fantasy Tactics Texture Pack repository provides a comprehensive example of FFT modding through the Reloaded-II framework. It consists of two main components:

1. **fftivc.asset.zoditexturepack** - The actual texture assets
2. **fftivc.config.zodioverwriter** - A configurator mod that dynamically manages texture application

### Key Architectural Insights

#### Texture Replacement System
The texture pack uses a **dynamic file replacement** approach rather than memory modification:

- **Path Structure**: `FFTIVC/data/enhanced/[category]/` mirrors the game's internal file structure
- **File Override**: Places replacement files at exact game paths to override defaults
- **No Memory Hooks**: Pure file-level replacement without runtime memory manipulation

#### Character-Related Discoveries

**Color Lookup Tables (LUTs)**:
- `lut_chara.tga` (64x64 RGBA) - Character color lookup table
- `lut_uichara.tga` (slightly larger) - UI character color table
- These are **palette transformation files** that could apply to sprite coloring

**Sprite References**:
- Configuration includes `SpriteOption` enum (Original, Mobile)
- Suggests different sprite sets can be swapped
- Mobile sprites mentioned as iOS/Android variants with different styling

#### Configurator Pattern - Dynamic Asset Management

The configurator uses a **resource bundling** approach:

```csharp
// Pattern from Mod.cs
string sourceDir = Path.Combine(_modRoot, "Resources", "StatusIcons", "PSX");
string targetDir = Path.Combine(texturePackDir, "FFTIVC", "data", "enhanced", "ui", "ffto", "icon");

if (option == Original) {
    DeleteManagedFiles(referenceDir, targetDir);  // Remove overrides
} else {
    CopyDirectory(sourceDir, targetDir);          // Apply overrides
}
```

**Key Insights**:
1. **Resources folder** contains all variant assets
2. **Copy/Delete operations** enable real-time switching
3. **Managed file tracking** ensures clean state transitions

#### Texture File Formats and Locations

**Primary Formats**:
- `.tga` files for most textures (background, UI elements, color LUTs)
- `.tex` files for UI textures (`ui_unit_tex_uitx.tex`, `ui_unit_info_assets_uitx.tex`)
- Numbered directories (001-116) contain map-specific textures

**Directory Structure Pattern**:
```
FFTIVC/data/enhanced/
├── bg/textures/           # Background textures by map number
├── ui/ffto/               # UI-related textures
│   ├── battle/texture/    # Battle UI elements
│   ├── common/texture/    # Shared UI components
│   └── unit/texture/      # Unit-specific UI
├── system/ffto/g2d/       # System graphics
└── vfx/post_process/      # Visual effects
```

### Applications to Sprite Color Modification

#### 1. LUT-Based Color Transformation
The `lut_chara.tga` files suggest FFT uses lookup tables for character coloring:
- Could potentially **hook LUT loading** instead of individual sprites
- **Single LUT modification** might affect all character sprites
- More efficient than per-sprite palette replacement

#### 2. Resource Management Pattern
The configurator's approach could be adapted for sprite colors:
```csharp
// Conceptual adaptation
string ramzaOriginal = "Resources/Sprites/Original/battle_ramza_spr.bin";
string ramzaRed = "Resources/Sprites/Red/battle_ramza_spr.bin";
// Copy appropriate variant to game path based on hotkey
```

#### 3. File Structure Insights
- **Exact path matching required** - game expects files at specific locations
- **File extension consistency** - `.bin` for sprites, `.tga` for color data
- **Nested directory structure** must be preserved exactly

#### 4. Configuration Integration
The Reloaded-II configuration system could be extended for color schemes:
```csharp
public enum CharacterColorScheme { Original, Red, Blue, Green, Purple }

[Category("Character Colors")]
[DisplayName("Ramza Color Scheme")]
public CharacterColorScheme RamzaColors { get; set; } = CharacterColorScheme.Original;
```

### Technical Implementation Recommendations

#### Option 1: LUT Interception (Most Promising)
- Hook loading of `lut_chara.tga` files
- Generate color-transformed LUTs for each scheme
- Single intercept point affects all sprites

#### Option 2: Sprite File Redirection (Current Approach)
- Continue with individual sprite file replacement
- Use configurator pattern for resource management
- Maintain separate sprite variants per color scheme

#### Option 3: Hybrid Approach
- Use LUT transformation for real-time color changes
- Fall back to file replacement for complex modifications
- Best performance with maximum flexibility

### Dependencies and Compatibility
The texture pack successfully integrates with:
- `fftivc.utility.modloader` - Core FFT mod loader
- `Reloaded.Memory.SigScan.ReloadedII` - For potential memory operations
- `reloaded.sharedlib.hooks` - Function hooking capability

This suggests our color mod could coexist with texture modifications using the same dependency chain.

### Critical Observation: No Sprite Color Modifications
**Important**: The texture pack focuses on UI, backgrounds, and effects but does **not modify character sprite colors**. This confirms that **character sprite coloring remains an unsolved challenge** in the FFT modding community, making our approach potentially groundbreaking if successful.