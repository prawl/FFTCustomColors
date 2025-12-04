# FFT Color Mod - Development Guide
<!-- KEEP UNDER 100 LINES TOTAL -->

## Overview
Color modification mod for FFT (Steam) using function hooking to swap character palettes via hotkeys. Uses Reloaded-II mod loader.

## Paths
- **Dev**: `C:\Users\ptyRa\Dev\FFT_Color_Mod`
- **Install**: `C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\Reloaded\Mods\FFT_Color_Mod`

## Technical Details
- **Format**: BGR (Blue-Green-Red), 3 bytes per color
- **Ramza Brown**: `0x17, 0x2C, 0x4A` (main tunic)
- **Approach**: Function hooking via signature scanning (not direct memory modification)
- **Status**: 34 tests passing ✅

## Critical Commands

```bash
# Run Tests - USE THE SCRIPT!
./run_tests.sh    # Git Bash (recommended)
.\run_tests.ps1   # PowerShell

# Manual test command (if script fails)
rm -rf bin obj && dotnet restore FFTColorMod.Tests.csproj && dotnet build FFTColorMod.Tests.csproj && dotnet test FFTColorMod.Tests.csproj --verbosity minimal

# Quick test result check (just pass/fail line)
./run_tests.sh 2>&1 | grep "Passed!"

# Build & Deploy
dotnet build -c Release
.\BuildLinked.ps1  # Quick deploy to Reloaded
```

## Development Style
**TLDDR (TDD + TLDR)**: Write ONE test at a time, then minimal code to pass. Add TLDR comments explaining what code does, not process.

## Hotkeys
- **F1-F5**: Color schemes (Blue/Red/Green/Purple/Original)
- **F9**: Rescan palettes

## Important Notes
- PAC files (not BIN/ISO)
- .NET 8.0
- `AllowUnsafeBlocks` required
- Tests need `<ImplicitUsings>enable</ImplicitUsings>` + `<Using Include="Xunit" />`
- Main project needs `<Compile Remove="Tests\**" />`

## Function Hooking Approach (From FFTGenericJobs)

**Key**: Hook sprite loading functions to modify palettes AS they load (not after).

### Required Dependencies
```xml
<!-- FFTColorMod.csproj -->
<PackageReference Include="Reloaded.Memory" Version="9.4.3" />
<PackageReference Include="Reloaded.Memory.Sigscan" Version="3.1.9" />
<PackageReference Include="Reloaded.Memory.SigScan.ReloadedII.Interfaces" Version="1.2.0" />
<PackageReference Include="Reloaded.SharedLib.Hooks" Version="1.9.0" />
```

```json
// ModConfig.json
"ModDependencies": ["Reloaded.Memory.SigScan.ReloadedII", "reloaded.sharedlib.hooks"]
```

### Implementation Pattern
```csharp
// Hook sprite loading
_startupScanner.AddMainModuleScan(
    "48 8B C4 48 89 58 ??",  // Find function signature
    result => _hooks.CreateHook<LoadSpriteDelegate>(LoadSpriteHook, gameBase + result.Offset).Activate()
);

private nint LoadSpriteHook(nint spriteData, int size) {
    var result = _loadSpriteHook.OriginalFunction(spriteData, size);
    ModifyPaletteInMemory(spriteData);  // Apply our PaletteDetector!
    return result;
}
```

### Next Steps
1. Find sprite loading signatures with x64dbg
2. Hook functions at startup
3. Apply existing color logic in hooks


# important-instruction-reminders
Do what has been asked; nothing more, nothing less.
NEVER create files unless they're absolutely necessary for achieving your goal.
ALWAYS prefer editing an existing file to creating a new one.
NEVER proactively create documentation files (*.md) or README files. Only create documentation files if explicitly requested by the User.
NEVER create new .md files without permission.

