# FFT Color Mod Research

## Programmatic Memory Scanning & Game Modding Techniques

### Overview
Programmatic memory scanning is a fundamental technique in game modding that allows developers to find and modify game data without manual debugging tools. This approach uses Windows APIs and pattern recognition to automate the process of finding and patching memory addresses.

### Core Windows API Functions

#### Essential Functions for Memory Operations
- **OpenProcess()**: Get handle to target process with specific access rights
- **ReadProcessMemory()**: Read bytes from process memory
- **WriteProcessMemory()**: Write bytes to process memory
- **VirtualQueryEx()**: Query memory page information
- **VirtualProtect()**: Change memory protection flags

#### Access Rights Required
```csharp
PROCESS_VM_READ = 0x0010     // For reading memory
PROCESS_VM_WRITE = 0x0020    // For writing memory
PROCESS_VM_OPERATION = 0x0008 // For memory operations
PROCESS_ALL_ACCESS = 0x001F0FFF // Full access
```

### Memory Pattern Scanning (Signature Scanning)

#### What is Pattern Scanning?
Pattern scanning (aka signature scanning or AoB - Array of Bytes) is a technique to find specific byte sequences in memory. This is crucial because:
- Memory addresses change due to ASLR (Address Space Layout Randomization)
- Different game versions have different memory layouts
- Dynamic memory allocation makes static addresses unreliable

#### Key Principles
1. **Scan for code, not data**: Search for assembly instructions that access data, not the data itself
2. **Use wildcards**: Mark bytes that may change with "??" or "00"
3. **Find unique patterns**: Ensure your signature is unique enough to avoid false positives

#### Implementation Strategy
```
1. Read memory page by page using VirtualQueryEx()
2. Search for byte pattern in each readable page
3. Use wildcards for dynamic bytes
4. Verify found addresses contain expected data structure
5. Apply patches to verified addresses
```

### Advanced Memory Scanning Libraries

#### Squalr (High Performance C# Scanner)
- **Up to 5x faster than Cheat Engine** using SIMD instructions
- Supports SSE/AVX/AVX-512 for high-speed scanning
- Available as NuGet package
- Multi-threaded scanning with parallel processing
- Full API for integration into C# projects

#### Memory.dll (Popular C# Library)
- Available on NuGet
- Features:
  - AoB scanning with masking
  - DLL injection capabilities
  - Named pipe communication
  - Multi-level pointer support
  - Module-relative addressing

#### ProcessMemoryUtilities.Net
- High-performance wrapper for kernel32/ntdll functions
- Generic type support for ReadProcessMemory/WriteProcessMemory
- Safe native calls with proper pinning
- Supports both 32-bit and 64-bit processes

### DLC Bypass & Protection Circumvention

#### Common DLC Protection Methods
1. **File-based checks**: Game checks for DLC files on disk
2. **API validation**: Steam/platform API confirms ownership
3. **Memory flags**: In-memory ownership flags
4. **Server validation**: Online verification of DLC status

#### Bypass Techniques

##### 1. API Hooking
- **Hook Steam API calls** that check DLC ownership
- Intercept functions like `BIsDlcInstalled()`
- Return positive results regardless of actual ownership
- Tools: SmokeAPI, CreamAPI use this approach

##### 2. Memory Patching
- **Pattern scan for DLC check code**
- Patch conditional jumps (JE → JMP)
- Modify ownership flags in memory
- Change comparison results

##### 3. DLL Injection/Proxy
- Replace steam_api.dll with modified version
- Forward legitimate calls to original DLL
- Intercept and modify DLC-related calls
- Maintain multiplayer/achievement functionality

##### 4. IAT (Import Address Table) Hooking
- Hook Windows API functions
- Intercept CreateFile/ReadFile for DLC files
- Return success for missing DLC content
- Bypass file existence checks

### Ramza Sprite Palette - Specific Approaches

