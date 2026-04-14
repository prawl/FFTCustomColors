using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Scrapes the currently-open shop screen by finding UE4 FString records
    /// in the game's heap. Each FString has layout:
    ///   [vtable u64][length_in_chars u64][UTF-16LE text]
    ///
    /// Rather than hard-coding the vtable (which moves with ASLR on each
    /// restart), we scan for short ASCII-as-UTF16 strings preceded by a valid
    /// length field. This catches all UI FStrings — callers filter by whatever
    /// vocabulary they expect.
    /// </summary>
    public static class ShopItemScraper
    {
        // UE4 heap range observed on this build. Expanded upward to catch
        // regions 0x15xxxxxxxx and 0x18xxxxxxxx where shop widgets live.
        // Full game memory range. FString vtable discovery uses this; the
        // scrape itself narrows to the widget-heap range (0x140xxxxxxx..0x170xxxxxxx)
        // once a vtable is known.
        private const long DiscoveryHeapMin = 0x100000000L;
        private const long DiscoveryHeapMax = 0x200000000L;
        private const long UE4HeapMin = 0x140000000L;
        private const long UE4HeapMax = 0x170000000L;

        public record ScrapedItem(string Name, long Address);

        /// <summary>
        /// Discovers the FString vtable dynamically by searching for a known
        /// header string ("Weapons") that's always visible in an Outfitter_Buy
        /// shop on the weapons tab. Reads the -16 offset to extract the vtable
        /// pointer. Returns null if not found.
        /// </summary>
        private static byte[]? DiscoverFStringVtable(MemoryExplorer explorer)
        {
            // UTF-16LE bytes of "Weapons"
            byte[] needle = { 0x57, 0x00, 0x65, 0x00, 0x61, 0x00, 0x70, 0x00, 0x6F, 0x00, 0x6E, 0x00, 0x73, 0x00 };
            var matches = explorer.SearchBytesInAllMemory(
                needle, 20, DiscoveryHeapMin, DiscoveryHeapMax, broadSearch: true);

            foreach (var (addr, _) in matches)
            {
                // Look 16 bytes back for the vtable pointer.
                byte[] vtableBytes;
                try { vtableBytes = explorer.Scanner.ReadBytes(addr - 16, 8); }
                catch { continue; }
                if (vtableBytes.Length < 8) continue;
                // Valid user-space pointer? High bytes 0x00 0x00.
                if (vtableBytes[6] != 0 || vtableBytes[7] != 0) continue;
                // Plausibly in a DLL (0x7FF... or 0x7FE...).
                if (vtableBytes[5] != 0x7F) continue;
                // Check length at +8 after vtable: should be 14 for "Weapons".
                byte[] lenBytes;
                try { lenBytes = explorer.Scanner.ReadBytes(addr - 8, 8); }
                catch { continue; }
                if (lenBytes.Length < 8) continue;
                long len = BitConverter.ToInt64(lenBytes, 0);
                if (len != 14) continue;
                return vtableBytes;
            }
            return null;
        }

        /// <summary>
        /// Scans the UE4 heap for FString records containing short ASCII text.
        /// Returns the list ordered by memory address — on this build, lower
        /// addresses correlate roughly with earlier allocation, which matches
        /// UI row order for shop lists.
        /// </summary>
        public static List<ScrapedItem> ScrapeVisibleItems(MemoryExplorer explorer)
        {
            var vtable = DiscoverFStringVtable(explorer);
            if (vtable != null)
            {
                ModLogger.Log($"[ShopItemScraper] discovered vtable: {BitConverter.ToString(vtable)}");
                return ScrapeByVtable(explorer, vtable);
            }
            ModLogger.Log($"[ShopItemScraper] no vtable discovered, falling back to heuristic scan");
            // Strategy: walk readable heap regions in the UE4 range. For each
            // 8-byte-aligned position, try to interpret the next 16 bytes as
            // [vtable_ptr][length_u64] and the bytes after as UTF-16 text. If
            // the shape fits (small length, printable ASCII chars, null
            // terminator where expected), capture the string.

            var scanner = explorer.Scanner;
            nint address = (nint)UE4HeapMin;
            var found = new Dictionary<string, long>(StringComparer.Ordinal);
            long totalBytes = 0;
            int regionsScanned = 0;
            int regionsSkipped = 0;
            const long MaxBytes = 500_000_000L;

            while (totalBytes < MaxBytes)
            {
                if (VirtualQuery(address, out MEMORY_BASIC_INFORMATION mbi,
                                 (uint)System.Runtime.InteropServices.Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) == 0)
                    break;

                if ((long)mbi.BaseAddress >= UE4HeapMax) break;

                bool isCommitted = mbi.State == 0x1000;
                // PAGE_READWRITE=0x04, PAGE_EXECUTE_READWRITE=0x40, PAGE_WRITECOPY=0x08
                bool isReadWrite = (mbi.Protect & 0x04) != 0
                                || (mbi.Protect & 0x08) != 0
                                || (mbi.Protect & 0x40) != 0;
                bool notGuard = (mbi.Protect & 0x100) == 0;
                bool notTooBig = (long)mbi.RegionSize <= 16_000_000;

                if (isCommitted && isReadWrite && notGuard && notTooBig)
                {
                    byte[] regionBytes;
                    try
                    {
                        regionBytes = scanner.ReadBytes(mbi.BaseAddress, (int)mbi.RegionSize);
                    }
                    catch
                    {
                        address = mbi.BaseAddress + (nint)mbi.RegionSize;
                        continue;
                    }
                    totalBytes += regionBytes.Length;
                    regionsScanned++;
                    ExtractFStringsFromRegion(regionBytes, (long)mbi.BaseAddress, found);
                }
                else
                {
                    regionsSkipped++;
                }

                nint next = mbi.BaseAddress + (nint)mbi.RegionSize;
                if (next <= address) break;
                address = next;
            }

            ModLogger.Log($"[ShopItemScraper] scanned {regionsScanned} regions ({totalBytes / 1024 / 1024}MB), skipped {regionsSkipped}, matched {found.Count} items, reached 0x{(long)address:X}");
            return found
                .Select(kv => new ScrapedItem(kv.Key, kv.Value))
                .OrderBy(i => i.Address)
                .ToList();
        }

        private static List<ScrapedItem> ScrapeByVtable(MemoryExplorer explorer, byte[] vtable)
        {
            // Use the debugged SearchBytesInAllMemory helper (batched region
            // reads with proper cleanup) rather than rolling our own walker,
            // which caused GC pressure and OOM crashes at large scan sizes.
            var candidates = explorer.SearchBytesInAllMemory(
                vtable, maxResults: 20_000, minAddr: UE4HeapMin, maxAddr: UE4HeapMax,
                broadSearch: true);

            ModLogger.Log($"[ShopItemScraper] vtable candidates: {candidates.Count}");

            var found = new Dictionary<string, long>(StringComparer.Ordinal);
            var scanner = explorer.Scanner;
            int skipAlign = 0, skipCtxTooShort = 0, skipLenRange = 0, skipLenOdd = 0, skipNotAscii = 0, skipTooShort = 0;

            foreach (var (addr, ctxStr) in candidates)
            {
                // FString records are 8-byte aligned.
                if (((long)addr & 0x7) != 0) { skipAlign++; continue; }

                // Use the context bytes captured during the search itself —
                // re-reading via scanner.ReadBytes gives stale data when UE4
                // reallocates the FString between search and read. The context
                // is a hex string: "AA BB CC ..." with 16 bytes before the
                // match and 32+ bytes after. Parse inline.
                byte[] ctx = ParseHex(ctxStr);
                if (ctx.Length < 16 + 8 + 8 + 4) { skipCtxTooShort++; continue; }

                // The match position within ctx: first 16 bytes are "before"
                // (pre-vtable padding), so vtable starts at ctx[16]. Wait —
                // actually the search placed context as 16 bytes before +
                // pattern + 32 bytes after. So ctx[16..24] IS the vtable
                // (the pattern itself) and ctx[24..32] is the length field.
                int vtOffset = Math.Min(16, ctx.Length - (8 + 8));
                // Safe: vtOffset + 16 is length end, leaving ctx.Length - (vtOffset + 16)
                // bytes for text.

                long lenBytes = BitConverter.ToInt64(ctx, vtOffset + 8);
                if (lenBytes < 4 || lenBytes > 80) { skipLenRange++; continue; }
                if ((lenBytes & 1) != 0) { skipLenOdd++; continue; }

                int textOffset = vtOffset + 16;
                int textAvail = ctx.Length - textOffset;
                int textToRead = (int)Math.Min(lenBytes, textAvail);
                // If we don't have full text, still try — short strings fit.

                var sb = new StringBuilder((int)(textToRead / 2));
                bool valid = true;
                for (int i = 0; i + 1 < textToRead; i += 2)
                {
                    byte lo = ctx[textOffset + i];
                    byte hi = ctx[textOffset + i + 1];
                    if (hi != 0) { valid = false; break; }
                    char c = (char)lo;
                    if (c < 0x20 || c > 0x7E) { valid = false; break; }
                    sb.Append(c);
                }
                if (!valid)
                {
                    if (skipNotAscii < 3)
                    {
                        ModLogger.Log($"[ShopItemScraper] notAscii sample 0x{(long)addr:X} len={lenBytes} ctx={ctxStr}");
                    }
                    skipNotAscii++;
                    continue;
                }
                if (sb.Length < 2) { skipTooShort++; continue; }

                var text = sb.ToString();
                long a = (long)addr + 16;
                if (!found.TryGetValue(text, out var existing) || a < existing)
                    found[text] = a;
            }

            ModLogger.Log($"[ShopItemScraper] extracted {found.Count} strings before filter; rejects: align={skipAlign} shortCtx={skipCtxTooShort} lenRange={skipLenRange} lenOdd={skipLenOdd} notAscii={skipNotAscii} tooShort={skipTooShort}");
            var sample = string.Join(", ", found.Keys.Take(30));
            ModLogger.Log($"[ShopItemScraper] sample: {sample}");

            var filtered = found
                .Where(kv => LooksLikeItemName(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
            ModLogger.Log($"[ShopItemScraper] after filter: {filtered.Count} item-like strings");

            return filtered
                .Select(kv => new ScrapedItem(kv.Key, kv.Value))
                .OrderBy(i => i.Address)
                .ToList();
        }

        private static void ExtractFStringsFromRegion(byte[] region, long baseAddr, Dictionary<string, long> found)
        {
            // At each 8-byte-aligned offset, check if bytes look like:
            //   [vtable_ptr][length_u64] followed by length*2 bytes of UTF-16
            // The vtable looks like a readable pointer: high bits should be
            // 0x00007FFD..0x00007FFF (typical for loaded modules in user
            // space on Windows x64).
            for (int i = 0; i + 32 <= region.Length; i += 8)
            {
                // Vtable candidate: 8 bytes forming a plausible pointer.
                // High 2 bytes must be 0x00 00 (user-space pointer).
                if (region[i + 6] != 0 || region[i + 7] != 0) continue;
                // Low 6 bytes shouldn't be all zero.
                bool nonzero = false;
                for (int k = 0; k < 6; k++) if (region[i + k] != 0) { nonzero = true; break; }
                if (!nonzero) continue;

                // Length candidate: u64 at offset +8. This is the byte count of
                // the UTF-16 text (NOT the char count — verified live on
                // "Weapons" = 7 chars stored as length=14 bytes).
                long lenBytes = BitConverter.ToInt64(region, i + 8);
                if (lenBytes < 4 || lenBytes > 80) continue;
                if ((lenBytes & 1) != 0) continue; // must be even

                int textStart = i + 16;
                if (textStart + (int)lenBytes > region.Length) continue;

                // Decode UTF-16LE — bail on any non-printable-ASCII char.
                int charCount = (int)(lenBytes / 2);
                var sb = new StringBuilder(charCount);
                bool valid = true;
                for (int j = 0; j < lenBytes; j += 2)
                {
                    byte lo = region[textStart + j];
                    byte hi = region[textStart + j + 1];
                    if (hi != 0) { valid = false; break; }
                    char c = (char)lo;
                    if (c < 0x20 || c > 0x7E) { valid = false; break; }
                    sb.Append(c);
                }
                if (!valid) continue;

                var text = sb.ToString();
                if (!LooksLikeItemName(text)) continue;

                long addr = baseAddr + textStart;
                if (!found.TryGetValue(text, out var existing) || addr < existing)
                    found[text] = addr;
            }
        }

        private static byte[] ParseHex(string hex)
        {
            // Input like "AA BB CC DD"
            var parts = hex.Split(' ');
            var bytes = new byte[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length != 2) return Array.Empty<byte>();
                bytes[i] = Convert.ToByte(parts[i], 16);
            }
            return bytes;
        }

        private static bool LooksLikeItemName(string s)
        {
            if (s.Length < 2) return false;
            if (!char.IsLetterOrDigit(s[0])) return false;
            if (s.Contains('/') || s.Contains('\\') || s.Contains(':') || s.Contains('?') || s.Contains('=')) return false;
            bool hasLetter = false;
            foreach (var c in s) if (char.IsLetter(c)) { hasLetter = true; break; }
            return hasLetter;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern int VirtualQuery(IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MEMORY_BASIC_INFORMATION
        {
            public nint BaseAddress;
            public nint AllocationBase;
            public uint AllocationProtect;
            public ushort PartitionId;
            public nuint RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }
    }
}
