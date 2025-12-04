# input_sprites/

This directory contains original, unmodified sprite files extracted from FFT PAC archives.
These serve as the source files for generating color variants.

## Source
All sprite files were extracted from:
```
C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\data\enhanced\0002\0002\fftpack\unit\
```

## Contents
- **178 sprite files** (6.7MB total)
- Includes all character sprites from FFT
- Named as `battle_[character]_spr.bin`
- Notable sprites include:
  - `battle_10m/10w_spr.bin` - Type 10 male/female (possibly Squires)
  - `battle_20m/20w_spr.bin` - Type 20 male/female (possibly Knights)
  - `battle_ramza_spr.bin` - Ramza (main character)
  - Various unique character sprites (Agrias, Delita, etc.)

## Purpose

These files are kept in version control to:
1. Allow regeneration of color variants with different algorithms
2. Enable contributors to test without needing FFT installed
3. Document which sprites have been processed
4. Provide a reference for the original colors

## Usage

To regenerate color variants from these sprites:
```bash
dotnet run -- process input_sprites FFTIVC/data/enhanced/fftpack/unit
```