using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pin the up-front validation for tile-target action arguments
    /// (battle_move, battle_attack, battle_ability). Default field
    /// values when the caller passes wrong JSON keys (e.g. {"x":8,"y":11}
    /// instead of {"locationId":8,"unitIndex":11}) are
    /// LocationId=-1, UnitIndex=0 — which produces a confusing
    /// "Cursor miss: at (0,0) expected (-1,0)" error deep in the nav
    /// loop instead of telling the caller they used the wrong fields.
    /// Live-captured 2026-04-25 Siedge Weald battle_attack from the
    /// shell. The validator surfaces the field-mismatch up front.
    /// </summary>
    public class TileTargetValidatorTests
    {
        [Fact]
        public void Validate_ValidCoords_ReturnsNull()
        {
            // Happy path: real coords from the helper.
            Assert.Null(TileTargetValidator.Validate(8, 11, "battle_attack"));
            Assert.Null(TileTargetValidator.Validate(0, 0, "battle_move"));
        }

        [Fact]
        public void Validate_NegativeX_DefaultLocationId_ReturnsHelpfulError()
        {
            // Default-LocationId case (-1, 0) — clear signal the caller
            // sent wrong JSON fields.
            var err = TileTargetValidator.Validate(-1, 0, "battle_attack");
            Assert.NotNull(err);
            Assert.Contains("battle_attack", err);
            Assert.Contains("locationId", err);
            Assert.Contains("unitIndex", err);
        }

        [Fact]
        public void Validate_NegativeY_ReturnsHelpfulError()
        {
            var err = TileTargetValidator.Validate(5, -1, "battle_move");
            Assert.NotNull(err);
            Assert.Contains("battle_move", err);
        }

        [Fact]
        public void Validate_BothNegative_ReturnsHelpfulError()
        {
            var err = TileTargetValidator.Validate(-1, -1, "battle_ability");
            Assert.NotNull(err);
            Assert.Contains("battle_ability", err);
        }

        [Fact]
        public void Validate_OutOfRangeY_ReturnsHelpfulError()
        {
            // FFT maps cap at ~16x16. y=99 is off the board.
            var err = TileTargetValidator.Validate(5, 99, "battle_attack");
            Assert.NotNull(err);
            Assert.Contains("99", err);
        }
    }
}
