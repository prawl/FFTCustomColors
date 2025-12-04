using System.Threading;

namespace FFTColorMod;

public class GameIntegration
{
    public MemoryScanner MemoryScanner { get; private set; }
    public PaletteDetector PaletteDetector { get; private set; }
    public HotkeyManager HotkeyManager { get; private set; }
    public bool IsInitialized { get; private set; }
    public bool IsMonitoring { get; private set; }
    public string? LastAppliedScheme { get; private set; }
    public int LastPaletteOffset { get; private set; } = -1;

    private byte[]? _testMemory;
    private Thread? _monitoringThread;
    private bool _stopRequested;

    public GameIntegration()
    {
        MemoryScanner = new MemoryScanner();
        PaletteDetector = new PaletteDetector();
        HotkeyManager = new HotkeyManager();
        IsInitialized = true;
    }

    public void StartMonitoring()
    {
        if (!IsMonitoring)
        {
            IsMonitoring = true;
            _stopRequested = false;
        }
    }

    public void StopMonitoring()
    {
        _stopRequested = true;
        IsMonitoring = false;
        _monitoringThread?.Join(1000); // Wait max 1 second
    }

    public void SetTestMemory(byte[] memory)
    {
        _testMemory = memory;
    }

    public void ProcessHotkey(int keyCode)
    {
        // Process the hotkey
        HotkeyManager.ProcessHotkey(keyCode);

        // Apply the color scheme if we have test memory
        if (_testMemory != null && HotkeyManager.CurrentScheme != "original")
        {
            var paletteOffsets = MemoryScanner.ScanForPalettes(_testMemory, PaletteDetector);
            if (paletteOffsets.Count > 0)
            {
                LastPaletteOffset = paletteOffsets[0];
                LastAppliedScheme = HotkeyManager.CurrentScheme;

                // Detect chapter and apply colors
                int chapter = PaletteDetector.DetectChapterOutfit(_testMemory, LastPaletteOffset);
                if (chapter > 0)
                {
                    MemoryScanner.ApplyColorScheme(_testMemory, LastPaletteOffset,
                        HotkeyManager.CurrentScheme, PaletteDetector, chapter);
                }
            }
        }
    }
}