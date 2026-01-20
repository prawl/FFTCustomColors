# FFTColorCustomizer Architecture

**FFT: The Ivalice Chronicles** color customization mod for **Reloaded-II** mod loader.

---

## System Overview Diagram

```
                                    RELOADED-II MOD LOADER
                                            |
                                            v
+==================================================================================================+
|                                        MOD ENTRY POINT                                           |
+==================================================================================================+
|                                                                                                  |
|   Mod.cs (IMod) --------> ModBootstrapper --------> ServiceContainer (DI)                       |
|       |                        |                           |                                    |
|       |                        +--------> ModInitializer   +--------> ServiceRegistry           |
|       |                        |               |                                                |
|       |                        +--------> HotkeyManager (F1 to open UI)                         |
|       |                                                                                         |
|       +-----> InterceptFilePath() --> FileInterceptor --> SpriteFileInterceptor                 |
|                                                                                                  |
+==================================================================================================+
                                            |
             +------------------------------+------------------------------+
             |                              |                              |
             v                              v                              v
+========================+    +===========================+    +========================+
|   CONFIGURATION LAYER  |    |    THEME MANAGEMENT       |    |     SPRITE OPERATIONS  |
+========================+    +===========================+    +========================+
|                        |    |                           |    |                        |
| Config.cs              |    | ThemeCoordinator          |    | ConfigBasedSpriteManager|
|   - JobThemes{}        |    |   - GetCurrentTheme()     |    |   - ApplyConfiguration()|
|   - StoryCharacterThemes{}| |   - CycleTheme()          |    |                        |
|   - RamzaHslSettings   |    |   - InterceptFilePath()   |    | +--------------------+ |
|                        |    |                           |    | | SpritePathResolver | |
| ConfigurationCoordinator|   | ThemeManagerAdapter       |    | | SpriteFileCopier   | |
|   - LoadConfiguration()|    |   - ApplyCharacterTheme() |    | | UserThemeApplicator| |
|   - SaveConfiguration()|    |   - CycleCharacterTheme() |    | | SpriteFileInterceptor|
|   - OpenConfigUI()     |    |                           |    | +--------------------+ |
|                        |    | ThemeService              |    |                        |
| ConfigurationManager   |    |   - ApplyTheme()          |    | RamzaNxdService        |
|   - Persistence (JSON) |    |   - GetAvailableThemes()  |    |   - NXD palette patching|
|                        |    |                           |    |   - Per-chapter colors |
+========================+    +===========================+    +========================+
             |                              |                              |
             +------------------------------+------------------------------+
                                            |
                                            v
+==================================================================================================+
|                                     USER INTERFACE LAYER                                         |
+==================================================================================================+
|                                                                                                  |
|   ConfigurationForm (WPF) ---+---> Generic Characters Section (38 jobs)                         |
|       |                      |                                                                  |
|       |                      +---> Story Characters Section (13+ characters)                    |
|       |                      |                                                                  |
|       |                      +---> WotL Jobs Section (Dark Knight, Onion Knight)                |
|       |                      |                                                                  |
|       |                      +---> Theme Editor Section                                         |
|       |                      |         |                                                        |
|       |                      |         +---> ThemeEditorPanel                                   |
|       |                      |         +---> HslColorPicker                                     |
|       |                      |         +---> PaletteModifier                                    |
|       |                      |                                                                  |
|       |                      +---> My Themes Section                                            |
|       |                                                                                         |
|       +---> CharacterRowBuilder (Dynamic row creation)                                          |
|       +---> PreviewCarousel (Theme preview with lazy loading)                                   |
|       +---> LazyLoadingManager (Deferred image loading)                                         |
|                                                                                                  |
+==================================================================================================+
                                            |
                                            v
+==================================================================================================+
|                                    FILE SYSTEM & DATA LAYER                                      |
+==================================================================================================+
|                                                                                                  |
|   SPRITE FILES:                          | CONFIGURATION:                                        |
|   FFTIVC/data/enhanced/fftpack/          | %RELOADEDIIMODS%/FFTColorCustomizer/                 |
|       unit/                              |     Config.json (User settings)                       |
|           sprites_original/              |     UserThemes.json (Custom themes)                   |
|           sprites_dark_knight/           |     UserThemes/[Job]/[theme]/sprite.bin              |
|           sprites_lucavi/                |                                                       |
|           ...                            | DATA FILES:                                           |
|       unit_psp/ (WotL jobs)              |     ColorMod/Data/                                    |
|           sprites_original/              |         StoryCharacters.json                          |
|           ...                            |         JobClasses.json                               |
|                                          |         WotLClasses.json                              |
|   RAMZA TEX FILES:                       |         SectionMappings/[Job].json                    |
|   FFTIVC/data/enhanced/system/ffto/g2d/  |                                                       |
|       tex_830.bin - tex_839.bin          |                                                       |
|                                          |                                                       |
|   NXD PALETTES:                          |                                                       |
|   FFTIVC/data/enhanced/nxd/              |                                                       |
|       charclut.nxd (Ramza color lookup)  |                                                       |
|                                                                                                  |
+==================================================================================================+
```

