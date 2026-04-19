using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class BattleStatusUiResolverTests
    {
        [Fact]
        public void Returns_ActiveUnitName_When_Cached()
        {
            Assert.Equal("Kenrick", BattleStatusUiResolver.Resolve("Kenrick"));
            Assert.Equal("Wilham", BattleStatusUiResolver.Resolve("Wilham"));
        }

        [Fact]
        public void Returns_Null_Before_FirstScan()
        {
            // First BattleStatus entry in a new battle — scan_move hasn't
            // populated _cachedActiveUnitName yet, so resolver returns null
            // and the ui= field stays absent rather than rendering "null".
            Assert.Null(BattleStatusUiResolver.Resolve(null));
        }
    }
}
