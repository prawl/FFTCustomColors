using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Meta-test: every skillset name referenced in production code and test
    /// cases must resolve via ActionAbilityLookup.GetSkillsetAbilities. Catches
    /// typos at test time (e.g. "Summon Magicks" vs the canonical "Summon",
    /// found session 37 during AbilityJpCosts hardening — the typo'd name
    /// returned null, which the defensive `if (x == null) return;` then
    /// turned into a silent no-op test).
    /// </summary>
    public class SkillsetNameReferenceTests
    {
        // Canonical names used in tests and production code. When you add a
        // new skillset reference, add it here so the compiler-free typo
        // catcher fires if the canonical name changes.
        [Theory]
        [InlineData("Items")]
        [InlineData("Fundaments")]
        [InlineData("Mettle")]
        [InlineData("Summon")]
        [InlineData("Martial Arts")]
        [InlineData("Black Magicks")]
        [InlineData("White Magicks")]
        [InlineData("Time Magicks")]
        [InlineData("Geomancy")]
        [InlineData("Bardsong")]
        [InlineData("Dance")]
        [InlineData("Arithmeticks")]
        [InlineData("Aim")]
        [InlineData("Steal")]
        [InlineData("Throw")]
        [InlineData("Jump")]
        [InlineData("Iaido")]
        [InlineData("Speechcraft")]
        [InlineData("Mystic Arts")]
        [InlineData("Arts of War")]
        [InlineData("Holy Sword")]
        [InlineData("Darkness")]
        public void KnownSkillsetName_Resolves(string skillsetName)
        {
            var result = ActionAbilityLookup.GetSkillsetAbilities(skillsetName);
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Theory]
        [InlineData("Summon Magicks")]    // ❌ actually "Summon"
        [InlineData("Black Magic")]        // ❌ plural-s difference
        [InlineData("Items Magicks")]      // ❌ made up
        [InlineData("")]
        public void CommonTypo_DoesNotResolve(string notASkillset)
        {
            // Pin the counter-examples — the skillset lookup is exact-match,
            // case-sensitive, so these return null. Helps future authors
            // understand why a test "passes trivially" if they typo the name.
            Assert.Null(ActionAbilityLookup.GetSkillsetAbilities(notASkillset));
        }
    }
}
