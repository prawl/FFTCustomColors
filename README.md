# FFT Color Customizer

A Reloaded-II mod for **Final Fantasy Tactics: The Ivalice Chronicles** that lets you customize unit colors with 210+ unique themes and a built-in theme editor.

[![GitHub](https://img.shields.io/badge/GitHub-Repository-blue?logo=github)](https://github.com/prawl/FFTCustomColors)

---

## What's New in v3.0

### Built-in Theme Editor
- Create your own custom color themes directly in-game
- Real-time sprite preview as you design
- Section-based color editing (Cape, Armor, Hair, Boots, etc.)
- HSL color picker with auto-shade generation for shadows and highlights
- Hex code input/output for precise color matching
- Copy/Paste colors between sections
- Themes automatically saved and available in the main configuration menu

**How to Use:**
1. Press `F1` → Select "Theme Editor" tab
2. Choose a job class (Knight, Mage, etc.) and gender
3. Click on sprite sections to edit their colors
4. Preview updates in real-time with 8-directional sprite view
5. Save your theme with a custom name
6. Your themes appear in dropdowns alongside built-in themes

### Ramza Customization System
- **3 Unique Ramza Themes**: Dark Knight, White Heretic, Crimson Blade
- Separate customization for all Ramza chapters (Ch1, Ch2-3, Ch4)
- Visual sprite previews in configuration menu

> **Note**: Ramza theme changes require game restart to take effect (technical limitation with TEX file loading)

---

## Features

### In-Game Theme Configuration
- Press `F1` anytime to open the configuration menu
- Real-time theme switching without game restart (except Ramza)
- Individual customization for male/female variants
- Preview system shows theme changes instantly

### Dynamic Theme Switching
The Color Customizer uses file-based sprite swapping, allowing real-time theme changes while playing!

- Changes apply when the game reloads sprite data (opening party menu, viewing character status, etc.)
- Mid-battle changes take effect in the next battle
- **Quick refresh tip**: Open and close the party formation screen to see new colors immediately

### Massive Theme Collection

| Category | Examples |
|----------|----------|
| **Story Character Exclusives** | Thunder God (Orlandeau), Sky Pirate (Cloud), Shadow Oracle (Rapha) |
| **Faction Themes** | Northern Sky, Southern Sky, Corpse Brigade, Lucavi |
| **Fantasy Themes** | Dragon Knight, Holy Knight, Shadow Monk, Frost Mage |
| **Special Themes** | Crimson Red, Royal Purple, Ocean Templar, Forest Ranger |
| **Ramza Themes** | Dark Knight, White Heretic, Crimson Blade |
| **Custom Themes** | Create unlimited themes with the built-in editor! |

### Job-Specific Theme System
Each job class can have unique themed variations:
- **Knight** → Holy Guard, Dark Knight, Steel Warrior
- **Mage** → Lucavi, Frost Mage, Shadow Caster
- **Ninja** → Shadow Assassin, Forest Ranger
- And many more combinations!

### Key Features
- **Individual Unit Customization** — Each job and character can have different themes
- **Gender-Specific Options** — Male and female variants can use different themes
- **Story Character Themes** — 9 unique characters with exclusive theme sets
- **Built-in Theme Editor** — Create and save your own custom color themes
- **No Sprite Replacement** — Works alongside other sprite mods
- **Automatic Theme Detection** — Discovers and loads all available themes

---

## Installation

### Requirements
- [Reloaded-II](https://github.com/Reloaded-Project/Reloaded-II/releases)
- [FFT:TIC Mod Loader](https://github.com/Nenkai/fftivc.utility.modloader)

### Load Order

> **IMPORTANT**: This mod must be placed **FIRST** in your mod load order!

```
1. Mod Loader
2. Color Customizer (THIS MOD - MUST BE FIRST)
3. WotL Character
4. Red and Blue Mages
5. Dark Knight and Onion Knight
6. FFT Level Scaling
7. Other mods...
```

If the mod menu appears but colors don't change, check your load order.

---

## Known Limitations

| Limitation | Details |
|------------|---------|
| **Ramza Theme Changes** | Require game restart to take effect (TEX file loading timing) |
| **Theme Editor** | Currently supports generic job classes only |
| **Per-Unit Colors** | All units of the same job share the same theme |

---

## Technical Notes

- Enhanced Edition tested (Classic untested)
- Non-invasive color swapping system
- Preserves original sprite structure
- Config saved per-profile
- Uses TEX file modification for Ramza themes
- Uses BIN sprite swapping for all other characters
- Custom themes stored in `%RELOADEDIIMODS%/FFTColorCustomizer/UserThemes/`

---

## Building from Source

```bash
# Run tests (1101 tests)
./RunTests.sh

# Build release
dotnet build ColorMod/FFTColorCustomizer.csproj -c Release

# Deploy to Reloaded-II mods folder (dev)
powershell -ExecutionPolicy Bypass -File ./BuildLinked.ps1
```

---

## Support & Feedback

Questions, suggestions, or theme requests?
- [GitHub Issues](https://github.com/prawl/FFTCustomColors/issues)
- Nexus Mods Posts section

---

## Credits

- **Author**: Paxtrick
- **Game**: Final Fantasy Tactics: The Ivalice Chronicles by Square Enix
- **Mod Framework**: [Reloaded-II](https://reloaded-project.github.io/Reloaded-II/)
- **NXD Tools**: [FF16Tools](https://github.com/Nenkai/FF16Tools) by Nenkai

## License

MIT
