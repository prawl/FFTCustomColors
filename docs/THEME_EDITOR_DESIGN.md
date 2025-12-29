# Theme Editor Design Document

## Overview

A built-in theme editor that allows users to create custom color themes for generic job classes through an intuitive UI with real-time preview.

## Goals

1. Allow users to create personalized themes without external tools
2. Provide professional-looking results through auto-generated cohesive palettes
3. Enable theme sharing between users
4. Roll out incrementally across job classes

---

## User Experience

### Workflow

1. User opens Config UI (F1)
2. Navigates to "Theme Editor" tab
3. Selects a template from dropdown (e.g., "Knight Male")
4. UI displays the sprite preview and section-based color pickers
5. User selects primary color for each section (Cape, Boots, Armor, etc.)
6. Preview updates in real-time as colors are adjusted
7. User clicks "Save", enters theme name (e.g., "Ocean Blue")
8. Theme files are generated and saved
9. User returns to main Config UI, scrolls to Knight Male
10. "Ocean Blue" now appears in the theme dropdown
11. User selects and applies the theme

### Key UX Principles

- **One color picker per section**: User picks primary color only
- **Auto-generated accents**: System generates highlight/shadow/outline colors automatically
- **Real-time preview**: Changes visible immediately as user adjusts colors
- **Separation of concerns**: Editor creates themes, main UI assigns them

---

## Technical Architecture

### Palette Structure

Each sprite's first 512 bytes contain palette data:
- 16 palettes × 16 colors × 2 bytes per color = 512 bytes
- Palette 0: Player colors (what we modify)
- Palettes 1-4: Enemy team colors (preserve from original)
- Palettes 5-7: Unused

### Section-to-Index Mapping

Each job class requires a mapping file defining which palette indices control which visual sections.

Example mapping for Knight Male:
```json
{
  "job": "Knight_Male",
  "sprite": "battle_knight_m_spr.bin",
  "sections": [
    {
      "name": "Cape",
      "displayName": "Cape",
      "indices": [3, 4, 5],
      "roles": ["shadow", "base", "highlight"]
    },
    {
      "name": "Boots",
      "displayName": "Boots",
      "indices": [6, 7],
      "roles": ["base", "highlight"]
    },
    {
      "name": "Armor",
      "displayName": "Armor",
      "indices": [8, 9, 10],
      "roles": ["shadow", "base", "highlight"]
    }
  ]
}
```

### Auto-Shade Generation Algorithm

When user selects a primary color, generate related shades using HSL manipulation:

```
Input: Primary color (from HSL sliders)

Shadow:    L = L * 0.65, S = min(S * 1.1, 1.0)
Base:      Original color (user's pick)
Highlight: L = min(L * 1.35, 0.95), S = S * 0.85
Outline:   L = L * 0.4,  S = min(S * 1.15, 1.0)

Convert to RGB, then to BGR555 for palette
Apply to corresponding indices based on "roles" mapping
```

### Sprite Preview Rendering Pipeline

The theme editor leverages the existing `BinSpriteExtractor` for real-time preview rendering.

**Existing Infrastructure (from BinSpriteExtractor.cs):**

| Component | Details |
|-----------|---------|
| **Palette reading** | `ReadPalette()` - Decodes BGR555 colors from bytes 0-511 |
| **Sprite extraction** | `ExtractSprite()` - Renders 4-bit indexed pixels using palette |
| **8-direction support** | `ExtractAllDirections()` - All poses with E-side mirroring |
| **Display scaling** | 3x scale (32×40 → 96×120) with nearest-neighbor |
| **Transparency** | Palette index 0 = transparent |

**BIN File Structure:**
```
Bytes 0-511:    Palette data (16 palettes × 16 colors × 2 bytes BGR555)
Bytes 512+:     Sprite pixel data (4-bit indexed, 256px wide sheet)

Sprite Sheet Positions:
  0: W (West)      3: NW
  1: SW            4: N (North)
  2: S (South)     5-7: Animation frames

E, NE, SE directions created by mirroring W, NW, SW
```

**Real-Time Preview Architecture:**