---

## Component Layers

### Layer 1: Mod Entry Point & Bootstrap

```
Mod.cs (Reloaded-II Entry Point)
    |
    +-- Mod() constructor
    |       |
    |       +-- ModBootstrapper.CreateForProduction()
    |       |       |
    |       |       +-- ServiceContainer (DI)
    |       |       +-- ServiceRegistry.ConfigureServices()
    |       |       +-- ModInitializer
    |       |       +-- ConfigurationCoordinator
    |       |       +-- ThemeCoordinator
    |       |
    |       +-- InitializeHotkeys() --> HotkeyManager (F1 opens UI)
    |
    +-- Start() --> Apply initial configuration
    |
    +-- InterceptFilePath() --> FileInterceptor --> Themed sprite path
```

**Key Responsibilities:**
- `Mod.cs`: Thin orchestrator implementing `IMod` interface
- `ModBootstrapper`: Factory for creating production/test configurations
- `ServiceContainer`: Generic dependency injection container
- `FileInterceptor`: Runtime sprite path interception

---

### Layer 2: Configuration System

```
Config.cs (Model)
    |
    +-- Dictionary<string, string> _jobThemes
    |       Key: "Knight_Male" --> Value: "dark_knight"
    |
    +-- Dictionary<string, string> _storyCharacterThemes
    |       Key: "Ramza" --> Value: "white_heretic"
    |
    +-- RamzaHslSettings (HSL color overrides)

ConfigurationCoordinator (Facade)
    |
    +-- GetConfiguration() --> Config
    +-- SetJobColor(jobProperty, scheme)
    +-- SaveConfiguration() --> ConfigurationManager.SaveConfig()
    +-- ApplyConfiguration() --> ConfigBasedSpriteManager

ConfigurationManager (Persistence)
    |
    +-- LoadConfig() --> JSON deserialization
    +-- SaveConfig() --> JSON serialization
    +-- GetAvailableColorSchemes()
```

**Data Flow:**
```
User selects theme --> Config updated --> SaveConfiguration() --> JSON file
                                              |
                                              v
                                   ApplyConfiguration() --> Sprite files copied
```

---

### Layer 3: Theme Management Pipeline

```
ThemeCoordinator (Orchestrator)
    |
    +-- InitializeThemes()
    +-- GetCurrentColorScheme()
    +-- SetColorScheme(scheme)
    +-- CycleColorScheme()
    +-- InterceptFilePath(path) --> Themed path
    |
    v
ThemeManagerAdapter (Implementation)
    |
    +-- ApplyInitialThemes()
    +-- CycleCharacterTheme(characterName)
    +-- ApplyCharacterTheme(character, theme)
    |
    v
ThemeService (Core Engine)
    |
    +-- ApplyTheme(character, theme)
    +-- CycleTheme(character)
    +-- GetAvailableThemes(character)
    +-- GetCurrentTheme(character)
```

