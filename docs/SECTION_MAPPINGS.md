# Section Mappings Guide

Section mapping files tell the Theme Editor which palette indices belong to which part of
a sprite (hair, armor, skirt...) and which index anchors each color slider.

Directory layout under `ColorMod/Data/SectionMappings/`:

| Location | Characters | Dropdown group |
|---|---|---|
| `*.json` (root) | Generic jobs (`Knight_Male.json`...) | Generic jobs, job-unlock order |
| `Story/*.json` | Story characters (`Agrias.json`...) | ── Story Characters ── |
| `NPC/*.json` | NPC characters (`Alma.json`...) | ── NPCs ── |
| `Monster/*.json` | Monster families | ── Monsters ── |

A character appears in the Theme Editor dropdown if and only if a mapping file exists.
Story/NPC/Monster groups list alphabetically (explicitly sorted in the loader).

## File Structure

```json
{
  "job": "Meliadoul",
  "sprite": "battle_h80_spr.bin",
  "sections": [
    {
      "name": "Dress",
      "displayName": "Dress",
      "indices": [7, 8, 9],
      "roles": ["shadow", "base", "highlight"]
    }
  ]
}
```

Multi-sprite characters use `"sprites": ["battle_aguri_spr.bin", "battle_kanba_spr.bin"]`
instead of `"sprite"`. All listed sprites get the same palette treatment.

## How shading ACTUALLY works (read this before trusting role names)

There are NO fixed role multipliers. An earlier system applied hardcoded lightness factors
per role name; that system is gone. Today:

1. Each section has one anchor index: the index whose role is `"base"` (or the explicit
   `"primaryIndex"` override). The color picker displays the anchor's color, and the color
   the user picks lands on the anchor exactly.
2. When a section is first edited, `RelativeShadeGenerator` snapshots every index's
   ORIGINAL HSL relationship to the anchor (hue offset, saturation ratio, lightness ratio)
   from the vanilla sprite, then re-applies those relationships to the picked color. The
   sprite's own original shading structure is the shading model.
3. Every role string other than `"base"` is documentation only. `"shadow"`, `"highlight"`,
   `"outline"`, `"accent"` label intent for the next human reader; the code never reads them.
   Keep using them for readability, but know they are inert.

### Knobs that matter

| Field | Effect |
|---|---|
| `"base"` role (or `primaryIndex`) | The anchor. Pick the MID-TONE main color of the section, never the darkest or lightest, because every other index scales relative to it. |
| `shadeMode` | Omitted = `preserve`: keeps the original hue drift between indices (good for 2-3 index clusters). `"uniformHue"`: forces every index to the picked hue with additive S/L offsets; use for 4+ index groups, which go incoherent at extreme colors in preserve mode. |
| `linkedTo` | One slider drives two sections: the section naming another section via `linkedTo` hides that section's picker and pushes its color there too. |
| `primaryIndex` | Explicit anchor override when the base-role index is not the right anchor. |

## Conventions

- Index 0 is transparency; never include it in a section.
- Index 1 is usually the character outline and index 2 the eyes; existing mappings leave
  them out of every section so they never shift with a theme. Confirm per sprite.
- Skin usually lives in the high indices (13-15). Some sprites share browns between hair
  and skin; grouping decides which slider wins those indices.

## NPC calibration workflow (2026-08-13)

New NPC characters ship with a PER-INDEX mapping: 15 sections named `Index 1` .. `Index 15`,
one palette index each, all role `"base"`. This gives the owner one slider per index to
discover empirically which index controls what, in-game. The owner then hands back
groupings ("4,5,6,7 = skirt, 11-14 = hair...") and the per-index file is replaced with a
grouped mapping: mid-tone anchor gets `"base"`, descriptive roles for the rest,
`"uniformHue"` for 4+ index groups. `Tests/Registry/NpcRosterContractTests.cs` enforces
full 1-15 coverage while a mapping is still per-index style (exactly 15 sections) and
relaxes once it is grouped.