```
┌─────────────────────────────────────────────────────────────────┐
│                     PREVIEW UPDATE FLOW                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  1. On template selection:                                       │
│     - Load original BIN file into byte[] (one-time)             │
│     - Clone to working byte[] for modifications                  │
│                                                                  │
│  2. On any slider change (~60fps capable):                       │
│     a. Convert HSL to RGB                                        │
│     b. Generate shadow/highlight variants                        │
│     c. Convert RGB to BGR555 (game format)                       │
│     d. Write colors to working palette (bytes 0-31 only)        │
│     e. Call ExtractSprite() with working data                   │
│     f. Convert Bitmap to WPF ImageSource                        │
│     g. Update preview Image control                              │
│                                                                  │
│  Performance: ~5-10ms per update (palette is only 512 bytes)    │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

**New Component: PaletteModifier**

Thin wrapper for in-memory palette manipulation:

```csharp
public class PaletteModifier
{
    private byte[] _originalData;   // Template sprite (immutable)
    private byte[] _workingData;    // Modified copy for preview
    private BinSpriteExtractor _extractor;

    public void LoadTemplate(string binPath)
    {
        _originalData = File.ReadAllBytes(binPath);
        _workingData = (byte[])_originalData.Clone();
    }

    public void SetPaletteColor(int index, Color color)
    {
        // Convert RGB to BGR555
        int r5 = (color.R * 31) / 255;
        int g5 = (color.G * 31) / 255;
        int b5 = (color.B * 31) / 255;
        ushort bgr555 = (ushort)(r5 | (g5 << 5) | (b5 << 10));

        // Write to palette 0 (player colors) at specified index
        int offset = index * 2;
        _workingData[offset] = (byte)(bgr555 & 0xFF);
        _workingData[offset + 1] = (byte)(bgr555 >> 8);
    }

    public Bitmap GetPreview(int spriteIndex = 2) // Default: South-facing
    {
        return _extractor.ExtractSprite(_workingData, spriteIndex, paletteIndex: 0);
    }

    public void Reset()
    {
        _workingData = (byte[])_originalData.Clone();
    }

    public byte[] GetModifiedPalette()
    {
        // Return just the 512 palette bytes for saving
        var palette = new byte[512];
        Array.Copy(_workingData, 0, palette, 0, 512);
        return palette;
    }
}
```

**Why This Approach Works:**

1. **Instant updates**: Only modifying 32 bytes (palette 0) per color change
2. **No file I/O during editing**: Everything happens in memory
3. **Existing code reuse**: `ExtractSprite()` handles all rendering complexity
4. **Color accuracy**: BGR555 conversion matches game's exact format
5. **Preview matches final**: Same rendering path as saved themes

### File Storage

**User themes stored in:**
```
%RELOADEDIIMODS%/FFTColorCustomizer/UserThemes/
  └── Knight_Male/
      └── ocean_blue/
          └── battle_knight_m_spr.bin
