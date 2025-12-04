using System;
using System.Collections.Generic;
using System.Diagnostics;
using Reloaded.Memory.Sigscan;
using Reloaded.Memory.Sigscan.Definitions.Structs;

namespace FFTColorMod
{
    // TLDR: Scans process memory for byte patterns without IStartupScanner
    public class ManualMemoryScanner
    {
        public List<PatternScanResult> ScanForPattern(Process process, string pattern)
        {
            // TLDR: Scans process memory for the given byte pattern
            var results = new List<PatternScanResult>();

            try
            {
                // Get process main module
                var mainModule = process.MainModule;
                if (mainModule == null) return results;

                var baseAddress = mainModule.BaseAddress;
                var moduleSize = mainModule.ModuleMemorySize;

                // Create scanner with process memory
                unsafe
                {
                    var scanner = new Scanner((byte*)baseAddress.ToPointer(), moduleSize);

                    // Scan for pattern
                    var result = scanner.FindPattern(pattern);
                    if (result.Found)
                    {
                        results.Add(result);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManualMemoryScanner] Error scanning: {ex.Message}");
            }

            return results;
        }

        public void ScanForPattern(Process process, string pattern, Action<long> onPatternFound)
        {
            // TLDR: Scans and executes callback for each pattern found
            var results = ScanForPattern(process, pattern);
            foreach (var result in results)
            {
                if (result.Found)
                {
                    onPatternFound(result.Offset);
                }
            }
        }
    }
}