#### The Challenge
- Ramza's sprite appears to be DLC-locked in FFT
- File modifications trigger DLC validation
- Need to bypass protection without breaking game

#### Potential Solutions

##### 1. Runtime Memory Patching
```csharp
// Find Ramza palette in memory using patterns
// Common patterns to search:
byte[] ramzaIdentifier = Encoding.ASCII.GetBytes("CH01_RAM");
byte[] paletteHeader = { 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF }; // Black to white
byte[] spriteDimensions = { 0x10, 0x00, 0x00, 0x00, 0x10, 0x00 }; // 16x16

// Apply palette directly to memory, bypassing file checks
WriteProcessMemory(handle, paletteAddress, newPalette, 288, out written);
```

##### 2. Hook DLC Validation
- Find DLC check function signature in memory
- Hook the function to always return "owned"
- Allow normal file modifications to work

##### 3. Sprite Aliasing
- Redirect Ramza sprite requests to modified generic sprite
- Use file system filter driver or API hooking
- Game loads modified sprite thinking it's generic

##### 4. Process Injection
- Inject code into game process
- Monitor sprite loading
- Replace palette data after DLC check passes

### Memory Scanning Best Practices

#### Performance Optimization
1. **Use multi-threading** for parallel page scanning
2. **Implement SIMD instructions** (SSE/AVX) for pattern matching
3. **Cache memory page information** to avoid redundant queries
4. **Scan only relevant memory regions** (skip system/protected areas)

#### Reliability
1. **Use multiple signatures** as fallbacks
2. **Verify data structure** after finding address
3. **Handle process architecture** (32-bit vs 64-bit)
4. **Account for different game versions** with version-specific patterns

#### Safety
1. **Always backup original values** before writing
2. **Use proper error handling** for API calls
3. **Respect memory protection** flags
4. **Implement undo functionality** for modifications

### Tools & Resources

#### Development Libraries
- **Squalr**: High-performance C# memory scanner
- **Memory.dll**: Comprehensive game hacking library
- **ProcessMemoryUtilities.Net**: Low-level memory operations
- **CheatEngine Library**: Port of CheatEngine features to C#

#### Analysis Tools
- **x64dbg**: Manual debugging and pattern finding
- **CheatEngine**: Memory scanning and testing
- **API Monitor**: Track API calls made by game
- **Process Monitor**: File/registry access monitoring

#### Pattern Creation
- **AOB Signature Maker**: Automated signature generation
- **Pattern generators**: Create wildcarded patterns
- **Hex editors**: Manual pattern extraction

### Implementation Considerations

#### Legal & Ethical
- Memory modification may violate game EULA
- DLC bypassing could be considered piracy
- Online use may result in bans
- Educational/personal use recommended

#### Technical Challenges
- Anti-cheat systems (VAC, EAC, BattlEye)
- Code obfuscation and packing
- Dynamic memory allocation
- Version differences between releases

#### For FFT Color Mod
- Focus on legitimate owned game modifications
- Prioritize non-invasive techniques
- Document methods for educational purposes
- Consider platform-specific limitations (Steam vs standalone)

---

## FFT Color Mod - Individual Unit Color Swapping Research

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

---

## Ramza Sprite Memory Patching - Targeted Solution

### The Problem
File-based sprite swapping works for **ALL sprites EXCEPT Ramza**:
- Generic job sprites (Knight, Archer, etc.) → File swapping works perfectly ✅
- Story characters (Agrias, Delita, etc.) → File swapping works ✅
- **Ramza sprite modifications → Triggers DLC validation failure** ❌

**Root Cause**: Ramza appears to be DLC-locked, causing file modification attempts to trigger protection mechanisms that revert changes or block loading.

### Solution Options & Confidence Levels

#### Option A: Bypass DLC Check (70% Confidence)
**Approach**: Hook or patch the DLC validation function to always return "owned" for Ramza content.