```

**Theme registry:**
```
%RELOADEDIIMODS%/FFTColorCustomizer/UserThemes.json
```

```json
{
  "themes": [
    {
      "name": "Ocean Blue",
      "job": "Knight_Male",
      "created": "2024-12-29T10:30:00Z",
      "sections": {
        "Cape": "#0047AB",
        "Boots": "#8B4513",
        "Armor": "#C0C0C0"
      }
    }
  ]
}
```

### Theme Sharing Format

Compact, portable format for sharing themes between users:

```json
{
  "version": 1,
  "name": "Ocean Blue",
  "job": "Knight_Male",
  "palette": "base64-encoded-512-bytes"
}
```

**Sharing workflow:**
1. User clicks "Export" on their theme
2. System generates JSON string, copies to clipboard
3. Recipient clicks "Import", pastes string
4. System validates, creates sprite file from palette bytes
5. Theme appears in recipient's dropdown

This approach guarantees identical results because we share the actual palette bytes, not just color values.

---

## UI Components

### Color Picker Component (Terraria-style HSL Sliders)

Each section uses a compact HSL slider system inspired by Terraria's character customization:

```
┌─────────────────────────────────────────────┐
│  Cape                                       │
│  ═══════════════════════════════════════   │  ← Hue (rainbow gradient 0-360°)
│  ═════════════════════════════════════     │  ← Saturation (gray → full color)
│  ═════════════════════════════════════     │  ← Lightness (dark → light)
│  [■] [📋] [📎]  #D15A37                    │  ← Preview, Copy, Paste, Hex
└─────────────────────────────────────────────┘
```

**Components per section:**

| Element | Description |
|---------|-------------|
| **Hue slider** | Rainbow gradient (0-360°), horizontal drag |
| **Saturation slider** | Gray to full color, updates based on current hue |
| **Lightness slider** | Black to white through the selected color |
| **Color preview swatch** | Shows current combined color |
| **Copy button** | Copies hex code to clipboard |
| **Paste button** | Pastes hex code from clipboard |
| **Hex display** | Shows current value, allows direct hex input |

**Interaction behavior:**

- **Drag anywhere on slider** - Jumps to position and starts dragging
- **Arrow keys** - Fine adjustment (±1) when slider is focused
- **Hex input** - Direct entry updates all three sliders automatically
- **Paste** - Accepts #RGB, #RRGGBB, or raw RRGGBB formats

**Why HSL over HSV?**

HSL (Hue/Saturation/Lightness) is more intuitive for non-artists:
- Lightness slider gives clear "darker/lighter" control
- Saturation at 0 = gray at any lightness level (predictable)
- Maps well to how users think: "I want this color but darker"

### Theme Editor Tab

```
┌──────────────────────────────────────────────────────────────────┐
│  Theme Editor                                                     │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  Template: [Knight Male        ▼]      [Import from Clipboard]   │
│                                                                   │
│  ┌────────────────┐   ┌────────────────────────────────────────┐ │
│  │                │   │  Cape                                   │ │
│  │                │   │  ══════════════════════════════════    │ │
│  │   ◄ SPRITE ►   │   │  ══════════════════════════════════    │ │
│  │    PREVIEW     │   │  ══════════════════════════════════    │ │
│  │                │   │  [■] [📋] [📎]  #0047AB                │ │
│  │                │   │                                         │ │
│  │                │   │  Boots                                  │ │
│  │                │   │  ══════════════════════════════════    │ │
│  │                │   │  ══════════════════════════════════    │ │
│  └────────────────┘   │  ══════════════════════════════════    │ │
│                       │  [■] [📋] [📎]  #8B4513                │ │
│                       │                                         │ │
│                       │  Armor                                  │ │
│                       │  ══════════════════════════════════    │ │
│                       │  ══════════════════════════════════    │ │
│                       │  ══════════════════════════════════    │ │
│                       │  [■] [📋] [📎]  #C0C0C0                │ │
│                       └────────────────────────────────────────┘ │
│                                                                   │
│  Theme Name: [________________________]                          │
│                                                                   │
│  ⚠ Once saved, themes cannot be edited. Export to modify.       │
│                                                                   │
│  [Save Theme]  [Reset]  [Cancel]                                │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
```

**Scrolling behavior:** If a job has many sections (5+), the right panel becomes scrollable while the sprite preview remains fixed on the left.

### My Themes Tab

```
┌─────────────────────────────────────────────────────────────┐
│  My Themes (7 total)                                        │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Knight Male                                                │
│  ├── Ocean Blue                        [Export] [Delete]   │
│  ├── Dark Crusader                     [Export] [Delete]   │
│  └── Forest Knight                     [Export] [Delete]   │
│                                                             │
│  Archer Female                                              │
│  ├── Shadow Hunter                     [Export] [Delete]   │
│  └── Golden Arrow                      [Export] [Delete]   │
│                                                             │
│  Monk Male                                                  │
│  ├── Iron Fist                         [Export] [Delete]   │
│  └── Temple Guardian                   [Export] [Delete]   │
│                                                             │
│  ─────────────────────────────────────────────────────────  │
│  Storage: 280 KB used                                       │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

Themes grouped by job, expanded by default. Storage indicator at bottom.

---

## Rollout Plan

### Phase 1 - Core/Starter Jobs (7 jobs × 2 genders = 14 sprites)
- Squire (Male/Female)
- Knight (Male/Female)
- Archer (Male/Female)
- Monk (Male/Female)
- Thief (Male/Female)
- Chemist (Male/Female)
- White Mage (Male/Female)

### Phase 2 - Magic/Support Jobs (6 jobs × 2 genders = 12 sprites)
- Black Mage (Male/Female)
- Time Mage (Male/Female)
- Summoner (Male/Female)
- Mediator (Male/Female)
- Oracle (Male/Female)
- Geomancer (Male/Female)

