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
4. **Cut a clean pose from the BMP** → a `FrameLayout` entry. See **"Cutting the sprite from the BMP"** below.
5. **Map palette index → body part** → a grouped `Data/SectionMappings/Monster/<Family>.json`. See
   **"Separating out the indices"** below.
6. **Wire the feature** (mirror the chocobo):
   - `Config.cs`: 3 `_jobMetadata` entries (category `"Monsters"`) + 3 `[JsonIgnore]` props `<Family>_RankI/II/III`.
   - `ConfigurationForm.Data.cs` `LoadMonsters`: a `"— <Family> —"` sub-label + 3 `AddMonsterRow` rows.
   - `ConfigurationForm.Data.cs` `ResetAllCharacters`: reset the 3 props.
   - Presets + apply: see **Generalize** below.
   - Theme editor: nothing to do — the `Monster/` folder scan + `── Monsters ──` group pick up any new `Monster/<X>.json` automatically.
7. **Deploy** with `BuildLinked.ps1` (game **closed** — it rebuilds the DLL), then restart + verify.

---

## Cutting the sprite from the BMP (the FrameLayout)

The editor preview crops a single pose from the family's HD BMP (`<id>_<Name>_hd.bmp`, **512×512,
4bpp indexed**). Monster sheets are **irregular** — poses are different sizes and are *not*
grid-aligned, so a uniform grid will slice them. Process:

1. **See the poses with numbered boxes** (handles the irregular layout for you):
   ```
   python scripts/monster/detect_bmp_poses.py "<...>/extracted_sprites/<id>_<Name>_hd.bmp" out\boxes.png
   ```
   It renders the BMP (native palette) with a numbered cyan box on each detected pose and prints exact
   `(x, y, w, h)` rects. Open `boxes.png`, pick the **two clean front-facing standing poses** by number
   (these become SW and NW; the editor mirrors them for NE/SE).
   *(Alternative: `scripts/overlay_frame_grid.ps1 -SrcPath <bmp> -OutPath grid.png -CellW 64 -CellH 64`
   draws a `(col,row)` grid if you prefer eyeballing cells — but the grid usually slices the poses, so
   the auto-detect rects are what you actually encode.)*

2. **Turn the two rects into a uniform cell + offset.** The two poses are usually one cell-width apart.
   Find `frameW × frameH` that bounds each, and an `(offsetX, offsetY)` so cell `swCol` lands on pose A
   and `nwCol` on pose B. Chocobo: boxes `(370,402,62×62)` and `(434,408,62×56)` → a **64×64** cell at
   **offset (368,400)**, SW=col 0, NW=col 1.

3. **Encode it** in `SpriteSheetExtractor.FrameLayout.For("<Family>")`:
   ```csharp
   "<Family>" => new FrameLayout(frameW, frameH, swCol, nwCol, row, offsetX, offsetY),
   // Chocobo: new FrameLayout(64, 64, 0, 1, 0, 368, 400)
   ```
   (`FrameLayout` already supports `offsetX/offsetY`; that was added for the chocobo.)

4. **Verify** by reopening the editor — the family should show a clean, full pose, not a sliced one.

---

## Separating out the indices (index → body part)

The recolor edits palette **indices**, so you must learn which index paints which body part.

**Bin pixel format:** after the **512-byte palette region** (16 palettes × 16 colors × 2 bytes BGR555),
pixel data is **4-bit indexed**, **256-wide** sheet, **even pixel = low nibble, odd = high nibble**
(note: this is the *opposite* nibble order from the 0x800-header character TEX format). Index 0 = transparent.

**Method A — read it off a render (fast, no game):**
```
python scripts/monster/render_index_map.py "<...>/Pac Files/0002/fftpack/unit/battle_<name>_spr.bin" out\
```
Produces `<name>_indexmap.png` (each index a distinct color) + `<name>_real.png` (real palette 0) and
prints the **pixel count per index**. Map by eye + counts: largest counts = the body gradient; a
near-black index = outline; tiny counts = eye/claws; mid counts = beak/feet. The full sheet shows parts
the single cropped editor pose hides (e.g. feet at the bottom of the sheet).

**Method B — one-slider-per-index in the editor (visual confirm):**
1. Drop a **diagnostic** `Data/SectionMappings/Monster/<Family>.json` with **one section per index**:
   `{ "name":"Index5","displayName":"Index 5","indices":[5],"roles":["base"],"primaryIndex":5,"shadeMode":"uniformHue" }`
   for indices 1–15.
2. `BuildLinked` (needs the FrameLayout + BMP + `sprites_original/` bin staged), open the theme editor →
   `── Monsters ──` → the family → move each "Index N" slider and watch which body part recolors.
