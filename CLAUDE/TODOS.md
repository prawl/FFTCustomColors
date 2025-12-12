# FFT Color Mod - Comprehensive Task Breakdown

## Project Overview
Implement job/gender-based color customization for FFT sprites with special handling for Ramza's DLC-locked sprite.

**Current State**: F1 hotkey changes ALL units globally → **Target State**: Context-sensitive colors per job/gender combination

---

## Phase 1: Foundation Research (CRITICAL - Blocks All Other Work)

### 🚨 PRIORITY 1: API Call Monitoring & Analysis
**Status**: Not Started
**Estimated Time**: 4-6 hours
**Confidence**: 90% (Standard reverse engineering)

#### Task 1.1: Set Up Monitoring Environment
- [ ] Download and configure API Monitor (http://www.rohitab.com/apimonitor)
- [ ] Install Process Monitor from Microsoft Sysinternals
- [ ] Set up x64dbg for deeper analysis if needed
- [ ] Create isolated test environment with FFT clean install

#### Task 1.2: Monitor FFT Sprite Loading
- [ ] Launch API Monitor with FFT.exe as target
- [ ] Filter for file I/O operations (CreateFile, ReadFile, FindFirstFile)
- [ ] Monitor during character selection screen
- [ ] Monitor during battle loading with different characters
- [ ] **CRITICAL**: Monitor specifically when Ramza appears vs other characters

**Expected Outputs**:
- List of all files accessed during sprite loading
- Ramza-specific file access patterns
- DLC validation API calls (if any)
- Error handling when files are modified

#### Task 1.3: Memory Layout Discovery
- [ ] Use CheatEngine to scan for known palette values
- [ ] Find sprite data structures in memory
- [ ] Identify memory addresses for currently selected unit
- [ ] Document memory layout of job/gender data

**Technical Details**:
```csharp
// Search patterns to test:
byte[] ramzaNamePattern = Encoding.ASCII.GetBytes("Ramza");
byte[] paletteHeaderPattern = { 0x00, 0x00, 0xFF, 0x7F }; // Transparent + White
byte[] jobClassPattern = { 0x01, 0x00, 0x00, 0x00 }; // Squire job ID
```

---

## Phase 2: Quick Diagnostic Test (Confidence Assessment)

### 🧪 PRIORITY 2: Ramza Modification Test Suite
**Status**: Not Started
**Estimated Time**: 2-3 hours
**Dependencies**: Phase 1 API monitoring results

#### Task 2.1: File Modification Response Test
- [ ] Backup original Ramza sprite file
- [ ] Make obvious visual change (solid red palette)
- [ ] Test game loading behavior:
  - [ ] Does game crash immediately?
  - [ ] Does it revert the file automatically?
  - [ ] Does it show error message?
  - [ ] Does it load but ignore changes?

#### Task 2.2: Memory Scanning Feasibility Test
- [ ] Use patterns from Phase 1 to search for Ramza palette in memory
- [ ] Test writing to found memory addresses
- [ ] Check if changes persist or get overwritten
- [ ] Document memory protection levels

#### Task 2.3: DLC Function Detection Test
- [ ] Search for common DLC validation patterns in FFT.exe:
  ```assembly
  ; Common patterns:
  ; Steam API calls: BIsDlcInstalled, GetDLCCount
  ; File existence checks: FindFirstFile + "dlc", "ramza"
  ; Registry checks: RegQueryValue + "DLC"
  ```
- [ ] Hook candidate functions using simple DLL injection
- [ ] Test if hooking prevents file modification issues

**Success Criteria**: Generate confidence report for each approach (A, B, C)

---

## Phase 3A: DLC Bypass Implementation (Option A - 70% Confidence)

### 📋 PRIORITY 3A: Hook-Based DLC Bypass
**Status**: Not Started
**Dependencies**: Phase 2 DLC function detection

#### Task 3A.1: DLC Function Analysis
- [ ] Disassemble identified DLC check functions
- [ ] Determine calling convention (stdcall, fastcall, etc.)
- [ ] Identify function parameters and return values
- [ ] Find all call sites to ensure complete coverage

#### Task 3A.2: Implement Function Hooking
```csharp
// Implementation framework:
public class DLCBypass
{
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll")]
    private static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

    public void InstallHook(IntPtr targetFunction)
    {
        // Create trampoline to original function
        // Patch original function to jump to our fake implementation
        // Ensure thread safety during patching
    }

    private bool FakeDLCCheck(IntPtr dlcIdentifier)
    {
        // Always return "DLC owned" for Ramza-related checks
        return true;
    }
}
```

#### Task 3A.3: Testing & Validation
- [ ] Test hook installation without crashes
- [ ] Verify normal DLC functionality still works
- [ ] Test Ramza file modification with hook active
- [ ] Validate that other characters remain unaffected

**Risk Mitigation**:
- Implement hook removal for clean uninstall
- Add extensive error handling and logging
- Test on multiple FFT versions if available

---

## Phase 3B: Memory Palette Implementation (Option B - 50% Confidence)

### 🧠 PRIORITY 3B: Direct Memory Palette Modification
**Status**: Not Started
**Dependencies**: Phase 2 memory scanning results

#### Task 3B.1: Palette Data Structure Analysis
- [ ] Reverse engineer FFT's palette format:
  - [ ] Color depth (15-bit? 16-bit? 24-bit?)
  - [ ] Color ordering (RGB? BGR?)
  - [ ] Palette size (16 colors? 256 colors?)
  - [ ] Memory alignment requirements

#### Task 3B.2: Palette Detection & Modification
```csharp
public class MemoryPaletteManager
{
    public struct PaletteEntry
    {
        public byte Red, Green, Blue, Alpha;

        public static PaletteEntry FromRGB555(ushort rgb555)
        {
            return new PaletteEntry
            {
                Red = (byte)((rgb555 & 0x1F) << 3),
                Green = (byte)(((rgb555 >> 5) & 0x1F) << 3),
                Blue = (byte)(((rgb555 >> 10) & 0x1F) << 3),
                Alpha = 255
            };
        }
    }

    public bool FindAndModifyRamzaPalette(PaletteEntry[] newColors)
    {
        // Scan memory for Ramza palette signature
        // Verify found address contains valid palette data
        // Write new palette colors directly to memory
        // Handle write protection and memory access rights
    }
}
```

#### Task 3B.3: Refresh & Persistence Handling
- [ ] Test if memory changes persist across scenes
- [ ] Implement periodic re-application if game overwrites
- [ ] Find sprite cache invalidation function if available

---

## Phase 3C: File Aliasing Implementation (Option C - 60% Confidence)

### 📁 PRIORITY 3C: File System Redirection
**Status**: Not Started
**Dependencies**: Phase 1 file access patterns

#### Task 3C.1: File API Hooking Setup
```csharp
public class FileAliasing
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFileW(
        string filename, uint access, uint share, IntPtr securityAttributes,
        uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    public IntPtr CreateFileHook(string filename, /* other parameters */)
    {
        // Check if this is a Ramza sprite request
        if (IsRamzaSpriteFile(filename))
        {
            // Redirect to modified generic sprite
            filename = RedirectToGenericSprite(filename);
            LogRedirection(filename);
        }

        return CallOriginalCreateFile(filename, /* other parameters */);
    }

    private bool IsRamzaSpriteFile(string path)
    {
        return path.Contains("ch01") ||
               path.Contains("ramza") ||
               path.Contains("chapter1");
    }
}
```

#### Task 3C.2: File Mapping Strategy
- [ ] Create generic sprite variants that match Ramza's structure
- [ ] Implement file name translation logic
- [ ] Handle both absolute and relative path requests
- [ ] Test with different color schemes

#### Task 3C.3: Integration Testing
- [ ] Verify redirection works during character selection
- [ ] Test during battle loading and sprite refresh
- [ ] Ensure no impact on save game functionality

---

## Phase 4: Job/Gender Color System Implementation

### 🎯 PRIORITY 4: Core Color Management System
**Status**: Not Started
**Dependencies**: At least one Phase 3 option working

#### Task 4.1: Unit Detection & Context System
```csharp
public class UnitColorManager
{
    public struct UnitContext
    {
        public string JobClass;     // "knight", "archer", etc.
        public string Gender;       // "male", "female"
        public bool IsStoryChar;    // true for Ramza, Agrias, etc.
        public string CharacterName; // "Ramza", "Agrias", or null
    }

    public UnitContext GetCurrentlySelectedUnit()
    {
        // Read memory to find selected unit's job/gender
        // Use addresses discovered in Phase 1
        // Return structured context for color decisions
    }
}
```

#### Task 4.2: Color Scheme Management
- [ ] Implement job/gender → color mapping system
- [ ] Create color persistence (registry/config file)
- [ ] Add support for story character overrides
- [ ] Implement F1 cycling logic with context awareness

#### Task 4.3: File-Based Sprite Swapping
- [ ] Organize color variant files by job/gender
- [ ] Implement atomic file swapping with rollback
- [ ] Add sprite refresh triggering (or battle prep limitation)
- [ ] Create user feedback system (notifications/overlay)

#### Task 4.4: Configuration & Persistence
```csharp
public class ColorConfiguration
{
    public Dictionary<string, string> JobColorMappings { get; set; }
    public Dictionary<string, string> StoryCharMappings { get; set; }

    public void SaveToFile(string configPath) { /* JSON serialization */ }
    public static ColorConfiguration LoadFromFile(string configPath) { /* JSON deserialization */ }
}
```

---

## Phase 5: Testing & Polish

### 🔧 PRIORITY 5: Integration & User Experience
**Status**: Not Started

#### Task 5.1: Comprehensive Testing Suite
- [ ] Test all job/gender combinations
- [ ] Validate color persistence across game sessions
- [ ] Test battle transitions and sprite refresh scenarios
- [ ] Verify compatibility with other FFT mods

#### Task 5.2: Error Handling & Recovery
- [ ] Implement rollback for failed color swaps
- [ ] Add comprehensive logging for troubleshooting
- [ ] Create diagnostic mode for advanced users
- [ ] Handle edge cases (corrupted sprites, missing files)

#### Task 5.3: User Interface Enhancements
- [ ] Add visual feedback for color changes
- [ ] Implement hotkey customization
- [ ] Create preset color scheme system
- [ ] Add export/import functionality for color configurations

---

## Implementation Priority Matrix

### Must Have (Release Blockers)
1. **API Monitoring Results** (Phase 1) - Blocks all technical decisions
2. **Working Ramza Solution** (Any Phase 3 option) - Core feature requirement
3. **Basic Job/Gender Detection** (Phase 4.1) - Context awareness needed
4. **File Swapping System** (Phase 4.3) - Core functionality

### Should Have (Quality Features)
1. **Multiple Ramza Solutions** (Backup options from Phase 3)
2. **Configuration Persistence** (Phase 4.4) - User convenience
3. **Sprite Refresh Solution** (Phase 4.3) - Better UX than battle prep limitation

### Could Have (Polish Features)
1. **Story Character Support** (Individual Agrias, Delita colors)
2. **Preset Color Schemes** (Phase 5.3) - User convenience
3. **Advanced UI Features** (Phase 5.3) - Nice to have

---

## Risk Assessment & Mitigation

### High Risk Items
1. **Ramza DLC Protection**: May be unbreakable → **Mitigation**: Implement exclusion mode
2. **Game Updates Breaking Hooks**: New versions could change addresses → **Mitigation**: Pattern-based scanning
3. **Anti-Cheat False Positives**: Memory modification might trigger warnings → **Mitigation**: File-based approach where possible

### Medium Risk Items
1. **Memory Layout Changes**: Different FFT versions → **Mitigation**: Multiple signature support
2. **Performance Impact**: Real-time memory scanning → **Mitigation**: Cache addresses, lazy scanning

### Low Risk Items
1. **Configuration Corruption**: Bad JSON data → **Mitigation**: Robust parsing with defaults
2. **File System Permissions**: Write access issues → **Mitigation**: Admin privilege detection

---

## Success Metrics

### Technical Milestones
- [ ] Ramza colors successfully modified (any method)
- [ ] Job/gender context detection working
- [ ] Color changes apply correctly to targeted groups
- [ ] No crashes or corruption during normal use

### User Experience Goals
- [ ] F1 key provides clear feedback about what changed
- [ ] Color schemes persist across game sessions
- [ ] Easy rollback to default colors
- [ ] Intuitive context-sensitive behavior

### Performance Targets
- [ ] < 100ms delay for color application
- [ ] < 5MB memory footprint for color management
- [ ] No noticeable impact on game loading times
- [ ] Stable operation across extended play sessions

---

## Development Timeline Estimate

**Phase 1**: 1-2 weeks (Research foundation)
**Phase 2**: 3-5 days (Quick diagnostics)
**Phase 3**: 1-3 weeks (Ramza solution - varies by option complexity)
**Phase 4**: 2-3 weeks (Core color system)
**Phase 5**: 1-2 weeks (Testing & polish)

**Total Estimate**: 6-10 weeks for complete implementation

**Minimum Viable Product**: Phases 1-4.3 (4-6 weeks)

---

## Legacy Features (Future Enhancements)

### Armor vs Hair Separation
- [ ] **Selective Palette Modification** - Skip indices 10-19 to preserve hair colors
- [ ] **Hair-Safe Theme Script** - Test cohesive theme script with hair preservation
- [ ] **Update Existing Themes** - Regenerate all themes to preserve original hair colors
- [ ] **Hair Color Customization** - Separate control for hair color (advanced feature)

### Community & Sharing
- [ ] **Team Uniform System** - One-click matching colors for entire party
- [ ] **Color Import/Export** - Share schemes via codes/files, community library
- [ ] **Screenshot Mode** - Temporary cinematic filters without permanent changes

### Dynamic & Reactive Colors
- [ ] **Color Evolution System** - Colors shift based on combat actions (fire=red, healing=blue)
- [ ] **Dynamic Battle Colors** - Status effects change colors (berserk=red, haste=blue)
- [ ] **Weather-Reactive Colors** - Rain darkens/adds shine, sun brightens, night desaturates
- [ ] **Battlefield Camouflage** - Auto-adjust to terrain (desert=sandy, snow=white)
- [ ] **Mirror Match Detection** - Auto-contrast colors vs same enemy job classes

### Release Preparation
- [ ] Create/update Reloaded-II mod image (Preview.png - 256x256 or 512x512)
- [ ] Update ModConfig.json with proper metadata:
  - [ ] Update ModId to proper format (e.g., "ptyra.fft.colormod")
  - [ ] Set Version to release version (e.g., "1.0.0")
  - [ ] Update Author name (from "YourName" to actual username)
  - [ ] Write compelling ModDescription
  - [ ] Update ModName if needed
  - [ ] Add Tags for discoverability
  - [ ] Set ProjectUrl to GitHub repository
- [ ] Create before/after screenshots
- [ ] Write mod description (100-200 words)
- [ ] Create installation guide
- [ ] Package for Reloaded-II Database
- [ ] Register on FFHacktics forum
- [ ] Test clean installation on fresh Reloaded-II setup