### Phase 3 - Advanced/Special Jobs (6 jobs, mixed genders = 12 sprites)
- Dragoon (Male/Female)
- Samurai (Male/Female)
- Ninja (Male/Female)
- Calculator (Male/Female)
- Bard (Male only)
- Dancer (Female only)
- Mime (Male/Female)

**Note:** Jobs without completed mappings will not appear in the template dropdown. Users will see a clear list of available templates.

---

## Storage Limits

### Theme Size Analysis

- One sprite BIN file ≈ 35-40 KB
- One user theme = 1 job = 1 BIN file ≈ 40 KB

| Themes | Approximate Size |
|--------|------------------|
| 10 | ~400 KB |
| 50 | ~2 MB |
| 100 | ~4 MB |
| 250 | ~10 MB |

### Soft Warning Approach

No hard limit on theme creation. Instead, display a friendly warning when storage grows large:

**Warning threshold: 50 themes (~2 MB)**

> "You have 75 custom themes using approximately 3 MB of storage. Consider deleting unused themes to save disk space."

Warning appears:
- In the "My Themes" management tab
- When saving a new theme (if over threshold)

This approach:
- Doesn't frustrate power users who want many themes
- Gently nudges casual users to clean up
- Avoids arbitrary hard limits that feel restrictive

---

## Implementation Tasks

### Engineering (One-time, reusable)

1. **Theme Editor UI**
   - New tab in ConfigurationForm
   - Template dropdown (filtered to mapped jobs only)
   - Color picker components per section
   - Real-time preview panel using WriteableBitmap

2. **Palette Engine**
   - Load/parse BIN sprite files
   - HSV color manipulation for auto-shade generation
   - Apply palette modifications to sprite data
   - Write modified BIN files

3. **Theme Management**
   - UserThemes.json registry
   - Save/load/delete user themes
   - Integration with existing theme dropdown system

4. **Import/Export**
   - Export theme to JSON string (clipboard)
   - Import theme from JSON string
   - Validation and error handling

### Research (Per job class)

1. **Run diagnostic tool** on original sprite
2. **View diagnostic sprite** in preview to identify sections
3. **Create mapping JSON** documenting indices per section

---

## Diagnostic Tool for Section Mapping

### Purpose

Before users can create themes for a job class, we need to discover which palette indices control which visual sections (cape, boots, armor, etc.). The diagnostic tool makes this research fast and accurate.

### How It Works

The tool replaces the sprite's palette with distinct rainbow colors, making each palette index visually identifiable:

```
Original Knight:                 Diagnostic Knight:
┌─────────────────┐              ┌─────────────────┐
│   Blue cape     │              │   YELLOW cape   │  ← "Cape uses indices 3,4,5"
│   Brown boots   │      →       │   CYAN boots    │  ← "Boots use indices 6,7"
│   Gray armor    │              │   PURPLE armor  │  ← "Armor uses indices 8,9,10"
└─────────────────┘              └─────────────────┘
```

### Rainbow Palette Assignment

| Index | Color | Hex | Visual |
|-------|-------|-----|--------|
| 0 | Transparent | - | (unchanged) |
| 1 | Red | #FF0000 | Skin/outline |
| 2 | Orange | #FF8000 | |
| 3 | Yellow | #FFFF00 | |
| 4 | Lime | #80FF00 | |
| 5 | Green | #00FF00 | |
| 6 | Cyan | #00FFFF | |
| 7 | Blue | #0000FF | |
| 8 | Purple | #8000FF | |
| 9 | Magenta | #FF00FF | |
| 10 | Pink | #FF0080 | |
| 11 | White | #FFFFFF | |
| 12 | Light Gray | #C0C0C0 | |
| 13 | Dark Gray | #808080 | |
| 14 | Brown | #804000 | |
| 15 | Black | #000000 | |

### Diagnostic Script (Python)

