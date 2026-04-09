using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class BattleUnitStateSecondaryTests
    {
        [Fact]
        public void BattleUnitState_ShouldHaveSecondaryAbilityField()
        {
            // BattleUnitState needs SecondaryAbility so the scan result
            // carries the roster-matched secondary through to CommandWatcher.
            var unit = new BattleUnitState
            {
                SecondaryAbility = 6
            };

            Assert.Equal(6, unit.SecondaryAbility);
        }

        [Fact]
        public void BattleUnitState_SecondaryAbility_DefaultsToZero()
        {
            var unit = new BattleUnitState();
            Assert.Equal(0, unit.SecondaryAbility);
        }
    }
}
