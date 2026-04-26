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
        public void EnemyTargetAbilities_WithNoEnemyOccupants_StillVisible()
        {
            // 2026-04-26 P6: hiding rule removed. Agents need to see
            // their full skillset even when no enemy is in range so they
            // can plan a move that brings a target into range.
            var abilities = new List<AbilityEntry>
            {
                MakeAbility("Attack", "enemy", new[] { ("ally", "Ramza"), ("ally", "Archer") }),
                MakeAbility("Fire", "enemy", new[] { ("ally", "Ramza"), ("ally", "Archer") }),
                MakeAbility("Tailwind", "ally", new[] { ("ally", "Ramza"), ("self", "Ramza") }),
                MakeAbility("Focus", "self", null),
            };

            var result = AbilityCompactor.Compact(abilities);

            // All four pass through.
            Assert.Equal(4, result.Count);
            Assert.Contains(result, a => a.Name == "Attack");
            Assert.Contains(result, a => a.Name == "Fire");
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
        public void CollapseHonorsTileDifferences_NoFamilyMerge()
        {
            // 2026-04-26 P6: with hiding removed, family-collapse must
            // still respect tile-key differences. Aim +3 here has a
            // distinct tile coordinate, breaking the run of identical
            // keys, so collapse splits into ("Aim (+1 to +2)", "Aim +3",
            // "Aim +5"). The MakeAbility helper indexes tiles by array
            // position, so we use distinct array LENGTHS (which produce
            // different X/Y coordinates) to make the tile keys differ.
            var oneEnemy = new[] { ("enemy", "Skeleton") };
            var twoEnemies = new[] { ("enemy", "Skeleton"), ("enemy", "Goblin") };
            var abilities = new List<AbilityEntry>
            {
                MakeAbility("Aim +1", "enemy", oneEnemy),
                MakeAbility("Aim +2", "enemy", oneEnemy),
                MakeAbility("Aim +3", "enemy", twoEnemies),  // different tile-key
                MakeAbility("Aim +5", "enemy", oneEnemy),
            };

            var result = AbilityCompactor.Compact(abilities);

            // Family-collapse breaks on tile mismatch.
            Assert.Equal(3, result.Count);
            Assert.Equal("Aim (+1 to +2)", result[0].Name);
            Assert.Equal("Aim +3", result[1].Name);
            Assert.Equal("Aim +5", result[2].Name);
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