```python
"""
Diagnostic Sprite Generator for FFT Color Customizer
Replaces palette with rainbow colors for visual section identification.

Usage: python diagnostic_sprite.py input.bin output_diagnostic.bin
"""

import sys

# Rainbow palette - each index gets a distinct, easily identifiable color
DIAGNOSTIC_COLORS = [
    (0, 0, 0),        # 0: Transparent (ignored by game)
    (255, 0, 0),      # 1: Red
    (255, 128, 0),    # 2: Orange
    (255, 255, 0),    # 3: Yellow
    (128, 255, 0),    # 4: Lime
    (0, 255, 0),      # 5: Green
    (0, 255, 255),    # 6: Cyan
    (0, 0, 255),      # 7: Blue
    (128, 0, 255),    # 8: Purple
    (255, 0, 255),    # 9: Magenta
    (255, 0, 128),    # 10: Pink
    (255, 255, 255),  # 11: White
    (192, 192, 192),  # 12: Light Gray
    (128, 128, 128),  # 13: Dark Gray
    (128, 64, 0),     # 14: Brown
    (0, 0, 0),        # 15: Black
]

def rgb_to_bgr555(r, g, b):
    """Convert RGB888 to BGR555 format used by FFT."""
    r5 = (r * 31) // 255
    g5 = (g * 31) // 255
    b5 = (b * 31) // 255
    return r5 | (g5 << 5) | (b5 << 10)

def create_diagnostic_sprite(input_path, output_path):
    """Replace palette 0 with rainbow colors for visual identification."""

    with open(input_path, 'rb') as f:
        data = bytearray(f.read())

    # Write rainbow colors to palette 0 (bytes 0-31, 16 colors × 2 bytes)
    for i, (r, g, b) in enumerate(DIAGNOSTIC_COLORS):
        bgr555 = rgb_to_bgr555(r, g, b)
        offset = i * 2
        data[offset] = bgr555 & 0xFF
        data[offset + 1] = (bgr555 >> 8) & 0xFF

    with open(output_path, 'wb') as f:
        f.write(data)

    print(f"Created diagnostic sprite: {output_path}")
    print("View in preview tool and note which colors appear on each body part.")

if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Usage: python diagnostic_sprite.py <input.bin> <output_diagnostic.bin>")
        sys.exit(1)

    create_diagnostic_sprite(sys.argv[1], sys.argv[2])
```

### Research Workflow

```
┌─────────────────────────────────────────────────────────────────┐
│                SECTION MAPPING RESEARCH WORKFLOW                 │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Step 1: Generate diagnostic sprite                              │
│  ────────────────────────────────────                            │
│  $ python diagnostic_sprite.py battle_knight_m_spr.bin \         │
│                                 battle_knight_m_diagnostic.bin   │
│                                                                  │
│  Step 2: View in preview tool or game                            │
│  ────────────────────────────────────                            │
│  Load diagnostic sprite and observe:                             │
│  - "The cape shows YELLOW, LIME, and GREEN"                      │
│  - "The boots show CYAN and BLUE"                                │
│  - "The armor shows PURPLE, MAGENTA, and PINK"                   │
│                                                                  │
│  Step 3: Record findings in mapping JSON                         │
│  ────────────────────────────────────────                        │
│  Create ColorMod/Data/SectionMappings/Knight_Male.json:          │
│                                                                  │
│  {                                                               │
│    "job": "Knight_Male",                                         │
│    "sprite": "battle_knight_m_spr.bin",                          │
│    "sections": [                                                 │
│      {                                                           │
│        "name": "Cape",                                           │
│        "displayName": "Cape",                                    │
│        "indices": [3, 4, 5],                                     │
│        "roles": ["shadow", "base", "highlight"]                  │
│      },                                                          │
│      {                                                           │
│        "name": "Boots",                                          │
│        "displayName": "Boots",                                   │
│        "indices": [6, 7],                                        │
│        "roles": ["base", "highlight"]                            │
│      },                                                          │
│      {                                                           │
│        "name": "Armor",                                          │
│        "displayName": "Armor",                                   │
│        "indices": [8, 9, 10],                                    │
│        "roles": ["shadow", "base", "highlight"]                  │
│      }                                                           │
│    ]                                                             │
│  }                                                               │
│                                                                  │
│  Step 4: Test mapping in Theme Editor                            │
│  ────────────────────────────────────                            │
│  - Job appears in template dropdown                              │
│  - Verify color changes affect correct body parts                │
│  - Adjust mapping if needed                                      │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Handling Shared Indices

Some palette indices may be used by multiple body parts (e.g., outline color shared across entire sprite). Options:

1. **Group as single section**: "Outline" controls all outline pixels
2. **Document as shared**: Include note in mapping that index affects multiple areas
3. **Exclude from editing**: Some indices (like skin tones) may be intentionally excluded

### Expected Research Time

| Task | Time Estimate |
|------|---------------|
| Generate diagnostic sprite | ~30 seconds |
| Visual inspection & note-taking | ~5-10 minutes |
| Create mapping JSON | ~5 minutes |
| **Total per job** | **~15 minutes** |

For Phase 1 (14 sprites), expect ~3.5 hours of research total.

---

## Data Files

### Section Mappings Location
```
ColorMod/Data/SectionMappings/
  ├── Knight_Male.json
  ├── Knight_Female.json
  ├── Archer_Male.json
  └── ...