**Theme Discovery:**
```
ColorSchemeCycler
    |
    +-- Scans FFTIVC/data/enhanced/fftpack/unit/
    +-- Finds sprites_[theme_name]/ directories
    +-- Returns available themes list
```

---

### Layer 4: Sprite Operations

```
ConfigBasedSpriteManager (Orchestrator)
    |
    +-- ApplyConfiguration()
    |       |
    |       +-- ApplyGenericJobThemes(config)
    |       +-- ApplyStoryCharacterThemes(config)
    |
    +-- InterceptFilePath(path)
    |
    +-- Delegates to:
            |
            +-- SpritePathResolver
            |       +-- GetSpriteNameForJob(jobProperty)
            |       +-- GetUnitPathForJob(jobName)
            |       +-- GetThemeSpriteDirectory(theme)
            |
            +-- SpriteFileCopier
            |       +-- CopyThemedSprite(sprite, theme)
            |       +-- RestoreOriginalSprite(sprite)
            |
            +-- UserThemeApplicator
            |       +-- IsUserTheme(job, theme)
            |       +-- ApplyUserTheme(sprite, theme, job)
            |
            +-- SpriteFileInterceptor
                    +-- InterceptFilePath(originalPath)
                    +-- GetActiveColorForJob(job)
```

---

### Layer 5: Special Character Handling (Ramza)

Ramza has unique handling due to multi-chapter sprite variations:

```
RamzaNxdService (NXD Palette Patching)
    |
    +-- ApplyAllChaptersToNxd(config)
    |       |
    |       +-- Chapter 1: Key=1, Armor=Blues
    |       +-- Chapter 2/3: Key=2, Armor=Purples
    |       +-- Chapter 4: Key=3, Armor=Teals
    |
    +-- ApplyBuiltInThemeToNxd(character, theme)
    +-- ApplyUserThemeToNxd(character, paletteData)

RamzaThemeCoordinator (TEX-based themes)
    |
    +-- GenerateAllRamzaThemes()
    +-- ApplyRamzaTheme(themeName)
    +-- RamzaTexGenerator --> TEX file generation
```

**Ramza File Structure:**
```
TEX Files (Sprite textures):
    tex_830.bin, tex_831.bin  --> Chapter 1
    tex_832.bin, tex_833.bin  --> Chapter 2/3
    tex_834.bin, tex_835.bin  --> Chapter 4

NXD File (Color palette override):
    charclut.nxd --> Palette lookup table (simpler approach)
```

---

### Layer 6: User Interface System

```
ConfigurationForm (Main WPF Window)
    |
    +-- Generic Characters Section (Collapsible)
    |       +-- 38 jobs (Knight, Archer, Monk, etc.)
    |       +-- Male/Female variants
    |
    +-- Story Characters Section (Collapsible)
    |       +-- 13+ characters (Ramza, Agrias, Orlandeau, etc.)
    |       +-- Per-chapter variants for Ramza
    |
    +-- WotL Jobs Section (Collapsible)
    |       +-- Dark Knight (Male/Female)
    |       +-- Onion Knight (Male/Female)
    |       +-- Requires GenericJobs mod
    |
    +-- Theme Editor Section (Collapsible)
    |       +-- ThemeEditorPanel
    |       |       +-- Template dropdown
    |       |       +-- Section color pickers
    |       |       +-- Real-time preview
    |       +-- HslColorPicker
    |       |       +-- H/S/L sliders
    |       |       +-- Hex code input
    |       +-- PaletteModifier
    |               +-- Auto-shade algorithm
    |
    +-- My Themes Section (Collapsible)
            +-- User-created themes
            +-- Export/Delete options

CharacterRowBuilder
    +-- AddGenericCharacterRow()
    +-- AddStoryCharacterRow()
    +-- Dynamic control creation

PreviewCarousel
    +-- Theme selection dropdown
    +-- Preview image (64x64)
    +-- LazyImageLoader (on-demand)

LazyLoadingManager
    +-- Defers image generation
    +-- Loads first 10 items
    +-- Loads rest when section expands
```

