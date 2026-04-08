# PAC File Structure (Ivalice Chronicles / TIC Remaster)

Source: FFHacktics Wiki. All game assets are packed in `.pac` files, unpacked via FF16Tools (`--gametype fft`).

## Enhanced Version

| File | Directory | Contents |
|------|-----------|----------|
| 0000.pac | bg/ | HD map resources |
| 0001.pac | char/ | HD character textures (not very useful) |
| 0002.pac | fftpack/ | Original game files (from WotL mobile) |
| 0002.en.pac | fftpack/ | English localized game files |
| 0003.pac | movie/ | Movie files |
| 0004.pac | nxd/ | NXD (Nex ExcelDB) resources + PZD (Panzer localization/text) files |
| 0004.en.pac | nxd/ | English NXD/PZD |
| 0005.pac | script/ | Enhanced replacement scripts for TEST.EVT |
| 0005.en.pac | script/ | English scripts |
| 0006.pac | sound/ | Sound resources |
| 0006.en.pac | sound/ | English sound |
| 0007.pac | system/ | System resources: fftpack.bin, g2d.dat |
| 0007.en.pac | system/ | English system (different fftpack.bin) |
| 0008.pac | ui/ | UI textures and definition files |
| 0008.en.pac | ui/ | English UI |
| 0009.pac | vfx/ | Visual effect textures (not very useful) |
| 0010.pac | font/ | Font resources |
| 0011.pac | shader/ | Shader resources |

Also has `.de.pac`, `.fr.pac`, `.ja.pac` variants for German, French, Japanese.

## Key Directories

- **fftpack/** — Original PSP/mobile game data. Contains sprites, maps, text, events
- **fftpack/text/** — Event dialogue scripts (`.mes` files, PSX text encoding)
- **nxd/** — Database tables (NXD format) for items, jobs, abilities, names, etc.
- **script/** — Enhanced event scripts replacing originals in TEST.EVT
- **sound/** — Music and sound effects (see audio track mappings below)

## NXD Database Files

The `.nxd` (Nex ExcelDB) files in 0004.pac contain structured game data tables:
- Character names, job names, ability names, item names
- All text strings used by the game
- Localized per language via `.en.pac`, `.ja.pac` etc.

## Unpacking

```bash
# Requires FF16Tools with --gametype fft flag
FF16Tools --gametype fft unpack <input.pac> <output_dir>
```