```

### User Data Location
```
%RELOADEDIIMODS%/FFTColorCustomizer/
  ├── UserThemes.json
  └── UserThemes/
      └── [JobName]/
          └── [theme_name]/
              └── [sprite].bin
```

---

## Theme System Integration

### How User Themes Appear in Dropdowns

User themes appear in the same dropdown as built-in themes, listed at the bottom after built-in options:

```
┌─────────────────────────────┐
│  Theme: [▼]                 │
├─────────────────────────────┤
│  original                   │  ← Built-in themes first
│  crimson_red                │
│  corpse_brigade             │
│  golden_knight              │
│  ─────────────────────────  │  ← Visual separator
│  Ocean Blue                 │  ← User themes at bottom
│  Dark Crusader              │
│  Forest Knight              │
└─────────────────────────────┘
```

### Sort Order

1. **Built-in themes**: Alphabetical (original always first if present)
2. **Separator line**: Visual divider
3. **User themes**: Alphabetical by name

### Implementation

`ThemeService.cs` discovers user themes by:

1. Scanning `UserThemes/[JobName]/` directory for subdirectories
2. Each subdirectory name = theme name
3. Verifying sprite file exists inside
4. Merging with built-in theme list at runtime

```csharp
// Pseudocode for theme discovery
public List<string> GetThemesForJob(string jobName)
{
    var themes = new List<string>();

    // Add built-in themes first
    themes.AddRange(GetBuiltInThemes(jobName));

    // Add separator marker
    themes.Add("---");

    // Add user themes from UserThemes/[jobName]/ directory
    var userThemesPath = Path.Combine(ModDirectory, "UserThemes", jobName);
    if (Directory.Exists(userThemesPath))
    {
        var userThemes = Directory.GetDirectories(userThemesPath)
            .Select(Path.GetFileName)
            .OrderBy(name => name);
        themes.AddRange(userThemes);
    }

    return themes;
}
```

### Theme Resolution Priority

When loading a theme by name:

1. Check `UserThemes/[job]/[name]/` first (user themes take priority)
2. Fall back to built-in theme directories
3. Fall back to "original" if theme not found

This allows users to "override" built-in theme names if desired, though this is discouraged via naming validation.

---

## Design Decisions

### Theme Naming

- **Allowed characters**: Any valid filename characters (alphanumeric, spaces, underscores, hyphens)
- **Max length**: 50 characters
- **Case sensitivity**: Case-insensitive ("Ocean Blue" and "ocean blue" are the same)
- **Reserved names**: Cannot use names matching built-in themes (crimson_red, corpse_brigade, etc.)
- **Duplicate handling**: Block with message "A theme with this name already exists"

### Theme Immutability

Themes cannot be edited after saving. The save dialog displays:

> "Once you save this theme, it cannot be edited. To modify a theme later, export it, import into a new theme, make changes, and save with a new name."

This keeps the system simple and leverages the import/export functionality for "editing."

**Workflow to modify an existing theme:**
1. Export existing theme (copies JSON to clipboard)
2. Open Theme Editor
3. Import the JSON string
4. Editor populates with exact colors from exported theme
5. Adjust colors as desired
6. Save with new name
7. Delete old theme if no longer needed

### Import/Export Flow

**Export format** includes both palette bytes and section colors:
```json
{
  "version": 1,
  "name": "Ocean Blue",
  "job": "Knight_Male",
  "palette": "base64-encoded-512-bytes",
  "sections": {
    "Cape": "#0047AB",
    "Boots": "#8B4513",
    "Armor": "#C0C0C0"
  }
}
```

- `palette`: Raw bytes for identical sprite reproduction
- `sections`: Original color choices for editor population when re-importing

**Export:** User clicks "Export" on a theme → JSON copied to clipboard → "Theme copied to clipboard!"

**Import:** User clicks "Import from Clipboard" in Theme Editor → system reads clipboard → validates JSON → populates editor with section colors → user can tweak and save as new theme

### Import Conflict Resolution

When importing a theme with a name that already exists:
- Auto-rename to "[Name] (2)", "[Name] (3)", etc.
- Display message: "Theme renamed to '[Name] (2)' because '[Name]' already exists"

### Import Validation

When importing, block with error message if:
- Clipboard doesn't contain valid JSON
- JSON is missing required fields
- Job template is not available (unmapped job): "Knight Male themes cannot be imported because Knight Male templates are not yet available"

### Preview Display

- **Pose**: Standing idle pose
- **Rotation**: User can rotate through sprite angles (reuses existing preview carousel)
- **Animation**: Static for v1 (animated preview is future enhancement)

### Mod Update Behavior

- UserThemes folder persists across mod updates (stored in Reloaded-II mods directory)
- UserThemes.json includes version field for future migration support
- No action required from users when updating the mod

---

## Open Considerations

### Future Enhancements (Out of scope for v1)

- **Universal themes**: Apply one color scheme across all jobs
- **Theme presets**: Pre-built starting points (warm, cool, earth tones)
- **Advanced mode**: Per-index control for power users
- **Story character support**: Extend editor to Agrias, Orlandeau, etc.
- **Animated preview**: Show walking/attack animations in real-time
- **Edit existing themes**: Direct modification without export/import flow

### Edge Cases to Handle

- Invalid import data (corrupted/tampered JSON) - validate and show error
- Sprite file corruption recovery - regenerate from stored color values
- Theme deletion while theme is actively selected - revert to "original"

---

## Success Criteria

1. User can create a custom theme in under 2 minutes
2. Auto-generated palettes look professional (no clashing colors)
3. Shared themes produce identical results between users
4. No technical knowledge required (no hex editing, no external tools)
5. Clear indication of which jobs are available for editing

---

## Implementation TODO

### Phase 0: Research & Tooling
- [x] Create `scripts/diagnostic_sprite.py` script
- [x] Run diagnostic on Squire Male sprite, document findings
- [x] Create `ColorMod/Data/SectionMappings/` directory
- [x] Create first mapping JSON (Squire_Male.json)

### Phase 1: Core Palette Engine
- [x] Create `PaletteModifier.cs` class
  - [x] `LoadTemplate(string binPath)` - load sprite into memory
  - [x] `SetPaletteColor(int index, Color color)` - RGB to BGR555 conversion
  - [x] `GetPreview(int spriteIndex)` - render with modified palette
  - [x] `Reset()` - restore original palette
  - [x] `GetModifiedPalette()` - export palette bytes
  - [x] `ApplySectionColor(section, baseColor)` - apply shades to section indices
  - [x] `SaveToFile(outputPath)` - save modified sprite to file
- [x] Create `HslColor.cs` helper struct
  - [x] RGB ↔ HSL conversion methods
  - [x] Auto-shade generation (shadow, highlight from base)
  - [x] `ColorShades` struct for returning shade variants
- [x] Create `SectionMapping.cs` model classes
  - [x] `SectionMappingLoader` - load JSON mappings from files
  - [x] `SectionMappingLoader.GetAvailableJobs()` - discover available mappings
  - [x] `SectionMappingLoader.LoadFromFile()` - load mapping from file
  - [x] `JobSection` - name, displayName, indices, roles
  - [x] `SectionMapping` - job, sprite, sections

### Phase 2: Theme Editor UI
- [x] Add "Theme Editor" tab to ConfigurationForm
- [x] Create ThemeEditorPanel component
  - [x] Template dropdown (job selector)
  - [x] Filter to only jobs with mapping files
  - [x] Load mapping on selection change
  - [x] Pass mappings directory from ConfigurationForm
  - [x] Layout controls with proper positioning
  - [x] Set minimum height (400px)
- [x] Create sprite preview panel (PictureBox)
  - [ ] Integrate with PaletteModifier
  - [x] Add rotation arrows (◄ ►) for 8 directions
  - [x] Set proper size (96x120 for 3x scale)
  - [ ] Wire up real-time preview updates
- [x] Create HSL color picker component (HslColorPicker.cs)
  - [x] Hue slider (0-360)
  - [x] Saturation slider (0-100)
  - [x] Lightness slider (0-100)
  - [x] H/S/L properties (get/set)
  - [x] CurrentColor property (returns RGB)
  - [x] SetColor(Color) method (RGB to HSL)
  - [x] ColorChanged event
  - [x] SectionName property
  - [ ] Color preview swatch
  - [ ] Hex code display/input
  - [ ] Copy/Paste buttons
- [x] Create section color pickers panel (container)
- [x] Generate section color pickers dynamically from mapping
- [x] Add scrolling for section color pickers panel
- [x] Add theme name input field
- [x] Add Save/Reset/Cancel buttons
- [x] Add ThemeEditorPanel to ConfigurationForm (visible when expanded)

### Phase 3: Theme Persistence
- [ ] Create `UserThemeService.cs`
  - [ ] `SaveTheme(jobName, themeName, paletteBytes, sectionColors)`
  - [ ] `LoadTheme(jobName, themeName)`
  - [ ] `DeleteTheme(jobName, themeName)`
  - [ ] `GetUserThemes(jobName)` - list user themes for a job
- [ ] Create `UserThemes.json` registry format
- [ ] Create user theme directory structure on first save
- [ ] Implement theme name validation
  - [ ] Check for duplicates
  - [ ] Check for reserved names (built-in themes)
  - [ ] Validate allowed characters
- [ ] Wire Save button to persistence
- [ ] Show success/error messages

### Phase 4: Theme System Integration
- [ ] Modify `ThemeService.cs` to discover UserThemes directory
- [ ] Add user themes to dropdown lists (after separator)
- [ ] Implement theme resolution priority (user → built-in → original)
- [ ] Test theme selection applies user themes correctly
- [ ] Handle deleted themes gracefully (fall back to original)

### Phase 5: Import/Export
- [ ] Create export JSON format (palette bytes + section colors)
- [ ] Implement "Export" button in My Themes tab
  - [ ] Generate JSON string
  - [ ] Copy to clipboard
  - [ ] Show confirmation message
- [ ] Implement "Import from Clipboard" button in Theme Editor
  - [ ] Parse JSON from clipboard
  - [ ] Validate format and job compatibility
  - [ ] Populate editor with imported colors
  - [ ] Handle name conflicts (auto-rename)
- [ ] Error handling for invalid import data

### Phase 6: My Themes Management Tab
- [ ] Create "My Themes" tab in ConfigurationForm
- [ ] Display themes grouped by job
- [ ] Add Export button per theme
- [ ] Add Delete button per theme
  - [ ] Confirmation dialog
  - [ ] Handle deletion of currently-selected theme
- [ ] Show storage usage indicator
- [ ] Show soft warning when over 50 themes

### Phase 7: Additional Job Mappings (Phase 1 Jobs)
- [ ] Knight Male/Female mappings
- [ ] Archer Male/Female mappings
- [ ] Monk Male/Female mappings
- [ ] Thief Male/Female mappings
- [ ] Chemist Male/Female mappings
- [ ] White Mage Male/Female mappings

### Phase 8: Polish & Testing
- [ ] End-to-end test: create theme → save → select → verify in-game
- [ ] Test import/export round-trip between users
- [ ] Test theme persistence across mod updates
- [ ] Test edge cases (corrupt files, missing directories)
- [ ] Performance testing (preview update latency)
- [ ] UI polish and visual consistency

### Future Phases (Out of Scope for v1)
- [ ] Phase 2 job mappings (Black Mage, Time Mage, Summoner, etc.)
- [ ] Phase 3 job mappings (Dragoon, Samurai, Ninja, etc.)
- [ ] Universal themes (apply to all jobs)
- [ ] Theme presets (warm, cool, earth tones)
- [ ] Advanced mode (per-index control)
- [ ] Story character support
- [ ] Animated preview

---

## Version History

- **v1.1** (2024-12-29): Added detailed technical specifications
  - Terraria-style HSL color picker component
  - Sprite preview rendering pipeline (leveraging BinSpriteExtractor)
  - PaletteModifier component for real-time preview
  - Diagnostic tool for section mapping research
  - Theme system integration (user themes in dropdowns)
- **v1.0** (2024-12-29): Initial design document