---

## Data Flow Diagrams

### Configuration Change Flow

```
USER ACTION                    SYSTEM RESPONSE
-----------                    ---------------
    |
    v
Select theme in dropdown
    |
    v
ConfigurationForm.PreviewCarousel_SelectionChanged()
    |
    v
_config["Knight_Male"] = "dark_knight"  (in-memory)
    |
    v
Click "Save" button
    |
    v
ConfigurationCoordinator.SaveConfiguration()
    |
    +-------> ConfigurationManager.SaveConfig()
    |              |
    |              v
    |         Write to Config.json
    |
    v
ConfigBasedSpriteManager.ApplyConfiguration()
    |
    +-------> ApplyGenericJobThemes()
    |              |
    |              v
    |         SpriteFileCopier.CopyThemedSprite()
    |              |
    |              v
    |         Copy sprites_dark_knight/battle_knight_m_spr.bin
    |               --> sprites_original/battle_knight_m_spr.bin
    |
    +-------> ApplyStoryCharacterThemes()
                   |
                   v
              RamzaNxdService.ApplyAllChaptersToNxd()
                   |
                   v
              Patch charclut.nxd with theme palettes
```

### Runtime File Interception Flow

```
GAME                          MOD
----                          ---
    |
    v
Request: "unit/battle_knight_m_spr.bin"
    |
    v
Mod.InterceptFilePath(originalPath)
    |
    v
FileInterceptor.InterceptFilePath()
    |
    v
SpriteFileInterceptor.InterceptFilePath()
    |
    +-------> Check per-job configuration
    |              |
    |              v
    |         Config["Knight_Male"] = "dark_knight"
    |              |
    |              v
    |         Return: "unit/sprites_dark_knight/battle_knight_m_spr.bin"
    |
    v
Game loads themed sprite
```

---

## Key Interfaces & Abstractions

| Interface | Purpose | Implementations |
|-----------|---------|-----------------|
| `IMod` | Reloaded-II mod interface | `Mod` |
| `IServiceContainer` | Dependency injection | `ServiceContainer` |
| `IConfigurationService` | Config I/O | `ConfigurationManager` |
| `IThemeService` | Theme operations | `ThemeService` |
| `IPathResolver` | Path resolution | `FFTIVCPathResolver`, `SimplePathResolver` |
| `ILogger` | Logging | `ModLogger`, `ConsoleLogger` |
| `IHotkeyHandler` | Hotkey detection | `HotkeyHandler`, `NullHotkeyHandler` |

---

## Sprite File Naming Conventions

### Generic Jobs
```
Pattern: battle_[job]_[m/w]_spr.bin

Examples:
    battle_knight_m_spr.bin   (Knight Male)
    battle_knight_w_spr.bin   (Knight Female)
    battle_monk_m_spr.bin     (Monk Male)
```

### Special Job Name Mappings
```
squire      --> battle_mina
chemist     --> battle_item
calculator  --> battle_san
bard        --> battle_gin    (male only)
dancer      --> battle_odori  (female only)
dragoon     --> battle_ryu
geomancer   --> battle_fusui
mediator    --> battle_waju
samurai     --> battle_samu
timemage    --> battle_toki
```

### WotL Jobs (Requires GenericJobs Mod)
```
darkknight   --> spr_dst_bchr_ankoku_[m/w]_spr.bin
onionknight  --> spr_dst_bchr_tama_[m/w]_spr.bin

Note: Uses unit_psp/ directory instead of unit/
```

### Story Characters
```
Pattern: battle_[character]_spr.bin

Examples:
    battle_musu_spr.bin   (Mustadio)
    battle_oru_spr.bin    (Orlandeau)
    battle_agri_spr.bin   (Agrias)
```

---

## Directory Structure

