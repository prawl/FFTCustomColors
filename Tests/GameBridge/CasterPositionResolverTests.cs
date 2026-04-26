using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// `battle_ability` was using `ReadGridPos()` (cursor position) as the
    /// caster's tile for two purposes: (a) auto-fill of self-target
    /// abilities, (b) PostAction pin so the success line shows the
    /// caster's tile, not the target. Both BROKE when the cursor sat on
    /// another unit (e.g. Wilham at (10,10) from a prior C+Up cycle):
    /// `battle_ability "Shout"` auto-filled (10,10) and the success line
    /// printed `→ (10,10) HP=528/528` (Wilham's data) for Ramza's Shout.
    /// Live-flagged 2026-04-25 playtest.
    ///
    /// CasterPositionResolver prefers the active unit's actual position
    /// from the last scan over the cursor read. Cursor is the fallback
    /// when no scan data exists (first turn / cache cleared).
    /// </summary>
    public class CasterPositionResolverTests
    {
        [Fact]
        public void ScanPosition_Preferred()
        {
            var (x, y) = CasterPositionResolver.Resolve(
                scannedActiveX: 7, scannedActiveY: 9,
                cursorX: 10, cursorY: 10);
            Assert.Equal(7, x);
            Assert.Equal(9, y);
        }

        [Fact]
        public void NoScanPosition_FallsBackToCursor()
        {
            var (x, y) = CasterPositionResolver.Resolve(
                scannedActiveX: null, scannedActiveY: null,
                cursorX: 10, cursorY: 10);
            Assert.Equal(10, x);
            Assert.Equal(9 + 1, y); // sanity: 10
        }

        [Fact]
        public void NeitherSource_ReturnsNegativeSentinel()
        {
            // No scan and cursor read failed (-1) — caller should reject.
            var (x, y) = CasterPositionResolver.Resolve(
                scannedActiveX: null, scannedActiveY: null,
                cursorX: -1, cursorY: -1);
            Assert.Equal(-1, x);
            Assert.Equal(-1, y);
        }

        [Fact]
        public void PartialScanData_FallsBackToCursor()
        {
            // Only one coord available is degenerate — fall back rather
            // than mix sources.
            var (x, y) = CasterPositionResolver.Resolve(
                scannedActiveX: 7, scannedActiveY: null,
                cursorX: 10, cursorY: 10);
            Assert.Equal(10, x);
            Assert.Equal(10, y);
        }

        [Fact]
        public void ScanPosition_PreferredEvenWhenCursorIsValid()
        {
            // Scan-driven preference applies even when cursor is somewhere
            // sensible (e.g. on the caster) — the scan is the canonical
            // source of "active unit identity & tile."
            var (x, y) = CasterPositionResolver.Resolve(
                scannedActiveX: 8, scannedActiveY: 10,
                cursorX: 8, cursorY: 10);
            Assert.Equal(8, x);
            Assert.Equal(10, y);
        }
    }
}
