# Section Mappings Guide

Section mapping files define how palette indices map to customizable sprite sections.

## File Structure

```json
{
  "job": "JobName_Gender",
  "sprite": "battle_xxx_m_spr.bin",
  "sections": [
    {
      "name": "SectionName",
      "displayName": "Display Name",
      "indices": [5, 6, 4, 3],
      "roles": ["base", "highlight", "shadow", "outline"]
    }
  ]
}
```

## Available Roles (from HslColor.cs)

| Role | Effect | Use For |
|------|--------|---------|
| `highlight` | Lightest (L * 1.35) | Brightest shade in set |
| `base` | Primary color | Main/primary shade |
| `shadow` | Darker (L * 0.65) | Second darkest |
| `outline` | Darkest (L * 0.45) | Darkest edge pixels |
| `accent` | Light detail (L * 1.5) | Light accent colors |
| `accent_shadow` | Slightly dark accent (L * 1.25) | Still lighter than base! |

**Important:** `accent_shadow` is LIGHTER than base, not darker. For dark shades use `shadow` or `outline`.

## Verification Process

1. Add a single test index to the JSON:
   ```json
   { "name": "Index7", "displayName": "Index 7", "indices": [7], "roles": ["base"] }
   ```

2. Deploy and test in Theme Editor - note what sprite part changes

3. Record the brightness level (lightest, darker, darkest, etc.)

4. Continue with next index until you identify all indices for a section

5. Group indices into sections with correct roles based on brightness order

## Index Order in Sections

Indices are NOT listed sequentially. They're ordered by role assignment:
- First index gets first role, second index gets second role, etc.

Example: `[12, 11, 13, 10]` with `["highlight", "base", "shadow", "outline"]`
- Index 12 = highlight (lightest)
- Index 11 = base
- Index 13 = shadow
- Index 10 = outline (darkest)

## Common Patterns

**4-index sections:**
- `[base, highlight, shadow, outline]` - Standard with highlight
- `[highlight, base, shadow, outline]` - When base isn't lightest

**3-index sections:**
- `[base, shadow, outline]` - No highlight

## Skipped Indices

Skip indices for:
- Face/skin tones (usually 1, 13-15)
- Eyes (usually 2)
- Character outline (usually 1)

These remain unchanged regardless of theme.

## Tips

- Palette indices range from 0-15
- Deploy after each change to test in Theme Editor
- The same index can appear in multiple visual areas
- Document which body parts each index affects