```
FFTColorCustomizer/
|
+-- ColorMod/                           # Main mod source
|   |
|   +-- Mod.cs                          # Entry point (IMod)
|   +-- ModContext.cs                   # Shared runtime context
|   |
|   +-- Configuration/                  # Configuration system
|   |   +-- Config.cs                   # Configuration model
|   |   +-- ConfigurationForm.cs        # WPF UI (main)
|   |   +-- ConfigurationForm.Data.cs   # UI data binding
|   |   +-- ConfigurationForm.Layout.cs # UI layout
|   |   +-- Configurator.cs             # Config helpers
|   |   +-- UI/                         # UI subcomponents
|   |       +-- CharacterRowBuilder.cs
|   |       +-- PreviewCarousel.cs
|   |       +-- ThemeComboBox.cs
|   |       +-- LazyLoadingManager.cs
|   |
|   +-- Core/                           # Core services
|   |   +-- ServiceContainer.cs         # DI container
|   |   +-- ServiceRegistry.cs          # Service configuration
|   |   +-- ThemeService.cs             # Theme engine
|   |   +-- ConfigurationService.cs     # Config service
|   |   +-- SpriteService.cs            # Sprite service
|   |   +-- ModComponents/              # Mod initialization
|   |       +-- ModBootstrapper.cs
|   |       +-- ModInitializer.cs
|   |       +-- ConfigurationCoordinator.cs
|   |       +-- ThemeCoordinator.cs
|   |       +-- FileInterceptor.cs
|   |
|   +-- Services/                       # Business services
|   |   +-- ThemeManager.cs
|   |   +-- ThemeManagerAdapter.cs
|   |   +-- RamzaNxdService.cs
|   |   +-- RamzaThemeCoordinator.cs
|   |   +-- CharacterDefinitionService.cs
|   |   +-- JobClassDefinitionService.cs
|   |   +-- GenericJobsDetector.cs
|   |
|   +-- Utilities/                      # Utility classes
|   |   +-- ConfigBasedSpriteManager.cs # Sprite orchestrator
|   |   +-- SpritePathResolver.cs       # Path resolution
|   |   +-- SpriteFileCopier.cs         # File operations
|   |   +-- SpriteFileInterceptor.cs    # Runtime interception
|   |   +-- UserThemeApplicator.cs      # User theme logic
|   |   +-- DynamicSpriteLoader.cs      # Lazy sprite loading
|   |   +-- FFTIVCPathResolver.cs       # FFTIVC path finding
|   |
|   +-- ThemeEditor/                    # Theme editor system
|   |   +-- ThemeEditorPanel.cs
|   |   +-- HslColorPicker.cs
|   |   +-- PaletteModifier.cs
|   |   +-- HslColor.cs
|   |   +-- SectionMapping.cs
|   |   +-- UserThemeService.cs
|   |
|   +-- Data/                           # Data files
|   |   +-- StoryCharacters.json
|   |   +-- JobClasses.json
|   |   +-- WotLClasses.json
|   |   +-- SectionMappings/
|   |
|   +-- FFTIVC/data/enhanced/           # Sprite assets
|       +-- fftpack/unit/               # Generic job sprites
|       |   +-- sprites_original/
|       |   +-- sprites_dark_knight/
|       |   +-- sprites_lucavi/
|       |   +-- ...
|       +-- fftpack/unit_psp/           # WotL job sprites
|       +-- system/ffto/g2d/            # Ramza TEX files
|       +-- nxd/                        # NXD palette files
|
+-- Tests/                              # Test suite (1101 tests)
|   +-- Configuration/
|   +-- Core/
|   +-- Integration/
|   +-- Utilities/
|   +-- Registry/
|
+-- docs/                               # Documentation
|   +-- ARCHITECTURE.md                 # This file
|   +-- REFACTOR.md                     # Refactoring status
|   +-- THEME_EDITOR_DESIGN.md          # Theme editor design
|   +-- RAMZA_TEXTURE_GUIDE.md          # Ramza color reference
|   +-- TEX_FILE_FORMAT.md              # TEX format spec
|   +-- NXD_FILE_FORMAT.md              # NXD format spec
|   +-- SPRITE_INDEX_MAPPINGS.md        # Palette index docs
|   +-- TODO_WOTL_JOBS.md               # WotL jobs TODO
|   +-- CREATING_RAMZA_THEMES.md        # Theme creation guide
|
+-- scripts/                            # Python utilities
|   +-- diagnostic_sprite.py
|   +-- ramza/
|       +-- analyze_colors.py
|       +-- generate_armor_themes.py
|
+-- tools/                              # External tools
    +-- FF16Tools.CLI.exe               # NXD file editor
```

