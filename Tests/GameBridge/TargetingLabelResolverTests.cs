using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class TargetingLabelResolverTests
    {
        [Fact]
        public void Prefers_LastAbilityName_When_Set()
        {
            var result = TargetingLabelResolver.Resolve(
                lastAbilityName: "Fire",
                selectedAbility: "Cure",
                selectedItem: "Attack");
            Assert.Equal("Fire", result);
        }

        [Fact]
        public void Falls_Back_To_SelectedAbility_When_LastIsNull()
        {
            var result = TargetingLabelResolver.Resolve(
                lastAbilityName: null,
                selectedAbility: "Cure",
                selectedItem: "Attack");
            Assert.Equal("Cure", result);
        }

        [Fact]
        public void Falls_Back_To_SelectedItem_When_Others_Null()
        {
            var result = TargetingLabelResolver.Resolve(
                lastAbilityName: null,
                selectedAbility: null,
                selectedItem: "Attack");
            Assert.Equal("Attack", result);
        }

        [Fact]
        public void Returns_Null_When_All_Null()
        {
            var result = TargetingLabelResolver.Resolve(null, null, null);
            Assert.Null(result);
        }

        // ResolveOrCursor: extends Resolve with a cursor-tile fallback for
        // BattleAttacking / BattleCasting. When the player enters targeting
        // via menu navigation (not via a battle_ability helper) and the
        // tracker hasn't latched an ability yet, the three ability inputs
        // are all null. Rather than drop ui= entirely, fall back to the
        // cursor position — matches BattleMoving's ui=(x,y) behavior.

        [Fact]
        public void ResolveOrCursor_FallsBackToCursorCoords_WhenAllAbilityInputsNull()
        {
            var result = TargetingLabelResolver.ResolveOrCursor(null, null, null, 5, 7);
            Assert.Equal("(5,7)", result);
        }

        [Fact]
        public void ResolveOrCursor_PrefersAbilityNameOverCursor()
        {
            var result = TargetingLabelResolver.ResolveOrCursor("Fire", null, null, 5, 7);
            Assert.Equal("Fire", result);
        }

        [Fact]
        public void ResolveOrCursor_PrefersSelectedAbilityOverCursor()
        {
            var result = TargetingLabelResolver.ResolveOrCursor(null, "Cure", null, 5, 7);
            Assert.Equal("Cure", result);
        }

        [Fact]
        public void ResolveOrCursor_PrefersSelectedItemOverCursor()
        {
            var result = TargetingLabelResolver.ResolveOrCursor(null, null, "Attack", 5, 7);
            Assert.Equal("Attack", result);
        }
    }
}
