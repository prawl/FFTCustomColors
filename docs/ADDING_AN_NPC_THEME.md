# Adding a Built-In NPC Colour Theme — Playbook

**In plain terms:** an NPC like Valmafra starts out offering only "original" in her dropdown.
This is how you turn a set of slider positions somebody picked in the theme editor into a theme
that ships with the mod, so every player gets it without touching a slider themselves.

You write down the colours in a small text file, run one command, and the mod grows a new theme.
Sibling playbook for monsters: [`ADDING_A_MONSTER.md`](ADDING_A_MONSTER.md).

Ledger row: **CC-21**.

---

## The model (why this is only a few colours)

A theme is **not** a picture and **not** a full palette. It is a **base colour per section**.

Each NPC has a section mapping (`ColorMod/Data/SectionMappings/NPC/<Character>.json`) that says
which palette slots make up the Outfit, the Cape, the Hair, and so on, and which slot in each
group is the **base** one. When you set a section's base colour, `RelativeShadeGenerator` re-derives
that section's highlight and shadow from the **original** sprite's relationships. So two colours
can repaint five palette slots, and the shading still looks like the artist drew it.

Consequences worth internalising:

- **A theme records only the sections that changed.** Anything you leave out of the recipe keeps
  its vanilla colour, byte for byte. A contract test enforces exactly that.
- **The hex in the theme editor is the base colour**, and it is what you copy into the recipe.
  Do not copy the H/S/L numbers; they are just that hex expressed differently.
- **The saved bin will not match your hex exactly.** The sprite format stores 5 bits per channel
  (BGR555), so `#2D2D2D` lands as `#292929`. That is normal and the theme editor does the same
  thing. Do not "fix" it.
- Only **palette 0** is touched. NPC sprite bins have palettes 1 to 3 sitting there all zero;
  leave them alone. (Some older Reis themes wrote junk into them. Harmless, but do not copy it.)

---

## The step-by-step

### 1. Get the numbers

Open the config window (F1), pick the character in the **Theme Editor** template dropdown, drag
sliders, and read off **the hex under each section you changed**. Screenshot it if someone else is
driving.

> **Scroll the whole panel.** Valmafra has five sections (Outfit, Hair Ribbon, Cape, Hair Color,
> Skin Color) and a screenshot only catches two or three. A section you cannot see is
> indistinguishable from a section that was deliberately left alone — decide which one it is
> before you write the recipe, because the recipe cannot express "unknown".

You do **not** need to press Save in the editor. Saving there creates a *user* theme, which is a
different thing from a *built-in* theme and lives in the user's own config.

### 1b. If the screenshot cut off the sliders

Do not guess and do not throw the screenshot away. The editor shows **both preview panes**,
and the Modified pane is the Original pane run through the palette swap, pixel for pixel. Line
the two panes up and every vanilla colour tells you what it became.

```bash
python scripts/recover_theme_from_screenshot.py <shot.png> Valmafra --theme dark_witch
```

It finds the Original pane, aligns the two, reports what each palette index became, then
**inverts the shade maths** to find the base colour that produced those shades, and prints a
ready-to-paste recipe. Requirements: lossless PNG, both panes visible, same pose in both, and
the editor showing `original` as the template.

Read the report, not just the recipe:

- **`verified`** on a section means the recovered base re-bakes to exactly the shades measured
  off the screen. That is the check that matters.
- **`N equivalent solutions`** is expected and harmless. The palette holds 5 bits per channel,
  so many base colours store identically. Any of them is correct.
- **A `WARNING` about an index drawing no pixels is the one thing you cannot ignore.** See below.

#### The limit of this technique

**An index with no visible pixels in the chosen pose cannot be recovered.** Not "recovered less
accurately" — not recovered at all. The recipe will still bake *something* there, derived from
the shade relationships, and it can differ from what the author's slider produced.

This is not hypothetical. For Valmafra's `dark_witch`, a hand-authored recipe and a recovered
one agreed on every visible pixel and still produced different bytes at index 6, the hair ribbon
shadow, which that pose does not draw. Every test in the suite passes either way, because every
test compares the bin to the recipe and both are self-consistent. Nothing downstream can catch it.

So: when the tool warns, either accept a shadow that may be slightly off, or get the slider value.
Rotating the preview to a pose that draws the missing part also works.

### 2. Write the recipe

`ColorMod/Data/BuiltInThemes/NPC/<Character>.json`. This is the **only hand-edited file** in the
whole process.

```json
{
  "character": "Valmafra",
  "themes": [
    {
      "name": "dark_witch",
      "sections": {
        "Outfit": "#2D2D2D",
        "HairRibbon": "#862727"
      }
    }
  ]
}
```

- `character` must match the `name` in `ColorMod/Data/StoryCharacters.json`.
- `name` is the theme key: **lowercase, snake_case**. It becomes the folder suffix and the value
  stored in the player's config, so it is effectively permanent once shipped.
- Section keys are the `name` fields from that character's section mapping (`HairRibbon`, not the
  `displayName` "Hair Ribbon").
- Add more objects to `themes` for more themes on the same character.

### 3. Bake it

```bash
dotnet run --project tools/ThemeBaker Valmafra      # one character
dotnet run --project tools/ThemeBaker               # everything on disk
```

The baker does two things:

1. Writes `ColorMod/FFTIVC/data/enhanced/fftpack/unit/sprites_<character>_<theme>/battle_<sprite>_spr.bin`
2. Appends the theme name to that character's `availableThemes` in `StoryCharacters.json`

It is **idempotent** — re-running it never duplicates a registry entry, so re-bake freely.

