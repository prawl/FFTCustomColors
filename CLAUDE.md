# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

### Build and Test
```bash
# Run all tests (1101 tests) - use this to validate changes
./RunTests.sh

# Build the mod (without tests)
dotnet build ColorMod/FFTColorCustomizer.csproj -c Release

# Run a single test
dotnet test --filter "FullyQualifiedName~TestName"
```

### Deploy for Development
```powershell
# DEV build - deploys ALL themes to Reloaded-II mods folder
powershell.exe -ExecutionPolicy Bypass -File ./BuildLinked.ps1
```

### Create Release Package
```powershell
# Production build with all themes
powershell.exe -ExecutionPolicy Bypass -File ./BuildLinked.Production.ps1

# Create distributable package
powershell.exe -ExecutionPolicy Bypass -File ./Publish.ps1
```

## Architecture

### Core Systems

**Mod Entry Point & Configuration**
- `Mod.cs` - Main entry point implementing `IMod`, coordinates all systems through dependency injection
- `Config.cs` - Strongly-typed configuration model with per-job and per-character color schemes
- `ModContext.cs` - Shared runtime context providing mod directory paths and logger instances
- `ServiceContainer.cs` - Central dependency injection container managing service lifecycles

**Theme Management Pipeline**
- `ThemeService.cs` - Core theme engine that discovers available themes from filesystem and manages theme metadata
- `ThemeManager.cs` - High-level orchestrator coordinating theme changes across sprites, configurations, and special cases (Ramza)
- `ThemeCoordinator.cs` - Bridges configuration changes to actual sprite file operations
- `DynamicSpriteLoader.cs` - Lazy-loads sprite files from ColorSchemes/ to data/ based on active configuration

**Sprite File Operations**
- `SpriteFileManager.cs` - Low-level BIN file I/O operations for sprite swapping
- `ConfigBasedSpriteManager.cs` - Maps configuration selections to actual sprite files
- `ConventionBasedSpriteResolver.cs` - Resolves sprite naming conventions (e.g., knight → battle_knight_[m/w]_spr.bin)

**Configuration UI System**
- `ConfigurationForm.cs` - WPF configuration window with tab-based organization
- `LazyImageLoader.cs` & `LazyLoadingManager.cs` - On-demand preview generation system
- `PreviewCarousel.cs` - Visual theme preview carousel component
- `ThemeComboBox.cs` - Custom dropdown with theme preview tooltips

### Special Character Handling

**Ramza TEX System** (Complex multi-stage pipeline)
- `RamzaThemeCoordinator.cs` - Orchestrates the entire Ramza theming process
- `RamzaTexThemeService.cs` - Manages TEX file theme associations and paths
- `RamzaTexGenerator.cs` - Creates new TEX files by color-transforming originals
- `RamzaColorTransformer.cs` - Applies palette transformations to TEX pixel data
- `RamzaTexSwapper.cs` - Handles physical TEX file swapping operations

## Key Conventions

### Sprite File Naming
- Generic jobs: `battle_[job]_[m/w]_spr.bin` (m=male, w=female)
- Story characters: Single gender variants per character
- Theme directories: `sprites_[theme_name]/` under FFTIVC/data/enhanced/fftpack/unit/

### Special Job Name Mappings
- squire → battle_mina
- chemist → battle_item
- calculator → battle_san
- bard → battle_gin (male only)
- dancer → battle_odori (female only)
- dragoon → battle_ryu
- geomancer → battle_fusui
- mediator → battle_waju
- samurai → battle_samu
- timemage → battle_toki

### WotL Job Name Mappings (Requires GenericJobs Mod)
- darkknight → spr_dst_bchr_ankoku (uses `unit_psp/` instead of `unit/`)
- onionknight → spr_dst_bchr_tama (uses `unit_psp/` instead of `unit/`)

### Theme Discovery
- Themes auto-detected from `sprites_*` directories
- Each theme must contain complete set of 38 job sprites
- Story character themes in separate directories per character

### Configuration Persistence
- User config: `%RELOADEDIIMODS%/FFTColorCustomizer/Config.json`
- Runtime changes merge with existing settings via `ConfigMerger.cs`
- F1 hotkey opens configuration UI during gameplay