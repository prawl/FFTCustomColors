# Chocobo Color Research

**Status:** Render/color pipeline cracked and confirmed in-game (2026-05-28). Feature not yet built.
**Goal:** Add the Chocobo as a themable entity with per-tier palettes (Yellow / Black-Purple / Red).

---

## TL;DR

In **Enhanced** mode the chocobo's color comes from the **embedded palette inside `battle_cyoko_spr.bin`** (the classic sprite bin, FFTPack file 89). Editing that palette recolors the in-game chocobo — the **exact same mechanism the mod already uses for class sprites**. No NXD/CLUT work is required.

- Shape/animation: `battle_cyoko_spr.bin` (file 89) **+** HD G2D textures `tex_1068` (yellow tier) / `tex_1069` (red tier).
- The HD `tex_1068/1069` are **4-bit *indexed*** images (each pixel = a palette slot, **not** an RGB color).
- The colors those indices resolve to come from the **cyoko bin's embedded palette** (16-bit BGR555, 16 palettes × 16 colors at the start of the file, offset `pal*32 + slot*2`).
- **`tori_spr.bin` and `cyomon*_spr.bin` are NOT used** for these chocobos — earlier edits there had zero effect.

---

## How we proved it

| Test | Result | Conclusion |
|---|---|---|
| Recolor `tori`/`cyomon` bin palettes | No change | Wrong files — chocobo loads `cyoko` (file 89), confirmed by the modloader hover log `[FFTPack] Accessing file 89 -> unit/battle_cyoko_spr.bin` + `[G2D] Accessing file 1068/1069`. |
| Recolor g2d `tex_1068` pixels (flat fill, via zodi pack) | No change | Zodi's g2d wasn't being mapped by the modloader (see channels below) — inert. |
| Recolor g2d `tex_1068` via **paxtrick** | Shape **garbled**, colors stayed | `tex_1068` is **4-bit indices**, not direct color. The overwrite scrambled indices (shape) but couldn't touch the externally-stored colors. |
| Recolor **`cyoko` bin palette** via paxtrick | Both chocobos went **flat magenta** | ✅ The HD indices resolve color from the **cyoko bin palette**. |

The "shape changed but color didn't" result from the g2d edit is the key discriminator: direct-color would have turned magenta; indexed scrambles shape while colors (from the external palette) persist.

---

## cyoko bin palette layout

`battle_cyoko_spr.bin` (49,572 B). Palettes at byte offset `pal*32`, 16 colors each, 16-bit little-endian BGR555 (`R=(v&0x1F)<<3, G=((v>>5)&0x1F)<<3, B=((v>>10)&0x1F)<<3`). Populated palettes and their tier-defining body color (slot 8):

**Tier → palette map (confirmed in-game 2026-05-28 via per-palette color test):**

| Tier | Palette | body character |
|---|---|---|
| **1 — Yellow** | **pal 0** | gold (slot8 R224 G192 B80) |
| **2 — Black/Purple** | **pal 1** | dark violet (slot5 R80 G64 B104, slot8 R136 G112 B144); darkest avg luminance |
| **3 — Red** | **pal 2** | orange/red (slot8 R240 G128 B88) |

Tiers 1/2/3 map cleanly to palettes 0/1/2. **Team note (confirmed via playtest):** unlike human jobs (where pal 0 = player, later palettes = enemy team colors), chocobos of a given tier use the **same palette for both player and enemy** (enemy yellow = pal 0, enemy black = pal 1). So recoloring pal 0/1/2 affects every chocobo of that tier regardless of team — no separate enemy palettes to chase. (The extra populated palettes 8/9/10/11/13 are Boco/special variants, not enemy-tier colors.) Other populated palettes (8/9/10/13 = light/tan, 11 ≈ dup of 0) are Boco/petrified/special variants, not the three overworld tiers. **To recolor a tier, edit slots 1–15 of its palette in `battle_cyoko_spr.bin`** (slot 0 = transparent; keep it).

---

## Modloader override channels (FFT IVC / `fftivc.utility.modloader`)

- **Unit sprites + UI + NXD** → compiled into `<game>/data/enhanced/modded.pac` at every game launch. `fftpack/unit/battle_cyoko_spr.bin` rides this channel. **Requires a game restart** to re-merge. A path must appear in `FFTIVC_Mod_Loader/FileLists/fftpack.txt` to be mergeable (`battle_cyoko_spr.bin` is listed).
- **G2D textures (`tex_NNNN.bin`)** → NOT in modded.pac. Served by a runtime hook on `g2d.dat`; at startup the modloader logs `G2D: <modid> mapping G2D file N from ...tex_N.bin`, **only for enabled mods that ship `system/ffto/g2d/tex_N.bin`**. (The Vortex-deployed zodi texture pack was *not* mapped — its g2d edits were inert. `paxtrick` *is* mapped.)
- **Active mod folder:** `<game>/Reloaded/Mods/paxtrick.fft.colorcustomizer` (ModId `paxtrick.fft.colorcustomizer`). The dev `FFTColorCustomizer/` folder in `$RELOADEDIIMODS` is empty. Note: `BuildLinked.ps1` deploys to `$RELOADEDIIMODS/FFTColorCustomizer`, **not** the active paxtrick folder.

---

## Tooling

