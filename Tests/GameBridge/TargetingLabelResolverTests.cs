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

        // cursorX / cursorY == -1 is the game's sentinel for "no tile selected
        // yet" (BattleAttacking first-frame, targeting mode not fully engaged).
        // Rendering ui="(-1,-1)" in that window is nonsense — return null
        // instead so the JSON serializer drops the field.

        [Fact]
        public void ResolveOrCursor_ReturnsNull_WhenCursorIsNegativeOne()
        {
            var result = TargetingLabelResolver.ResolveOrCursor(null, null, null, -1, -1);
            Assert.Null(result);
        }

        [Fact]
        public void ResolveOrCursor_ReturnsNull_WhenOnlyCursorXIsNegativeOne()
        {
            // Defensive: x=-1 alone means the cursor isn't initialized even
            // if y was populated (or vice versa). Treat either being -1 as
            // "no cursor".
            Assert.Null(TargetingLabelResolver.ResolveOrCursor(null, null, null, -1, 7));
            Assert.Null(TargetingLabelResolver.ResolveOrCursor(null, null, null, 5, -1));
        }

        [Fact]
        public void ResolveOrCursor_AbilityNameStill_Returns_EvenWithNegativeCursor()
        {
            // When the tracker has latched an ability, we should still render
            // the ability name even if cursor is -1 (e.g. the player pressed
            // Enter on Attack but cursor hasn't landed on a tile yet).
            var result = TargetingLabelResolver.ResolveOrCursor("Fire", null, null, -1, -1);
            Assert.Equal("Fire", result);
        }
    }
}