It reuses the production classes (`SectionMappingLoader`, `PaletteModifier`,
`RelativeShadeGenerator`), so a baked bin is byte-identical to one the theme editor would have
saved. There is no second implementation of the colour maths to drift.

**Never hand-edit a baked bin.** Edit the recipe and re-bake.

### 4. Tell the tests about it

`Tests/Registry/NpcRosterContractTests.cs` asserts each NPC's exact theme list. Add the character
to `ExpectedThemes`:

```csharp
private static readonly Dictionary<string, string[]> ExpectedThemes = new()
{
    { "Valmafra", new[] { "original", "dark_witch" } },
};
```

Everything else is automatic: `NpcBuiltInThemeContractTests` discovers recipes off disk, so a new
recipe file grows its own test cases with no edit.

### 5. Run the suite, then deploy and look at it

```bash
./RunTests.sh
powershell.exe -ExecutionPolicy Bypass -File ./BuildLinked.ps1
```

This is a data-only change, so `BuildLinked.ps1` is enough; **do not kill a running game** to test
it. Pick the theme in the config window and check the preview carousel.

**Always confirm the folder actually deployed**, not just that the build said "successful":

```bash
ls "$RELOADEDIIMODS/paxtrick.fft.colorcustomizer/FFTIVC/data/enhanced/fftpack/unit/" | grep <character>
```

`BuildLinked.ps1` copies themed sprite folders for the characters it finds **in the registry**
(`StoryCharacters.json`). That used to be a hardcoded list of ten story characters, which meant
every NPC theme folder was silently skipped: the registry advertised the theme, the folder never
arrived, the player picked it and got vanilla, and nothing logged a word. Fixed 2026-08-21 by
deriving the list from the registry. If you ever see a theme apply as vanilla with a green suite,
suspect the deploy before you suspect the palette.

> Some story characters cache their sprite and need a restart before a theme change *renders*
> in-game. That is an engine wall, not a bug in your theme. See the Walled section of
> `docs/TODO.md`.

---

## What the tests actually guard

`Tests/Registry/NpcBuiltInThemeContractTests.cs` runs five checks per (character, theme):

| Test | Catches |
|---|---|
| `..._Is_Listed_In_The_Character_Registry` | recipe baked but never registered, so nobody can select it |
| `..._Ships_A_Sprite_Bin` | registered but never baked, so selecting it silently shows vanilla |
| `..._Bin_Matches_A_Fresh_Bake_Of_Its_Recipe` | **the anti-drift gate.** Recipe, section mapping, or shade maths changed without a re-bake |
| `..._Actually_Recolours_Something` | non-vacuity: a recipe that changes nothing would otherwise pass every other check |
| `..._Leaves_Untouched_Sections_And_Pixels_Alone` | a recipe bleeding into sections it never named, or into pixel data outside the palette |

The third one is the load-bearing one, and it is **proven to bind**: mutating a recipe hex makes
it fail by name. If you change `RelativeShadeGenerator`, expect it to go red for every shipped
theme — that is the test doing its job, and the fix is to re-bake, not to relax the test.

---

## Traps

1. **A section missing from the screenshot is ambiguous.** Confirm "unchanged" rather than
   assuming it. The recipe has no way to say "I do not know".
2. **Section key, not display name.** `HairRibbon` works, `Hair Ribbon` throws with the list of
   valid names.
3. **Theme keys are forever.** Once a player's `Config.json` holds `dark_witch`, renaming the
   theme silently resets them to original.
4. **The dropdown label is derived, not authored.** `ThemeNameFormatter.FormatThemeName` splits
   on underscores and title-cases, so `dark_witch` displays as "Dark Witch" for free. Pick the
   key so that transform produces the label you want; there is no separate display-name field.
5. **Do not judge a run by the exit code.** Read the failing test *names*. This repo has been
   burned by a green-looking suite that never asserted anything.

## Harvested themes (CC-31): free colours the game already made

Some unit bins carry finished alternate colour schemes the artists left in palette slots 1 to 7.
Shipping one is not designing a theme, it is promoting that slot over slot 0 verbatim. Prefer this
over inventing colours when a viable slot exists: the four themes the owner rates best turned out
to be exactly this.

Recipe lives at `ColorMod/Data/BuiltInThemes/Harvested/<Character>.json`:

    { "character": "Elmdore", "themes": [ { "name": "southern_sky", "sourceSlot": 2 } ] }

Then `dotnet run --project tools/ThemeBaker` and add the theme to the character's expected list in
`Tests/Registry/NpcRosterContractTests.cs` if they are an NPC.

Differences from a designed recipe, all of which make this the easier path:

- No section mapping is involved, so it works for characters whose mapping lives under
  `SectionMappings/Story/` (Agrias) exactly as for the NPCs. The sprite list comes from
  `StoryCharacters.json`.
- It is a byte move, so none of the CC-33 colour conversion hazards apply. No quantisation, no
  shade generation, no truncated slider readout.
- Every sprite the character owns is baked, so Agrias gets both `aguri` and `kanba`.

**Check the slot is not vacuous before you write the recipe.** A census found three candidates
that were byte identical or near identical to slot 0, which would have shipped a dropdown entry
that changes nothing. The baker refuses an identical slot outright, and
`HarvestedThemeContractTests` asserts non-vacuity per sprite, but the cheapest check is counting
how many of the 16 entries differ from slot 0 before committing to a name.

**Naming.** The generic jobs already ship slots 1 to 4 as northern_sky, southern_sky,
corpse_brigade and lucavi. Reuse that convention, since the slot index is the engine's faction
selector and a player running southern_sky generics then gets a matching story character.
