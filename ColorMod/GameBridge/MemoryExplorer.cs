using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Dumps raw hex data from unit memory slots for field discovery.
    /// Claude analyzes these dumps to find unknown offsets (HP, position, CT, etc).
    /// </summary>
    public class MemoryExplorer
    {
        private readonly GameMemoryScanner _scanner;
        private readonly string _outputDirectory;

        public GameMemoryScanner Scanner => _scanner;

        public MemoryExplorer(GameMemoryScanner scanner, string bridgeDirectory)
        {
            _scanner = scanner;
            _outputDirectory = bridgeDirectory;
        }

        /// <summary>
        /// Dumps the full 0x258 bytes for a unit slot as formatted hex + ASCII.
        /// </summary>
        public string DumpUnit(int slot)
        {
            if (!_scanner.IsInitialized)
                return "ERROR: Memory scanner not initialized";

            if (slot < 0 || slot >= GameMemoryScanner.MaxUnitSlots)
                return $"ERROR: Invalid slot {slot} (0-{GameMemoryScanner.MaxUnitSlots - 1})";

            var addr = _scanner.GetUnitAddress(slot);
            var data = _scanner.ReadBytes(addr, GameMemoryScanner.UnitSlotSize);

            if (data.Length == 0)
                return "ERROR: Failed to read memory";

            var sb = new StringBuilder();
            sb.AppendLine($"Unit Slot {slot} - Address: 0x{addr:X}");
            sb.AppendLine($"Size: 0x{GameMemoryScanner.UnitSlotSize:X} ({GameMemoryScanner.UnitSlotSize}) bytes");
            sb.AppendLine($"Dumped at: {DateTime.UtcNow:O}");
            sb.AppendLine();

            // Known fields summary
            sb.AppendLine("=== Known Fields ===");
            sb.AppendLine($"  +0x00 spriteSet: {data[0]} (0x{data[0]:X2})");
            sb.AppendLine($"  +0x01 unitIndex: {data[1]} (0x{data[1]:X2})");
            sb.AppendLine($"  +0x02 job:       {data[2]} (0x{data[2]:X2})");
            sb.AppendLine($"  +0x1C exp:       {data[0x1C]}");
            sb.AppendLine($"  +0x1D level:     {data[0x1D]}");
            sb.AppendLine($"  +0x1E brave:     {data[0x1E]}");
            sb.AppendLine($"  +0x1F faith:     {data[0x1F]}");
            var nameId = BitConverter.ToUInt16(data, 0x230);
            sb.AppendLine($"  +0x230 nameId:   {nameId} (0x{nameId:X4})");
            sb.AppendLine();

            // Full hex dump with ASCII
            sb.AppendLine("=== Raw Hex Dump ===");
            sb.AppendLine("Offset   00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F  ASCII");
            sb.AppendLine("------   -----------------------------------------------  ----------------");

            for (int i = 0; i < data.Length; i += 16)
            {
                sb.Append($"+0x{i:X4}   ");

                // Hex bytes
                for (int j = 0; j < 16; j++)
                {
                    if (i + j < data.Length)
                        sb.Append($"{data[i + j]:X2} ");
                    else
                        sb.Append("   ");
                }

                sb.Append(" ");

                // ASCII representation
                for (int j = 0; j < 16 && i + j < data.Length; j++)
                {
                    byte b = data[i + j];
                    sb.Append(b >= 0x20 && b < 0x7F ? (char)b : '.');
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Writes the hex dump to a file in the bridge directory.
        /// </summary>
        public void DumpUnitToFile(int slot)
        {
            var content = DumpUnit(slot);
            var filePath = Path.Combine(_outputDirectory, $"hexdump_slot_{slot}.txt");

            try
            {
                File.WriteAllText(filePath, content);
                ModLogger.Log($"[MemoryExplorer] Dumped slot {slot} to {filePath}");
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[MemoryExplorer] Failed to write dump: {ex.Message}");
            }
        }

        /// <summary>
        /// Searches game memory for a uint16 value. Used to locate battle data structures
        /// by searching for known values like HP.
        /// </summary>
        public void SearchMemoryForUInt16(ushort value, string label)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Memory Search for uint16 {value} (0x{value:X4}) - \"{label}\"");
            sb.AppendLine($"Searched at: {DateTime.UtcNow:O}");
            sb.AppendLine();

            try
            {
                var process = Process.GetCurrentProcess();
                var matches = new List<(nint address, string context)>();

                // Search all readable memory regions
                nint address = 0;
                int regionsSearched = 0;

                while (true)
                {
                    if (VirtualQueryEx(process.Handle, address, out MEMORY_BASIC_INFORMATION mbi, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) == 0)
                        break;

                    // Only search committed, readable memory
                    if (mbi.State == 0x1000 && // MEM_COMMIT
                        (mbi.Protect & 0xEE) != 0 && // readable (PAGE_READONLY, PAGE_READWRITE, PAGE_EXECUTE_READ, etc)
                        (mbi.Protect & 0x100) == 0) // not PAGE_GUARD
                    {
                        regionsSearched++;
                        var regionBytes = _scanner.ReadBytes(mbi.BaseAddress, (int)mbi.RegionSize);

                        byte lo = (byte)(value & 0xFF);
                        byte hi = (byte)(value >> 8);

                        for (int i = 0; i < regionBytes.Length - 1; i++)
                        {
                            if (regionBytes[i] == lo && regionBytes[i + 1] == hi)
                            {
                                nint foundAddr = mbi.BaseAddress + i;
                                // Get surrounding context (16 bytes before and after)
                                int ctxStart = Math.Max(0, i - 16);
                                int ctxEnd = Math.Min(regionBytes.Length, i + 18);
                                var ctx = new byte[ctxEnd - ctxStart];
                                Array.Copy(regionBytes, ctxStart, ctx, 0, ctx.Length);

                                var ctxHex = BitConverter.ToString(ctx).Replace("-", " ");
                                matches.Add((foundAddr, ctxHex));

                                if (matches.Count >= 200) break; // cap results
                            }
                        }
                    }

                    // Move to next region
                    nint nextAddr = mbi.BaseAddress + (nint)mbi.RegionSize;
                    if (nextAddr <= address) break; // overflow protection
                    address = nextAddr;

                    if (matches.Count >= 200) break;
                }

                sb.AppendLine($"Searched {regionsSearched} memory regions");
                sb.AppendLine($"Found {matches.Count} matches");
                sb.AppendLine();

                // Show matches near the known unit data base
                var unitBase = _scanner.UnitDataBase;
                sb.AppendLine($"Unit data array at: 0x{unitBase:X}");
                sb.AppendLine();

                foreach (var (addr, ctx) in matches)
                {
                    long dist = Math.Abs((long)addr - (long)unitBase);
                    string proximity = dist < 0x100000 ? " ** NEAR UNIT DATA **" : "";
                    sb.AppendLine($"  0x{addr:X} (dist: 0x{dist:X}){proximity}");
                    sb.AppendLine($"    {ctx}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"ERROR: {ex.Message}");
            }

            var filePath = Path.Combine(_outputDirectory, $"search_{label}.txt");
            try { File.WriteAllText(filePath, sb.ToString()); }
            catch { /* ignore */ }
        }

        /// <summary>
        /// Searches ALL readable process memory for an arbitrary byte pattern.
        /// Returns matches as a list of (address, contextHex) tuples.
        /// Used to find data in heap memory that snapshot/diff can't reach.
        /// </summary>
        public List<(nint address, string context)> SearchBytesInAllMemory(byte[] pattern, int maxResults = 200)
        {
            var matches = new List<(nint address, string context)>();
            if (pattern == null || pattern.Length == 0) return matches;

            var process = Process.GetCurrentProcess();
            nint address = 0;
            long totalBytesSearched = 0;
            const long maxTotalBytes = 500_000_000L; // 500MB cap
            int regionsSearched = 0;

            while (matches.Count < maxResults && totalBytesSearched < maxTotalBytes)
            {
                if (VirtualQueryEx(process.Handle, address, out MEMORY_BASIC_INFORMATION mbi, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) == 0)
                    break;

                // Only search committed, read-write private memory (safest — heap allocations)
                bool isReadWrite = (mbi.Protect & 0x04) != 0; // PAGE_READWRITE only
                bool isCommitted = mbi.State == 0x1000;
                bool notGuard = (mbi.Protect & 0x100) == 0;
                bool notTooBig = (long)mbi.RegionSize <= 4_000_000; // 4MB max per region
                bool isPrivateOrMapped = mbi.Type == 0x20000 || mbi.Type == 0x40000; // MEM_PRIVATE or MEM_MAPPED

                if (isCommitted && isReadWrite && notGuard && notTooBig && isPrivateOrMapped)
                {
                    regionsSearched++;
                    byte[] regionBytes;
                    try
                    {
                        regionBytes = _scanner.ReadBytes(mbi.BaseAddress, (int)mbi.RegionSize);
                    }
                    catch
                    {
                        // Skip regions that fail to read
                        nint skip = mbi.BaseAddress + (nint)mbi.RegionSize;
                        if (skip <= address) break;
                        address = skip;
                        continue;
                    }
                    totalBytesSearched += regionBytes.Length;

                    for (int i = 0; i <= regionBytes.Length - pattern.Length; i++)
                    {
                        bool match = true;
                        for (int j = 0; j < pattern.Length; j++)
                        {
                            if (regionBytes[i + j] != pattern[j]) { match = false; break; }
                        }
                        if (match)
                        {
                            nint foundAddr = mbi.BaseAddress + i;
                            int ctxStart = Math.Max(0, i - 16);
                            int ctxEnd = Math.Min(regionBytes.Length, i + pattern.Length + 32);
                            var ctx = new byte[ctxEnd - ctxStart];
                            Array.Copy(regionBytes, ctxStart, ctx, 0, ctx.Length);
                            matches.Add((foundAddr, BitConverter.ToString(ctx).Replace("-", " ")));
                            if (matches.Count >= maxResults) break;
                        }
                    }
                }

                nint nextAddr = mbi.BaseAddress + (nint)mbi.RegionSize;
                if (nextAddr <= address) break;
                address = nextAddr;
            }

            ModLogger.Log($"[MemoryExplorer] SearchBytes: {regionsSearched} regions, {totalBytesSearched / 1024 / 1024}MB searched, {matches.Count} matches");
            return matches;
        }

        /// <summary>
        /// Searches a specific memory range for a uint16 value appearing as uint32 LE (value, 0x00, 0x00).
        /// Writes matches to file with surrounding context.
        /// </summary>
        public void SearchNearAddress(nint baseAddr, int rangeBytes, ushort value, string label)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Targeted search for {value} (0x{value:X4}) near 0x{baseAddr:X}, range: +/- 0x{rangeBytes:X}");
            sb.AppendLine();

            nint start = baseAddr - rangeBytes;
            int totalSize = rangeBytes * 2;
            var data = _scanner.ReadBytes(start, totalSize);

            if (data.Length == 0)
            {
                sb.AppendLine("ERROR: Could not read memory region");
            }
            else
            {
                byte lo = (byte)(value & 0xFF);
                byte hi = (byte)(value >> 8);
                int matchCount = 0;

                for (int i = 0; i < data.Length - 1; i++)
                {
                    if (data[i] == lo && data[i + 1] == hi)
                    {
                        matchCount++;
                        nint addr = start + i;
                        long offset = (long)addr - (long)baseAddr;
                        string sign = offset >= 0 ? "+" : "";

                        // 32 bytes context
                        int ctxStart = Math.Max(0, i - 16);
                        int ctxLen = Math.Min(48, data.Length - ctxStart);
                        var ctx = new byte[ctxLen];
                        Array.Copy(data, ctxStart, ctx, 0, ctxLen);

                        sb.AppendLine($"  0x{addr:X} (offset {sign}0x{offset:X})");
                        sb.AppendLine($"    {BitConverter.ToString(ctx).Replace("-", " ")}");
                    }
                }
                sb.AppendLine();
                sb.AppendLine($"Total matches: {matchCount}");
            }

            var filePath = Path.Combine(_outputDirectory, $"search_near_{label}.txt");
            try { File.WriteAllText(filePath, sb.ToString()); }
            catch { /* ignore */ }
        }

        /// <summary>
        /// Reads a block of bytes at an absolute address and returns as hex string.
        /// </summary>
        public string? ReadBlock(nint address, int count)
        {
            try
            {
                var raw = _scanner.ReadBytes(address, count);
                if (raw.Length == 0) return null;
                return BitConverter.ToString(raw).Replace("-", " ");
            }
            catch { return null; }
        }

        /// <summary>
        /// Reads 1, 2, or 4 bytes at an absolute memory address.
        /// Returns (value, rawBytes) or null if read fails.
        /// </summary>
        public (long value, byte[] raw)? ReadAbsolute(nint address, int size)
        {
            try
            {
                var raw = _scanner.ReadBytes(address, size);
                if (raw.Length == 0) return null;

                long value = size switch
                {
                    1 => raw[0],
                    2 => BitConverter.ToUInt16(raw, 0),
                    4 => BitConverter.ToUInt32(raw, 0),
                    _ => raw[0]
                };
                return (value, raw);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Reads multiple addresses in a single call. Much faster than individual ReadAbsolute calls
        /// since it avoids per-call overhead. Returns values in the same order as the input.
        /// </summary>
        public long[] ReadMultiple(ReadOnlySpan<(nint address, int size)> reads)
        {
            var results = new long[reads.Length];
            for (int i = 0; i < reads.Length; i++)
            {
                var (addr, size) = reads[i];
                try
                {
                    results[i] = size switch
                    {
                        1 => _scanner.ReadByte(addr),
                        2 => _scanner.ReadUInt16(addr),
                        4 => BitConverter.ToUInt32(_scanner.ReadBytes(addr, 4), 0),
                        _ => _scanner.ReadByte(addr)
                    };
                }
                catch
                {
                    results[i] = 0;
                }
            }
            return results;
        }

        [DllImport("kernel32.dll")]
        private static extern int VirtualQueryEx(nint hProcess, nint lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORY_BASIC_INFORMATION
        {
            public nint BaseAddress;
            public nint AllocationBase;
            public uint AllocationProtect;
            public nuint RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        /// <summary>
        /// Dumps all active units (for a quick overview).
        /// </summary>
        public void DumpAllActiveUnits()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"All Active Units - {DateTime.UtcNow:O}");
            sb.AppendLine();

            for (int i = 0; i < GameMemoryScanner.MaxUnitSlots; i++)
            {
                if (_scanner.IsUnitActive(i))
                {
                    sb.AppendLine($"--- Slot {i} ---");
                    sb.AppendLine(DumpUnit(i));
                    sb.AppendLine();
                }
            }

            var filePath = Path.Combine(_outputDirectory, "hexdump_all_active.txt");
            try
            {
                File.WriteAllText(filePath, sb.ToString());
                ModLogger.Log($"[MemoryExplorer] Dumped all active units to {filePath}");
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[MemoryExplorer] Failed to write dump: {ex.Message}");
            }
        }
        // ===== Differential Memory Scanner =====

        private readonly Dictionary<string, List<(nint baseAddr, byte[] data)>> _snapshots = new();

        /// <summary>
        /// Snapshots all writable memory regions in the game's main module.
        /// </summary>
        public void TakeSnapshot(string label)
        {
            var process = Process.GetCurrentProcess();
            var module = process.MainModule;
            if (module == null) return;

            nint moduleBase = module.BaseAddress;
            nint moduleEnd = moduleBase + module.ModuleMemorySize;
            var regions = new List<(nint baseAddr, byte[] data)>();
            long totalBytes = 0;

            nint address = moduleBase;
            while (address < moduleEnd)
            {
                if (VirtualQueryEx(process.Handle, address, out MEMORY_BASIC_INFORMATION mbi, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) == 0)
                    break;

                // Only snapshot committed, writable regions (skip code sections)
                bool isWritable = (mbi.Protect & 0x04) != 0  // PAGE_READWRITE
                               || (mbi.Protect & 0x40) != 0  // PAGE_EXECUTE_READWRITE
                               || (mbi.Protect & 0x08) != 0; // PAGE_WRITECOPY
                bool isCommitted = mbi.State == 0x1000; // MEM_COMMIT
                bool notGuard = (mbi.Protect & 0x100) == 0; // not PAGE_GUARD
                bool notTooLarge = (long)mbi.RegionSize <= 10_000_000; // skip >10MB regions

                if (isCommitted && isWritable && notGuard && notTooLarge)
                {
                    var data = _scanner.ReadBytes(mbi.BaseAddress, (int)mbi.RegionSize);
                    if (data.Length > 0)
                    {
                        regions.Add((mbi.BaseAddress, data));
                        totalBytes += data.Length;
                    }
                }

                nint next = mbi.BaseAddress + (nint)mbi.RegionSize;
                if (next <= address) break;
                address = next;
            }

            _snapshots[label] = regions;
            ModLogger.Log($"[MemoryExplorer] Snapshot '{label}': {regions.Count} regions, {totalBytes / 1024}KB");
        }

        /// <summary>
        /// Snapshots all readable, committed private/mapped RW heap memory.
        /// Same filter as SearchBytesInAllMemory but stores full regions for diffing.
        /// </summary>
        public void TakeHeapSnapshot(string label)
        {
            var process = Process.GetCurrentProcess();
            var regions = new List<(nint baseAddr, byte[] data)>();
            long totalBytes = 0;

            nint address = 0;
            while (true)
            {
                if (VirtualQueryEx(process.Handle, address, out MEMORY_BASIC_INFORMATION mbi,
                    (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) == 0)
                    break;

                bool isReadWrite = (mbi.Protect & 0x04) != 0;
                bool isCommitted = mbi.State == 0x1000;
                bool notGuard = (mbi.Protect & 0x100) == 0;
                bool notTooBig = (long)mbi.RegionSize <= 4_000_000;
                bool isPrivateOrMapped = mbi.Type == 0x20000 || mbi.Type == 0x40000;

                if (isCommitted && isReadWrite && notGuard && notTooBig && isPrivateOrMapped)
                {
                    try
                    {
                        var data = _scanner.ReadBytes(mbi.BaseAddress, (int)mbi.RegionSize);
                        if (data.Length > 0)
                        {
                            regions.Add((mbi.BaseAddress, data));
                            totalBytes += data.Length;
                        }
                    }
                    catch { }
                }

                nint next = mbi.BaseAddress + (nint)mbi.RegionSize;
                if (next <= address) break;
                address = next;
            }

            _snapshots[label] = regions;
            ModLogger.Log($"[MemoryExplorer] Heap snapshot '{label}': {regions.Count} regions, {totalBytes / 1024 / 1024}MB");
        }

        /// <summary>
        /// Diffs two snapshots and writes changed addresses to file.
        /// </summary>
        public void DiffSnapshots(string fromLabel, string toLabel, string outputLabel)
        {
            if (!_snapshots.ContainsKey(fromLabel) || !_snapshots.ContainsKey(toLabel))
            {
                ModLogger.LogError($"[MemoryExplorer] Missing snapshot: '{fromLabel}' or '{toLabel}'");
                return;
            }

            var fromRegions = _snapshots[fromLabel];
            var toRegions = _snapshots[toLabel];

            // Index 'to' regions by base address for fast lookup
            var toByBase = new Dictionary<nint, byte[]>();
            foreach (var (baseAddr, data) in toRegions)
                toByBase[baseAddr] = data;

            var sb = new StringBuilder();
            sb.AppendLine($"Diff: {fromLabel} → {toLabel}");
            sb.AppendLine($"Diffed at: {DateTime.UtcNow:O}");
            sb.AppendLine();

            int totalChanges = 0;
            var changes = new List<(nint addr, byte oldVal, byte newVal)>();

            foreach (var (baseAddr, fromData) in fromRegions)
            {
                if (!toByBase.TryGetValue(baseAddr, out var toData)) continue;
                int len = Math.Min(fromData.Length, toData.Length);

                for (int i = 0; i < len; i++)
                {
                    if (fromData[i] != toData[i])
                    {
                        changes.Add((baseAddr + i, fromData[i], toData[i]));
                        totalChanges++;
                    }
                }
            }

            sb.AppendLine($"Total changed bytes: {totalChanges}");
            sb.AppendLine();

            // Group by 16-byte aligned blocks for readability
            foreach (var (addr, oldVal, newVal) in changes)
            {
                // Show as byte and also try uint16/uint32 interpretation
                sb.AppendLine($"  0x{addr:X}: {oldVal:X2} → {newVal:X2}  (byte: {oldVal} → {newVal})");
            }

            var filePath = Path.Combine(_outputDirectory, $"diff_{outputLabel}.txt");
            try
            {
                File.WriteAllText(filePath, sb.ToString());
                ModLogger.Log($"[MemoryExplorer] Diff written: {totalChanges} changes to {filePath}");
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[MemoryExplorer] Failed to write diff: {ex.Message}");
            }
        }
    }
}
