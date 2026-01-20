# FFT Color Customizer

A Reloaded-II mod for **Final Fantasy Tactics: The War of the Lions Remaster** that lets you customize unit colors with 210+ unique themes.

## Features

- **210+ Color Themes** for generic jobs (Knight, Black Mage, Dragoon, etc.)
- **Story Character Themes** for Agrias, Orlandeau, Ramza (per-chapter), and more
- **In-Game Configuration** - Press `F1` to open the theme selector during gameplay
- **Live Preview** - See theme previews before applying
- **Per-Job Customization** - Set different themes for each job class

## Installation

### Requirements
- [Reloaded-II](https://github.com/Reloaded-Project/Reloaded-II/releases)
- [FFT:TIC Mod Loader](https://github.com/Nenkai/fftivc.utility.modloader) (fftivc.utility.modloader)
- Reloaded.Memory.SigScan.ReloadedII
- reloaded.sharedlib.hooks

### Steps
1. Install Reloaded-II and configure it for FFT
2. Install the required dependencies from Reloaded-II's mod browser
3. Download the latest release and extract to your Reloaded-II mods folder
4. Enable the mod in Reloaded-II

## Usage

1. Launch FFT through Reloaded-II
2. Press **F1** during gameplay to open the configuration menu
3. Select themes for each job or story character
4. Changes apply immediately (except Ramza, which requires a restart)

## Supported Jobs

| Generic Jobs | Special Jobs |
|-------------|--------------|
| Squire, Knight, Archer | Dark Knight* |
| Monk, Thief, Geomancer | Onion Knight* |
| Dragoon, Samurai, Ninja | Bard (M), Dancer (F) |
| Black Mage, White Mage | Mime, Calculator |
| Time Mage, Summoner, Mediator | Chemist, Orator |

*Requires GenericJobs mod

## Story Characters

- Ramza (Chapter 1, Chapter 2/3, Chapter 4)
- Agrias, Orlandeau, Delita, Ovelia
- Mustadio, Rapha, Marach, Meliadoul
- Beowulf, Reis, Construct 8, Cloud

## Building from Source

```bash
# Run tests (1101 tests)
./RunTests.sh

# Build release
dotnet build ColorMod/FFTColorCustomizer.csproj -c Release

# Deploy to Reloaded-II mods folder (dev)
powershell -ExecutionPolicy Bypass -File ./BuildLinked.ps1

# Create release package
powershell -ExecutionPolicy Bypass -File ./Publish.ps1
```

## Project Structure

```
ColorMod/
  Mod.cs              # Entry point
  Config.cs           # User configuration model
  Services/           # Theme management, sprite operations
  Configuration/      # WPF config UI
  FFTIVC/data/        # Theme sprite files (188 theme sets)
  RamzaThemes/        # Ramza-specific TEX themes
```

## Technical Notes

- Generic jobs use **sprite file swapping** - all units of a job share the same theme
- Ramza uses **charclut.nxd palette patching** for per-chapter theming
- Configuration persists to `%RELOADEDIIMODS%/FFTColorCustomizer/Config.json`

## License

MIT

## Credits

- **Author**: Paxtrick
- **Game**: Final Fantasy Tactics: The War of the Lions Remaster by Square Enix
- **Mod Framework**: [Reloaded-II](https://reloaded-project.github.io/Reloaded-II/)
- **NXD Tools**: [FF16Tools](https://github.com/Nenkai/FF16Tools) by Nenkai
