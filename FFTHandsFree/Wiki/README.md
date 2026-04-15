# Wiki Reference

Scraped authoritative game data from external FFT wikis, stored locally so
Claude Code sessions can port details into structured C# tables (per-job
equippability, item effects, story-character unique classes, etc.) without
repeated web fetches.

## Structured docs (markdown, Claude-readable)

| File | Purpose |
|---|---|
| `Abilities.md` | Ability names by skillset, quick summary of effects. |
| `BattleMechanics.md` | Turn order, CT, charging, counter, reaction conditions. |
| `DamageFormulas.md` | Damage formulas per weapon type, Brave/Faith multipliers. |
| `Equipment.md` | Per-weapon-type job lists + stat tables (short form). |
| `FormulaTable.md` | Numeric damage multipliers, Zodiac table. |
| `GameDataStructs.md` | On-disk / in-memory data layouts we've reverse-engineered. |
| `Jobs.md` | 22 generic jobs with unlock requirements + stat multipliers. |
| `MapFormat.md` | Tile layout, terrain types, height rules. |
| `PacFiles.md` | .pac archive format (game-data extraction reference). |
| `PartyManagement.md` | Party composition strategies + job-level planning. |
| `SkillsetsAndStatus.md` | Status effects that specific skillsets inflict/cure. |
| `StatusEffects.md` | Full status-effect encyclopedia (Protect, Petrify, Haste, etc.). |
| `StoryCharacters.md` | Recruit conditions + primary skillsets per story character. |
| `StoryWalkthrough.md` | Chapter-by-chapter battle list. |
| `ZodiacAndElements.md` | Zodiac compatibility table, element weakness/absorb rules. |

## Raw scrape dumps (txt, long-form, authoritative)

These are unedited copies of the Final Fantasy wiki's equipment pages. They
contain the canonical per-item and per-type eligibility sentences ("Knight's
swords can be wielded by Ramza's gallant knight, knights, dark knights, ...").
Use them when porting data into C# tables — the .md summaries above are
human-curated and may lag the raw source.

| File | Covers |
|---|---|
| `weapons.txt` | All 14 weapon categories with per-category job lists + every weapon's stats. |
| `armor.txt` | Heavy armor, clothes, robes, dresses, helmets, hats, hair adornments, shields — each with per-category job lists. |
| `accessories.txt` | Shoes, armguards, rings, armlets, cloaks, perfume, lip rouge. |
| `adorments.txt` | Short list of hair adornments (Ribbon, Hairband, Barrette) with effects. |

## How to use

- **Porting data into C#** — open the `.txt` for the category you're
  extending, grep for "can be wielded by" / "can be worn by" / "can be
  equipped by" sentences. Those give you the per-job eligibility rules
  verbatim. Cross-check with `Equipment.md` for a quick summary of stats.
- **Claude planning during battle** — the `.md` docs are the shorter
  reference Claude consults in-session. Keep them under ~200 lines each.
- **Ivalice Chronicles Enhanced Mode notes** — both `.md` and `.txt`
  contain a few game-version differences (e.g. hair adornments are
  female-only on classic FFT but available to males in ICE Enhanced).
  Prefer ICE-specific rules when they differ.

## Provenance

Scraped 2026-04-15 session 16 while porting per-job equippability data.
No subsequent edits — they are snapshots. Refresh by re-scraping the wiki
pages when new info is needed; do NOT hand-edit to "correct" them, since
that breaks the authoritative-source property.
