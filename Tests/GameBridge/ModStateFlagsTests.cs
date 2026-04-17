using System.IO;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class ModStateFlagsTests
    {
        private static string MakeTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"ModStateFlagsTest_{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            return dir;
        }

        [Fact]
        public void Get_ReturnsNullWhenFlagUnset()
        {
            var dir = MakeTempDir();
            var flags = new ModStateFlags(dir);
            Assert.Null(flags.Get("missing"));
        }

        [Fact]
        public void Set_WritesFlagAndGetReadsItBack()
        {
            var dir = MakeTempDir();
            var flags = new ModStateFlags(dir);
            flags.Set("tab", 3);
            Assert.Equal(3, flags.Get("tab"));
        }

        [Fact]
        public void Set_PersistsAcrossInstances()
        {
            // The disk-backing contract: a new instance reading the same
            // bridge directory sees flags set by an earlier instance.
            // This is what makes flags survive a C# process restart (mod
            // reload / Reloaded-II relaunch without a full game restart).
            var dir = MakeTempDir();
            var a = new ModStateFlags(dir);
            a.Set("tab", 2);
            a.Set("last_eqa_unit_slot", 7);

            var b = new ModStateFlags(dir);
            Assert.Equal(2, b.Get("tab"));
            Assert.Equal(7, b.Get("last_eqa_unit_slot"));
        }

        [Fact]
        public void Clear_RemovesFlag()
        {
            var dir = MakeTempDir();
            var flags = new ModStateFlags(dir);
            flags.Set("temp", 1);
            flags.Clear("temp");
            Assert.Null(flags.Get("temp"));
        }

        [Fact]
        public void Clear_PersistsToDisk()
        {
            var dir = MakeTempDir();
            var a = new ModStateFlags(dir);
            a.Set("ephemeral", 5);
            a.Clear("ephemeral");

            var b = new ModStateFlags(dir);
            Assert.Null(b.Get("ephemeral"));
        }

        [Fact]
        public void ClearAll_RemovesEverything()
        {
            var dir = MakeTempDir();
            var flags = new ModStateFlags(dir);
            flags.Set("a", 1);
            flags.Set("b", 2);
            flags.Set("c", 3);
            flags.ClearAll();
            Assert.Empty(flags.Snapshot());
        }

        [Fact]
        public void Load_RecoversGracefullyFromCorruptFile()
        {
            // Corrupt JSON should not crash construction — the disk file
            // could end up in any state (partial write, disk full mid-flush,
            // manual edit). Recover to an empty dictionary rather than
            // throwing at mod init.
            var dir = MakeTempDir();
            File.WriteAllText(Path.Combine(dir, "mod_state.json"), "{this is not: valid json]");
            var flags = new ModStateFlags(dir);
            Assert.Empty(flags.Snapshot());
            // And we can still Set afterward.
            flags.Set("x", 1);
            Assert.Equal(1, flags.Get("x"));
        }

        [Fact]
        public void Snapshot_ReturnsIndependentCopy()
        {
            // Snapshot returns a read-only view. Callers shouldn't be able
            // to mutate the underlying dict via the snapshot reference.
            var dir = MakeTempDir();
            var flags = new ModStateFlags(dir);
            flags.Set("a", 1);

            var snap = flags.Snapshot();
            Assert.Single(snap);

            flags.Set("b", 2);
            // Snapshot remains unchanged after further writes.
            Assert.Single(snap);
        }
    }
}
