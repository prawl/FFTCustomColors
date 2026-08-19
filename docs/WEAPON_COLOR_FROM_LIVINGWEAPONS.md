# Battle weapon colour: what LivingWeapons has proven, for the ColorCustomizer weapon slider

STATUS: JOURNAL. Last updated 2026-08-19 after four live rounds with the owner.

**OWNERSHIP: LW writes this file** (the LivingWeapons session). CC, read it, do not edit it.
Write your findings in `WEAPON_COLOR_CC_FINDINGS.md` beside it, which is yours. One writer per
file, so neither of us clobbers the other. If we disagree, say so in your file and cite the
evidence; I will reconcile here.

The authoritative row for anything marked PROVEN is `docs/LIVE_LEDGER.md` in
`C:\Users\ptyRa\Dev\FFTLivingWeapons`. If this file and that row disagree, that row wins.

---

## Plain version

The colour a weapon wears when a unit swings it in battle comes from a 512 byte colour table at
the front of one PlayStation era file, and a mod can ship its own copy of that file and repaint
it. That much is proven live and is not in doubt.

What we can NOT yet do is choose which of the sixteen palettes a given weapon uses. Vanilla hands
thirteen palettes to a hundred and twenty seven weapons, we know exactly which weapon gets which,
and three separate attempts to change that assignment have all failed live. Finding where the game
reads that assignment from is the one open question, and it is the thing worth two people.

---

## PROVEN: the repaint mechanism

- File: FFTPack file **71**, `unit/battle_wep_spr.bin`. Classic FFT `WEP.SPR`.
- Ship at `<mod>/FFTIVC/data/enhanced/fftpack/unit/battle_wep_spr.bin`.
- Pristine source: `data/enhanced/0002.pac`, path `fftpack/unit/battle_wep_spr.bin`,
  **85504 bytes, md5 `cf6ad45e04fef2b1795dfff5b8e54c21`**. Hash gate it.
  NEVER use the loose copy at `data/enhanced/0002/0002/fftpack/unit/battle_wep_spr.bin`
  (md5 `6439436f...`): it is an old flat red test artifact with palettes 1-8 zeroed.
- Proven by shipping all sixteen palettes flattened to distinct colours with the pixel block byte
  identical. Weapons rendered flat in the forged colours while the rest of the frame stayed
  vanilla. Owner live verified 2026-08-19.

### The file is THREE pages, not one

This corrects an earlier version of this doc. Verified by byte arithmetic and hashes:

```
palA   @0x00000    512 B                page1 @0x00200  32768 B   rows   0-255  WEAPONS
palB   @0x08200    512 B  (== palA)     page2 @0x08400  32768 B   rows 256-511  arcs, sparkles
palC   @0x10400    512 B  (different)   page3 @0x10600  18432 B   rows 512-655  impact effects
                                                          sums to exactly 85504
```

- palA and palB are **byte identical**. palC is a separate bank of near black monotone fades with
  the PSX semi transparency bit set on all 240 live entries, i.e. an additive glow bank.
- **page 2 uses palette slots 11-15 and nothing else**: 0 ink pixels in slots 1-10, 5825 in 11-15.
- Reading the pixel block as one 256x664 image is WRONG. It splices palB and palC into the picture
  as 4 row junk bands at y 256-259 and y 516-519, and mislocates every row above 255.
- The real image is 656 rows in three pages. Rows 360-511 are entirely blank, about 19 KB of
  unused pixel budget.
- Pixels are 4bpp, **low nibble first**, 256 px wide.

### Ship the file at exactly 85504 bytes

From the loader source: the read copies the full request size (0x15000 = 86016) out of an
`ArrayPool` rental it never clears, while reading only `min(size, fileLength)` bytes into it. A
short file leaves pool garbage in the tail; a longer one gets read past vanilla.

---

## THE INSTRUMENT YOU MUST USE BEFORE BELIEVING ANY OVERRIDE RESULT

This is the most valuable thing in this document and it cost a retracted PROVEN row.

The Reloaded modloader log distinguishes **registering** a file from the game **reading** it:

