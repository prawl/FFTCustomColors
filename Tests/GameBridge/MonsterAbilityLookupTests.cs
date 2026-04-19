using FFTColorCustomizer.GameBridge;
using System.Linq;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Tests for <see cref="MonsterAbilityLookup.GetByName"/> and the
    /// name↔class integration with <see cref="MonsterAbilities.GetAbilities"/>.
    /// Neither class had direct tests prior to session 47.
    ///
    /// Key invariant: every ability name in MonsterAbilities.ClassToAbilities
    /// MUST have metadata in MonsterAbilityLookup.ByName. A class-to-name
    /// edit without the corresponding metadata edit breaks scan_move
    /// rendering silently.
    /// </summary>
    public class MonsterAbilityLookupTests
    {
        [Fact]
        public void GetByName_KnownAbility_ReturnsInfo()
        {
            var info = MonsterAbilityLookup.GetByName("Tackle");
            Assert.NotNull(info);
            Assert.Equal("Tackle", info!.Name);
        }

        [Fact]
        public void GetByName_Unknown_ReturnsNull()
        {
            Assert.Null(MonsterAbilityLookup.GetByName("Lightsaber"));
        }

        [Fact]
        public void GetByName_NullOrEmpty_ReturnsNull()
        {
            Assert.Null(MonsterAbilityLookup.GetByName(null));
            Assert.Null(MonsterAbilityLookup.GetByName(""));
        }

        [Theory]
        [InlineData("Thunder Anima", "Lightning")]
        [InlineData("Water Anima", "Water")]
        [InlineData("Ice Anima", "Ice")]
        [InlineData("Wind Anima", "Wind")]
        public void SkeletonAnimas_CarryCorrectElement(string name, string expectedElement)
        {
            var info = MonsterAbilityLookup.GetByName(name);
            Assert.NotNull(info);
            Assert.Equal(expectedElement, info!.Element);
        }

        [Fact]
        public void DreadGaze_TargetsBravery_NotHP()
        {
            // Panther/Eye family ability — should have an added-effect
            // description rather than being a simple damage attack.
            var info = MonsterAbilityLookup.GetByName("Dread Gaze");
            Assert.NotNull(info);
            Assert.Contains("Bravery", info!.Effect, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Doom_IsRange3_WithDoomStatus()
        {
            var info = MonsterAbilityLookup.GetByName("Doom");
            Assert.NotNull(info);
            Assert.Equal("3", info!.HRange);
            Assert.Contains("Doom", info.AddedEffect ?? "");
        }

        [Fact]
        public void GetByName_AbilityNames_ShouldBeUnique()
        {
            // If two entries share a name the Dictionary init throws — but
            // just in case future edits cause a silent override, cross-check
            // that every known genus-pool ability is retrievable.
            var knownNames = new[]
            {
                "Tackle", "Eye Gouge", "Spin Punch",
                "Bite", "Self-Destruct",
                "Talon Dive", "Beak",
                "Chop", "Wing Buffet",
            };
            foreach (var name in knownNames)
            {
                Assert.NotNull(MonsterAbilityLookup.GetByName(name));
            }
        }

        // ---- Integration with MonsterAbilities.ClassToAbilities ----

        [Fact]
        public void MonsterAbilities_Goblin_HasCanonicalLoadout()
        {
            var abilities = MonsterAbilities.GetAbilities("Goblin");
            Assert.NotNull(abilities);
            Assert.Contains("Tackle", abilities);
            Assert.Contains("Eye Gouge", abilities);
        }

        [Fact]
        public void MonsterAbilities_UnknownClass_ReturnsNull()
        {
            Assert.Null(MonsterAbilities.GetAbilities("NotARealMonster"));
            Assert.Null(MonsterAbilities.GetAbilities(null));
        }

        [Fact]
        public void EveryClassAbilityName_HasLookupMetadata()
        {
            // Cross-check invariant: every ability listed under a monster
            // class must have a corresponding entry in MonsterAbilityLookup
            // so scan_move can render it. Catches drift where a new
            // ability is assigned to a class but the metadata isn't wired.
            var allClasses = new[]
            {
                "Goblin", "Black Goblin",
                "Bomb", "Grenade", "Exploder",
                "Steelhawk", "Cockatrice",
            };
            var missing = new System.Collections.Generic.List<string>();
            foreach (var cls in allClasses)
            {
                var abilities = MonsterAbilities.GetAbilities(cls);
                if (abilities == null) continue;
                foreach (var ability in abilities)
                {
                    if (MonsterAbilityLookup.GetByName(ability) == null)
                        missing.Add($"{cls}→{ability}");
                }
            }
            Assert.True(missing.Count == 0,
                $"Abilities listed on monsters but missing from MonsterAbilityLookup: {string.Join(", ", missing)}");
        }
    }
}
