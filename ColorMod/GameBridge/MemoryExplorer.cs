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
        /// Searches ALL readable memory regions (not just PAGE_READWRITE) for an arbitrary byte pattern.
        /// Includes PAGE_READONLY, PAGE_EXECUTE_READ, PAGE_WRITECOPY, etc.
        /// Use this when text or data may be in read-only or code sections.
        /// </summary>
        public List<(nint address, string context)> SearchBytesAllRegions(byte[] pattern, int maxResults = 200)
        {
            var matches = new List<(nint address, string context)>();
            if (pattern == null || pattern.Length == 0) return matches;

            var process = Process.GetCurrentProcess();
            nint address = 0;
            long totalBytesSearched = 0;
            const long maxTotalBytes = 2_000_000_000L; // 2GB cap (broader search needs more room)
            int regionsSearched = 0;

            while (matches.Count < maxResults && totalBytesSearched < maxTotalBytes)
            {
                if (VirtualQueryEx(process.Handle, address, out MEMORY_BASIC_INFORMATION mbi, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) == 0)
                    break;

                // Search ALL committed, readable memory (not just read-write)
                bool isCommitted = mbi.State == 0x1000;
                bool isReadable = (mbi.Protect & 0xEE) != 0; // PAGE_READONLY, PAGE_READWRITE, PAGE_EXECUTE_READ, etc.
                bool notGuard = (mbi.Protect & 0x100) == 0;
                bool notTooBig = (long)mbi.RegionSize <= 16_000_000; // 16MB max per region

                if (isCommitted && isReadable && notGuard && notTooBig)
                {
                    regionsSearched++;
                    byte[] regionBytes;
                    try
                    {
                        regionBytes = _scanner.ReadBytes(mbi.BaseAddress, (int)mbi.RegionSize);
                    }
                    catch
                    {
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

            ModLogger.Log($"[MemoryExplorer] SearchBytesAllRegions: {regionsSearched} regions, {totalBytesSearched / 1024 / 1024}MB searched, {matches.Count} matches");
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
        /// Searches heap memory for the battle scenario struct and returns the MAP ID.
        /// The scenario struct has location ID at +0x24 and MAP ID at +0x30 (both uint32).
        /// Searches PAGE_READWRITE heap regions for a uint32 value in the valid MAP range (1-127)
        /// at offset +0x0C from a uint32 matching a known location ID pattern.
        /// Returns -1 if not found.
        /// </summary>
        public int FindScenarioMapId(int locationHint = -1)
        {
            try
            {
                // Search heap for the battle scenario struct.
                // The struct has location ID (uint32) at +0x24 and map ID (uint32) at +0x30.
                // Between them (+0x28 to +0x2F) are: unknown, music ID — 8 bytes.
                // We collect ALL matches and return the most plausible one.
                using var process = Process.GetCurrentProcess();
                nint address = nint.Zero;
                int regionsSearched = 0;
                long totalBytes = 0;
                var candidates = new List<(int mapId, int locId, int scenarioId, nint addr)>();

                while (regionsSearched < 500 && totalBytes < 300_000_000)
                {
                    if (VirtualQueryEx(process.Handle, address, out MEMORY_BASIC_INFORMATION mbi, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) == 0)
                        break;

                    nint nextAddr = mbi.BaseAddress + (nint)mbi.RegionSize;
                    if (nextAddr <= address) break;
                    address = nextAddr;

                    bool isReadWrite = (mbi.Protect & 0x04) != 0;
                    bool isCommitted = mbi.State == 0x1000;
                    bool notGuard = (mbi.Protect & 0x100) == 0;
                    bool notTooBig = (long)mbi.RegionSize <= 4_000_000;
                    bool isPrivateOrMapped = mbi.Type == 0x20000 || mbi.Type == 0x40000;

                    if (!(isCommitted && isReadWrite && notGuard && notTooBig && isPrivateOrMapped))
                        continue;

                    regionsSearched++;
                    byte[] regionBytes;
                    try { regionBytes = _scanner.ReadBytes(mbi.BaseAddress, (int)mbi.RegionSize); }
                    catch { continue; }
                    totalBytes += regionBytes.Length;

                    // Search for pattern: locId(1-42) at +0x24, mapId(1-127) at +0x30
                    for (int i = 0x24; i <= regionBytes.Length - 0x3C; i += 4)
                    {
                        uint locId = BitConverter.ToUInt32(regionBytes, i);
                        if (locId < 1 || locId > 42) continue;

                        uint mapId = BitConverter.ToUInt32(regionBytes, i + 0x0C);
                        if (mapId < 1 || mapId > 127) continue;

                        // Struct base validation
                        uint scenarioId = BitConverter.ToUInt32(regionBytes, i - 0x24);
                        if (scenarioId < 50 || scenarioId > 600) continue;

                        uint subType = BitConverter.ToUInt32(regionBytes, i - 0x20);
                        if (subType > 20) continue;

                        // Fields between location and map should be plausible
                        uint field28 = BitConverter.ToUInt32(regionBytes, i + 0x04); // +0x28
                        uint musicId = BitConverter.ToUInt32(regionBytes, i + 0x08); // +0x2C
                        if (musicId > 200) continue;
                        if (field28 > 200) continue;

                        uint surfaceType = BitConverter.ToUInt32(regionBytes, i + 0x10); // +0x34
                        if (surfaceType > 50) continue;

                        if (i + 0x38 >= regionBytes.Length) continue;
                        uint teams = BitConverter.ToUInt32(regionBytes, i + 0x38);
                        if (teams < 1 || teams > 4) continue;

                        nint foundAddr = mbi.BaseAddress + i - 0x24;
                        candidates.Add(((int)mapId, (int)locId, (int)scenarioId, foundAddr));
                    }
                }

                if (candidates.Count > 0)
                {
                    // Write all candidates to a file for debugging
                    try
                    {
                        var debugPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".", "claude_bridge", "map_candidates.txt");
                        var lines = new List<string> { $"Found {candidates.Count} candidates ({regionsSearched} regions, {totalBytes/1024}KB):" };
                        foreach (var c in candidates)
                            lines.Add($"  scenario={c.scenarioId} loc={c.locId} map={c.mapId} at 0x{c.addr:X}");
                        File.WriteAllLines(debugPath, lines);
                    }
                    catch { /* best effort */ }

                    // Log all candidates
                    foreach (var c in candidates)
                        ModLogger.Log($"[Map] Candidate: scenario={c.scenarioId} loc={c.locId} map={c.mapId} at 0x{c.addr:X}");

                    // If we have a location hint, filter to candidates matching that location
                    if (locationHint >= 0)
                    {
                        var locMatches = candidates.FindAll(c => c.locId == locationHint);
                        if (locMatches.Count > 0)
                        {
                            var best2 = locMatches[0];
                            ModLogger.Log($"[Map] Selected (loc hint={locationHint}): scenario={best2.scenarioId} loc={best2.locId} map={best2.mapId} at 0x{best2.addr:X}");
                            return best2.mapId;
                        }
                    }

                    // Filter to low-address candidates (< 0x10000000) which are live battle data,
                    // not the massive lookup tables at high addresses (0x42xx+, 0x43xx+)
                    var lowAddr = candidates.FindAll(c => (long)c.addr < 0x10000000);
                    if (lowAddr.Count > 0)
                    {
                        // Take the one with the highest scenario ID among low-address matches
                        var best3 = lowAddr[0];
                        foreach (var c in lowAddr)
                            if (c.scenarioId > best3.scenarioId)
                                best3 = c;
                        ModLogger.Log($"[Map] Selected (low-addr, {lowAddr.Count} candidates): scenario={best3.scenarioId} loc={best3.locId} map={best3.mapId} at 0x{best3.addr:X}");
                        return best3.mapId;
                    }

                    // Fallback: use first match at lowest address
                    var best = candidates[0];

                    ModLogger.Log($"[Map] Selected: scenario={best.scenarioId} loc={best.locId} map={best.mapId} at 0x{best.addr:X}");
                    return best.mapId;
                }

                ModLogger.Log($"[Map] Scenario struct not found ({regionsSearched} regions, {totalBytes / 1024}KB searched)");
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[Map] FindScenarioMapId error: {ex.Message}");
            }
            return -1;
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