---

## Design Patterns Used

| Pattern | Location | Purpose |
|---------|----------|---------|
| **Facade** | `ConfigurationCoordinator`, `ThemeCoordinator` | Simplify complex subsystems |
| **Strategy** | `ThemeService`, `SpritePathResolver` | Multiple implementations |
| **Observer** | `ConfigUIRequested` event | Decouple components |
| **Adapter** | `ThemeManagerAdapter`, `ConfigurationManagerAdapter` | Bridge interfaces |
| **Factory** | `ModBootstrapper.CreateForProduction/Testing()` | Create instances |
| **Dependency Injection** | `ServiceContainer` | Decouple dependencies |
| **Lazy Initialization** | `LazyLoadingManager` | Defer expensive operations |
| **Singleton** | `CharacterServiceSingleton` (legacy) | Global access (being migrated to DI) |

---

## Initialization Sequence

```
1. Reloaded-II loads Mod.dll
       |
       v
2. Mod constructor called
       |
       +-- InitializeLogger()
       +-- ModBootstrapper.CreateForProduction(modPath)
       |       |
       |       +-- ServiceContainer created
       |       +-- ServiceRegistry.ConfigureServices()
       |       +-- ModInitializer created
       |       +-- ThemeCoordinator created
       |
       +-- InitializeConfiguration(configPath)
       |       |
       |       +-- ConfigurationCoordinator created
       |       +-- Load Config.json
       |
       +-- InitializeHotkeys()
               |
               +-- HotkeyManager created (F1 handler)
       |
       v
3. Mod.Start() called
       |
       +-- ModInitializer.InitializeStoryCharacterThemes()
       +-- ConfigBasedSpriteManager.ApplyConfiguration()
       |
       v
4. Game running
       |
       +-- Each sprite request --> InterceptFilePath()
       +-- F1 pressed --> OpenConfigurationUI()
```

---

## Codebase Statistics

| Metric | Value |
|--------|-------|
| C# Source Files | ~120 |
| Lines of Code | ~12,000+ |
| Classes | 84 |
| Interfaces | 8 |
| Passing Tests | 1,101 |
| Generic Jobs | 38 (19 × Male/Female) |
| Story Characters | 13+ |
| Built-in Themes | 4-20 per job |

---

## Related Documentation

- [REFACTOR.md](REFACTOR.md) - Refactoring status and technical debt
- [THEME_EDITOR_DESIGN.md](THEME_EDITOR_DESIGN.md) - Theme editor implementation
- [RAMZA_TEXTURE_GUIDE.md](RAMZA_TEXTURE_GUIDE.md) - Ramza color mappings
- [TEX_FILE_FORMAT.md](TEX_FILE_FORMAT.md) - TEX file format specification
- [NXD_FILE_FORMAT.md](NXD_FILE_FORMAT.md) - NXD file format specification
- [SPRITE_INDEX_MAPPINGS.md](SPRITE_INDEX_MAPPINGS.md) - Palette index documentation
- [CREATING_RAMZA_THEMES.md](CREATING_RAMZA_THEMES.md) - Theme creation guide
- [TODO_WOTL_JOBS.md](TODO_WOTL_JOBS.md) - WotL jobs implementation status

---

*Documentation created January 2026*
*Based on commit 7b1dc4a (Refactor Config.cs)*
