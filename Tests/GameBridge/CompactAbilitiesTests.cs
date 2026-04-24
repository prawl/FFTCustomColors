using System.Collections.Generic;
using System.Linq;
using FFTColorCustomizer.GameBridge;
using FFTColorCustomizer.Utilities;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class CompactAbilitiesTests
    {
        [Fact]
        public void HideEnemyTargetAbilities_WithNoEnemyOccupants()
        {
            var abilities = new List<AbilityEntry>
            {
                // S60: Attack is always preserved so Claude can see the weapon's
                // range / element / on-hit effect even when no enemy is adjacent.
                MakeAbility("Attack", "enemy", new[] { ("ally", "Ramza"), ("ally", "Archer") }),
                MakeAbility("Fire", "enemy", new[] { ("ally", "Ramza"), ("ally", "Archer") }),
                MakeAbility("Tailwind", "ally", new[] { ("ally", "Ramza"), ("self", "Ramza") }),
                MakeAbility("Focus", "self", null),
            };

            var result = AbilityCompactor.Compact(abilities);

            // Attack stays (S60 policy: always visible)
            Assert.Contains(result, a => a.Name == "Attack");
            // Fire is enemy-target and stays hidden when no enemies in tiles
            Assert.DoesNotContain(result, a => a.Name == "Fire");
            // Tailwind and Focus should remain
            Assert.Contains(result, a => a.Name == "Tailwind");
            Assert.Contains(result, a => a.Name == "Focus");
        }

        [Fact]
        public void KeepEnemyTargetAbilities_WhenEnemyPresent()
        {
            var abilities = new List<AbilityEntry>
            {
                MakeAbility("Attack", "enemy", new[] { ("enemy", "Skeleton"), ("ally", "Ramza") }),
            };

            var result = AbilityCompactor.Compact(abilities);

            Assert.Single(result);
            Assert.Equal("Attack", result[0].Name);
        }

        [Fact]
        public void CollapseAimFamily_WithIdenticalTargets()
        {
            var targets = new[] { ("enemy", "Skeleton"), ("ally", "Ramza") };
            var abilities = new List<AbilityEntry>
            {
                MakeAbility("Aim +1", "enemy", targets),
                MakeAbility("Aim +2", "enemy", targets),
                MakeAbility("Aim +3", "enemy", targets),
                MakeAbility("Aim +5", "enemy", targets),
                MakeAbility("Aim +7", "enemy", targets),
                MakeAbility("Aim +10", "enemy", targets),
                MakeAbility("Aim +20", "enemy", targets),
            };

            var result = AbilityCompactor.Compact(abilities);

            Assert.Single(result);
            Assert.Equal("Aim (+1 to +20)", result[0].Name);
            // Should preserve the target tiles from the first entry
            Assert.Equal(2, result[0].ValidTargetTiles!.Count);
        }

        [Fact]
        public void DoNotCollapse_WhenTargetsDiffer()
        {
            var abilities = new List<AbilityEntry>
            {
                MakeAbility("Aim +1", "enemy", new[] { ("enemy", "Skeleton") }),
                MakeAbility("Aim +2", "enemy", new[] { ("enemy", "Skeleton"), ("enemy", "Goblin") }),
            };

            var result = AbilityCompactor.Compact(abilities);

            Assert.Equal(2, result.Count);
            Assert.Equal("Aim +1", result[0].Name);
            Assert.Equal("Aim +2", result[1].Name);
        }

        [Fact]
        public void DoNotCollapse_NonNumberedAbilities()
        {
            var targets = new[] { ("ally", "Ramza") };
            var abilities = new List<AbilityEntry>
            {
                MakeAbility("Potion", "ally", targets),
                MakeAbility("Hi-Potion", "ally", targets),
                MakeAbility("X-Potion", "ally", targets),
            };

            var result = AbilityCompactor.Compact(abilities);

            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void CollapseSkipsHiddenAbilities_InMiddle()
        {
            // Aim +3 has no enemies — gets hidden. But Aim +1, +2, +5 should still collapse.
            var withEnemy = new[] { ("enemy", "Skeleton") };
            var allyOnly = new[] { ("ally", "Ramza") };
            var abilities = new List<AbilityEntry>
            {
                MakeAbility("Aim +1", "enemy", withEnemy),
                MakeAbility("Aim +2", "enemy", withEnemy),
                MakeAbility("Aim +3", "enemy", allyOnly), // will be hidden
                MakeAbility("Aim +5", "enemy", withEnemy),
            };

            var result = AbilityCompactor.Compact(abilities);

            // Aim +3 hidden, remaining collapse: Aim (+1 to +5)
            Assert.Single(result);
            Assert.Equal("Aim (+1 to +5)", result[0].Name);
        }

        private static AbilityEntry MakeAbility(string name, string target, (string occupant, string unitName)[]? tiles)
        {
            var entry = new AbilityEntry { Name = name, Target = target };
            if (tiles != null)
            {
                entry.ValidTargetTiles = tiles.Select((t, i) => new ValidTargetTile
                {
                    X = i, Y = i,
                    Occupant = t.occupant,
                    UnitName = t.unitName,
                }).ToList();
            }
            return entry;
        }
    }
}
