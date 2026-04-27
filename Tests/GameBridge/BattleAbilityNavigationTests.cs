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

        // 2026-04-26 PM: Knight Ramza had primary=Arts of War, secondary=null.
        // Bridge offered Focus (a Mettle ability Ramza learned as Squire),
        // and `battle_ability "Focus"` failed with "Skillset 'Mettle' not in
        // submenu: Attack, Arts of War" — because FindAbility's all-skillsets
        // fallback returned skillsetName="Mettle" even though Mettle wasn't
        // in available. Caller then tried to navigate to a Mettle submenu
        // that doesn't exist on Knight's screen.
        //
        // Fix: when availableSkillsets is non-null AND non-empty, restrict
        // the search to those skillsets. The caller has told us what's
        // equipped; respect that. The all-skillsets fallback is reserved
        // for the "no info from caller" case (null/empty available list).
        [Fact]
        public void FindAbility_NotInAvailableSkillsets_ReturnsNull()
        {
            // Focus is in Mettle. Knight Ramza's available list is just
            // ["Arts of War"]. Should NOT find Focus by falling through
            // to Mettle.
            var result = BattleAbilityNavigation.FindAbility(
                "Focus", availableSkillsets: new[] { "Arts of War" });

            Assert.Null(result);
        }

        [Fact]
        public void FindAbility_AvailableSkillsets_StillResolvesAttack()
        {
            // The synthetic "Attack" skillset must always resolve, even
            // when availableSkillsets is restrictive — Attack is the
            // basic action available regardless of equipped skillsets.
            var result = BattleAbilityNavigation.FindAbility(
                "Attack", availableSkillsets: new[] { "Arts of War" });

            Assert.NotNull(result);
            Assert.Equal("Attack", result.Value.skillsetName);
        }

        [Fact]
        public void FindAbility_NullAvailableSkillsets_FallsThroughToAll()
        {
            // No info from caller — fall through to all-skillsets.
            // Backwards-compat with callers that don't pass available.
            var result = BattleAbilityNavigation.FindAbility("Focus", availableSkillsets: null);

            Assert.NotNull(result);
            Assert.Equal("Mettle", result.Value.skillsetName);
        }

        [Fact]
        public void FindAbility_EmptyAvailableSkillsets_FallsThroughToAll()
        {
            // Empty array == "no equipped skillsets info" — fall through.
            // Different from a populated array which is restrictive.
            var result = BattleAbilityNavigation.FindAbility(
                "Focus", availableSkillsets: System.Array.Empty<string>());

            Assert.NotNull(result);
            Assert.Equal("Mettle", result.Value.skillsetName);
        }

        [Fact]
        public void FindAbility_InAvailableSkillsets_Resolves()
        {
            // If Mettle IS equipped (as secondary), Focus resolves cleanly.
            var result = BattleAbilityNavigation.FindAbility(
                "Focus", availableSkillsets: new[] { "Arts of War", "Mettle" });

            Assert.NotNull(result);
            Assert.Equal("Mettle", result.Value.skillsetName);
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

        // DontAct (and other action-blocking statuses) gray the Abilities slot
        // in the visible action menu. The visible cursor auto-skips it when
        // navigating up/down. Memory cursor lags this — it reports the slot
        // index it WOULD be on without the skip. Without correcting for this,
        // battle_wait nav presses Down once "to reach Wait" but actually moves
        // to Status (overshooting because the visible cursor was already on
        // Wait). Live-flagged 2026-04-26 playtest #9.

        [Fact]
        public void EffectiveMenuCursor_Disabled_MemoryReads1_Returns2()
        {
            // DontAct + memory reads Abilities (1) — visible cursor auto-skipped
            // to Wait (2). Trust the visible position.
            int result = BattleAbilityNavigation.EffectiveMenuCursor(
                memoryCursor: 1, moved: false, acted: false, disabled: true);
            Assert.Equal(2, result);
        }

        [Fact]
        public void EffectiveMenuCursor_Disabled_MemoryReads0_Returns0()
        {
            // DontAct + memory reads Move (0) — Move is still valid (DontMove
            // is a separate status), no skip needed.
            int result = BattleAbilityNavigation.EffectiveMenuCursor(
                memoryCursor: 0, moved: false, acted: false, disabled: true);
            Assert.Equal(0, result);
        }

        [Fact]
        public void EffectiveMenuCursor_Disabled_MemoryReads2_TrustsMemory()
        {
            // Already on Wait, no correction needed.
            int result = BattleAbilityNavigation.EffectiveMenuCursor(
                memoryCursor: 2, moved: false, acted: false, disabled: true);
            Assert.Equal(2, result);
        }

        [Fact]
        public void EffectiveMenuCursor_DisabledAndMoved_MemoryReads1_Returns2()
        {
            // Both moved and disabled: cursor at 1 (Abilities) is greyed,
            // visible cursor on Wait (2). Disabled rule wins.
            int result = BattleAbilityNavigation.EffectiveMenuCursor(
                memoryCursor: 1, moved: true, acted: false, disabled: true);
            Assert.Equal(2, result);
        }

        [Fact]
        public void EffectiveMenuCursor_DisabledOmitted_DefaultsToFalse()
        {
            // Backward-compatible default — existing callers don't pass disabled.
            int result = BattleAbilityNavigation.EffectiveMenuCursor(
                memoryCursor: 1, moved: false, acted: false);
            Assert.Equal(1, result);
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
        public void FindAbility_NotInRestrictiveAvailable_ReturnsNull()
        {
            // 2026-04-26 PM: behavior changed. When availableSkillsets is
            // a NON-EMPTY restrictive list, we trust it — don't fall
            // through to all-skillsets. The OLD behavior returned the
            // cross-skillset hit (Aurablast → Martial Arts even when
            // available = ["Jump"]); but downstream nav then tried to
            // open a Martial Arts submenu that doesn't exist on a Dragoon
            // screen, producing cryptic "Skillset 'Martial Arts' not in
            // submenu" errors. Returning null lets the caller emit a
            // clean "ability not in equipped skillsets" error instead.
            //
            // Fall-through still applies when availableSkillsets is null
            // or empty (caller didn't tell us what's equipped — see
            // FindAbility_NullAvailableSkillsets_FallsThroughToAll).
            var result = BattleAbilityNavigation.FindAbility("Aurablast", new[] { "Jump" });
            Assert.Null(result);
        }

        [Theory]
        // Instant action abilities have CastSpeed=0 (resolve immediately).
        [InlineData("Focus", 0)]
        [InlineData("Potion", 0)]
        // Cast-time spells have CastSpeed>0 (queued; resolve when CT counter reaches 100).
        // Values sourced from ActionAbilityLookup.cs skillset tables.
        [InlineData("Cure", 25)]
        [InlineData("Fire", 25)]
        [InlineData("Firaga", 15)]
        [InlineData("Haste", 50)]
        [InlineData("Holy", 17)]
        public void FindAbility_SurfacesCastSpeed(string abilityName, int expectedCastSpeed)
        {
            var result = BattleAbilityNavigation.FindAbility(abilityName);
            Assert.NotNull(result);
            Assert.Equal(expectedCastSpeed, result.Value.castSpeed);
        }

    }
}