**Technical Implementation**:
```csharp
// Pattern scan for DLC check function
byte[] dlcCheckPattern = { 0x48, 0x8B, 0xC4, 0x48, 0x89, 0x??, 0x10, 0xFF, 0x??, 0x??, 0x84, 0xC0 };

// Hook function to always return true
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
private delegate bool DLCCheckDelegate(IntPtr dlcId);

private bool FakeDLCCheck(IntPtr dlcId) => true; // Always return owned
```

**Critical Research Questions**:
- Where is the DLC check function located in FFT.exe?
- What calling convention and parameters does it use?
- Is it called once at startup or repeatedly during gameplay?

#### Option B: Direct Memory Palette Swap (50% Confidence)
**Approach**: Bypass file system entirely - find Ramza's palette in memory and modify it directly.

**Technical Implementation**:
```csharp
// Search for Ramza palette signature in memory
byte[] ramzaPalettePattern = {
    0x00, 0x00,        // Color 0: Transparent
    0xFF, 0x7F,        // Color 1: White (15-bit RGB)
    0x??, 0x??,        // Color 2: Primary armor (varies)
    0x??, 0x??         // Color 3: Secondary armor (varies)
};

// Direct memory write to palette data
WriteProcessMemory(processHandle, ramzaPaletteAddress, newPalette, 288, out bytesWritten);
```

**Critical Research Questions**:
- What is the exact memory layout of FFT's sprite palette data?
- Does the game use 15-bit or 24-bit color format?
- How frequently does the game reload palette data from cache?

#### Option C: Sprite Aliasing (60% Confidence)
**Approach**: Redirect Ramza sprite loading requests to point to a modified generic sprite file.

**Technical Implementation**:
```csharp
// Hook CreateFile/ReadFile to redirect Ramza requests
[DllImport("kernel32.dll", SetLastError = true)]
private static extern IntPtr CreateFileW(
    [MarshalAs(UnmanagedType.LPWStr)] string filename,
    uint access, uint share, IntPtr securityAttributes,
    uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

private IntPtr CreateFileHook(string filename, /* other params */)
{
    if (filename.Contains("ramza") || filename.Contains("ch01"))
    {
        // Redirect to modified generic sprite
        filename = filename.Replace("ch01_ram", "generic_knight_modified");
    }
    return originalCreateFile(filename, /* other params */);
}
```

**Critical Research Questions**:
- What file naming convention does FFT use for character sprites?
- Are sprite requests made through standard Windows file APIs or custom functions?
- Can we intercept at the file system level or need lower-level hooking?

### Research Priority Matrix

#### Highest Value Research: API Call Monitoring
**Tool**: API Monitor or Process Monitor
**Target**: Monitor FFT.exe during Ramza sprite loading
**Goal**: Identify exact APIs called and parameters used

**Expected Discoveries**:
1. **File Access Patterns**: Which files are accessed when Ramza appears
2. **DLC Validation Flow**: When and how DLC checks occur
3. **Memory Allocation**: Where sprite data is loaded in memory
4. **Error Handling**: How the game responds to missing/modified files

#### Quick Diagnostic Test Approach
```csharp
public class RamzaDiagnostic
{
    public void RunQuickTest()
    {
        // Test 1: File modification detection
        ModifyRamzaFile();
        var result1 = TestGameLoading(); // Does it crash? Revert? Ignore?

        // Test 2: Memory scan for Ramza palette
        var ramzaMemoryAddress = ScanForRamzaPalette();
        var result2 = ramzaMemoryAddress != IntPtr.Zero;

        // Test 3: DLC function identification
        var dlcFunctions = ScanForDLCValidationCalls();
        var result3 = dlcFunctions.Count > 0;

        GenerateConfidenceReport(result1, result2, result3);
    }
}
```

### Implementation Decision Tree

