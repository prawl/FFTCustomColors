# Hair-Highlight Fix — Process & Tooling

How to fix the hair-highlight-follows-skin-color bug on a generic job.
Squire Male, Squire Female, and White Mage Male are done; this is the
repeatable process for the rest.

The earlier dead-end R&D (pixel classifiers, TEX-vs-SPR theories — all
superseded) lived in `docs/TODO_HAIR_HIGHLIGHT_FIX.md`, now retired; git
history still has it if ever needed. **This** file is the current, working
process — start here.

---

## The bug

FFT sprites paint the hair **highlight** (the light strand on top of the head)
with palette **index 15** — the *same* index as skin base. So in the mod's
colour customiser the highlight follows the **Skin** slider instead of **Hair**.
Set skin dark, the hair gets a dark speckle. That's the bug.

It can't be fixed by just editing the colour table, because:

- index 15 is *also* the actual face and hands (real skin), and
- the hair-mass indices (10–13) are *also* boots and gloves.

So you can't blanket-remap "index 15" (it kills the face), and you can't trust
"index-15 next to hair-indices = hair" (might be a hand next to a glove).

## Two render pipelines — fix BOTH

| Pipeline | File | Rendered by |
|---|---|---|
| In-game battle sprite | `ColorMod/FFTIVC/data/enhanced/system/ffto/g2d/tex_NNNN.bin` — a **pair**, N and N+1 | the game |
| Config UI "Sprite Preview" | `ColorMod/Images/<Job>/original/NNNN_<Job>_hd.bmp` | the F1 config window |

Separate files, separate formats. Fix one and not the other and the preview
and the game disagree. (We lost half a session to exactly this.)

## Two kinds of fix

### Type A — SectionMapping JSON edit (easy)

Some jobs paint the highlight with a *dedicated* index that's merely
**mis-classified** as skin in `ColorMod/Data/SectionMappings/<Job>.json`.
Example — **Squire Female**: index 14 was listed under `SkinColor` but is
actually hair. Fix = move that index from `SkinColor` to the `Hair` section.
No pixel editing, no tooling. (commit `d003f2a4`)

**How to tell it's Type A:** if `SkinColor` lists an index *besides* 15
(e.g. 14), check whether that index appears only in the hair on the sprite.
If yes → Type A, just move it. If the highlight is genuinely index 15 (shared
with the face) → Type B.

### Type B — pixel remap (the main case)

Most short-haired males (Squire Male, White Mage Male, …). The highlight *is*
index 15, shared with face/hands. You must remap the highlight **pixels** from
15 → 12 (hair base), surgically, without touching the face. The rest of this
doc is the Type B process.

## File formats

**TEX** (`tex_NNNN.bin`): `0x800`-byte header, then 4-bit indexed pixel data
(2 px/byte, **high nibble = first/left pixel**), sheet width **512 px**.
Sprites are packed in **80-row frame slots** (rows 0–79, 80–159, …).

**HD BMP** (`NNNN_<Job>_hd.bmp`): standard 4-bit indexed BMP, **512×512**, rows
stored **bottom-up**, pixel data at offset 118, 16-colour BGRA palette at
offset 54. Same 80-row slots, but **offset by +8** (an 8-row top margin).

**Vanilla source TEX** (unmodified, fix *from* these):
`C:/Users/ptyRa/AppData/Local/FFTSpriteToolkit/working/.FFTSpriteToolkit/sprites_rgba/tex_NNNN.bin`

## Job → file reference

TEX numbers per generic job, from the FFT Sprite Toolkit notes. Job names and
SPR filenames here are the *toolkit's* names — the mod's SectionMappings use
different ones (e.g. "Priest" = White Mage; `battle_priest_m` = `battle_siro_m`).
The **TEX numbers** are what matter; verified for the jobs marked done.

| Job (toolkit name) | TEX pair | SPR file |
|---|---|---|
| Squire Male ✅ | 992, 993 | battle_mina_m_spr.bin |
| Squire Female ✅ | 994, 995 | battle_mina_w_spr.bin |
| Chemist Male | 996, 997 | battle_item_m_spr.bin |
| Chemist Female | 998, 999 | battle_item_w_spr.bin |
| Knight Male | 1000, 1001 | battle_knight_m_spr.bin |
| Knight Female | 1002, 1003 | battle_knight_w_spr.bin |
| Archer Male | 1004, 1005 | battle_archer_m_spr.bin |
| Archer Female | 1006, 1007 | battle_archer_w_spr.bin |
| Monk Male | 1008, 1009 | battle_monk_m_spr.bin |
| Monk Female | 1010, 1011 | battle_monk_w_spr.bin |
| Priest / White Mage Male ✅ | 1012, 1013 | battle_priest_m_spr.bin (= battle_siro_m) |
| Priest / White Mage Female | 1014, 1015 | battle_priest_w_spr.bin |
| Black Mage Male | 1016, 1017 | battle_kuro_m_spr.bin |
| Black Mage Female | 1018, 1019 | battle_kuro_w_spr.bin |
| Time Mage Male | 1020, 1021 | battle_toki_m_spr.bin |
| Time Mage Female | 1022, 1023 | battle_toki_w_spr.bin |
| Summoner Male | 1024, 1025 | battle_sho_m_spr.bin |
| Summoner Female | 1026, 1027 | battle_sho_w_spr.bin |
| Thief Male | 1028, 1029 | battle_shi_m_spr.bin |

