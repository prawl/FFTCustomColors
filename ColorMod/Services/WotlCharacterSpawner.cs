using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Reloaded.Memory.Sigscan;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Services;

/// <summary>
/// Auto-adds Balthier and Luso to the party when a save is loaded,
/// eliminating the Cheat Engine requirement from the WotL Characters mod.
///
/// Approach: Convert existing generic units in-place by changing their
/// Character ID (spriteSet), Job, and unitIndex fields — matching what
/// the Cheat Engine manual process does.
/// </summary>
public class WotlCharacterSpawner
{
    // Unit data array layout (from FFT_Egg_Control by dicene)
    private const int SlotCount = 55;
    private const int SlotSize = 0x258; // 600 bytes per unit

    // Known offsets within each unit slot
    private const int OffsetSpriteSet = 0x00;  // Character ID / sprite reference
    private const int OffsetUnitIndex = 0x01;
    private const int OffsetJob = 0x02;        // Current job
    private const int OffsetNameId = 0x230;    // Unit Name ID (uint16 LE) - indexes into CharaName table

    // Sentinel for empty slot
    private const byte EmptySlotMarker = 0xFF;

    // Generic human job range (74=Squire through 93=Mime)
    // Jobs below 74 are story character unique jobs (Holy Knight, Divine Knight, etc.)
    private const byte MinGenericJobId = 74;
    private const byte MaxGenericJobId = 93;

    // AoB pattern to locate the unit data array base address
    private const string UnitDataAoB =
        "48 8D 05 ?? ?? ?? ?? 48 03 C8 74 ?? 8B 43 ?? F2 0F 10 43 ?? F2 0F 11 41 ?? 89 41 ?? 0F B7 43 ?? 66 89 41";

    // WotL character definitions
    private static readonly WotlCharacter[] Characters =
    {
        new(JobId: 162, Name: "Balthier"),
        new(JobId: 163, Name: "Luso"),
    };

    private readonly nuint _unitDataAddress;
    private readonly CancellationTokenSource _cts = new();
    private Task? _pollingTask;
    private bool _bothPresent; // suppress repeated "already in party" logs

    private record WotlCharacter(byte JobId, string Name);

    private WotlCharacterSpawner(nuint unitDataAddress)
    {
        _unitDataAddress = unitDataAddress;
    }

    /// <summary>
    /// Creates and starts the spawner. Returns null if the AoB scan fails.
    /// </summary>
    public static WotlCharacterSpawner? CreateAndStart()
    {
        var address = ScanForUnitDataArray();
        if (address == nuint.Zero)
            return null;

        var spawner = new WotlCharacterSpawner(address);
        spawner.StartPolling();
        return spawner;
    }

    private static unsafe nuint ScanForUnitDataArray()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var mainModule = process.MainModule;
            if (mainModule == null)
            {
                ModLogger.LogError("[WotlSpawner] Cannot access main module");
                return nuint.Zero;
            }

            var baseAddress = (byte*)mainModule.BaseAddress;
            var moduleSize = mainModule.ModuleMemorySize;

            ModLogger.Log($"[WotlSpawner] Scanning {moduleSize / 1024 / 1024}MB at 0x{(nuint)baseAddress:X}");

            using var scanner = new Scanner(baseAddress, moduleSize);
            var result = scanner.FindPattern(UnitDataAoB);

            if (!result.Found)
            {
                ModLogger.LogWarning("[WotlSpawner] AoB pattern not found — WotL Characters mod may not be active");
                return nuint.Zero;
            }

            // RIP-relative addressing: read Int32 at pattern+3, address = pattern+7+offset
            var patternAddress = (byte*)baseAddress + result.Offset;
            int ripOffset = *(int*)(patternAddress + 3);
            var resolvedAddress = (nuint)(patternAddress + 7 + ripOffset);

