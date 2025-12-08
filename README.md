# FFT Color Mod

Custom color palettes for Final Fantasy Tactics (Steam) using professionally edited sprites from better_palettes.

## Features

- **F1 Hotkey**: Cycle through 6 color schemes in-game
- **Persistent**: Color choices persist between game sessions (file-based swapping)
- **Currently Supported**: Male Knight sprite only (more coming soon)
- **Color Schemes**: Original, Corpse Brigade, Lucavi, Northern Sky, Smoke, Southern Sky

## Installation

1. Install [Reloaded-II mod loader](https://github.com/Reloaded-II/Reloaded-II)
2. Download FFColorMod from releases
3. Extract to: `Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\Reloaded\Mods\`
4. Launch Reloaded-II and enable "FFT Color Mod"
5. Start the game through Reloaded-II

## Usage

Press **F1** in-game to cycle through color schemes. Changes apply immediately.

## Building from Source

### Requirements
- .NET SDK 8.0+
- Windows (Git Bash recommended for scripts)

### Quick Deploy
```powershell
# Builds and deploys directly to Reloaded-II mods folder
./BuildLinked.ps1
```

### Run Tests
```bash
./RunTests.sh
```

### Project Structure
```
FFColorMod/
├── ColorMod/
│   ├── FFTIVC/                 # Sprite files and color variants
│   │   └── data/enhanced/
│   │       └── fftpack/unit/   # Original sprites + variant folders
│   ├── Utilities/              # Core functionality
│   │   ├── HotkeyHandler.cs   # F1 key detection
│   │   └── SpriteFileManager.cs # Sprite file swapping
│   ├── Mod.cs                  # Main mod entry point
│   └── ModConfig.json          # Reloaded-II configuration
├── Tests/                      # Unit tests (77 tests)
├── BuildLinked.ps1            # Build & deploy script
└── RunTests.sh                # Test runner script
```

## Color Schemes

| Scheme | Description | Theme |
|--------|-------------|-------|
| Original | Default FFT colors | Classic brown/beige |
| Corpse Brigade | Blue armor variant | Cool blue tones |
| Lucavi | Dark demon theme | Deep purple |
| Northern Sky | Holy knight colors | Light silver/gold |
| Smoke | Stealthy gray | Dark gray/black |
| Southern Sky | Warm desert theme | Orange/red tones |

## Technical Implementation

- **Method**: Physical file swapping (not memory patching)
- **Persistence**: Swapped files remain until changed (no save system needed)
- **Performance**: Instant switching with no game restart required
- **Compatibility**: Works with FFTIVC utility modloader

## Troubleshooting

- **Mod not loading**: Ensure Reloaded-II is properly installed and FFT is launched through it
- **F1 not working**: Check if another mod is using F1 hotkey
- **Missing sprites**: Run BuildLinked.ps1 to verify deployment

## Development Status

- ✅ Male Knight sprite with 6 color variants
- ⬜ Female Knight and other job sprites (planned)
- ⬜ Enemy unit color variants (planned)
- ⬜ Configuration UI (planned)

## Credits

- **Sprites**: better_palettes mod team (professional color editing)
- **Framework**: Reloaded-II by Sewer56
- **Testing**: XUnit with FluentAssertions

## License

For personal use only. Final Fantasy Tactics © Square Enix.