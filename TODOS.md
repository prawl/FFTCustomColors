# FFT Color Mod - TODO List

## 🎉 COMPLETED
- ✅ Generic sprite color swapping WORKING! (December 5, 2024)
- ✅ F1-F4 hotkeys successfully cycle through color variants
- ✅ Created WhiteSilver, OceanBlue, DeepPurple custom color schemes
- ✅ Deployed mod successfully with BuildLinked.ps1
- ✅ Confirmed all color variants have unique palettes (MD5 verified)

## 🌈 CURRENT ISSUES
- **Rainbow Warrior Syndrome**: Unique character sprites (like Laurentius) turn into walking pride flags
  - Generic job sprites (Knight, Monk, etc.) work fine
  - Unique characters have different palette layouts causing wild color transformations
  - Laurentius particularly affected with neon green hair and rainbow armor

## 📋 HIGH PRIORITY
- [ ] Fix unique character sprite transformations
  - Create sprite-specific transformation rules
  - Detect sprite type before applying colors
  - Preserve skin tones for unique characters
- [ ] Add sprite type detection in ImprovedPaletteHandler
  - Check filename patterns (battle_* vs unique names)
  - Apply different transformation logic per sprite type

## 🎨 COLOR IMPROVEMENTS
- [ ] Fine-tune color transformations
  - WhiteSilver: Adjust to be more silver/platinum
  - OceanBlue: Perfect the ocean/teal shades
  - DeepPurple: Enhance royal purple tones
- [ ] Add more color schemes
  - [ ] Crimson Red
  - [ ] Forest Green
  - [ ] Golden/Bronze
  - [ ] Black/Shadow

## 🔧 TECHNICAL TASKS
- [ ] Create SpriteTypeDetector class
  - Identify job sprites vs unique characters
  - Map sprite names to transformation rules
- [ ] Implement selective palette transformation
  - Preserve certain palette indices (skin, eyes)
  - Only transform armor/clothing colors
- [ ] Add configuration file for color schemes
  - JSON config for easy color customization
  - Per-sprite-type transformation rules

## 🚀 FUTURE FEATURES
- [ ] In-game UI for color selection (Reloaded-II config)
- [ ] Per-character color settings
- [ ] Team color coordination (all party members matching)
- [ ] Enemy color variants
- [ ] Boss-specific color schemes
- [ ] Seasonal/holiday themes

## 🐛 BUGS TO FIX
- [ ] Some sprites showing as rainbow/neon when they shouldn't
- [ ] Investigate why certain palettes transform incorrectly
- [ ] Check if female variants have same issues

## 📝 DOCUMENTATION
- [ ] Document which sprites work correctly vs problematic ones
- [ ] Create color transformation guide
- [ ] Add troubleshooting section for rainbow sprites
- [ ] Update README with known issues

## 🎮 TESTING
- [ ] Test all job classes with each color scheme
- [ ] Test all unique/story characters
- [ ] Verify multiplayer compatibility
- [ ] Check cutscene appearances

## NOTES
- Current deployment path: `C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\Reloaded\Mods\FFT_Color_Mod`
- 138 sprite files currently processed
- "Rainbow Laurentius" is now our unofficial mascot 🌈