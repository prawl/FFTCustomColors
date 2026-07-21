using Xunit;

namespace FFTColorCustomizer.Tests
{
    /// <summary>
    /// CC-11: constructing <c>Mod</c> makes it write Config.json into its own folder, which
    /// under test is the SHARED test bin directory. xUnit runs test classes in parallel, so
    /// two Mod-constructing classes could race on that one file (the CI-observed IOException
    /// "being used by another process"). Every test class that constructs Mod joins this
    /// collection, which serializes them; classes using only temp dirs stay parallel.
    /// </summary>
    [CollectionDefinition("ModBinDir")]
    public class ModBinDirCollection
    {
    }
}
