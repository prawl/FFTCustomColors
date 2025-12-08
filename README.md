# FFT Color Mod

Custom color palettes for Final Fantasy Tactics (Steam) using professionally edited sprites from better_palettes.

## Features

- **F1 Hotkey**: Cycle through 6 color schemes
- **Color Schemes**: Original, Corpse Brigade, Lucavi, Northern Sky, Smoke, Southern Sky
- **All Job Classes**: Works with Knight, Monk, Ninja, and all sprites
- **Save Preferences**: Remembers your color choice between sessions

## Installation

1. Install [Reloaded-II mod loader](https://github.com/Reloaded-II/Reloaded-II)
2. Download the latest release from [Releases](https://github.com/ptyRa/FFT_Color_Mod/releases)
3. Extract to: `[Steam]\steamapps\common\FINAL FANTASY TACTICS\Reloaded\Mods\`
4. Enable the mod in Reloaded-II
5. Launch FFT through Reloaded-II

## Usage

Press **F1** in-game to cycle through color schemes.

## Development

### Requirements
- .NET SDK 8.0+
- Visual Studio 2022 or VS Code

### Building
```bash
cd ColorMod
powershell -ExecutionPolicy Bypass -File ./BuildLinked.ps1
```

### Project Structure
```
FFT_Color_Mod/
├── ColorMod/           # Main mod code
│   ├── FFTIVC/        # Sprite files
│   └── Tests/         # Unit tests
├── FFTColorMod.sln    # Solution file
└── README.md
```

### Testing
```bash
dotnet test FFTColorMod.Tests.csproj
```

## Color Schemes

| Scheme | Description |
|--------|-------------|
| Original | Default game colors |
| Corpse Brigade | Blue armor theme |
| Lucavi | Dark purple theme |
| Northern Sky | Light/holy colors |
| Smoke | Gray/dark theme |
| Southern Sky | Warm tones |

## Technical Details

- **Framework**: Reloaded-II mod loader
- **Method**: File-based sprite swapping
- **Format**: FFT sprite files (.bin) with BGR palette data
- **Source**: Sprites from better_palettes mod (professionally edited)

## Credits

- **Developer**: ptyRa
- **Sprites**: better_palettes mod team
- **Tools**: Reloaded-II by Sewer56

## License

For personal use only. Final Fantasy Tactics is property of Square Enix.