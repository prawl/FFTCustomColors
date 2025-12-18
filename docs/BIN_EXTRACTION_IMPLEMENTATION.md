# BIN Extraction Implementation Guide (Solution #4)

## Overview
Dynamic extraction of sprite directions directly from .bin files at runtime, eliminating the need for pre-generated PNG files.

## Benefits
- **Zero Storage Increase**: Uses existing .bin files already in the mod
- **All 8 Directions**: Can extract N, S, E, W plus corners
- **Dynamic**: Automatically supports new themes without regeneration
- **Memory Efficient**: Only caches what's currently viewed

## Implementation Steps (TDD Approach)

### Step 1: Create Test Project Structure

```csharp
// Tests/SpriteExtraction/BinSpriteExtractorTests.cs
using NUnit.Framework;
using System.Drawing;
using System.IO;

[TestFixture]
public class BinSpriteExtractorTests
{
    private BinSpriteExtractor _extractor;
    private byte[] _testSpriteData;

    [SetUp]
    public void Setup()
    {
        _extractor = new BinSpriteExtractor();
        // Load a test sprite file for testing
        _testSpriteData = File.ReadAllBytes(@"test_data\battle_mina_m_spr.bin");
    }

    [Test]
    public void ReadPalette_ShouldExtract16Colors()
    {
        // Arrange
        var data = _testSpriteData;

        // Act
        var palette = _extractor.ReadPalette(data, 0);

        // Assert
        Assert.AreEqual(16, palette.Length);
        Assert.AreEqual(Color.Transparent, palette[0]); // First color should be transparent
    }

    [Test]
    public void ExtractSprite_ShouldReturnCorrectDimensions()
    {
        // Arrange
        var data = _testSpriteData;

        // Act
        var sprite = _extractor.ExtractSprite(data, 0, 0);

        // Assert
        Assert.AreEqual(64, sprite.Width);
        Assert.AreEqual(64, sprite.Height);
    }

    [Test]
    public void ExtractAllDirections_ShouldReturn8Images()
    {
        // Arrange
        var binPath = @"test_data\battle_mina_m_spr.bin";

        // Act
        var sprites = _extractor.ExtractAllDirections(binPath);

        // Assert
        Assert.AreEqual(8, sprites.Length);
        Assert.IsNotNull(sprites[0]); // West
        Assert.IsNotNull(sprites[6]); // East (mirrored)
    }

    [Test]
    public void MirrorImage_ShouldFlipHorizontally()
    {
        // Arrange
        var original = new Bitmap(10, 10);
        original.SetPixel(2, 5, Color.Red);

        // Act
        var mirrored = _extractor.MirrorImage(original);

        // Assert
        Assert.AreEqual(Color.Red, mirrored.GetPixel(7, 5));
    }

    [Test]
    public void Cache_ShouldReturnSameInstanceOnSecondCall()
    {
        // Arrange
        var binPath = @"test_data\battle_mina_m_spr.bin";

        // Act
        var sprites1 = _extractor.ExtractAllDirections(binPath);
        var sprites2 = _extractor.ExtractAllDirections(binPath);

        // Assert
        Assert.AreSame(sprites1, sprites2);
    }
}
```

### Step 2: Implement Core Extraction Logic