3. Record the groups; replace the diagnostic with the **grouped** mapping.

**Grouped section shape** (final): each section is
`{ name, displayName, indices:[dark..light], roles:[...], primaryIndex, shadeMode:"uniformHue" }`.
`uniformHue` is **required** — it forces one hue and varies only L/S (clean shading). `Preserve` mode
produces rainbow garbage on monsters. Leave **unused/inert** indices out (every sprite has a few).

**Chocobo result (worked example):** Primary = `[3,4,5,6,7,8]` (body, `primaryIndex 6`) · Feet = `[9,10,11]`
· Eyes = `[15]` · indices `1,2,12,13,14` unused. (Shipped mapping kept only Primary, since the editor's
cropped pose can't show feet/eyes to verify — add Feet/Eyes once you pick a pose that shows them.)

---

## Generalize the chocobo code — DONE (this refactor shipped with the 14-family batch)

The chocobo shipped as bespoke classes to prove the path; the families are now data, not code:

- `ChocoboThemePresets` → **`MonsterThemeRegistry`**: the `Families` table of
  `{ family, displayName, bin, tierDisplayNames, paletteIndices = [0,1,2], presets-per-tier }`.
- `ChocoboThemeCoordinator` → **`MonsterThemeCoordinator`**: loops every registered family, recolors each
  family's bin (read original, `MonsterRecolor.ApplyTheme` per tier, write).
- `ChocoboRecolor` → **`MonsterRecolor`**: generic; resolves the family/editor-key from the tier key.
- `Config.cs` `_jobMetadata` is seeded from the registry (`BuildJobMetadata`) — no per-tier C# properties.
- `ConfigurationForm.Data.cs` `LoadMonsters`/`ResetAllCharacters` and `CharacterRowBuilder.AddMonsterRow` /
  `LoadMonsterPreview` are parameterized by family.
- `FrameLayout.For`, `Data/SectionMappings/Monster/`, and the editor's `── Monsters ──` scan are generic.

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
| **Malboro** | `battle_mol_spr.bin` ⚠ | 1092, 1093, 1143 (Malboro_2) | **NOT `mara`** — `battle_mara_spr.bin` is a *humanoid* (Marach/Malak). `mol` = Morbol; its palettes 0/1/2 are the three tiers, and 1092's embedded palette matches `mol` pal0 exactly. |
| **Behemoth** | `battle_behi_spr.bin` | 1094, 1095, 1132 (Behemoth_2) | |
| **Dragon** | `battle_dora1_spr.bin` ⚠ | 1096, 1097, 1104, 1105 | **NOT `dora`** — `battle_dora_spr.bin` is a humanoid with empty palettes 1/2. `dora1` palettes 0/1/2 decode to **green / blue / red** = Dragon / Blue Dragon / Red Dragon. All four 1096–1105 BMPs share `dora1` pal0 1:1; 1096 is the editor crop sheet. |
| **Hydra** *(deferred)* | `battle_hebi_spr.bin` ✗ | 1098/1099, 1136 (Tiamat_2) | **No 3-tier palette swap exists here** — `hebi` (`= snake`) has only palette 0 populated (1/2 are all-black), and its pixel data doesn't match the orange-dragon BMP 1098_Tiamat. Hydra/Greater Hydra/Tiamat need a separate source-bin study before they can ship. |

(Reference / already shipped: **Chocobo** = `battle_cyoko_spr.bin`, HD `1068_Chocobo_hd.bmp` / `1069`.)

**Status (2026-05-28):** all of the above are **implemented** except **Hydra** (deferred — see its row). The
generalization below is **done**: families are now data in `MonsterThemeRegistry`, applied by
`MonsterThemeCoordinator`, recolored by `MonsterRecolor`. Adding a family = one registry entry + its
`Monster/<Family>.json`, HD BMP in `Images/<Family>/original/`, original bin in `sprites_original/`, and a
`FrameLayout.For("<Family>")` case. No other code changes.

---

## Reference implementation (Chocobo Family — shipped)

`ColorMod/Services/ChocoboThemePresets.cs`, `ChocoboRecolor.cs`, `ChocoboThemeCoordinator.cs` ·
`Config.cs` (`Chocobo_RankI/II/III`) · `ConfigurationForm.Data.cs` (`LoadMonsters`/`AddMonsterRow`) ·
`CharacterRowBuilder.AddMonsterRow` + `LoadChocoboPreview` · `SpriteSheetExtractor.FrameLayout.For("Chocobo")` +
`Monster/` editor group in `ThemeEditorPanel.cs` · `Data/SectionMappings/Monster/Chocobo.json` ·
`Images/Chocobo/original/1068_Chocobo_hd.bmp` · tests in `Tests/Services/ChocoboRecolorTests.cs`.
