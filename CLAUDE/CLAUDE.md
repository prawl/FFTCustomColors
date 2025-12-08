# FFT Color Mod - Development Guide

## Current Implementation (v0.5.0)
- **F1 Cycling**: Uses better_palettes sprites (6 color schemes)
- **File Swapping**: Copies sprites to `unit/` directory for real-time changes
- **Preferences**: Saves/loads color choice between sessions

## Key Components

### Mod.cs
- `MonitorHotkeys()`: F1 key detection
- `ProcessHotkeyPress()`: Cycles schemes
- `SwitchPacFile()`: Sprite file swapping
- `SetColorScheme()`: Saves preference

### ColorSchemeCycler.cs
- Schemes: original, corpse_brigade, lucavi, northern_sky, smoke, southern_sky

## Development

### Adding Sprites
1. Get from: `C:/Users/ptyRa/Dev/better_palettes/`
2. Copy to: `ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_[scheme]/`
3. Deploy: Run BuildLinked.ps1

## Status
- ✅ F1 cycling (6 schemes)
- ✅ Knight sprite ready
- ✅ Preferences persist
- ✅ 104 tests passing
- 🔄 Add more job sprites

## Tech Notes
- BGR format, palette in first 288 bytes
- File swapping approach
- Each directory needs complete sprite set