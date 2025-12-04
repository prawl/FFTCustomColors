# FFT Color Mod - Technical Planning & Research

## 🚨 BREAKTHROUGH: FFTGenericJobs Analysis Changes Everything!

After analyzing the successful **FFTGenericJobs** mod, we discovered they use a completely different approach that **successfully modifies FFT through memory manipulation**. This solves our palette reloading problem!

### Key Discovery
**They succeed by hooking game functions and patching memory at startup**, not by modifying data after loading. This is why their changes persist while ours didn't.

### New Approach: Function Hooking + Signature Scanning
1. **Find functions dynamically** using byte pattern signatures
2. **Hook sprite/palette loading functions** to modify data as it loads
3. **Patch memory locations** to prevent palette reloading
4. **Use proven tools**: Reloaded.Memory.SigScan + Reloaded.SharedLib.Hooks

### Why This Changes Everything
- ✅ **Solves palette reloading**: Hook the loading function, not the loaded data
- ✅ **Proven to work**: FFTGenericJobs successfully modifies FFT this way
- ✅ **Survives updates**: Signature scanning finds addresses dynamically
- ✅ **Reuses our code**: All 27 tests and PaletteDetector logic still apply

**See detailed analysis below in "CRITICAL DISCOVERY" section**

---

## Memory Manipulation Research Findings

### Root Cause Analysis: FFT Palette System Behavior
Through extensive testing, we discovered that FFT uses a **dynamic palette reloading system**:

1. **Initial Search**: Found 8 palettes across multiple memory regions
2. **Successful Modification**: All WriteProcessMemory operations succeeded (256 bytes each)
3. **Color Transformation**: Verified `80 40 60` → `30 30 80` (purple to red)
4. **Palette Disappearance**: After modification, subsequent searches find "No palettes found"
5. **Conclusion**: FFT actively reloads palettes from source data, overriding memory modifications

### Technical Achievements
- **Memory Search Evolution**: 6 → 26 → 38 regions searched
- **Palette Discovery**: Found palettes up to 0x15E000000 (high graphics memory)
- **Multiple Detection**: Successfully found both Chapter 1 and Chapter 2 patterns
- **Comprehensive Coverage**: Searched from base memory to GPU texture regions (12GB+)

**Finding**: FFT's palette system actively reloads from source data, making real-time memory modification ineffective for persistent visual changes.

## Asset Replacement Hook Approach

### Why File Interception is Recommended
- **Solves Root Cause**: Intercepts at source before FFT's palette reloading
- **Persistent Changes**: Modifications survive asset management system
- **Performance**: No continuous memory scanning overhead
- **Reliability**: Works with FFT's loading system instead of against it

### Technical Implementation

#### Reloaded-II Universal Redirector Setup
```csharp
// Add to FFTColorMod.csproj
<PackageReference Include="Reloaded.Universal.Redirector.Interfaces" Version="2.1.0" />

// Update ModConfig.json dependencies
"ModDependencies": [
    "reloaded.universal.redirector"  // Add this
]
```

#### Basic File Hook Implementation
```csharp
public class FFTSpriteHooks
{
    private IRedirectorController _redirector;
    private PaletteDetector _paletteDetector; // Reuse existing TDD code!

    public void Start(IModLoaderV1 loader)
    {
        _redirector = loader.GetController<IRedirectorController>();
        _paletteDetector = new PaletteDetector();

        // Hook sprite files
        _redirector.RegisterFileHook("**/*.spr", ModifySpriteFile);
        _redirector.RegisterFileHook("**/*.pac", ModifyArchiveFile);
    }

    private byte[] ModifySpriteFile(string filePath, byte[] originalData)
    {
        if (_currentColorScheme == ColorScheme.Original)
            return originalData;

        // Use existing TDD methods
        var chapter = _paletteDetector.DetectChapterOutfit(originalData);
        return _paletteDetector.ReplacePaletteColors(originalData, chapter, _currentColorScheme);
    }
}
```

### FFT File Structure Findings

#### PAC Archive Format
- Steam version uses `.pac` files instead of BIN/ISO
- Numbered PAC files by resource type
- Sprites contained in specific PAC archives

#### SPR File Structure
```csharp
// Discovered structure
Header:         32 bytes
Palette Data:   768 bytes (256 colors × 3 bytes BGR)
Sprite Data:    Variable (indexed pixel data)
Animation Data: Variable (frame sequences)

PALETTE_OFFSET = 32  // Where our tests found patterns
```

