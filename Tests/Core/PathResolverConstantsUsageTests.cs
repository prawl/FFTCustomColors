using System;
using System.IO;
using FFTColorMod.Core;
using FFTColorMod.Services;
using Xunit;

namespace FFTColorMod.Tests.Core
{
    public class PathResolverConstantsUsageTests : IDisposable
    {
        private readonly string _testRoot;
        private readonly PathResolver _resolver;

        public PathResolverConstantsUsageTests()
        {
            _testRoot = Path.Combine(Path.GetTempPath(), $"FFTColorModTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testRoot);

            var sourcePath = Path.Combine(_testRoot, "source");
            var userPath = Path.Combine(_testRoot, "user");
            Directory.CreateDirectory(sourcePath);
            Directory.CreateDirectory(userPath);

            _resolver = new PathResolver(_testRoot, sourcePath, userPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, true);
            }
        }

        [Fact]
        public void GetDataPath_ShouldUseDataDirectoryConstant()
        {
            // Act
            var dataPath = _resolver.GetDataPath("test.json");

            // Assert
            var expected = Path.Combine(_testRoot, ColorModConstants.DataDirectory, "test.json");
            Assert.Equal(expected, dataPath);
        }

        [Fact]
        public void GetConfigPath_ShouldUseConfigFileNameConstant()
        {
            // Act
            var configPath = _resolver.GetConfigPath();

            // Assert
            Assert.EndsWith(ColorModConstants.ConfigFileName, configPath);
        }

        [Fact]
        public void GetSpritePath_ShouldUseSpritePathConstants()
        {
            // Act
            var spritePath = _resolver.GetSpritePath("Agrias", "lucavi", "battle_aguri_0.bmp");

            // Assert
            Assert.Contains(ColorModConstants.FFTIVCPath, spritePath);
            Assert.Contains(ColorModConstants.UnitPath, spritePath);
            Assert.Contains(ColorModConstants.EnhancedPath, spritePath);
            Assert.Contains(ColorModConstants.FFTPackPath, spritePath);
        }

        [Fact]
        public void GetPreviewImagePath_ShouldUsePreviewPrefix()
        {
            // Act
            var previewPath = _resolver.GetPreviewImagePath("Cloud", "vampyre");

            // Assert
            Assert.Contains(ColorModConstants.PreviewPrefix, previewPath);
            Assert.EndsWith(ColorModConstants.PngExtension, previewPath);
        }
    }
}