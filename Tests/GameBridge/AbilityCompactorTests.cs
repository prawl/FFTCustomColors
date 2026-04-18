using System.Collections.Generic;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// AbilityCompactor prunes enemy-target abilities with no enemies in range
    /// and collapses numbered families (Aim +1, +2, ...) with identical tile
    /// lists into a single entry.
    /// </summary>
    public class AbilityCompactorTests
    {
        private static AbilityEntry MakeAbility(string name, string target,
            params (int x, int y, string? occupant)[] tiles)
        {
            var tileList = new List<ValidTargetTile>();
            foreach (var t in tiles)
            {
                tileList.Add(new ValidTargetTile { X = t.x, Y = t.y, Occupant = t.occupant });
            }
            return new AbilityEntry
            {
                Name = name,
                Target = target,
                ValidTargetTiles = tileList,
                TotalTargets = tileList.Count,
            };
        }

        [Fact]
        public void Compact_EmptyInput_ReturnsEmpty()
        {
            var result = AbilityCompactor.Compact(new List<AbilityEntry>());
            Assert.Empty(result);
        }

        [Fact]
        public void Compact_EnemyAbilityWithEnemyInRange_Kept()
        {
            var input = new List<AbilityEntry>
            {
                MakeAbility("Fire", "enemy", (5, 5, "enemy")),
            };
            var result = AbilityCompactor.Compact(input);
            Assert.Single(result);
            Assert.Equal("Fire", result[0].Name);
        }

        [Fact]
        public void Compact_EnemyAbilityWithNoEnemyInRange_Hidden()
        {
            var input = new List<AbilityEntry>
            {
                MakeAbility("Fire", "enemy", (5, 5, "ally"), (5, 6, null)),
            };
            var result = AbilityCompactor.Compact(input);
            Assert.Empty(result);
        }

        [Fact]
        public void Compact_AllyAbility_AlwaysKept_EvenWithoutEnemies()
        {
            var input = new List<AbilityEntry>
            {
                MakeAbility("Cure", "ally", (5, 5, "ally")),
            };
            var result = AbilityCompactor.Compact(input);
            Assert.Single(result);
        }

        [Fact]
        public void Compact_SelfAbility_AlwaysKept()
        {
            var input = new List<AbilityEntry>
            {
                MakeAbility("Shout", "self"),
            };
            var result = AbilityCompactor.Compact(input);
            Assert.Single(result);
        }

        [Fact]
        public void Compact_NumberedFamilySameTiles_Collapsed()
        {
            // Aim +1, Aim +2, Aim +3 with the same target tiles should collapse
            // to a single entry "Aim (+1 to +3)".
            var input = new List<AbilityEntry>
            {
                MakeAbility("Aim +1", "enemy", (5, 5, "enemy")),
                MakeAbility("Aim +2", "enemy", (5, 5, "enemy")),
                MakeAbility("Aim +3", "enemy", (5, 5, "enemy")),
            };
            var result = AbilityCompactor.Compact(input);
            Assert.Single(result);
            Assert.Contains("Aim", result[0].Name);
            Assert.Contains("+1", result[0].Name);
            Assert.Contains("+3", result[0].Name);
        }

        [Fact]
        public void Compact_NumberedFamilyDifferentTiles_NotCollapsed()
        {
            // If the tile lists differ, each entry is kept separately.
            var input = new List<AbilityEntry>
            {
                MakeAbility("Aim +1", "enemy", (5, 5, "enemy")),
                MakeAbility("Aim +2", "enemy", (6, 6, "enemy")), // different tile
            };
            var result = AbilityCompactor.Compact(input);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void Compact_SingleNumberedFamilyMember_NotCollapsed()
        {
            // A lone "Aim +5" shouldn't be rewritten — nothing to collapse.
            var input = new List<AbilityEntry>
            {
                MakeAbility("Aim +5", "enemy", (5, 5, "enemy")),
                MakeAbility("Fire", "enemy", (5, 5, "enemy")),
            };
            var result = AbilityCompactor.Compact(input);
            Assert.Equal(2, result.Count);
            Assert.Equal("Aim +5", result[0].Name);
        }

        [Fact]
        public void Compact_MixedUsable_AndHidden_PreservesOrder()
        {
            var input = new List<AbilityEntry>
            {
                MakeAbility("Fire", "enemy", (5, 5, "enemy")),                // kept
                MakeAbility("Blizzard", "enemy", (5, 6, "ally")),              // hidden
                MakeAbility("Cure", "ally", (5, 7, "ally")),                   // kept
            };
            var result = AbilityCompactor.Compact(input);
            Assert.Equal(2, result.Count);
            Assert.Equal("Fire", result[0].Name);
            Assert.Equal("Cure", result[1].Name);
        }

        [Fact]
        public void Compact_NoMatch_PassthroughAllNonEnemyTargets()
        {
            // Chakra and Shout both self-target; should pass through unchanged.
            var input = new List<AbilityEntry>
            {
                MakeAbility("Chakra", "self"),
                MakeAbility("Shout", "self"),
            };
            var result = AbilityCompactor.Compact(input);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void Compact_EnemyAbilityWithEmptyTileList_Hidden()
        {
            // No valid target tiles → treated as no enemies in range.
            var input = new List<AbilityEntry>
            {
                MakeAbility("Fire", "enemy"),
            };
            var result = AbilityCompactor.Compact(input);
            Assert.Empty(result);
        }

        [Fact]
        public void Compact_AimPlusFamily_CollapsedRangeMentionsBothEnds()
        {
            // Aim +1..+20 with same tiles → "Aim (+1 to +20)".
            var input = new List<AbilityEntry>();
            for (int i = 1; i <= 20; i++)
            {
                input.Add(MakeAbility($"Aim +{i}", "enemy", (5, 5, "enemy")));
            }
            var result = AbilityCompactor.Compact(input);
            Assert.Single(result);
            Assert.Contains("+1", result[0].Name);
            Assert.Contains("+20", result[0].Name);
        }
    }
}