```csharp
// ColorMod/SpriteExtraction/BinSpriteExtractor.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

public interface IBinSpriteExtractor
{
    Image[] ExtractAllDirections(string binPath);
    Image ExtractSprite(byte[] data, int index, int paletteIndex);
    Color[] ReadPalette(byte[] data, int paletteIndex);
    Image MirrorImage(Image original);
}

public class BinSpriteExtractor : IBinSpriteExtractor
{
    private readonly Dictionary<string, Image[]> _cache = new Dictionary<string, Image[]>();

    // Sprite sheet layout (indices in the .bin file)
    private readonly int[] SPRITE_INDICES = new int[]
    {
        0,  // West (facing left)
        1,  // Southwest
        2,  // South (facing down)
        3,  // Northwest
        4,  // North (facing up/away)
        // 5-7 will be created by mirroring
    };

    public Image[] ExtractAllDirections(string binPath)
    {
        // Check cache first
        if (_cache.ContainsKey(binPath))
        {
            return _cache[binPath];
        }

        if (!File.Exists(binPath))
        {
            throw new FileNotFoundException($"Sprite file not found: {binPath}");
        }

        var data = File.ReadAllBytes(binPath);
        var sprites = new Image[8];

        // Extract the 5 base directions
        for (int i = 0; i < 5; i++)
        {
            sprites[i] = ExtractSprite(data, SPRITE_INDICES[i], 0);
        }

        // Create mirrored versions for East directions
        sprites[5] = MirrorImage(sprites[3]); // Northeast from Northwest
        sprites[6] = MirrorImage(sprites[0]); // East from West
        sprites[7] = MirrorImage(sprites[1]); // Southeast from Southwest

        // Cache the result
        _cache[binPath] = sprites;

        return sprites;
    }

    public Image ExtractSprite(byte[] data, int spriteIndex, int paletteIndex)
    {
        var palette = ReadPalette(data, paletteIndex);

        // Skip palette data (512 bytes) to get to sprite data
        int spriteDataOffset = 512;

        // Each sprite is 32x40 pixels in the sheet
        int spriteWidth = 32;
        int spriteHeight = 40;
        int sheetWidth = 256;

        // Calculate position in sprite sheet
        int xOffset = spriteIndex * 32;
        int yOffset = 0;

        // Create bitmap for the extracted sprite
        var bitmap = new Bitmap(64, 64, PixelFormat.Format32bppArgb);

        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
        }

        // Extract sprite pixels
        for (int y = 0; y < spriteHeight; y++)
        {
            for (int x = 0; x < spriteWidth; x++)
            {
                int sheetX = xOffset + x;
                int sheetY = yOffset + y;

                // Calculate byte position in sprite data
                int pixelIndex = (sheetY * sheetWidth) + sheetX;
                int byteIndex = spriteDataOffset + (pixelIndex / 2);

                if (byteIndex < data.Length)
                {
                    byte pixelByte = data[byteIndex];

                    // Get 4-bit color index (alternates between low and high nibble)
                    int colorIndex = (pixelIndex % 2 == 0)
                        ? (pixelByte & 0x0F)        // Low nibble
                        : ((pixelByte >> 4) & 0x0F); // High nibble

                    if (colorIndex < palette.Length)
                    {
                        // Scale 2x and center in 64x64 frame
                        var color = palette[colorIndex];
                        for (int sy = 0; sy < 2; sy++)
                        {
                            for (int sx = 0; sx < 2; sx++)
                            {
                                int destX = (x * 2) + sx;
                                int destY = (y * 2) + sy - 8; // Offset up slightly

                                if (destX >= 0 && destX < 64 && destY >= 0 && destY < 64)
                                {
                                    bitmap.SetPixel(destX, destY, color);
                                }
                            }
                        }
                    }
                }
            }
        }

        return bitmap;
    }

    public Color[] ReadPalette(byte[] data, int paletteIndex)
    {
        var palette = new Color[16];
        int baseOffset = paletteIndex * 32; // Each palette is 16 colors * 2 bytes

        for (int i = 0; i < 16; i++)
        {
            int offset = baseOffset + (i * 2);

            if (offset + 1 < data.Length)
            {
                // Read 16-bit color value (little endian)
                ushort bgr555 = BitConverter.ToUInt16(data, offset);

                // Convert BGR555 to RGB888
                int b = ((bgr555 >> 10) & 0x1F) * 255 / 31;
                int g = ((bgr555 >> 5) & 0x1F) * 255 / 31;
                int r = (bgr555 & 0x1F) * 255 / 31;

                // First color is typically background, make it transparent
                if (i == 0)
                {
                    palette[i] = Color.Transparent;
                }
                else
                {
                    palette[i] = Color.FromArgb(255, r, g, b);
                }
            }
            else
            {
                palette[i] = Color.Black;
            }
        }

        return palette;
    }

    public Image MirrorImage(Image original)
    {
        var bitmap = new Bitmap(original);
        bitmap.RotateFlip(RotateFlipType.RotateNoneFlipX);
        return bitmap;
    }

    public void ClearCache()
    {
        foreach (var sprites in _cache.Values)
        {
            foreach (var sprite in sprites)
            {
                sprite?.Dispose();
            }
        }
        _cache.Clear();
    }
}
```

### Step 3: Integrate with CharacterRowBuilder