- **FF16Tools.CLI** (`~/Downloads/FF16Tools.CLI-1.13.2-win-x64/win-x64/FF16Tools.CLI.exe`): `nxd-to-sqlite -i <dir> -o out.sqlite -g fft`, `sqlite-to-nxd`, `unpack`/`list-files` (`.pac`). `tex-conv` does **not** parse these raw g2d tex.
- NXD layouts: `tools/Nex/Layouts/ffto/*.layout`. `CharCLUT` = 16-color RGB888 palettes (small table, not the chocobo's source). `OverrideEntryData.Spriteset`/`MainJob` re-point a unit's appearance — this is how the third-party **Black Boco** mod recolors Boco (`overrideentrydata.nxd`).
- Extracted base pacs: `C:\Users\ptyRa\OneDrive\Desktop\Pac Files\` (originals in `0004/nxd/`, sprites in `0002/fftpack/unit/`).
- tex decode: `scripts/decode_tex_to_png.ps1` (RGB555 256×256) and `scripts/hair_fix/tex2png.py` (4-bit indexed, 512-wide). Python: use `python` (not `python3` — Windows Store alias) and `C:\` paths (not `/c/`).

---

## Build procedure (proposed)

1. Recolor the relevant palette(s) inside a copy of `battle_cyoko_spr.bin`.
2. Drop it in `paxtrick.fft.colorcustomizer/FFTIVC/data/enhanced/fftpack/unit/battle_cyoko_spr.bin`.
3. Restart the game (modloader re-merges into `modded.pac`).
4. Long-term: wire the chocobo into the theme system as a themable entity so the F1 editor produces per-tier `cyoko` palette variants, reusing the existing class-sprite palette machinery.

Backups/scratch from the investigation: `working/chocobo_test_backup/`.

---

## Section mapping (palette index → body part)

Confirmed in-game via the theme editor (one-slider-per-index diagnostic). The pixel→index assignment is shared across all three tiers, so this maps Yellow/Black/Red identically. Mapping shipped in `Data/SectionMappings/Story/Chocobo.json` (`shadeMode: uniformHue`):

| Section | Indices (dark→light) | primaryIndex | Notes |
|---|---|---|---|
| **Primary Color** | 3, 4, 5, 6, 7, 8 | 6 | the chocobo body (also the beak — same orange) |
| **Feet** | 9, 10, 11 | 10 | legs/feet |
| **Eyes** | 15 | 15 | single-pixel eye |

Indices **1, 2, 12, 13, 14 are unused/inert** (changing them has no visible effect — common across FFT sprites).

### Repeatable playbook for adding any monster

1. Drop a **one-section-per-index** diagnostic mapping in `Data/SectionMappings/Story/<Monster>.json` (indices 1–15, each its own section, `uniformHue`), plus the monster's original bin in `sprites_original/` and its HD `<id>_<Name>_hd.bmp` in `Images/<Monster>/original/`.
2. Add a `FrameLayout.For("<Monster>")` entry so the editor crops a clean standing pose (irregular sheets need a pixel offset — find the pose cell with `scripts/overlay_frame_grid.ps1`, or auto-detect bounding boxes). Chocobo = `FrameLayout(64,64,0,1,0, 368,400)`.
3. Run `BuildLinked.ps1`, open the editor, twiddle each index slider, record which body part each paints. (Cropped feet etc. can be read off the full-sheet render instead — decode the bin's 4-bit pixels, color by index.)
4. Replace the diagnostic with grouped sections (body/feet/eyes/etc.) like the table above.

---

## Feature implementation (shipped)

The **Monsters ▸ Chocobo Family** feature is built and live:

- **Config**: `Chocobo_RankI/II/III` props (`Config.cs`, `_jobMetadata` category "Monsters").
- **Config window**: a **Monsters** collapsible section (before WotL) → "— Chocobo Family —" → three rows
  (Yellow/Black/Red Chocobo) with preset dropdowns **and recolored preview thumbnails**
  (`ConfigurationForm.Data.cs` `LoadMonsters`, `CharacterRowBuilder.AddMonsterRow` + `LoadChocoboPreview`).
- **Theme editor**: a `── Monsters ──` group with one **Chocobo** entry (`ThemeEditorPanel.cs` +
  `SectionMappingLoader.GetAvailableMonsters` + `FrameLayout.For("Chocobo")` = `64×64 @ (368,400)`).
- **Apply**: `ChocoboThemeCoordinator` recolors palettes 0/1/2 of `battle_cyoko_spr.bin` on config apply,
  rebuilding from `sprites_original/` each time (idempotent); restart to render in-game.
- **Recolor engine**: `ChocoboRecolor.ApplyTheme` — built-in presets recolor via `RelativeShadeGenerator`
  (uniformHue) from a base color; user themes copy their saved palette's section colors.
- **Presets**: Rank I White/Blue/Orange · Rank II Crimson/Emerald/Violet · Rank III Cyan/Lime/Magenta
  (`ChocoboThemePresets.cs`).
- **User themes** are **tier-agnostic** — saved under one editor key (`"Chocobo"`) and offered on all three
  tier dropdowns; dropdowns refresh live on save/delete.
- Tests: `Tests/Services/ChocoboRecolorTests.cs`.

**To add the next monster, follow [`ADDING_A_MONSTER.md`](ADDING_A_MONSTER.md)** — it has the full playbook
plus the asset table (sprite bins + HD BMP ids) for every unimplemented family.