- `mapping G2D file N` and the other mod load lines: printed at startup. **Proves nothing.**
- `[FFTPack] Accessing file 71 -> unit/battle_wep_spr.bin`: the game read **its own** copy.
- `Accessing MODDED file 71` (with `max buffer size`): the game read **your** copy. Only this line
  makes a screenshot admissible.

Logs live at `%APPDATA%\Reloaded-Mod-Loader-II\Logs\<UTC> ~ FFT_enhanced.txt` (filename UTC,
in log timestamps LOCAL). Worked example: `lw289_palette_selector.py --checklog` in the LW repo.

An entire night went into proving a g2d palette bank worked. The loader log showed that entry had
been read **zero times in eighteen launches**. Four independent lenses had already agreed it
worked. Check the log FIRST. Every negative below is admissible only because this check passed.

---

## ANSWERED: which palette each weapon draws from

**It is the classic PSX `BATTLE.BIN` item graphics record, and the remaster did not move it.**

```
offset(itemId) = 0x02D3E6 + (itemId - 1) * 2       itemId 1-based, matches ItemData ids
byte0 high nibble  X  = the WEAPON's palette index into battle_wep_spr.bin
byte0 low  nibble  Y  = the SWING ARC / effect palette index
byte1              ZZ = which graphic, interpreted RELATIVE to the item's category
```

Source: FFHacktics "Item Graphics". `battle_bin` is **not** in any `data/enhanced` pac; it lives
at `data/classic/0002.en.pac`, path `fftpack/battle_bin.en.bin`, 1397096 bytes. It is FFTPack
file index **0**.

**Two independent confirmations, both done before it was believed:**

1. **Live, four for four.** With the census sheet deployed and the serve proven from the log, the
   owner swung four swords and the measured palettes matched the file exactly:

   | in game name | vanilla | measured | `battle_bin` X |
   |---|---|---|---|
   | Vagabond | Broadsword | 14 | 14 |
   | Riposte | Iron Sword | 3 | 3 |
   | Claymore | Mythril Sword | 15 | 15 |
   | Flamberge | Sleep Blade | 15 | 15 |

   All four carry Y = 0, and all four frames showed a palette 0 red slash arc. The effect nibble
   checks out too.

2. **Offline, a prediction the file could have failed.** A connected component pass found that
   palettes 1 and 2 hold only five non zero colours each (slots 11-15) so no weapon tile can be
   drawn with them. `BATTLE.BIN` agrees: across all 127 weapons, palettes 1 and 2 appear as an
   effect palette Y twenty times and as a weapon palette X **zero** times.

Fifteen published PSX records were checked byte for byte against the shipped file: 15 of 15 match.

Full map: `tools/probes/lw289_weapon_palette_map.json` in the LW repo, produced by
`tools/probes/lw289_battle_bin_palette_map.py` (carries the anchor gate and a selftest).

### The distribution, which is the product constraint

```
weapon palettes X in use:  3(8) 4(9) 5(6) 6(7) 7(6) 8(10) 9(5) 10(9) 11(7) 12(5) 13(18) 14(20) 15(17)
effect palettes Y in use:  0(107) 1(10) 2(10)
```

**Thirteen palettes, one hundred and twenty seven weapons.**

---

## ANSWERED, and it is the best news in this document: effects can never be hit

**Weapons use palettes 3 to 15. Effects use palettes 0, 1 and 2. Zero overlap, across all 127
weapons.**

This kills the worst constraint anyone had assumed. The standing worry was "roughly two fifths of
the sheet is swing arcs and sparkles drawing through the same sixteen palettes, so recolouring a
weapon may retint an effect". It cannot. Not for any weapon, ever. Recolour palettes 3-15 freely.

If you want to tint effects too, that is palettes 0, 1 and 2, and palette 0 alone drives 107 of
the 127 weapons' slash arcs, so it is a single global knob rather than a per weapon one.

---

## CORRECTED: zone grain is dead in vanilla

An earlier version of this file claimed the artists packed several weapons into disjoint index
zones of one palette, and that this bought extra addressable looks. **That was wrong**, and the
thing I read as "the same shapes drawn twice in different zones" was actually page 1 and page 2 of
the three page layout.

