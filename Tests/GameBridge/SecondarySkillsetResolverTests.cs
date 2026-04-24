using System.Collections.Generic;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class SecondarySkillsetResolverTests
    {
        // Fake skillset lookup: maps a few test ability names → skillsets.
        // Keeps the tests independent of ActionAbilityLookup's full dataset.
        private static string? FakeLookup(string name) => name switch
        {
            "Focus" or "Salve" or "Shout" or "Ultima" => "Mettle",
            "Potion" or "Hi-Potion" or "Phoenix Down" => "Items",
            "Cure" or "Cura" or "Raise" => "White Magicks",
            _ => null,
        };

        [Fact]
        public void ByteResolved_UsesByteValue()
        {
            var result = SecondarySkillsetResolver.Resolve(
                byteResolvedSecondary: "Items",
                abilityNames: null,
                primarySkillset: "Mettle",
                previousCache: null,
                getSkillsetForAbility: FakeLookup);
            Assert.Equal("Items", result);
        }

        [Fact]
        public void ByteResolved_OverridesPreviousCache()
        {
            // Unit just changed secondary — byte is authoritative.
            var result = SecondarySkillsetResolver.Resolve(
                byteResolvedSecondary: "White Magicks",
                abilityNames: new[] { "Potion" },
                primarySkillset: "Mettle",
                previousCache: "Items",
                getSkillsetForAbility: FakeLookup);
            Assert.Equal("White Magicks", result);
        }

        [Fact]
        public void ByteZero_AbilitiesHaveNonPrimary_InfersSecondary()
        {
            // Ramza turn with Mettle primary + Items secondary; byte read 0;
            // abilities list contains Potion (Items). Should infer "Items".
            var result = SecondarySkillsetResolver.Resolve(
                byteResolvedSecondary: null,
                abilityNames: new[] { "Focus", "Salve", "Potion", "Phoenix Down" },
                primarySkillset: "Mettle",
                previousCache: null,
                getSkillsetForAbility: FakeLookup);
            Assert.Equal("Items", result);
        }

        [Fact]
        public void ByteZero_AbilitiesAllPrimary_PreservesPreviousCache()
        {
            // S60 fix: scan's FilterAbilitiesBySkillsets upstream EXCLUDES the
            // secondary's abilities when the byte read 0 (the filter keys on
            // the same byte). So inference can't find any non-primary entry.
            // Previous logic blanked the cache to null — breaking the submenu
            // detection for the rest of the turn. Fix: preserve prior value.
            var result = SecondarySkillsetResolver.Resolve(
                byteResolvedSecondary: null,
                abilityNames: new[] { "Focus", "Salve", "Shout" },
                primarySkillset: "Mettle",
                previousCache: "Items",
                getSkillsetForAbility: FakeLookup);
            Assert.Equal("Items", result);
        }

        [Fact]
        public void ByteZero_NoAbilities_PreservesPreviousCache()
        {
            // Extreme transient case: scan didn't even populate abilities list.
            var result = SecondarySkillsetResolver.Resolve(
                byteResolvedSecondary: null,
                abilityNames: null,
                primarySkillset: "Mettle",
                previousCache: "Items",
                getSkillsetForAbility: FakeLookup);
            Assert.Equal("Items", result);
        }

        [Fact]
        public void ByteZero_EmptyAbilities_PreservesPreviousCache()
        {
            var result = SecondarySkillsetResolver.Resolve(
                byteResolvedSecondary: null,
                abilityNames: new string[0],
                primarySkillset: "Mettle",
                previousCache: "Items",
                getSkillsetForAbility: FakeLookup);
            Assert.Equal("Items", result);
        }

        [Fact]
        public void ByteZero_NoCache_ReturnsNull()
        {
            // First scan ever, no cache to preserve. Return null (legitimately
            // unknown). Once the byte reads correctly once, the cache sticks.
            var result = SecondarySkillsetResolver.Resolve(
                byteResolvedSecondary: null,
                abilityNames: null,
                primarySkillset: "Mettle",
                previousCache: null,
                getSkillsetForAbility: FakeLookup);
            Assert.Null(result);
        }

        [Fact]
        public void ByteZero_AbilityMatchesPrimary_SkipsAndPreserves()
        {
            // All abilities resolve to the primary skillset — no secondary
            // inferable from the data. Preserve previous cache.
            var result = SecondarySkillsetResolver.Resolve(
                byteResolvedSecondary: null,
                abilityNames: new[] { "Focus", "Ultima" },
                primarySkillset: "Mettle",
                previousCache: "White Magicks",
                getSkillsetForAbility: FakeLookup);
            Assert.Equal("White Magicks", result);
        }

        [Fact]
        public void ByteZero_UnknownAbilityName_SkipsToNext()
        {
            // First ability has null lookup → skip. Second ability resolves to
            // non-primary → use.
            var result = SecondarySkillsetResolver.Resolve(
                byteResolvedSecondary: null,
                abilityNames: new[] { "UnknownAbility", "Potion" },
                primarySkillset: "Mettle",
                previousCache: null,
                getSkillsetForAbility: FakeLookup);
            Assert.Equal("Items", result);
        }
    }
}