```
START: Ramza sprite modification needed
│
├─ API Monitoring Results Available?
│  ├─ YES → Choose highest confidence option based on findings
│  └─ NO → Run API monitoring first (blocks all other work)
│
├─ Option A: DLC Bypass
│  ├─ DLC function found? → Implement hook → Test
│  └─ DLC function not found? → Try Option B or C
│
├─ Option B: Memory Palette
│  ├─ Palette pattern found? → Implement direct write → Test
│  └─ Palette not found? → Try Option A or C
│
└─ Option C: File Aliasing
   ├─ File APIs hooked successfully? → Implement redirect → Test
   └─ Hooking failed? → Return to Option A or B
```

### Success Criteria & Validation

**Technical Success**:
- Ramza appears with modified colors in-game ✅
- No crashes, error messages, or file corruption ✅
- Modification persists across game restarts ✅
- Other character sprites continue working normally ✅

**Risk Assessment**:
- **Low Risk**: Memory-only modifications (Option B)
- **Medium Risk**: File aliasing (Option C)
- **High Risk**: DLC bypass (Option A) - may trigger anti-tamper

### Fallback Strategy
If all three options fail, implement **Ramza exclusion mode**:
- Job/gender color system works for ALL characters except Ramza
- Display warning: "Ramza colors require manual file modification"
- Provide instructions for manual sprite replacement for advanced users

This ensures the main color modification system remains functional while acknowledging the Ramza limitation.

---

## UPDATE: Memory Patching Results (December 2024)

After extensive testing with RamzaPatcher, direct memory patching causes game crashes due to:
- **Memory access violations** when scanning protected regions
- **CLR errors** when interacting with the game's managed memory
- **Instability** even with safety limits (10 regions, 5-second intervals)
- **Process handle issues** with OpenProcess permissions

The diagnostic tool successfully found:
- 73,530 potential palette locations
- Steam API integration confirmed
- DLC protection mechanisms active

However, safely accessing these memory regions at runtime proved problematic, causing immediate crashes.

## Revised Approaches for Ramza Color Modification

### Option D: Steam API Emulation (High Confidence - 80%)
**Approach**: Use existing Steam emulators that bypass DLC checks entirely
- **CreamAPI**: Drop-in replacement for steam_api.dll that spoofs DLC ownership
- **SmokeAPI**: Similar tool with broader compatibility
- **GreenLuma**: Steam client-level DLC unlocker

**Implementation**:
1. Replace steam_api.dll with CreamAPI version
2. Configure cream_api.ini to unlock Ramza DLC
3. Normal file swapping should then work

**Pros**: Well-tested, widely used, no custom code needed
**Cons**: Requires third-party tools, may violate Steam ToS

### Option E: FFTPatcher Integration (Medium Confidence - 60%)
**Approach**: Use FFTPatcher's sprite editing capabilities
- FFTPatcher can modify sprite data in ISO/game files
- Could pre-patch Ramza sprites with all color variants
- Load different pre-patched versions based on user selection

**Implementation**:
1. Create multiple patched game data files with different Ramza colors
2. Swap data files instead of individual sprites
3. May require game restart for changes

### Option F: Reloaded-II File Redirection (Medium Confidence - 50%)
**Approach**: Use Reloaded-II's built-in file redirection at a lower level
- Reloaded-II has powerful file system hooks
- May bypass DLC checks if hooked early enough
- Could redirect Ramza sprite requests before validation

**Implementation**:
```csharp
// Use Reloaded's IFileRedirector interface
public class RamzaRedirector : IFileRedirector
{
    public bool TryRedirect(string path, out string newPath)
    {
        if (path.Contains("ramza") || path.Contains("ch01"))
        {
            // Redirect to non-DLC sprite
            newPath = path.Replace("ch01", "knight_m");
            return true;
        }
        newPath = null;
        return false;
    }
}
```

### Option G: Sprite ID Manipulation (Low Confidence - 30%)
**Approach**: Change Ramza's sprite ID to point to a non-DLC sprite slot
- Modify save game or memory to change Ramza's sprite ID
- Make him use a generic knight sprite slot that we can modify
- Essentially make Ramza not be "Ramza" from the game's perspective