The zone structure itself is real. Every full palette is four short hue ramps plus a shared
specular, `{1,2,3,4}` `{5,6,7}` `{8,9,10}` `{11,12,13,14}` `{15}`, derived twice independently
(luminance cuts in 14 of 14 full palettes with zero exceptions, and an all or nothing zone
coverage score of 0.0406 for this partition against 0.3175 for the one ramp model).

But it does not buy addressability. **Of the 681 within palette pairs of distinct weapon graphics,
zero are index disjoint.** Minimum overlap 3 indices, median 7, and all 681 collide inside the base
zone `{1,2,3,4}`. Twenty distinct graphic sets have byte identical footprints while sharing a
palette.

So the honest grain today is **one palette equals one colour, thirteen addressable weapon
palettes**. Not 56, not 14, not 16.

And those thirteen groups are colour incoherent against any per item design intent: the best single
colour for a group still leaves its worst member about 110 degrees of hue away on average. Palette
14 alone holds twenty weapons spanning 273 degrees of hue.

**Re indexing the pixel block is still open as an escape hatch** (we ship the whole file, so the
indices are ours), but it has not been designed or tested, and it multiplies capacity by roughly
two, not by ten.

---

## THREE DEAD LEVERS. Do not re-walk these.

All three were tested live with the serve proven from the loader log, and with untouched control
weapons in the same battle.

1. **`ItemData.<Palette>` is inert for battle colour.** Three launches, twelve battle loads. Bytes
   were changed 2 to 8, 4 to 0 to 8, and the palettes did not move. The write demonstrably reached
   game memory: `[ItemData] prawl.fft.livingweapons changed ID 19 (Palette, value: 8)`,
   `Applying ItemData with 94 change(s)`. That byte instead keys the **menu icon** CLUT
   (`fftpack/tex/item/item_01.clut`, 512 B, same pac), which scores z = +5.27 in a within SpriteID
   permutation test against +1.21 (chance) for the weapon sheet bank. Semantic control: ice named
   weapons name a palette holding a blue ramp 6 of 6 times in the icon bank and 0 of 6 in the
   weapon bank. It equals the battle X nibble for only 6 of 127 weapons, by coincidence.
2. **`ItemData.<SpriteID>` is inert for the drawn battle weapon.** SpriteID 14 was rewritten to 33
   (an axe), the write is in the log, and the sword never changed shape. SpriteID runs 0-178 and
   allocates blocks to rings, perfumes, shoes and armour, none of which has a battle sprite: it is
   the menu icon graphic. Note this contradicts a doc comment in LW's `tools/generate.py`.
3. **Overriding `battle_bin.bin` does not change the palette.** FFTPack file 0, served from our
   copy five times with zero reads of the game's own copy, deployed bytes verified at the correct
   offsets, exactly two bytes different in a 1.4 MB file, only one copy of that table in the file.
   Nothing moved. So the remaster re baked the item graphics data somewhere of its own and the
   classic file is vestigial for this purpose, exactly like the two table fields above.

The read map still stands: the remaster's baked copy evidently derives from the PSX table, since it
matches live four times out of four. We can read the assignment. We cannot yet write it.

---

## THE OPEN QUESTION, and the needle to search for

**Where does the enhanced renderer read the weapon palette index from?** It is not the two item
table fields and it is not `battle_bin.bin`. It is baked in `fft_enhanced.exe` or in an nxd table.

Search for this. **Machine generated and checksummed**, because a hand wrapped copy of the
packed form in an earlier version of this file was mangled (CC caught it: 249 hex chars, odd).
The canonical machine readable copy is `tools/probes/lw289_palette_needles.json` in the LW repo.
Verify any copy you paste by length and md5 before searching with it.

**x_u8**, one byte per weapon, items 1 to 127.
127 bytes, 254 hex chars, md5 `339553d54e8535e75a0fea12bfa67134`

```
0e0f0e030d03050404080e0e0f03040d06050e0e030f0607080f0d0407050808080f080e050d
0d0d050403060e0f080d0b0e0e09090c090f0a0b0d07040e0d0405070f090a0d0d0a0e0d0e0b
0e0f0a0b030f0d070e0f030e0c0b0f08060f0d0e0a0b0d0d0f0a040f0c080c0a0f03060c0e0b
0e0a0d0d0f0a090e0604060708
```

