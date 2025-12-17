using System;
using System.IO;
using Xunit;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Utilities;

namespace Tests
{
    public class ConfigBasedSpriteManagerSimpleTest : IDisposable
    {
        private string _testModPath;
        private string _testSourcePath;

        public ConfigBasedSpriteManagerSimpleTest()
        {
            _testModPath = Path.Combine(Path.GetTempPath(), "FFTTest_" + Guid.NewGuid());
            _testSourcePath = Path.Combine(_testModPath, "ColorMod", "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(_testSourcePath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testModPath))
            {
                Directory.Delete(_testModPath, true);
            }
        }

        [Fact]
        public void DirectoryExists_AfterCreation()
        {
            Assert.True(Directory.Exists(_testSourcePath));
        }

        [Fact]
        public void CanWriteAndReadFile()
        {
            var testFile = Path.Combine(_testSourcePath, "test.txt");
            File.WriteAllText(testFile, "test content");

            Assert.True(File.Exists(testFile));
            Assert.Equal("test content", File.ReadAllText(testFile));
        }
    }
}
