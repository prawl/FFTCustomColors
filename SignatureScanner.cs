using System;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;
using Reloaded.Memory.Sigscan.Definitions.Structs;
using Reloaded.Hooks.Definitions;

namespace FFTColorMod
{
    public class SignatureScanner
    {
        public IReloadedHooks? Hooks { get; private set; }
        public PaletteDetector? PaletteDetector { get; private set; }
        public string ColorScheme { get; private set; } = "original";

        public void AddScan(IStartupScanner scanner, string pattern, string name, Action<PatternScanResult> onFound)
        {
            scanner.AddMainModuleScan(pattern, onFound);
        }

        public void SetupHooks(IStartupScanner scanner)
        {
            // Add a basic sprite loading pattern
            scanner.AddMainModuleScan("48 8B C4", result => { });
        }

        public void SetupHooks(IStartupScanner scanner, IReloadedHooks hooks)
        {
            // Store the hooks reference
            Hooks = hooks;
            SetupHooks(scanner);
        }

        public void SetPaletteDetector(PaletteDetector detector)
        {
            PaletteDetector = detector;
        }

        public void SetColorScheme(string scheme)
        {
            ColorScheme = scheme;
        }

        public unsafe IntPtr ProcessSpriteData(IntPtr spriteData, int size)
        {
            // Safety checks
            if (spriteData == IntPtr.Zero || size <= 0 || PaletteDetector == null)
            {
                return spriteData;
            }

            Console.WriteLine($"[SignatureScanner] ProcessSpriteData: Checking sprite at 0x{spriteData.ToInt64():X}, size={size}");

            try
            {
                // Check if this is a palette (256 colors * 3 bytes = 768 bytes minimum)
                if (size >= 768)
                {
                    byte* data = (byte*)spriteData;

                    // Use PaletteDetector to identify if this is a Ramza palette
                    // Convert pointer to byte array for detector
                    byte[] paletteData = new byte[768];
                    for (int i = 0; i < 768; i++)
                    {
                        paletteData[i] = data[i];
                    }
                    int chapter = PaletteDetector.DetectChapterOutfit(paletteData, 0);

                    if (chapter > 0)
                    {
                        Console.WriteLine($"[SignatureScanner] Detected Chapter {chapter} Ramza palette!");

                        // Apply color scheme if not original
                        if (ColorScheme != "original")
                        {
                            Console.WriteLine($"[SignatureScanner] Applying {ColorScheme} color scheme");

                            // Apply RED color to main tunic colors
                            if (ColorScheme == "red")
                            {
                                // Modify the brown colors to red
                                // Original brown: 0x17, 0x2C, 0x4A (BGR)
                                // Red color: 0x00, 0x00, 0xFF (BGR)

                                for (int i = 0; i < 256; i++)
                                {
                                    int offset = i * 3;

                                    // Check if this is a brown color
                                    if (data[offset] == 0x17 && data[offset + 1] == 0x2C && data[offset + 2] == 0x4A)
                                    {
                                        // Change to red
                                        data[offset] = 0x00;     // Blue
                                        data[offset + 1] = 0x00; // Green
                                        data[offset + 2] = 0xFF; // Red
                                        Console.WriteLine($"[SignatureScanner] Modified color at index {i}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SignatureScanner] Error in ProcessSpriteData: {ex.Message}");
            }

            return spriteData;
        }
    }
}