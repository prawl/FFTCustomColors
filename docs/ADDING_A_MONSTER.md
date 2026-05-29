# Adding a Monster to the Color Customizer — Playbook

This is the repeatable process for adding a new themable **monster family**, derived from
implementing the **Chocobo Family** (the full reverse-engineering story is in
[`CHOCOBO_COLOR_RESEARCH.md`](CHOCOBO_COLOR_RESEARCH.md)). Read that once; use this to ship the next one fast.

---

## How monster color works (the model)

- Each monster **family = 3 ranks** (e.g. Chocobo / Black Chocobo / Red Chocobo). All three ranks
  share **one sprite bin** `battle_<name>_spr.bin` (FFTPack, in `unit/`), as **palettes 0 / 1 / 2**
  of that bin. Palette layout: 16 colors × 2 bytes (**16-bit BGR555**) at byte offset `pal*32`;
  index 0 is transparent.
- In **Enhanced** mode the visible sprite is an **HD G2D texture** (`<id>_<Name>_hd.bmp`, 4bpp
  indexed, 512-wide). The texture supplies the *shape/indices*; the **color comes from the bin's
  palette** — the BMP's 16-color index order matches the bin palette 1:1 (verified for chocobo).
- **Recolor = edit the bin's palette** (pal 0/1/2). Editing the HD-tex pixels does *not* recolor.
- **Player and enemy of the same rank share the palette** (no human-job team-color split).
- Deploy the edited bin to `<mod>/FFTIVC/data/enhanced/fftpack/unit/battle_<name>_spr.bin`; FFTPack
  serves it after a **game restart** (it's merged into `modded.pac` at launch — see channels doc).

The recolor itself is an HSL **uniformHue** transform from a chosen base color (or a copied user
palette), applied only to the family's body section indices. `ChocoboRecolor` is generic and reusable.

---

## Asset locations (the toolbox)

| Asset | Where |
|---|---|
| HD BMP previews (indexed, palette-swappable) | `C:\Users\ptyRa\OneDrive\Desktop\Extracted Game Files\extracted_sprites\<id>_<Name>_hd.bmp` |
| Extracted game data — sprite bins | `C:\Users\ptyRa\OneDrive\Desktop\Pac Files\0002\fftpack\unit\battle_<name>_spr.bin` |
| Extracted game data — NXD tables | `C:\Users\ptyRa\OneDrive\Desktop\Pac Files\0004\nxd\` |
| FF16Tools.CLI (nxd↔sqlite, pac unpack/list, `-g fft`) | `C:\Users\ptyRa\Downloads\FF16Tools.CLI-1.13.2-win-x64\win-x64\FF16Tools.CLI.exe` |
| Modloader FileList (valid override paths) | `<game>\Reloaded\Mods\FFTIVC_Mod_Loader\FileLists\fftpack.txt` |
| NXD layouts (schemas) | `tools/Nex/Layouts/ffto/*.layout` |
| Gridded-sheet helper | `scripts/overlay_frame_grid.ps1` |
| Tex/bin → PNG decoders | `scripts/decode_tex_to_png.ps1`, `scripts/hair_fix/gridnumber.py` |
| Live modloader log (`[FFTPack]/[G2D] Accessing file N`) | shown in the Reloaded console; the color mod also tees its own to `<mod>\paxtrick.fft.colorcustomizer\logs\live_log.txt` |

Python note: use `python` (not `python3`, which hits the Windows Store alias) and `C:\` paths (not `/c/`).

---

## The step-by-step

1. **Identify the family's bin + HD BMP.** Hover a unit of the family in the party/battle menu and
   read the modloader log: `[FFTPack] Accessing file N -> unit/battle_<name>_spr.bin` (the palette
   source) and `[G2D] Accessing file <id>` (the HD tex / BMP id). Cross-check the table below.
2. **(Optional) Confirm color is in the bin palette.** Flood pal 0/1/2 with magenta/green/blue, deploy,
   restart, confirm the unit recolors. (Already proven for the chocobo; skip unless a family surprises you.)
3. **Stage editor assets** (add to the repo *and* let `BuildLinked.ps1` deploy them):
   - original bin → `ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_original/battle_<name>_spr.bin`
   - `<id>_<Name>_hd.bmp` → `ColorMod/Images/<Family>/original/<id>_<Name>_hd.bmp`
4. **Frame layout for the preview.** Monster HD sheets are irregular (poses aren't grid-aligned). Run
   `scripts/overlay_frame_grid.ps1 -SrcPath <bmp> -OutPath grid.png -CellW 64 -CellH 64`, find a clean
   standing-pose cell (or auto-detect bounding boxes — see chocobo notes), then add a case to
   `SpriteSheetExtractor.FrameLayout.For("<Family>")`: `new FrameLayout(w, h, swCol, nwCol, row, offsetX, offsetY)`.
   (Chocobo = `FrameLayout(64,64, 0,1,0, 368,400)`.)
5. **Map index → body part.** Create a *diagnostic* `Data/SectionMappings/Monster/<Family>.json` with
   **one section per index** (1–15, each `shadeMode: uniformHue`). `BuildLinked`, open the theme editor →
   `── Monsters ──` → the family, twiddle each "Index N" slider and note what changes — OR read it off a
   full-sheet render (decode the bin's 4-bit pixels at offset 512, 256-wide, even=low/odd=high nibble,
   color each index). Then replace it with **grouped** sections (Primary / Feet / Eyes / …) like
   `Data/SectionMappings/Monster/Chocobo.json`. Unused/inert indices are common — leave them out.
6. **Wire the feature** (mirror the chocobo):
   - `Config.cs`: 3 `_jobMetadata` entries (category `"Monsters"`) + 3 `[JsonIgnore]` props `<Family>_RankI/II/III`.
   - `ConfigurationForm.Data.cs` `LoadMonsters`: a `"— <Family> —"` sub-label + 3 `AddMonsterRow` rows.
   - `ConfigurationForm.Data.cs` `ResetAllCharacters`: reset the 3 props.
   - Presets + apply: see **Generalize** below.
   - Theme editor: nothing to do — the `Monster/` folder scan + `── Monsters ──` group pick up any new `Monster/<X>.json` automatically.
7. **Deploy** with `BuildLinked.ps1` (game **closed** — it rebuilds the DLL), then restart + verify.

---

## Generalize the chocobo code (do this when adding monster #2)

The chocobo shipped as bespoke classes to prove the path. Before adding Goblin et al., refactor so
families are data, not code:

- `ChocoboThemePresets` → **`MonsterThemeRegistry`**: a table (or JSON) of
  `{ family, displayName, bin, tierDisplayNames, paletteIndices = [0,1,2], presets-per-tier }`.
- `ChocoboThemeCoordinator` → **`MonsterThemeCoordinator`**: loop every registered family, recolor each
  family's bin (the per-bin logic is unchanged — read original, `ChocoboRecolor.ApplyTheme` per tier, write).
- `ChocoboRecolor` — already generic, keep as-is (rename if desired).
- `CharacterRowBuilder.AddMonsterRow` / `LoadChocoboPreview` — parameterize by family (bin name, image folder, tier key).
- `FrameLayout.For` and `Data/SectionMappings/Monster/` — already generic.

User themes are **tier-agnostic** and saved under one editor key per family (chocobo uses `"Chocobo"`).
Keep that pattern: one editor entry per family, its saved themes offered on all three tier dropdowns.

---

## Unimplemented monster families — asset table

Sprite bins live in `Pac Files\0002\fftpack\unit\` and the modloader FileList. HD BMPs in
`extracted_sprites\`. Some FFT internal names differ from the bestiary name (noted). The second / `_2`
BMP id is an additional G2D tex for the family (like chocobo's 1068/1069 = two tex over one bin); the
bin's palettes 0/1/2 still drive all three ranks — confirm per family with the hover log + a palette decode.

| Family (bestiary) | Sprite bin | HD BMP id(s) | Notes |
|---|---|---|---|
| **Goblin** | `battle_gob_spr.bin` | 1070, 1071 | |
| **Bomb** | `battle_bom_spr.bin` | 1072, 1073, 1134 (Bomb_2) | |
| **Panther** | `battle_hyou_spr.bin` | 1074, 1075, 1137 (Coeurl_2) | FFT calls it **Coeurl** (`hyou` = panther) |
| **Mindflayer** | `battle_ika_spr.bin` | 1076, 1077 | sprite **Squid** (`ika` = squid) |
| **Skeleton** | `battle_sukeru_spr.bin` | 1078, 1079 | |
| **Ghost** | `battle_yurei_spr.bin` | 1080, 1081 | `yurei` = ghost |
| **Ahriman** | `battle_arli_spr.bin` *(confirm via hover log)* | 1082, 1083, 1131 (Ahriman_2) | |
| **Aevis** | `battle_tori_spr.bin` | 1084, 1085, 1144 (Cockatrice_2) | FFT **Cockatrice** (`tori` = bird). This is the bin my early test mis-hit — it has yellow/red palettes because it's the Cockatrice, not the chocobo. |
| **Pig** | `battle_uri_spr.bin` | 1086, 1087, 1145 (Pig_2) | `uri` = boar |
| **Trent** | `battle_ki_spr.bin` | 1088, 1089 | FFT **Treant** (`ki` = tree) |
| **Minotaur** | `battle_minota_spr.bin` | 1090, 1091, 1142 (Minotaur_2) | |
| **Malboro** | `battle_mara_spr.bin` | 1092, 1093, 1143 (Malboro_2) | |
| **Behemoth** | `battle_behi_spr.bin` | 1094, 1095, 1132 (Behemoth_2) | |
| **Dragon** | `battle_dora_spr.bin` (also `dora1`, `dora2`) | 1096, 1097, 1104, 1105 | Multiple dragon sprites; Tiamat is a *separate* family (`battle_hebi_spr.bin`? → 1098/1099/1136). Decode palettes to map ranks. |

(Reference / already shipped: **Chocobo** = `battle_cyoko_spr.bin`, HD `1068_Chocobo_hd.bmp` / `1069`.)

---

## Reference implementation (Chocobo Family — shipped)

`ColorMod/Services/ChocoboThemePresets.cs`, `ChocoboRecolor.cs`, `ChocoboThemeCoordinator.cs` ·
`Config.cs` (`Chocobo_RankI/II/III`) · `ConfigurationForm.Data.cs` (`LoadMonsters`/`AddMonsterRow`) ·
`CharacterRowBuilder.AddMonsterRow` + `LoadChocoboPreview` · `SpriteSheetExtractor.FrameLayout.For("Chocobo")` +
`Monster/` editor group in `ThemeEditorPanel.cs` · `Data/SectionMappings/Monster/Chocobo.json` ·
`Images/Chocobo/original/1068_Chocobo_hd.bmp` · tests in `Tests/Services/ChocoboRecolorTests.cs`.