## CRITICAL DISCOVERY: FFTGenericJobs Implementation Analysis

### Revolutionary Findings from FFTGenericJobs Mod
After analyzing the successful FFTGenericJobs mod (adds Dark Knight & Onion Knight), we discovered a **completely different approach to FFT modding** that successfully modifies the game through memory manipulation. This changes everything!

#### Key Technologies They Use
1. **Signature Scanning (SigScan)**: Dynamically finds memory addresses using byte patterns
2. **Function Hooking**: Intercepts game functions to modify behavior
3. **Direct Memory Patching**: Writes specific byte sequences to memory addresses
4. **FFT-Specific Mod Loader**: Uses `fftivc.utility.modloader` by Nenkai

#### Their Dependencies (From GenericJobs.csproj)
```xml
<PackageReference Include="Reloaded.Memory" Version="9.4.3" />
<PackageReference Include="Reloaded.Memory.Sigscan" Version="3.1.9" />
<PackageReference Include="Reloaded.Memory.SigScan.ReloadedII.Interfaces" Version="1.2.0" />
<PackageReference Include="Reloaded.SharedLib.Hooks" Version="1.9.0" />
```

#### How They Modify FFT Successfully
```csharp
// They use signature scanning to find function addresses:
_startupScanner.AddMainModuleScan(
    "48 8B C4 48 89 58 ?? 48 89 70 ?? ...",  // Byte pattern
    result => {
        if (result.Found) {
            // Hook the function or patch memory
            _hooks.CreateHook<DelegateType>(HookFunction, gameBase + result.Offset);
        }
    }
);

// Direct memory patching that works:
private unsafe void WriteMemory(nuint address, byte[] data) {
    fixed (byte* dataPtr = data) {
        Reloaded.Memory.Memory.Instance.SafeWrite(address,
            new Span<byte>(dataPtr, data.Length));
    }
}

// Example patches they apply:
WriteMemory(_gameBase + offset, [0x90, 0x90, 0x90]); // NOP instructions
WriteMemory(_gameBase + offset, [0x07, 0x00, 0x00]); // Data modifications
```

### Why Their Memory Manipulation Works vs Ours Didn't

#### Their Approach (WORKS ✅)
- **Hooks game functions** before assets are loaded
- **Patches memory at specific offsets** identified through reverse engineering
- **Uses signature scanning** to find addresses dynamically (survives updates)
- **Modifies game logic** not just data values
- **Timing is critical**: Patches applied at startup through hooks

#### Our Initial Approach (FAILED ❌)
- Tried to modify palette data after it was loaded
- FFT reloads palettes from source files periodically
- Didn't hook the loading functions
- Modified data instead of logic

### How This Applies to Our Color Mod

#### Option 1: Hook Sprite Loading Functions (RECOMMENDED)
Instead of modifying palettes in memory after loading, we should:
1. Find the sprite loading function using signature scanning
2. Hook the function that loads .SPR files from PAC archives
3. Modify the palette data as it's being loaded
4. This prevents FFT from reloading original colors

```csharp
// Conceptual implementation using FFTGenericJobs approach
[Function(CallingConventions.Microsoft)]
private delegate nint LoadSpriteDelegate(nint spriteData, int size);

private nint LoadSpriteHook(nint spriteData, int size) {
    // Let original function load the sprite
    var result = _loadSpriteHook.OriginalFunction(spriteData, size);

    // Now modify the palette data in the loaded sprite
    ModifyPaletteInMemory(spriteData);

    return result;
}
```

#### Option 2: Patch Palette Reload Logic
Find and patch the code that triggers palette reloading:
1. Use signature scanning to find the reload function
2. NOP out the reload calls
3. Our memory modifications become permanent

#### Option 3: Hybrid Approach (File + Memory Hooks)
Combine both approaches:
1. Use Universal Redirector for initial file loading
2. Use memory hooks to prevent reloading
3. Best of both worlds

### Required Changes to Our Project

#### Add New Dependencies
```xml
<!-- Add to FFTColorMod.csproj -->
<PackageReference Include="Reloaded.Memory" Version="9.4.3" />
<PackageReference Include="Reloaded.Memory.Sigscan" Version="3.1.9" />
<PackageReference Include="Reloaded.Memory.SigScan.ReloadedII.Interfaces" Version="1.2.0" />
<PackageReference Include="Reloaded.SharedLib.Hooks" Version="1.9.0" />
```

