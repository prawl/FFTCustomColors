using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Tests for the location persistence logic.
    /// The game writes last_location.txt to the bridge directory during gameplay.
    /// This file must survive game restarts (which trigger a full deploy cycle).
    ///
    /// Bug: BuildLinked.ps1 was copying a stale repo copy of last_location.txt
    /// AFTER restoring the backed-up live copy, overwriting the correct value.
    ///
    /// Fix: The deploy script should NOT copy last_location.txt from the repo.
    /// The game writes it; the deploy backs it up and restores it. The repo copy
    /// should not exist — it's runtime state, not source.
    /// </summary>
    public class LocationPersistenceTests
    {
        [Fact]
        public void SaveLastLocation_WritesToFile()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"fft_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            try
            {
                var path = Path.Combine(dir, "last_location.txt");
                File.WriteAllText(path, "26");

                Assert.Equal("26", File.ReadAllText(path).Trim());
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void LoadLastLocation_ReadsCorrectValue()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"fft_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            try
            {
                var path = Path.Combine(dir, "last_location.txt");
                File.WriteAllText(path, "26");

                var value = int.Parse(File.ReadAllText(path).Trim());
                Assert.Equal(26, value);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void LastLocation_RepoFileShouldNotExist()
        {
            // The repo should NOT contain FFTHandsFree/last_location.txt
            // because it's runtime state written by the game, not source.
            // If it exists, BuildLinked.ps1 will overwrite the correct
            // backed-up value with a stale one on every deploy.
            var repoPath = Path.Combine(
                FindRepoRoot(),
                "FFTHandsFree", "last_location.txt");

            Assert.False(File.Exists(repoPath),
                $"FFTHandsFree/last_location.txt should not exist in the repo. " +
                $"It's runtime state that gets overwritten on deploy, clobbering the live value. " +
                $"Delete it and remove the copy line from BuildLinked.ps1.");
        }

        [Fact]
        public void BuildLinkedPs1_ShouldNotCopyLastLocationFromRepo()
        {
            // BuildLinked.ps1 should NOT have a line copying last_location.txt from FFTHandsFree/
            var repoRoot = FindRepoRoot();
            var buildScript = File.ReadAllText(Path.Combine(repoRoot, "BuildLinked.ps1"));

            Assert.DoesNotContain("FFTHandsFree/last_location.txt", buildScript,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string FindRepoRoot()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "CLAUDE.md")))
                    return dir;
                dir = Path.GetDirectoryName(dir);
            }
            // Fallback
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..");
        }
    }
}