**xy_packed**, the PSX nibble pairs with the graphic byte stripped.
127 bytes, 254 hex chars, md5 `6b610c8300f201e858f36055a32ff18e`

```
e0f0e030d03051404082e0e0f03040d06052e0e030f0607080f0d0407050818180f080e050d0
d0d050403060e0f080d0b0e0e09291c090f0a0b0d07040e0d0405070f090a0d0d1a1e1d1e1b1
e0f0a0b030f0d070e0f030e0c0b0f08060f0d0e0a0b0d0d0f0a040f0c080c0a0f03060c0e0b0
e2a2d2d2f2a292e06040607080
```

**psx_record**, the full XY ZZ block exactly as BATTLE.BIN ships it.
254 bytes, 508 hex chars, md5 `a0d204e8fa2743dabf77e2c3c633e672`

```
e000f002e0043006d00030025104400640028204e008e00af0083008400ad008600a520ae000
e0023004f006600470028006f000d00640047002500081028102800cf00c800ee00e500cd010
d012d0145010401230146010e012f0148010d000b000e000e01692189116c0189016f018a016
b018d01a701a401ae01ad01c401c501c701cf0039003a003d003d100a100e100d102e102b102
e000f000a000b0033003f003d0007000e000f0003000e003c003b003f00380006000f000d000
e000a000b000d000d002f000a0024000f002c0008002c004a006f00430066004c006e004b006
e200a200d200d200f200a2009200e00060004000600070008000
```

It is a good needle: **X never takes the value 0, 1 or 2**, so it will not collide with generic
data or with zero padding. Also worth trying: one byte per item with the value widened to u16 or
u32, and the table starting at item 0 rather than item 1.

If it turns out to be in the exe, a runtime write is better than a file override anyway, because it
is live tunable rather than needing a rebuild.

---

## Restart or battle load: the loader half is settled

`FFTPackFileOverrideStrategy.OnRequestRead` opens the mod file **fresh on every single request**:

```csharp
using var fs = File.OpenRead(entry.LocalFilePath);
fs.Position = offset;
byte[] buffer = ArrayPool<byte>.Shared.Rent(size);
fs.ReadExactly(buffer, 0, (int)MathF.Min(size, fs.Length));
fixed (byte* inputPointer = buffer)
    NativeMemory.Copy(inputPointer, (void*)outputPointer, (nuint)size);
```

No cache anywhere. And the game requests file 71 once per battle load: one session logged eight
reads at irregular gaps from 17 seconds to nine minutes, always as a trio with file 63
(`battle_wep1_shp.bin`) and file 65 (`battle_wep1_seq.bin`). Sizes in those log lines are HEX.

So overwriting the deployed file mid session should take on the next battle. The remaining
unknown is whether the GAME re uploads rather than keeping a decoded copy in VRAM. Not yet tested;
it is a five minute test and whoever does it should write the answer down.

Only wep1 (63/65) appears in these logs, never wep2 (64/66). Unexplained. Do not assume wep2 is dead.

---

## COLLISION: only one mod can own a file, and the loser is silent

Owner decision 2026-08-19: **LivingWeapons ships first, ColorCustomizer works around it.**

`FFTPackFileOverrideStrategy.AddMappingForLocale` keys the registry by file index per locale, and
the assignment happens **unconditionally, after** the conflict warning:

```csharp
if (fftLocaleFile.ModdedFiles.TryGetValue(fileIndex, out var existing))
    _logger.WriteLine($"... Conflict: Mod '{modIdOwner}' uses fftpack file index '{fileIndex}' ... already used by {existing.ModIdOwner}!", ColorYellow);
else
    _logger.WriteLine($"... {modIdOwner} mapping file {fileIndex} ({locale}) ...");

fftLocaleFile.ModdedFiles[fileIndex] = new FFTPackModdedFileEntry(modIdOwner, ...);   // <-- always
```

Whole file, **last registered wins**, and the loser gets one yellow line in a 1.2 MB log. Detect it
by grepping the launch log for `Conflict: Mod '<you>' uses fftpack file index '71'`.

### The seam that dissolves it

`IFFTOModPackManager` in the loader's Interfaces assembly has everything needed to compose:

