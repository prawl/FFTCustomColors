# FFT Color Mod - Individual Unit Color Swapping Research

## Current State
- F1 hotkey cycles through 21 color schemes but changes ALL units globally
- File-based sprite swapping approach is working
- Need to implement per-unit or per-job/gender color customization

## Discovery: Better Palettes Approach
Better Palettes implements job/gender-based customization via Reloaded-II config menu:
- ALL male squires can be one color
- ALL female squires can be another color
- Each job/gender combination gets its own setting
- This is more practical than true per-individual-unit colors

## Proposed Solution: Context-Sensitive F1 with Job/Gender Mapping

### Core Concept
Instead of changing ALL sprites globally, change colors based on job/gender of targeted unit:
- Target a knight and press F1 → ALL male knights change color
- Target a female archer and press F1 → ALL female archers change color
- Maintains file-based swapping but with smarter targeting

### Implementation Phases

#### Phase 1: Get Unit Job/Gender Detection Working
- Find memory addresses for currently selected/targeted unit
- Identify unit's job class ID (knight, archer, squire, etc.)
- Identify unit's gender flag (male/female)
- Create mapping key like "knight_male" or "archer_female"

#### Phase 2: Implement Job-Based Color Mapping
```csharp
public class JobColorManager
{
    // Map of JobClass_Gender → ColorScheme
    Dictionary<string, string> JobColorMappings = new()
    {
        ["squire_male"] = "original",
        ["squire_female"] = "corpse_brigade",
        ["knight_male"] = "lucavi",
        ["knight_female"] = "northern_sky",
        // etc...
    };

    public void ProcessF1Press()
    {
        var targetUnit = GetTargetedUnit();
        var jobType = GetUnitJobType(targetUnit);  // "knight"
        var gender = GetUnitGender(targetUnit);    // "male"
        var key = $"{jobType}_{gender}";

        // Cycle to next color for this job/gender combo
        var nextColor = GetNextColorScheme(JobColorMappings[key]);
        JobColorMappings[key] = nextColor;

        // Swap sprite files for this job/gender
        SwapSpritesForJobGender(jobType, gender, nextColor);

        ShowNotification($"All {gender} {jobType}s → {nextColor}");
    }
}
```

#### Phase 3: Handle Sprite Refresh
**Option A: Battle Prep Only**
- Only allow color changes during formation/prep screen
- Colors lock when battle starts
- Avoids refresh problem entirely

**Option B: Force Refresh**
- Simulate mouse hover events to trigger sprite reload
- Or find and call sprite cache invalidation function
- Or trigger menu state change to force redraw

### Technical Implementation Details

#### File Structure for Job-Based Colors
```
FFTIVC/data/enhanced/fftpack/unit/
├── battle_knight_m_spr.bin      # Active male knight sprite
├── battle_knight_w_spr.bin      # Active female knight sprite
├── sprites_corpse_brigade/       # Color variant source
│   ├── battle_knight_m_spr.bin
│   └── battle_knight_w_spr.bin
└── sprites_lucavi/              # Another color variant
    ├── battle_knight_m_spr.bin
    └── battle_knight_w_spr.bin
```

#### Sprite Swapping Logic
```csharp
private void SwapSpritesForJobGender(string job, string gender, string colorScheme)
{
    // Generate file name based on job and gender
    var genderChar = gender == "male" ? "m" : "w";
    var spriteFile = $"battle_{job}_{genderChar}_spr.bin";

    // Copy from color variant folder to active folder
    var source = $"sprites_{colorScheme}/{spriteFile}";
    var dest = $"fftpack/unit/{spriteFile}";
    File.Copy(source, dest, true);

    // Trigger refresh (implementation TBD)
    ForceGameSpriteReload();
}
```

### Alternative Approaches Considered

#### 1. Hook-Based Unit Differentiation (Complex)
- Hook sprite loading function to detect which unit is requesting
- Maintain map of UnitID → ColorScheme
- Redirect to different files per individual unit
- **Pros**: True per-unit colors
- **Cons**: Need complex unit tracking, file proliferation

#### 2. Memory-Based Palette Swapping (Failed Previously)
- Modify palette in memory after sprite loads
- Game continuously reloads from cache, overwrites changes
- Would need to hook rendering pipeline

#### 3. In-Game Menu Addition (Complex)
- Add "Color Palette" option to unit formation menu
- Requires extensive UI hooking like FFTGenericJobs
- More user-friendly but significantly more complex

#### 4. Save File Manipulation (Risky)
- Store color preferences in save data
- Risk of save corruption
- May not support customization data

### Benefits of Job/Gender-Based Approach

1. **Simpler than per-unit** - No individual unit tracking needed
2. **Cohesive teams** - All knights match, all archers match, etc.
3. **Predictable** - Players know what they're changing
4. **Memory efficient** - One sprite file per job/gender combo
5. **Natural persistence** - File state persists across sessions

### Enhanced Features (Future)

- **Story characters** get individual entries (Ramza, Agrias, etc.)
- **Enemy colors** - Different color rules for enemy units
- **Chapter progression** - Colors evolve as story progresses
- **Team uniforms** - Quick presets for matching color schemes

### Key Challenges to Solve

1. **Unit Detection**: Finding memory addresses for selected unit's job/gender
2. **Sprite Refresh**: Triggering game to reload sprites after swap
3. **Persistence**: Saving job/gender → color mappings between sessions
4. **UI Feedback**: Showing player which job/gender they're modifying

### Next Steps

1. Use x64dbg to find unit selection and job/gender memory addresses
2. Implement basic job/gender detection
3. Test context-sensitive F1 with job-based swapping
4. Solve sprite refresh issue (or limit to battle prep)