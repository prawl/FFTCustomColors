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

        // Session 35: additional edge coverage.

        [Fact]
        public void Compact_NonConsecutiveFamilyMembers_NotCollapsed()
        {
            // Aim +1, Fire, Aim +2 — the Fire interrupts the run, so Aim
            // entries should not collapse into a single range.
            var input = new List<AbilityEntry>
            {
                MakeAbility("Aim +1", "enemy", (5, 5, "enemy")),
                MakeAbility("Fire", "enemy", (5, 5, "enemy")),
                MakeAbility("Aim +2", "enemy", (5, 5, "enemy")),
            };
            var result = AbilityCompactor.Compact(input);
            Assert.Equal(3, result.Count);
            Assert.Equal("Aim +1", result[0].Name);
            Assert.Equal("Fire", result[1].Name);
            Assert.Equal("Aim +2", result[2].Name);
        }

        [Fact]
        public void Compact_FamilyWithHiddenMiddleEntry_SkipsAndCollapsesVisible()
        {
            // Aim +1 has enemies, Aim +2 doesn't, Aim +3 has enemies.
            // The compactor skips hidden entries inside the family run and
            // collapses the visible pair (+1, +3) — matching the existing
            // production loop's "skip hidden, keep scanning" behavior.
            var input = new List<AbilityEntry>
            {
                MakeAbility("Aim +1", "enemy", (5, 5, "enemy")),
                MakeAbility("Aim +2", "enemy", (5, 5, "ally")),   // hidden
                MakeAbility("Aim +3", "enemy", (5, 5, "enemy")),
            };
            var result = AbilityCompactor.Compact(input);
            Assert.Single(result);
            Assert.Contains("+1", result[0].Name);
            Assert.Contains("+3", result[0].Name);
        }

        [Fact]
        public void Compact_TwoIndependentFamilies_BothCollapse()
        {
            var input = new List<AbilityEntry>
            {
                MakeAbility("Aim +1", "enemy", (5, 5, "enemy")),
                MakeAbility("Aim +2", "enemy", (5, 5, "enemy")),
                MakeAbility("Charge +1", "enemy", (6, 6, "enemy")),
                MakeAbility("Charge +2", "enemy", (6, 6, "enemy")),
                MakeAbility("Charge +3", "enemy", (6, 6, "enemy")),
            };
            var result = AbilityCompactor.Compact(input);
            Assert.Equal(2, result.Count);
            Assert.Contains("Aim", result[0].Name);
            Assert.Contains("Charge", result[1].Name);
        }

        [Fact]
        public void Compact_CollapsedFamily_PreservesElementAndMp()
        {
            // When a family collapses, non-tile fields (Element, Mp, HRange,
            // CastSpeed, AddedEffect) should come from the first entry.
            var input = new List<AbilityEntry>
            {
                new AbilityEntry
                {
                    Name = "Fira +1",
                    Target = "enemy",
                    Element = "Fire",
                    Mp = 10,
                    HRange = "3",
                    CastSpeed = 8,
                    ValidTargetTiles = new List<ValidTargetTile>
                    {
                        new ValidTargetTile { X = 5, Y = 5, Occupant = "enemy" }
                    },
                    TotalTargets = 1,
                },
                new AbilityEntry
                {
                    Name = "Fira +2",
                    Target = "enemy",
                    Element = "Fire",
                    Mp = 15, // different — should be overridden by first entry's value
                    HRange = "3",
                    CastSpeed = 8,
                    ValidTargetTiles = new List<ValidTargetTile>
                    {
                        new ValidTargetTile { X = 5, Y = 5, Occupant = "enemy" }
                    },
                    TotalTargets = 1,
                },
            };
            var result = AbilityCompactor.Compact(input);
            Assert.Single(result);
            Assert.Equal("Fire", result[0].Element);
            Assert.Equal(10, result[0].Mp);
            Assert.Equal("3", result[0].HRange);
            Assert.Equal(8, result[0].CastSpeed);
        }

        [Fact]
        public void Compact_NameWithoutTrailingNumber_NotCollapsed()
        {
            // "Cure" doesn't match the numbered-family regex — should pass
            // through unchanged even with identical tile lists.
            var input = new List<AbilityEntry>
            {
                MakeAbility("Cure", "ally", (5, 5, "ally")),
                MakeAbility("Cura", "ally", (5, 5, "ally")),
            };
            var result = AbilityCompactor.Compact(input);
            Assert.Equal(2, result.Count);
        }

        // Session 36: direct tests for the extracted IsHidden helper.

        [Fact]
        public void IsHidden_EnemyAbilityWithEnemyOccupant_False()
        {
            var a = MakeAbility("Fire", "enemy", (5, 5, "enemy"));
            Assert.False(AbilityCompactor.IsHidden(a));
        }

        [Fact]
        public void IsHidden_EnemyAbilityWithoutEnemyOccupant_True()
        {
            var a = MakeAbility("Fire", "enemy", (5, 5, "ally"), (5, 6, null));
            Assert.True(AbilityCompactor.IsHidden(a));
        }

        [Fact]
        public void IsHidden_EnemyAbilityEmptyTileList_True()
        {
            var a = MakeAbility("Fire", "enemy");
            Assert.True(AbilityCompactor.IsHidden(a));
        }

        [Fact]
        public void IsHidden_BasicAttackNoTargets_AlwaysVisible()
        {
            // S60: the basic Attack entry should ALWAYS appear in the
            // active unit's ability list — Claude needs to see the
            // weapon's range / element / on-hit effect even when no
            // enemy is adjacent, so positioning + ability-pick decisions
            // can be made with that info in hand.
            var a = MakeAbility("Attack", "enemy");
            Assert.False(AbilityCompactor.IsHidden(a));
        }

        [Fact]
        public void IsHidden_BasicAttackWithNonEnemyOccupants_StillVisible()
        {
            // Even if only allies/empty tiles are in range, Attack stays.
            var a = MakeAbility("Attack", "enemy", (5, 5, "ally"), (5, 6, null));
            Assert.False(AbilityCompactor.IsHidden(a));
        }

        [Fact]
        public void IsHidden_AllyTarget_AlwaysFalse_EvenWithNoTiles()
        {
            // Ally-target abilities aren't hidden even with no targets —
            // the ability is still potentially useful for self-cast etc.
            var a = MakeAbility("Cure", "ally");
            Assert.False(AbilityCompactor.IsHidden(a));
        }

        [Fact]
        public void IsHidden_SelfTarget_AlwaysFalse()
        {
            var a = MakeAbility("Shout", "self");
            Assert.False(AbilityCompactor.IsHidden(a));
        }
    }
}
