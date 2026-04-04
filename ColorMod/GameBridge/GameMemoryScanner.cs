using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FFTColorCustomizer.Utilities;
using Reloaded.Memory.Sigscan;
using Reloaded.Memory.Sigscan.Structs;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Scans game memory to find the unit data array using AoB signature scanning.
    /// Provides safe memory read methods for accessing unit data fields.
    /// </summary>
    public unsafe class GameMemoryScanner
    {
        // AoB pattern from UNIT_DATA_STRUCTURE.md (x64 RIP-relative LEA)
        private const string UnitDataAoB =
            "48 8D 05 ?? ?? ?? ?? 48 03 C8 74 ?? 8B 43 ?? F2 0F 10 43 ?? F2 0F 11 41 ?? 89 41 ?? 0F B7 43 ?? 66 89 41";

        public const int UnitSlotSize = 0x258; // 600 bytes per unit
        public const int MaxUnitSlots = 55;

        // Known IC remaster field offsets
        public const int OffsetSpriteSet = 0x00;
        public const int OffsetUnitIndex = 0x01;
        public const int OffsetJob = 0x02;
        public const int OffsetExperience = 0x1C;
        public const int OffsetLevel = 0x1D;
        public const int OffsetBrave = 0x1E;
        public const int OffsetFaith = 0x1F;
        public const int OffsetSecondaryAbility = 0x07; // Index into character's unlocked ability list
        public const int OffsetReactionAbility = 0x08; // Ability ID
        public const int OffsetSupportAbility = 0x0A;  // Ability ID
        public const int OffsetMovementAbility = 0x0C;  // Ability ID
        public const int OffsetNameId = 0x230;
        public const int OffsetDisplayOrder = 0x122; // Grid position in party menu (changes with sort)

        // UI State Buffer (discovered via differential scanning)
        // Located in the game's main module at a fixed address
        public static readonly nint UIStateBufferAddress = (nint)0x1407AC7CA;
        public const int UIOffsetCursorIndex = 0x00;
        public const int UIOffsetHp = 0x02;
        public const int UIOffsetMaxHp = 0x06;
        public const int UIOffsetMp = 0x08;
        public const int UIOffsetMaxMp = 0x0C;
        public const int UIOffsetJob = 0x20;
        public const int UIOffsetBrave = 0x22;
        public const int UIOffsetFaith = 0x24;

        public nint UnitDataBase { get; private set; }
        public bool IsInitialized => UnitDataBase != 0;

        public bool Initialize()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var module = process.MainModule;
                if (module == null)
                {
                    ModLogger.LogError("[GameMemory] Could not get main module");
                    return false;
                }

                ModLogger.Log($"[GameMemory] Scanning module: {module.ModuleName}, Base: 0x{module.BaseAddress:X}, Size: 0x{module.ModuleMemorySize:X}");

                using var scanner = new Scanner(process, module);
                var result = scanner.FindPattern(UnitDataAoB);

                if (!result.Found)
                {
                    ModLogger.LogError("[GameMemory] AoB pattern not found!");
                    return false;
                }

                // Resolve RIP-relative address: pattern + 7 + *(int*)(pattern + 3)
                nint patternAddr = module.BaseAddress + result.Offset;
                int displacement = *(int*)(patternAddr + 3);
                UnitDataBase = patternAddr + 7 + displacement;

                ModLogger.Log($"[GameMemory] Unit data array found at: 0x{UnitDataBase:X}");
                ModLogger.Log($"[GameMemory] Pattern at offset: 0x{result.Offset:X}, displacement: 0x{displacement:X}");

                return true;
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[GameMemory] Scan failed: {ex.Message}");
                return false;
            }
        }

        public nint GetUnitAddress(int slot)
        {
            if (!IsInitialized || slot < 0 || slot >= MaxUnitSlots)
                return 0;
            return UnitDataBase + (slot * UnitSlotSize);
        }

        public byte ReadByte(nint address)
        {
            if (address == 0) return 0;
            try { return *(byte*)address; }
            catch { return 0; }
        }

        public void WriteByte(nint address, byte value)
        {
            if (address == 0) return;
            try { *(byte*)address = value; }
            catch { }
        }

        public ushort ReadUInt16(nint address)
        {
            if (address == 0) return 0;
            try { return *(ushort*)address; }
            catch { return 0; }
        }

        public byte[] ReadBytes(nint address, int count)
        {
            if (address == 0 || count <= 0) return Array.Empty<byte>();
            try
            {
                var result = new byte[count];
                Marshal.Copy(address, result, 0, count);
                return result;
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }

        /// <summary>
        /// Reads a unit field at the given offset within a unit slot.
        /// </summary>
        public byte ReadUnitByte(int slot, int offset)
        {
            var addr = GetUnitAddress(slot);
            if (addr == 0) return 0;
            return ReadByte(addr + offset);
        }

        public ushort ReadUnitUInt16(int slot, int offset)
        {
            var addr = GetUnitAddress(slot);
            if (addr == 0) return 0;
            return ReadUInt16(addr + offset);
        }

        /// <summary>
        /// Returns true if the unit slot has an active unit (unitIndex != 0xFF).
        /// </summary>
        public bool IsUnitActive(int slot)
        {
            return ReadUnitByte(slot, OffsetUnitIndex) != 0xFF;
        }
    }
}