The HD BMP for a job is in `ColorMod/Images/<Job>/original/`; its filename
carries a number that may *not* match the TEX number (see Squire Male).

---

## The Type B process

All tooling is in `scripts/hair_fix/`. Use `python` (not `python3`); stdlib
only, no PIL. Put intermediates in `working/` (gitignored).

> ⚠ **The example commands below hardcode `--hair 10,11,12`** — that's *White
> Mage Male's* hair indices, shown as a concrete example. **Substitute the
> job's own hair indices** (from step 0). For **Squire Male** (next up) it's
> `--hair 11,12,13`.

### 0. Gather

- The job's TEX pair (`tex_N`, `tex_N+1`) and HD BMP — see the **Job → file
  reference** table above. It's a guide, not gospel — verify per job (e.g.
  Squire Male's HD BMP is numbered `924` but its tex pair is `992/993`).
- The job's **hair indices** from `ColorMod/Data/SectionMappings/<Job>.json` —
  the `indices` of the Hair / BootsAndHair / HairBootsGloves section.
  (White Mage Male: `10,11,12`. Squire/Knight/Summoner Male: `11,12,13`.)
- Confirm the frame layout with `framedetect.py` — expect 80-row slots.

### 1. TEX — standing-pose pass

Standing/walking poses have the head at the top of each 80-row slot, and their
highlight is *connected to the face* — a flood-fill can't separate them. So
bound a remap to the top of each slot:

```
python scripts/hair_fix/hairclassify.py <vanilla_tex_N>   working/t_N.bin  --hair 10,11,12 --maxy 12 --frameh 80
python scripts/hair_fix/hairclassify.py <vanilla_tex_N+1> working/t_N1.bin --hair 10,11,12 --maxy 12 --frameh 80
```

`--maxy 12` is the cutoff: the top 12 rows of each slot = the hair region.
12 worked for White Mage Male — a reasonable starting point, but verify per
job: add `--debugline 9` to paint a bright cutoff line, render with
`tex2png.py`, eyeball it, and adjust `--maxy` until the line sits just below
the hair.

### 2. TEX — flood-fill the special poses

Special poses (cast, KO, crouch …) have the head at odd positions, so a
slot-relative cutoff misses them. The flood-fill catches them
position-agnostically: it finds index-15 *islands* and flips one to hair only
if its border is mostly hair indices. (The face is a big blob bordered by
skin-shadow and background — it stays put.)

```
python scripts/hair_fix/persprite.py working/t_N.bin  working/t_N.bin  --floodfill --all --hair 10,11,12 --threshold 0.6
python scripts/hair_fix/persprite.py working/t_N1.bin working/t_N1.bin --floodfill --all --hair 10,11,12 --threshold 0.6
```

### 3. Render & eyeball

```
python scripts/hair_fix/gridnumber.py working/t_N.bin working/grid.png 3
```

Open `grid.png` — this numbered grid (skin forced **red**, magenta
background) is the key review artifact. A remaining stray shows as **red
inside the gold hair**; note those cell numbers. Body/limb-only cells have no
hair — ignore. Red in the *face* is correct — leave it. Worth showing to a
human: a fresh pair of eyes catches strays fast, and they know which poses
show hair.

### 4. Targeted flood-fill for stragglers

For cells that still have strays, lower the threshold to catch boundary
islands; `cellzoom.py` to inspect specific cells up close:

```
python scripts/hair_fix/persprite.py working/t_N.bin working/t_N.bin --cells 51,53,61 --floodfill --hair 10,11,12 --threshold 0.4
python scripts/hair_fix/cellzoom.py  working/t_N.bin working/zoom.png --cells 51,53,61 --scale 8
```

Drop the threshold further (0.4 → 0.3) for stubborn ones. **Stop when a pass
flips 0 islands** — that means the leftover red is genuinely the face, not a
stray. A few stray pixels on a KO/crouch pose are not worth chasing.

### 5. HD BMP — same fix, BMP format

```
python scripts/hair_fix/bmphair.py  <vanilla_bmp> --remap working/b.bmp --maxy 12 --frameh 80 --offset 8
python scripts/hair_fix/persprite.py working/b.bmp working/b.bmp --floodfill --all --hair 10,11,12 --threshold 0.6
python scripts/hair_fix/bmphair.py  working/b.bmp --render working/b_check.png --blackskin
```

`bmphair.py --remap` is the BMP standing-pose pass (note `--offset 8`).
`persprite.py` auto-detects BMP vs TEX. Render with `--blackskin` to spot
strays (black specks in the gold hair).

