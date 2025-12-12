# Story Character Sprite Support Status

## Overview
As of December 12, 2024, we have partial support for story character sprite theming with F1/F2 hotkeys.

## Working Story Characters (Confirmed)
These characters successfully change colors when cycling through themes with F1/F2:

1. **Orlandeau** (`battle_oru_spr.bin`)
   - Status: ✅ WORKING
   - Note: Uses correct filename `battle_oru_spr.bin` not `battle_oran_spr.bin`

2. **Malak/Marach** (`battle_mara_spr.bin`)
   - Status: ✅ WORKING
   - Note: Shows red theme correctly

3. **Reis** (`battle_reze_spr.bin` and `battle_reze_d_spr.bin`)
   - Status: ✅ WORKING
   - Note: Both human and dragon forms work

4. **Agrias** (`battle_aguri_spr.bin` and `battle_kanba_spr.bin`)
   - Status: ✅ WORKING
   - Note: Both sprites work with themes

5. **Beowulf** (`battle_beio_spr.bin`)
   - Status: ✅ WORKING

## Not Working Story Characters
These characters do NOT change colors with F1/F2:

1. **Mustadio** (`battle_musu_spr.bin`)
   - Status: ❌ NOT WORKING
   - Issue: Game appears to load from different location than mod directory
   - Attempted fixes:
     - ✅ Sprite exists in all theme directories
     - ✅ Sprite is copied when F1/F2 pressed
     - ✅ Sprite has themed colors applied
     - ❌ Game still shows original colors
     - ❌ Copying to 0002 directory didn't work

2. **Rafa** (`battle_rafa_spr.bin`)
   - Status: ❓ UNTESTED (planned to skip for now)

## Other Story Characters (Status Unknown)
These have themed sprites created but haven't been tested:
- Delita (`battle_dily_spr.bin`, `battle_dily2_spr.bin`, `battle_dily3_spr.bin`)
- Ovelia (`battle_hime_spr.bin`)
- Alma (`battle_aruma_spr.bin`)
- Cloud (`battle_cloud_spr.bin`)

## Technical Details

### File Locations
- Mod sprites: `ColorMod\FFTIVC\data\enhanced\fftpack\unit\sprites_[theme]\`
- Deployed to: `Reloaded\Mods\FFT_Color_Mod\FFTIVC\data\enhanced\fftpack\unit\`
- Game's 0002 directory: `data\enhanced\0002\0002\fftpack\unit\`

### Key Discoveries
1. Story character sprites need exact filenames (e.g., `battle_oru_spr.bin` not `battle_oran_spr.bin`)
2. Some story characters load from mod directory, others may load from PAC files or other locations
3. The mod's file swapping system (`SpriteFileManager`) correctly copies sprites when F1/F2 is pressed
4. Theme colors are successfully applied using palette index copying from generic units

### Current Theme Application
- Using simple palette copying from generic Knight sprite as reference
- Copies palette indices 1-8 (main armor/clothing colors)
- Applied to first 8 palettes of story character sprites

## Next Steps
1. Focus on customizing themes for the 5 working story characters
2. Investigate why Mustadio loads from different location
3. Consider creating story-character-specific color schemes
4. Test remaining story characters when time permits