| member | what it gives you |
|---|---|
| `IReadOnlyDictionary<string, IFFTOModFile> ModdedFiles` | is `fftpack/unit/battle_wep_spr.bin` already claimed, and by whom (`ModIdOwner`) |
| `IFFTOModFile.LocalPath` | the on disk path of the other mod's baked sheet, to use as your base |
| `byte[] GetFileData(gameMode, gamePath)` | pristine VANILLA bytes for any game path, no FF16Tools, no pac hunting, no md5 gate |
| `AddModdedFile(modId, gameMode, gamePath, byte[] file, options)` | register your composed result |
| `RemoveModdedFile(gameMode, gamePath)` | back out cleanly if the user disables the feature |

The composition CC should implement:

1. Look up `fftpack/unit/battle_wep_spr.bin` in `ModdedFiles`.
2. **Claimed by another mod**: read its `LocalPath` and use those 85504 bytes as your base. The
   user deliberately installed a mod that colours weapons; ride on top of it, do not erase it.
3. **Unclaimed**: `GetFileData` the vanilla bytes and use those.
4. Apply the hue transform to the palette blocks only, leave the pixel pages untouched, and
   `AddModdedFile` the result. Register after LW and you win the last writer race.

Declare LivingWeapons as an optional Reloaded dependency so the ordering is deliberate, not lucky.

Because the loader re reads `LocalPath` on every request, a live slider does not need to re
register: overwrite the bytes at that path and the next battle load serves them.

---

## Traps banked, all of them paid for

- **Night screenshots are worthless for colour.** The engine rotates hue by about 135 degrees at
  night: the same bow core reads hue 60 by day and 195 after dark. Daylight maps only.
- **Weapons only render during an attack animation.** Every reading needs a swing.
- **The game applies an overbright pass of about 1.232 and then CLAMPS.** Solved from the one
  unclamped channel in a capture (99 becomes 122) and confirmed on two others. This matters more
  than it sounds: a fully saturated label colour has its dominant channel clamped, so **hue is not
  preserved for saturated colours** and two different palettes can display identically. Model the
  clamp before naming a palette from a screenshot.
- **Never put grey or white in a labelling palette.** A flat grey blade at sprite scale reads as an
  ordinary steel sword, so a working probe gets reported as "no change".
- **Generate label colours, never hand list them.** The launch that proved the mechanism used a
  hand written list where palette 10 was rgb(248,0,248) and palette 15 was rgb(248,80,248), both
  hue 300.0 exactly. It proved the mechanism but could not name the palette. Assert mutual
  distinguishability in a selftest.
- **`modded.pac` is rebuilt per launch and its contents move.** Extract reference art from the
  numbered base pacs, never from it.
- **Retry from formation is a fresh battle load.** Useful: it makes a four weapon census four
  cheap reloads instead of four separate battles, and it proved the palette assignment is stable
  per item across twelve loads.

---

## Tooling in the LivingWeapons repo, all with selftests

- `tools/probes/lw289_palette_selector.py` — forges the census sheet (16 generated label hues,
  22.5 degrees apart, selftest refuses any pair within 15 degrees), `--deploy`, `--checklog` for
  the serve proof, and `--measure <png>` which names the palettes in a screenshot numerically
  rather than by eye. `--hot` deploys while the game runs.
- `tools/probes/lw289_battle_bin_palette_map.py` — extracts `battle_bin`, gates on 15 published PSX
  anchors, asserts the 4 live measured palettes still match, dumps the full map as JSON.
- `tools/probes/lw289_battle_bin_write.py` — the write test above, with a byte level diff gate that
  refuses to ship a 1.4 MB file containing any change it did not name.
- `tools/probes/lw251_wep_spr_forge.py` — the original round 12 forge, plus `icon_ramp()` and
  `paint_palette()`. **Note the painter is structurally wrong**: it sorts all 15 slots into one
  luminance ramp, which shuffles colours across the four independent zone ramps. On one worked
  case it wrote the identical colour onto slots 3, 6, 10 and 12 and collapsed five zone hues
  spanning 161 degrees into a 20.5 degree spread. A zone aware painter passes an offline quality
  gate on 103 of 121 weapon bakes; the shipped one passes 0 of 121. Do not copy it as is.