#### Update ModConfig.json Dependencies
```json
"ModDependencies": [
    "Reloaded.Memory.SigScan.ReloadedII",
    "reloaded.sharedlib.hooks",
    "fftivc.utility.modloader"  // Check if this helps with FFT-specific functionality
]
```

### Signature Patterns We Need to Find

Based on FFT's behavior, we need to find:
1. **Sprite Loading Function**: Called when loading .SPR files
2. **Palette Application Function**: Applies palette to sprites
3. **Palette Reload Function**: The one that overwrites our changes
4. **PAC Archive Extraction**: Where files are loaded from archives

We can find these using tools like:
- x64dbg with FFT_enhanced.exe
- IDA Pro or Ghidra for static analysis
- Cheat Engine for runtime analysis

### Implementation Strategy

#### Phase 0: Research & Pattern Discovery (2-3 days)
1. Use x64dbg to trace sprite loading
2. Find function signatures for:
   - LoadSprite / LoadPalette functions
   - Palette reload triggers
   - PAC file extraction
3. Document byte patterns for signature scanning

## Implementation Plan

### Phase 1: Proof of Concept with Hooks (3-4 days)
1. Add SigScan and Memory dependencies
2. Research FFT_enhanced.exe with x64dbg to find:
   - Sprite loading function signatures
   - Palette application points
3. Implement basic function hooks
4. Test palette modifications through hooks

### Phase 2: Full Hook Integration (3 days)
1. Hook all sprite-related functions
2. Integrate existing PaletteDetector logic
3. Apply hotkey system to hooked modifications
4. Test all 4 chapter variations with hooks

### Phase 3: Hybrid Approach (2 days)
1. Combine Universal Redirector with hooks
2. File interception for initial load
3. Hooks to prevent palette reloading
4. Performance optimization

### Phase 4: Polish & Release (1 day)
1. Cleanup and optimization
2. Comprehensive logging
3. User documentation
4. Package for distribution

## Actionable Next Steps

### Immediate Actions
1. **Add new dependencies** to FFTColorMod.csproj:
   ```bash
   dotnet add package Reloaded.Memory --version 9.4.3
   dotnet add package Reloaded.Memory.Sigscan --version 3.1.9
   dotnet add package Reloaded.Memory.SigScan.ReloadedII.Interfaces --version 1.2.0
   dotnet add package Reloaded.SharedLib.Hooks --version 1.9.0
   ```

2. **Study FFTGenericJobs patterns** for sprite-related functions
3. **Create signature scanner** to find our target functions
4. **Implement hook-based approach** using existing PaletteDetector

### Research Tools Needed
- **x64dbg**: For runtime analysis and finding function signatures
- **Process Monitor**: To trace file access patterns
- **Cheat Engine**: For memory scanning and testing

### Expected Challenges & Solutions
- **Finding correct signatures**: Use multiple patterns, test thoroughly
- **Hook timing**: Ensure hooks are installed before game loads assets
- **Compatibility**: Test with other mods, especially fftivc.utility.modloader

## Alternative Approaches (Future)

### Shishi Sprite Editor (Manual)
- **Pros**: Well-documented, community support
- **Cons**: Manual process, not real-time
- **Use Case**: Creating base modified sprites

### GPU Shader Interception
- **Pros**: Most powerful, unlimited effects
- **Cons**: Complex implementation (8/10 difficulty)
- **Use Case**: Advanced visual effects

### Direct File Modification
- **Pros**: Permanent changes
- **Cons**: No real-time switching
- **Use Case**: Distribution of pre-modified sprites

## Code Reuse Strategy

All 27 existing tests transfer directly:
- `DetectChapterOutfit()` - works on file data
- `ReplacePaletteColors()` - transforms file palettes
- `FindPalette()`/`FindAllPalettes()` - scan file offsets
- `HotkeyManager` - triggers cache refresh instead of memory write

## Risk Mitigation
- **Backup**: Keep original detection/transformation logic
- **Fallback**: Memory approach remains as alternative
- **Testing**: 27 existing tests validate core logic
- **Logging**: Comprehensive debugging output