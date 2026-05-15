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
> job's own hair indices** (from step 0). For **Knight Male** (next up) it's
> `--hair 11,12,13`.

### 0. Gather

- The job's TEX pair (`tex_N`, `tex_N+1`) and HD BMP — see the **Job → file
  reference** table above. It's a guide, not gospel — verify per job (e.g.
  Squire Male's HD BMP is numbered `924` but its tex pair is `992/993`).
- The job's **hair-mass indices** — from the Hair / BootsAndHair /
  HairBootsGloves section of `ColorMod/Data/SectionMappings/<Job>.json`, take
  the `base` / `shadow` / `outline` roles but **NOT** the `highlight` role.
  That set is the flood-fill's `--hair` argument: it's what *encloses* a
  trapped highlight. The highlight-role index is sparse and is often painted
  right against the face — feed it to the border test and the face gets eaten
  (see Gotchas). White Mage Male: `10,11,12` (no highlight role). Knight Male:
  section `12,11,13,14`, 14 = `highlight` → flood-fill `--hair 11,12,13`.
- Confirm the frame layout with `framedetect.py` — expect 80-row slots.

### 1. TEX — standing-pose pass

Standing/walking poses have the head at the top of each 80-row slot, and their
highlight is *connected to the face* — a flood-fill can't separate them. So
`--blanket` a remap to the top of each slot (remaps every index-15 pixel above
the cutoff; the `--maxy` wall is what protects the face):

```
python scripts/hair_fix/hairclassify.py <vanilla_tex_N>   working/t_N.bin  --hair 10,11,12 --maxy 12 --frameh 80 --blanket
python scripts/hair_fix/hairclassify.py <vanilla_tex_N+1> working/t_N1.bin --hair 10,11,12 --maxy 12 --frameh 80 --blanket
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

`0.6` is the *starting* threshold. If a render later shows a **face**
recolored, the flood-fill is over-catching — raise it. A genuine trapped
highlight scores ~0.85–1.0 hair-border; a face sits ~0.65–0.7, so there's
usually a clean gap to land in (Knight Male needed `0.75`). Step 4 covers the
opposite case — *under*-catch.

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
| `hairclassify.py` | TEX standing-pose remap; `--blanket` for the flat top-of-slot pass, `--maxy` cutoff, `--debugline` to tune |
| `persprite.py` | per-sprite + **flood-fill** remap; `--floodfill --all` is the workhorse; auto-detects TEX *and* BMP |
| `bmphair.py` | BMP standing-pose remap (`--remap`), render (`--render`), frame analysis (`--analyze`) |
| `straycheck.py` | read-only: lists which cells still have hair-enclosed index-15 islands; auto-detects TEX *and* BMP — the "which cells need work" tool |
| `gridnumber.py` | render a sheet with numbered sprite boxes; skin forced red, for stray-spotting; `--skin` picks which indices count as skin |
| `cellzoom.py` | crop & zoom specific cells for close inspection; `--skin` like gridnumber |
| `tex2png.py` | plain TEX → PNG render |
| `framedetect.py` | detect the frame-slot layout of a sheet |

Notes:

- The **fix** tools (`hairclassify`, `persprite`) are palette-agnostic — they
  work on any job unchanged.
- The **render** tools (`gridnumber`, `cellzoom`, `tex2png`) have WMM's palette
  hardcoded — the other colours are just approximate, but skin is forced red
  for stray-spotting. `gridnumber`/`cellzoom` take `--skin` to pick which
  indices are "skin": needed when idx 14 is a hair index on the job (see
  Gotchas). `bmphair --render` reads the real palette from the BMP.
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
- **idx 14 isn't always skin.** `gridnumber`/`cellzoom` default to forcing idx
  14+15 red — but on some jobs idx 14 is a *hair* index (Squire Male's
  `BootsAndHair` = `11,12,13,14`; Knight Male's `BootsGlovesHair` =
  `12,11,13,14`). There, pass `--skin 15` or the hair-accent paints red and
  swamps the stray signal. Check the job's SectionMapping: if 14 sits in a hair
  section it's hair, if it's in `SkinColor` it's skin.
- **The flood-fill `--hair` set ≠ the JSON hair section.** Use the hair-MASS
  roles (base/shadow/outline); EXCLUDE the `highlight`-role index. The
  highlight index is sparse and often sits right next to the face, so feeding
  it to the border test makes the face read as "hair-enclosed" and the
  flood-fill eats it. Knight Male's first Type B pass used the full section
  `11,12,13,14` and recolored ~411 face pixels; the fix is `--hair 11,12,13`.

## Testing

`straycheck.py` is the flood-fill detector as a read-only report — "for a job's
tex or bmp, which cells still have hair-enclosed index-15 islands?" Run it
before and after a fix; no flagged cells = clean. Still TODO: wire it into
`RunTests.sh` as an automated assertion across every job's tex + bmp.

## Status

**▶ IN PROGRESS: Knight Male — Type B (awaiting visual review).**

- **Type A — done, committed `d7f3c523`.** idx 14 (a hair-highlight shade) was
  wrongly under `SkinColor`; moved into `BootsGlovesHair`. JSON is now hair
  `12,11,13,14`, skin `15`. For the render tools pass `--skin 15` — 14 is hair
  now, so the default `--skin 14,15` would paint hair red.
- **Type B — in `working/`, not yet committed.** Two over-catch traps hit and
  fixed: (1) the first flood-fill used `--hair 11,12,13,14` (the whole JSON
  hair section) and ate ~411 face pixels — idx 14 frames the face; fixed by
  dropping to `--hair 11,12,13`. (2) even then, at the default `--threshold
  0.6` the flood-fill ate a 50px front-facing face (its hair-border fraction
  is 0.68); the genuine highlights all score ≥0.81, so `--threshold 0.75`
  separates them cleanly. **Current build: `--hair 11,12,13 --threshold 0.75`**
  — straycheck clean, BMP==TEX gold-check passes, all 12 face-like regions
  verified untouched. **Awaiting visual review** — `working/kn_changemap.png`
  (RED = remapped to hair, GREEN = face rescued from the over-catch).

### Done
- ✅ **Squire Female** — `d003f2a4` — Type A (SectionMapping JSON edit)
- ✅ **White Mage Male** — `3d10acb9` — Type B, full sheet (tex_1012/1013 + HD BMP)
- ✅ **Squire Male** — `a7a1ee94` — Type B. Standing-pose highlight strands
  remapped (tex_992 + HD BMP). Remaining strays are 2–8px specks on
  special/action frames; reviewed against the numbered grid and judged not
  worth chasing (step-4 stop rule). tex_993 reviewed and classified as
  animation frames — skipped.

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