### 6. Deploy & verify

- Copy the fixed `tex_N`, `tex_N+1`, and `<bmp>` into the worktree
  (`ColorMod/...`), overwriting the originals.
- `powershell.exe -ExecutionPolicy Bypass -File ./BuildLinked.ps1`
- Verify deployed file hashes match the worktree
  (`git hash-object` both sides).
- **Restart the game** — the TEX loads into sprite memory at startup; it does
  not re-read from disk while running. The HD BMP/preview reloads when you
  reopen the F1 window.
- Check the job in a battle, and in the F1 Config UI preview.

### 7. Commit

One commit per job — the TEX pair + the HD BMP. Message: `Hair-highlight fix: <Job>`.

---

## Tooling (`scripts/hair_fix/`)

| Script | Purpose |
|---|---|
| `hairclassify.py` | TEX standing-pose remap (`--maxy` cutoff, `--debugline` to tune) |
| `persprite.py` | per-sprite + **flood-fill** remap; `--floodfill --all` is the workhorse; auto-detects TEX *and* BMP |
| `bmphair.py` | BMP standing-pose remap (`--remap`), render (`--render`), frame analysis (`--analyze`) |
| `gridnumber.py` | render a sheet with numbered sprite boxes; skin forced red, for stray-spotting |
| `cellzoom.py` | crop & zoom specific cells for close inspection |
| `tex2png.py` | plain TEX → PNG render |
| `framedetect.py` | detect the frame-slot layout of a sheet |

Notes:

- The **fix** tools (`hairclassify`, `persprite`) are palette-agnostic — they
  work on any job unchanged.
- The **render** tools (`gridnumber`, `cellzoom`, `tex2png`) have WMM's palette
  hardcoded, but they force skin → red, so they're fine for stray-spotting on
  any job (the other colours are just approximate). `bmphair --render` reads
  the real palette from the BMP.
- The old `scripts/fix_hair_highlight_*.py` are the **superseded** crude
  Y-threshold approach — don't use them.

## Gotchas (hard-won)

- **Duplicate mod folders.** A Vortex-installed copy sharing the ModId
  (`paxtrick.fft.colorcustomizer`) shadows the dev build — deploys silently
  don't apply. Check `%RELOADEDIIMODS%` for any folder *other than*
  `FFTColorCustomizer` carrying that ModId, and delete it.
- **TEX vs preview.** The TEX fix only shows in an actual battle; the HD BMP
  fix only shows in the Config UI preview. Always fix both.
- **Frame height is 80**, not 40 — the atlas is 80-row slots.
- **BMP offset is +8** — the BMP has an 8-row top margin; the TEX has none.
- **The game caches the TEX** — restart the game to see TEX changes.

## Testing (TODO)

The flood-fill detector, run as an assertion, *is* the regression test: "for
every job's tex + bmp, are there any fully-hair-enclosed index-15 islands?"
→ 0 = clean. Not yet wired into `RunTests.sh`.

## Status

**▶ NEXT: Squire Male** — finish his full sheet (see the ⚠ note below).

### Done
- ✅ **Squire Female** — `d003f2a4` — Type A (SectionMapping JSON edit)
- ✅ **White Mage Male** — `3d10acb9` — Type B, full sheet (tex_1012/1013 + HD BMP)

### Partial — needs redo
- ⚠ **Squire Male** — `a7a1ee94` fixed the **standing poses only**, with the
  old pure connected-component classifier (made before the 80-row-slot and
  special-pose work). His special poses are **not** done. Redo his whole sheet
  (tex_992/993 + HD BMP) with the current pipeline.

### Remaining

Only jobs whose sprites actually **show hair** need the fix — armored,
helmeted, hooded, or big-hat jobs don't have the bug. The roster that needs
it (per in-game inspection):

- ⬜ Chemist — Female
- ⬜ Knight — Male, Female
- ⬜ Archer — Male, Female
- ⬜ Monk — Female
- ⬜ Time Mage — Female
- ⬜ Summoner — Male, Female
- ⬜ Mediator — Female
- ⬜ Mystic — Female
- ⬜ Geomancer — Male, Female
- ⬜ Samurai — Female
- ⬜ Ninja — Female
- ⬜ Calculator — Male, Female
- ⬜ Bard *(male-only job)*
- ⬜ Dancer *(female-only job)*

(Squire Male also remains — see **Partial** above.) Tex numbers for some are
in the Job → file reference table above; the rest need looking up.

### Story characters

These story characters also show hair and need the fix — but they are **not**
covered by the generic-job process above. Story sprites have their own file
layout, and Ramza has a separate TEX system entirely (`RamzaThemeCoordinator`
et al.). Treat as a separate effort; investigate each character's file layout
first.

- ⬜ Agrias
- ⬜ Beowulf
- ⬜ Cloud
- ⬜ Mustadio
- ⬜ Ramza — all chapters (Ch1, Ch2/3, Ch4)
- ⬜ Rapha
- ⬜ Reis
