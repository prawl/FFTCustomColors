using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class BattleAbilityNavigationTests
    {
        [Theory]
        [InlineData("Focus", "Mettle", 0)]
        [InlineData("Shout", "Mettle", 7)]
        [InlineData("Throw Stone", "Mettle", 2)]
        [InlineData("Ultima", "Mettle", 8)]
        [InlineData("Potion", "Items", 0)]
        [InlineData("Phoenix Down", "Items", 13)]
        [InlineData("Fire", "Black Magicks", 0)]
        [InlineData("Firaga", "Black Magicks", 2)]
        [InlineData("Aim +1", "Aim", 0)]
        [InlineData("Aim +20", "Aim", 7)]
        [InlineData("Cyclone", "Martial Arts", 0)]
        [InlineData("Pummel", "Martial Arts", 1)]
        [InlineData("Cure", "White Magicks", 0)]
        [InlineData("Holy", "White Magicks", 14)]
        [InlineData("Haste", "Time Magicks", 0)]
        public void FindAbilityInSkillset_ReturnsCorrectPosition(
            string abilityName, string expectedSkillset, int expectedIndex)
        {
            var result = BattleAbilityNavigation.FindAbility(abilityName);

            Assert.NotNull(result);
            Assert.Equal(expectedSkillset, result.Value.skillsetName);
            Assert.Equal(expectedIndex, result.Value.indexInSkillset);
        }

        [Fact]
        public void FindAbility_UnknownAbility_ReturnsNull()
        {
            var result = BattleAbilityNavigation.FindAbility("Nonexistent Spell");
            Assert.Null(result);
        }

        [Theory]
        [InlineData("Focus", true)]
        [InlineData("Shout", true)]
        [InlineData("Throw Stone", false)]
        [InlineData("Cure", false)]
        [InlineData("Potion", false)]
        [InlineData("Tailwind", false)]
        public void FindAbility_CorrectlySetsIsSelfTarget(string abilityName, bool expectedSelf)
        {
            var result = BattleAbilityNavigation.FindAbility(abilityName);

            Assert.NotNull(result);
            Assert.Equal(expectedSelf, result.Value.isSelfTarget);
        }

        [Theory]
        [InlineData("Mettle", new[] { "Attack", "Mettle", "Items" }, 1)]
        [InlineData("Items", new[] { "Attack", "Mettle", "Items" }, 2)]
        [InlineData("Mettle", new[] { "Attack", "Mettle" }, 1)]
        [InlineData("Fundaments", new[] { "Attack", "Fundaments", "Items" }, 1)]
        public void FindSkillsetIndex_ReturnsCorrectPosition(
            string skillsetName, string[] submenuItems, int expectedIndex)
        {
            int index = BattleAbilityNavigation.FindSkillsetIndex(skillsetName, submenuItems);
            Assert.Equal(expectedIndex, index);
        }

        [Fact]
        public void FindSkillsetIndex_NotFound_ReturnsNegative()
        {
            int index = BattleAbilityNavigation.FindSkillsetIndex("Throw", new[] { "Attack", "Mettle", "Items" });
            Assert.Equal(-1, index);
        }

        [Fact]
        public void FindAbility_Attack_ReturnsAttackSkillset()
        {
            var result = BattleAbilityNavigation.FindAbility("Attack");

            Assert.NotNull(result);
            Assert.Equal("Attack", result.Value.skillsetName);
            Assert.Equal(0, result.Value.indexInSkillset);
            Assert.False(result.Value.isSelfTarget);
        }

        [Fact]
        public void EffectiveMenuCursor_AfterMove_MemoryReads0_Returns1()
        {
            // After move, memory reads 0 but game cursor is at Abilities (1).
            int result = BattleAbilityNavigation.EffectiveMenuCursor(memoryCursor: 0, moved: true, acted: false);
            Assert.Equal(1, result);
        }

        [Fact]
        public void EffectiveMenuCursor_AfterAbility_MemoryReads1_Returns0()
        {
            // After ability-only (no move), memory reads 1 but game cursor is at Move (0).
            int result = BattleAbilityNavigation.EffectiveMenuCursor(memoryCursor: 1, moved: false, acted: true);
            Assert.Equal(0, result);
        }

        [Fact]
        public void EffectiveMenuCursor_NoMoveNoAct_TrustsMemory()
        {
            int result = BattleAbilityNavigation.EffectiveMenuCursor(memoryCursor: 0, moved: false, acted: false);
            Assert.Equal(0, result);
        }

        [Fact]
        public void EffectiveMenuCursor_AfterMove_MemoryReadsNonZero_TrustsMemory()
        {
            // If memory already reads non-zero after move, trust it
            int result = BattleAbilityNavigation.EffectiveMenuCursor(memoryCursor: 2, moved: true, acted: false);
            Assert.Equal(2, result);
        }

        [Fact]
        public void EffectiveMenuCursor_AfterAbility_MemoryReads0_TrustsMemory()
        {
            // If memory already reads 0 after ability, trust it (already correct)
            int result = BattleAbilityNavigation.EffectiveMenuCursor(memoryCursor: 0, moved: false, acted: true);
            Assert.Equal(0, result);
        }

        [Fact]
        public void FindAbility_Jump_FoundInJumpSkillset()
        {
            // The collapsed "Jump" entry from CollapseJumpAbilities should be findable
            // via battle_ability "Jump". It maps to the Jump skillset at index 0.
            var result = BattleAbilityNavigation.FindAbility("Jump", new[] { "Jump", "Martial Arts" });

            Assert.NotNull(result);
            Assert.Equal("Jump", result.Value.skillsetName);
            Assert.Equal(0, result.Value.indexInSkillset);
            Assert.False(result.Value.isSelfTarget);
        }

        [Fact]
        public void FindAbility_Jump_NotFoundWhenJumpSkillsetUnavailable()
        {
            // "Jump" is a synthetic ability only created when Jump skillset is available.
            // If Jump isn't in available skillsets and the special case doesn't match,
            // the fallback won't find it because AllSkillsets uses hardcoded lists.
            var result = BattleAbilityNavigation.FindAbility("Jump", new[] { "Mettle", "Items" });
            Assert.Null(result);
        }

        [Theory]
        [InlineData("Focus", true)]    // Self, AoE=1 → true self-only
        [InlineData("Shout", true)]    // Self, AoE=1 → true self-only
        [InlineData("Chakra", false)]  // Self, AoE=2 → self-radius, needs AoE confirm
        [InlineData("Cyclone", false)] // Self, AoE=2 → self-radius
        [InlineData("Fire", false)]    // Targeted
        public void IsTrueSelfTarget_DistinguishesSelfOnlyFromSelfRadius(
            string abilityName, bool expectedTrueSelf)
        {
            var result = BattleAbilityNavigation.FindAbility(abilityName);
            Assert.NotNull(result);
            Assert.Equal(expectedTrueSelf, result.Value.isTrueSelfOnly);
        }

        [Fact]
        public void FindAbility_FallsBackToAllSkillsets_WhenNotInAvailable()
        {
            // Aurablast is in Martial Arts. If the available skillsets don't include
            // Martial Arts (e.g. secondary wasn't detected), the fallback search
            // should still find it.
            var result = BattleAbilityNavigation.FindAbility("Aurablast", new[] { "Jump" });

            Assert.NotNull(result);
            Assert.Equal("Martial Arts", result.Value.skillsetName);
        }

    }
}
