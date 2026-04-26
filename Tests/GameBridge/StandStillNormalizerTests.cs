using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// `execute_turn 7 9 "Phoenix Down" 6 9` while standing on (7,9)
    /// would otherwise fail with "Tile (7,9) is not in the valid move
    /// range" — the BFS-emitted move list excludes the origin tile.
    /// Live-flagged 2026-04-25 playtest. Caller is asking for stand-still
    /// + act; the helper normalizes a same-tile move to "no move" so the
    /// bundle skips battle_move entirely and dispatches just the ability.
    /// </summary>
    public class StandStillNormalizerTests
    {
        [Fact]
        public void SameTile_ClearsMove()
        {
            var (mx, my) = StandStillNormalizer.NormalizeSameTile(7, 9, 7, 9);
            Assert.Null(mx);
            Assert.Null(my);
        }

        [Fact]
        public void DifferentTile_PreservesMove()
        {
            var (mx, my) = StandStillNormalizer.NormalizeSameTile(6, 5, 7, 9);
            Assert.Equal(6, mx);
            Assert.Equal(5, my);
        }

        [Fact]
        public void NullMove_ReturnsUnchanged()
        {
            var (mx, my) = StandStillNormalizer.NormalizeSameTile(null, null, 7, 9);
            Assert.Null(mx);
            Assert.Null(my);
        }

        [Fact]
        public void PartialNullMove_ReturnsUnchanged()
        {
            // Only one coordinate set — TurnPlan ignores partial moves
            // anyway (requires both X and Y), but the normalizer should
            // not invent the missing one.
            var (mx, my) = StandStillNormalizer.NormalizeSameTile(7, null, 7, 9);
            Assert.Equal(7, mx);
            Assert.Null(my);
        }

        [Fact]
        public void NullCurrentPosition_PreservesMove()
        {
            // Can't compare against unknown current position — defer to
            // normal validation (move list lookup).
            var (mx, my) = StandStillNormalizer.NormalizeSameTile(7, 9, null, null);
            Assert.Equal(7, mx);
            Assert.Equal(9, my);
        }
    }
}