            ModLogger.Log($"[WotlSpawner] Unit data array found at 0x{resolvedAddress:X}");
            return resolvedAddress;
        }
        catch (Exception ex)
        {
            ModLogger.LogError($"[WotlSpawner] AoB scan failed: {ex.Message}");
            return nuint.Zero;
        }
    }

    private void StartPolling()
    {
        _pollingTask = Task.Run(async () =>
        {
            ModLogger.Log("[WotlSpawner] Polling started — waiting for save to load...");

            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(2000, _cts.Token);
                    TryConvertCharacters();
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    ModLogger.LogError($"[WotlSpawner] Polling error: {ex.Message}");
                }
            }
        }, _cts.Token);
    }

    private unsafe bool TryConvertCharacters()
    {
        var basePtr = (byte*)_unitDataAddress;

        // Check if a save is loaded: slot 0 unitIndex != 0xFF and job > 0
        if (basePtr[OffsetUnitIndex] == EmptySlotMarker || basePtr[OffsetJob] == 0)
        {
            // No save loaded — reset state so we re-check on next save load
            _bothPresent = false;
            return false;
        }

        // Check which characters already exist
        bool balthierPresent = FindSlotWithJob(basePtr, Characters[0].JobId) >= 0;
        bool lusoPresent = FindSlotWithJob(basePtr, Characters[1].JobId) >= 0;

        if (balthierPresent && lusoPresent)
        {
            if (!_bothPresent)
            {
                ModLogger.Log("[WotlSpawner] Both Balthier and Luso already in party");
                _bothPresent = true;
            }
            return true;
        }

        // Count how many characters we still need to add
        var missing = new System.Collections.Generic.List<WotlCharacter>();
        foreach (var character in Characters)
        {
            if (FindSlotWithJob(basePtr, character.JobId) < 0)
                missing.Add(character);
        }

        if (missing.Count == 0)
            return false;

        int availableGenerics = CountGenerics(basePtr);
        if (availableGenerics < missing.Count)
        {
            if (!_bothPresent) // reuse flag to avoid log spam
                ModLogger.LogWarning($"[WotlSpawner] Need {missing.Count} generic unit(s) but only {availableGenerics} available — recruit more generics");
            return false;
        }

        // Convert generics for missing characters
        foreach (var character in missing)
        {
            int donorSlot = FindLastGenericSlot(basePtr);
            if (donorSlot < 0)
                break;

            ConvertUnit(basePtr, donorSlot, character);
        }

        return false; // keep polling
    }

    private static unsafe void ConvertUnit(byte* basePtr, int slotIndex, WotlCharacter character)
    {
        var slot = basePtr + slotIndex * SlotSize;

        byte oldSpriteSet = slot[OffsetSpriteSet];
        byte oldJob = slot[OffsetJob];
        ushort oldNameId = *(ushort*)(slot + OffsetNameId);

        // Change Character ID (spriteSet) and Job — matching the Cheat Engine process
        slot[OffsetSpriteSet] = character.JobId;
        slot[OffsetJob] = character.JobId;

        // Set the Name ID at +0x230 (uint16 LE) to the CharaName table key
        // This is the field the game uses to look up the displayed character name
        *(ushort*)(slot + OffsetNameId) = character.JobId;

        ModLogger.Log($"[WotlSpawner] Converted slot {slotIndex}: sprSet {oldSpriteSet}->{character.JobId}, job {oldJob}->{character.JobId}, nameId {oldNameId}->{character.JobId} ({character.Name})");
    }

    private static unsafe int FindSlotWithJob(byte* basePtr, byte jobId)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            var slot = basePtr + i * SlotSize;
            if (slot[OffsetUnitIndex] != EmptySlotMarker && slot[OffsetJob] == jobId)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Find the last generic unit in the roster (searching from the end).
    /// Generic recruits have spriteSet 128 (male) or 129 (female) and job in range 74-93 (Squire-Mime).
    /// Story characters also use spriteSet 128 but have unique jobs outside the generic range.
    /// </summary>
    private static unsafe int FindLastGenericSlot(byte* basePtr)
    {
        for (int i = SlotCount - 1; i >= 0; i--)
        {
            var slot = basePtr + i * SlotSize;
            if (slot[OffsetUnitIndex] != EmptySlotMarker
                && slot[OffsetJob] >= MinGenericJobId
                && slot[OffsetJob] <= MaxGenericJobId
                && (slot[OffsetSpriteSet] == 128 || slot[OffsetSpriteSet] == 129))
                return i;
        }
        return -1;
    }

    private static unsafe int CountGenerics(byte* basePtr)
    {
        int count = 0;
        for (int i = 0; i < SlotCount; i++)
        {
            var slot = basePtr + i * SlotSize;
            if (slot[OffsetUnitIndex] != EmptySlotMarker
                && slot[OffsetJob] >= MinGenericJobId
                && slot[OffsetJob] <= MaxGenericJobId
                && (slot[OffsetSpriteSet] == 128 || slot[OffsetSpriteSet] == 129))
                count++;
        }
        return count;
    }

    public void Stop()
    {
        _cts.Cancel();
        _pollingTask?.Wait(TimeSpan.FromSeconds(5));
        _cts.Dispose();
        ModLogger.Log("[WotlSpawner] Stopped");
    }
}