```csharp
// Modify CharacterRowBuilder.cs
private readonly IBinSpriteExtractor _binExtractor = new BinSpriteExtractor();

private void UpdateGenericPreviewImages(PreviewCarousel carousel, string jobName, string theme)
{
    string fileName = jobName.ToLower()
        .Replace(" (male)", "_male")
        .Replace(" (female)", "_female")
        .Replace(" ", "_")
        .Replace("(", "")
        .Replace(")", "");

    // Try to load from .bin file first
    var binPath = GetSpriteBinPath(fileName, theme);

    if (File.Exists(binPath))
    {
        try
        {
            var sprites = _binExtractor.ExtractAllDirections(binPath);

            // Reorder for carousel: SW, SE, NE, NW, N, S, E, W
            var carouselOrder = new[] { 1, 7, 5, 3, 4, 2, 6, 0 };
            var orderedSprites = new Image[8];

            for (int i = 0; i < carouselOrder.Length; i++)
            {
                orderedSprites[i] = sprites[carouselOrder[i]];
            }

            carousel.SetImages(orderedSprites);
            ModLogger.LogSuccess($"Loaded 8 directional views from .bin for {jobName} - {theme}");
            return;
        }
        catch (Exception ex)
        {
            ModLogger.LogError($"Failed to extract from .bin: {ex.Message}");
            // Fall back to embedded resources
        }
    }

    // Fall back to embedded PNG resources (existing code)
    // ...
}

private string GetSpriteBinPath(string jobName, string theme)
{
    // Map job names to sprite file names
    var spriteMap = new Dictionary<string, string>
    {
        { "squire_male", "battle_mina_m_spr.bin" },
        { "squire_female", "battle_mina_w_spr.bin" },
        { "knight_male", "battle_knight_m_spr.bin" },
        { "knight_female", "battle_knight_w_spr.bin" },
        // ... add all mappings
    };

    if (!spriteMap.ContainsKey(jobName))
        return null;

    var spriteName = spriteMap[jobName];
    var themePath = theme == "original" ? "sprites_original" : $"sprites_{theme}";

    return Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
        "..", "..",
        "FFTIVC", "data", "enhanced", "fftpack", "unit",
        themePath,
        spriteName
    );
}
```

### Step 4: Performance Optimization

```csharp
// Add lazy loading and preloading support
public class BinSpriteExtractor : IBinSpriteExtractor
{
    private readonly object _cacheLock = new object();

    public async Task PreloadCharacterAsync(string characterName, IEnumerable<string> themes)
    {
        await Task.Run(() =>
        {
            foreach (var theme in themes)
            {
                var binPath = GetSpriteBinPath(characterName, theme);
                if (File.Exists(binPath))
                {
                    ExtractAllDirections(binPath); // This will cache it
                }
            }
        });
    }

    // Thread-safe cache access
    public Image[] ExtractAllDirections(string binPath)
    {
        lock (_cacheLock)
        {
            if (_cache.ContainsKey(binPath))
            {
                return _cache[binPath];
            }
        }

        // Extract sprites...
        var sprites = /* extraction logic */;

        lock (_cacheLock)
        {
            _cache[binPath] = sprites;
        }

        return sprites;
    }
}
```

## Testing Strategy

1. **Unit Tests**: Test each method in isolation
2. **Integration Tests**: Test with actual .bin files
3. **Performance Tests**: Measure extraction time
4. **Memory Tests**: Ensure proper disposal and caching
5. **UI Tests**: Verify carousel updates correctly

## Migration Path

1. **Phase 1**: Implement extractor with tests
2. **Phase 2**: Add as optional feature (fallback to PNGs)
3. **Phase 3**: Remove PNG resources after validation
4. **Phase 4**: Optimize caching and preloading

## Expected Results

- **Extraction Time**: ~50ms per character (first load)
- **Cache Hit Time**: <1ms
- **Memory Usage**: ~2MB for cached sprites (disposed on form close)
- **Storage Savings**: 3.2MB (all PNG files removed)

## Error Handling

```csharp
public class BinExtractionException : Exception
{
    public string BinPath { get; }
    public int SpriteIndex { get; }

    public BinExtractionException(string message, string binPath, int spriteIndex, Exception inner)
        : base(message, inner)
    {
        BinPath = binPath;
        SpriteIndex = spriteIndex;
    }
}
```

## Debugging Tools

```csharp
// Add debug output for extraction
public class DebugBinExtractor : BinSpriteExtractor
{
    protected override void LogExtraction(string binPath, int index)
    {
        ModLogger.LogDebug($"[BIN] Extracting sprite {index} from {Path.GetFileName(binPath)}");
    }

    protected override void LogCache(string binPath, bool hit)
    {
        ModLogger.LogDebug($"[BIN] Cache {(hit ? "HIT" : "MISS")}: {Path.GetFileName(binPath)}");
    }
}
```

## Final Checklist

- [ ] All tests passing (aim for 95%+ coverage)
- [ ] Performance meets targets (<50ms extraction)
- [ ] Memory properly managed (no leaks)
- [ ] Falls back gracefully to PNGs
- [ ] Works with all characters and themes
- [ ] Cache persists during session
- [ ] Proper error messages for missing files
- [ ] Debug logging can be enabled/disabled