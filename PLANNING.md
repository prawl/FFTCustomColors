# FFT Color Mod - Technical Planning & Research

## 🔥 ULTRA-DEEP ANALYSIS: FFTGenericJobs Complete Architecture Decoded!

### CRITICAL DISCOVERIES FROM DEEP DIVE (December 2024)

After thorough sequential analysis of FFTGenericJobs source code, we've uncovered the **EXACT implementation patterns** that make their memory manipulation successful:

#### 1. **StartEx vs Start - The Missing Link**
- **PROBLEM**: Our `Start()` method never gets called by Reloaded-II
- **SOLUTION**: Use `StartEx(IModLoaderV1 loaderApi, IModConfigV1 modConfig)` instead!
- **PROOF**: GenericJobs/Template/Startup.cs line 51 - StartEx DOES get called
- **KEY**: StartEx is the actual entry point Reloaded-II uses, not Start

#### 2. **ModContext Pattern - Immediate Initialization**
```csharp
// Their Pattern (WORKS):
public class Startup : IMod {
    public void StartEx(IModLoaderV1 loaderApi, IModConfigV1 modConfig) {
        // Collect all services HERE
        _mod = new Mod(new ModContext {
            Hooks = _hooks,
            Logger = _logger,
            ModLoader = _modLoader
        });
    }
}

public class Mod : ModBase {
    public Mod(ModContext context) {
        // Everything available IMMEDIATELY in constructor!
        Initialize(); // Can hook right away!
    }
}
```

#### 3. **IStartupScanner Dependency - Not Optional!**
- **CRITICAL**: They list "Reloaded.Memory.SigScan.ReloadedII" as REQUIRED dependency
- **Location**: ModConfig.json line 60
- **Effect**: Guarantees IStartupScanner is available when mod loads
- **Our Fix**: Add same dependency to our ModConfig.json

#### 4. **Direct Memory Patching with SafeWrite**
```csharp
// Their WriteMemory implementation (lines 326-332):
private unsafe void WriteMemory(nuint address, byte[] data) {
    fixed (byte* dataPtr = data) {
        Reloaded.Memory.Memory.Instance.SafeWrite(address,
            new Span<byte>(dataPtr, data.Length));
    }
}
```
- Uses `Reloaded.Memory.Memory.Instance.SafeWrite` for safe memory writing
- Writes NOPs (0x90), jumps (0xEB), and data values directly

#### 5. **Temporary Patching Pattern - Genius Execution Control**
```csharp
// Temporarily disable game code (lines 434-452):
private void ApplyTempPatches(bool disable) {
    if (disable) {
        // Replace with NOPs to disable
        WriteMemory(addr, [0x90, 0x90, 0x90]);
    } else {
        // Restore original instructions
        WriteMemory(addr, [0x89, 0x83, 0x38]);
    }
}

// Usage pattern:
ApplyTempPatches(true);  // Disable game code
DoOurModifications();     // Make changes safely
CallOriginalFunction();   // Let game continue
ApplyTempPatches(false); // Re-enable game code
```

#### 6. **Hook Pattern for Runtime Modification**
```csharp
// Hook creation (lines 196, 200, etc):
_hooks.CreateHook<DelegateType>(HookFunction, address).Activate()

// Hook function pattern:
private ReturnType HookFunction(params) {
    // Pre-processing
    ModifyData();

    // Call original
    var result = _hook.OriginalFunction(params);

    // Post-processing
    ModifyMoreData();

    return result;
}
```

### 🎯 SOLUTION FOR OUR COLOR MOD

Based on these discoveries, here's our exact implementation path:

#### Step 1: Fix Initialization (IMMEDIATE)
1. Create `Template/Startup.cs` with StartEx entry point
2. Implement ModContext pattern
3. Pass all services to Mod constructor
4. Initialize hooks in constructor, not Start()

#### Step 2: Add Required Dependencies
```json
"ModDependencies": [
    "Reloaded.Memory.SigScan.ReloadedII",  // CRITICAL!
    "reloaded.sharedlib.hooks"
]
```

#### Step 3: Find Sprite Loading Signatures
We need signatures for:
- Sprite/palette loading from PAC files
- Palette application to sprites
- The reload function that overwrites our changes

#### Step 4: Implement Hooks
```csharp
// Hook sprite loading to modify palettes AS they load:
private nint LoadSpriteHook(nint spriteData, int size) {
    var result = _loadSpriteHook.OriginalFunction(spriteData, size);

    // Apply our existing PaletteDetector logic HERE!
    if (_paletteDetector.DetectChapterOutfit(spriteData)) {
        _paletteDetector.ReplacePaletteColors(spriteData, _currentScheme);
    }

    return result;
}
```

#### Step 5: Optional - Temporary Patch Pattern
If palette reloading persists, temporarily NOP the reload call:
```csharp
// During our color modification:
WriteMemory(reloadFunctionAddr, [0x90, 0x90, 0x90]); // NOP
ModifyColors();
WriteMemory(reloadFunctionAddr, originalBytes);      // Restore
```