### Recommended Path Forward

Given the crashes with memory patching, the most practical approach is:

1. **First Try**: Steam API emulation (CreamAPI) - highest success rate
2. **Second Try**: Reloaded-II file redirection - stays within mod framework
3. **Fallback**: Accept Ramza limitation, document manual workarounds

The Steam API emulation approach is widely used in the modding community and has proven reliable for bypassing DLC checks without the instability of runtime memory patching.

---

## Sprite Refresh Investigation (December 2024)

### The Core Problem
FFT uses **DirectX 12** for rendering, which fundamentally changes how textures are managed:
- Textures are uploaded to GPU memory via command lists
- Once in VRAM, textures remain cached until explicitly released
- CPU-side memory modifications don't affect GPU-cached textures
- File swapping works for new sprites but not already-loaded ones

### Investigation Summary

#### What We Found
1. **Sprite Name References**: Located 4 sprite name strings in memory (gin, mina, san, item)
2. **No Direct Pointers**: These strings aren't referenced by sprite management structures
3. **Possible Tables**: Found arrays of 8-10 pointers, but they contained random/repeating values
4. **D3D12 Resources**: Located 368 possible ID3D12Resource objects in memory

#### What We Tried

##### 1. Memory Corruption Near Sprite Names (Failed)
- Corrupted 32 bytes near sprite name strings
- No visual refresh occurred
- Sprite names appear to be in logs/temp buffers, not management structures

##### 2. Pointer Search for Management Structures (Failed)
- Searched for pointers TO sprite name addresses
- Found no references - names likely in temporary memory
- Game probably uses indices or hashes, not direct pointers

##### 3. DirectX 12 Resource Manipulation (Too Dangerous)
- Found D3D12 resource objects but can't safely modify
- Modifying fence values or descriptor heaps risks crashes
- Would need access to command queue to properly trigger reload

##### 4. Window State Manipulation (User Tested - Failed)
- Alt+Tab doesn't trigger resource reload in D3D12
- Window minimize/restore doesn't help
- D3D12 maintains resources across window state changes

### Technical Explanation: Why D3D12 Makes This Impossible

#### D3D12 Architecture
```
CPU Memory          GPU Memory
----------          ----------
Sprite File  →  Upload via  →  Texture in VRAM
             Command List      (Cached permanently)
                    ↓
             Resource Barrier
                    ↓
             Descriptor Heap
                    ↓
               GPU Renders
```

#### The Fundamental Barrier
1. **Command Lists**: All GPU operations go through command lists
2. **Resource Barriers**: State transitions require explicit barriers
3. **Descriptor Heaps**: SRV descriptors point to GPU textures
4. **No Direct Access**: Can't modify GPU memory from CPU side

### Confirmed Working Solution
**File Swapping + Scene Change/Restart**
- File swapping successfully replaces sprite files on disk
- New sprites (not yet loaded) use the updated files
- Already-loaded sprites require scene change or game restart
- This is the only reliable method without game source code access

### What Would Be Required for Live Refresh
To achieve live sprite refresh would require ONE of:
1. **Hook D3D12 command queue** - Inject commands to reload textures
2. **Find game's reload function** - Call internal sprite loading code
3. **Implement command list recording** - Complex and game-specific
4. **Modify game executable** - Add reload functionality

All of these approaches are:
- Extremely complex and fragile
- Likely to cause crashes
- Game-version specific
- Beyond the scope of a color mod

### Final Verdict
**Live sprite refresh is not feasible** with DirectX 12 without deep game modifications. The current file-swapping approach with manual scene changes is the optimal solution given the technical constraints.

### User Workarounds
Users can see updated sprites by:
1. **Changing scenes** (enter/exit battle, change maps)
2. **Restarting the game**
3. **Waiting for unloaded sprites** to appear with new colors

This is a limitation of modern graphics APIs, not a bug in our implementation.