### 🚨 WHY THIS CHANGES EVERYTHING

**Old Approach (Failed)**:
- Modified palette data AFTER loading
- FFT reloaded palettes, overwriting changes
- No control over execution flow

**New Approach (Will Work)**:
- Hook loading functions directly
- Modify data DURING load process
- Control execution with temp patches
- Prevent reloading through hooks

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

## CRITICAL DISCOVERY #2: FFTGenericJobs DUAL-ENTRY INITIALIZATION

### The Secret: TWO-LAYER Architecture!

After deep analysis, FFTGenericJobs uses a **two-layer initialization pattern** that bypasses the IStartupScanner problem:

1. **Startup.cs** (Entry point - IMod interface)
   - Implements `StartEx(IModLoaderV1 loaderApi, IModConfigV1 modConfig)`
   - This DOES get called by Reloaded-II
   - Collects all necessary services (IReloadedHooks, ILogger, etc.)
   - Creates ModContext with all services
   - Instantiates the actual Mod class with ModContext

2. **Mod.cs** (Business logic - ModBase class)
   - Constructor receives ModContext with ALL services already initialized
   - Doesn't need Start() to be called - everything happens in constructor
   - Can immediately use IReloadedHooks to create hooks
   - IStartupScanner issue becomes irrelevant for basic hooks

### Why This Works When Ours Doesn't

**Our Current Structure (BROKEN):**
```csharp
public class Mod : IMod {
    public Mod() { /* Can't do much here */ }
    public void Start(IModLoader modLoader) { /* NEVER GETS CALLED */ }
}
```

**FFTGenericJobs Structure (WORKS):**
```csharp
// Startup.cs - Gets called by Reloaded
public class Startup : IMod {
    public void StartEx(IModLoaderV1 loaderApi, IModConfigV1 modConfig) {
        // Gather all services here
        _mod = new Mod(new ModContext {
            Hooks = _hooks,
            Logger = _logger,
            ModLoader = _modLoader
        });
    }
}

// Mod.cs - Business logic
public class Mod : ModBase {
    public Mod(ModContext context) {
        // Have everything we need immediately!
        _hooks = context.Hooks;
        // Can create hooks right here!
    }
}
```

### The IStartupScanner Mystery Solved

FFTGenericJobs DOES try to use IStartupScanner but:
1. They check if it's available (lines 177-182)
2. If NOT available, they log error and **return**
3. **BUT** - The mod still works because they use CreateWrapper/CreateHook directly!

The key insight: **They don't actually NEED IStartupScanner for basic hooks!**

They can use `_hooks.CreateHook()` directly with known addresses. IStartupScanner is just for pattern scanning to FIND addresses dynamically.

### How They Actually Hook Without IStartupScanner

Even when IStartupScanner fails, they could still:
1. Use hardcoded offsets (if they knew them)
2. Use their own Scanner class (Reloaded.Memory.Sigscan)
3. Hook functions they can find other ways

### SOLUTION FOR OUR MOD

We need to adopt the two-layer pattern:

1. **Create Template/Startup.cs**:
```csharp
public class Startup : IMod {
    public void StartEx(IModLoaderV1 loaderApi, IModConfigV1 modConfig) {
        // This WILL get called
        var modContext = new ModContext {
            Hooks = _hooks,
            Logger = _logger,
            ModLoader = _modLoader
        };
        _mod = new Mod(modContext);
    }
}
```

2. **Modify our Mod.cs**:
```csharp
public class Mod : ModBase {
    public Mod(ModContext context) {
        _hooks = context.Hooks;
        // NOW we can create hooks immediately!
        InitializeHooks();
    }
}
```

3. **For Pattern Scanning Without IStartupScanner**:
```csharp
// Use Reloaded.Memory.Sigscan directly
var scanner = new Scanner(_process, _process.MainModule);
var result = scanner.CompiledFindPattern("B9 00 03 00 00");
if (result.Found) {
    var hook = _hooks.CreateHook<PaletteLoadDelegate>(
        MyHookFunction,
        _gameBase + result.Offset
    );
    hook.Activate();
}
```

### Why FFTGenericJobs Still Works

The mod successfully adds Dark Knight and Onion Knight by:
1. Using the two-layer initialization to get hooks working
2. Patching memory directly with WriteMemory()
3. Hooking critical functions even without IStartupScanner
4. Using hardcoded patterns that rarely change

### Action Items for Our Mod

1. **IMMEDIATE**: Adopt the Startup.cs/ModBase pattern
2. **Use StartEx** instead of Start (it actually gets called!)
3. **Pass services via ModContext** to avoid waiting for Start()
4. **Hook directly** with known addresses from our pattern analysis
5. **Use Scanner class** directly instead of waiting for IStartupScanner

This explains EVERYTHING about why FFTGenericJobs works while ours doesn't!


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