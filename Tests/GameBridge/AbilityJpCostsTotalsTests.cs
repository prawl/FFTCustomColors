using FFTColorCustomizer.GameBridge;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pins the total JP cost per skillset (sum of every priced ability).
    /// Any additional/removed/repriced ability shows up here as a changed
    /// total. Ensures bulk edits don't silently drift numbers.
    ///
    /// Session 47. Totals computed from the current CostByName table at
    /// pin time; update when a deliberate change lands.
    /// </summary>
    public class AbilityJpCostsTotalsTests
    {
        private static int TotalForSkillset(string skillsetName)
        {
            var abilities = ActionAbilityLookup.GetSkillsetAbilities(skillsetName);
            if (abilities == null) return 0;
            int total = 0;
            foreach (var a in abilities)
            {
                var cost = AbilityJpCosts.GetCost(a.Name);
                if (cost.HasValue) total += cost.Value;
            }
            return total;
        }

        // Pin each major skillset's total. The literal value pinned below
        // is whatever the current table produces — bumping it requires
        // thinking about what ability changed and why.

        [Fact]
        public void Items_Total()
        {
            // Potion/Hi-Potion/X-Potion + Ether/Hi-Ether/Elixir + status items
            // + Phoenix Down = sum of ~14 consumables.
            Assert.Equal(4040, TotalForSkillset("Items"));
        }

        [Fact]
        public void ArtsOfWar_Total()
        {
            // 8 Rend* abilities.
            Assert.Equal(2200, TotalForSkillset("Arts of War"));
        }

        [Fact]
        public void Aim_Total()
        {
            // Aim +1/+2/+3/+4/+5/+7/+10/+20.
            Assert.Equal(3300, TotalForSkillset("Aim"));
        }

        [Fact]
        public void WhiteMagicks_Total()
        {
            // Cure chain + Raise chain + Protect/Shell + Holy.
            Assert.Equal(6270, TotalForSkillset("White Magicks"));
        }

        [Fact]
        public void BlackMagicks_Total()
        {
            // Fire/Thunder/Blizzard + status + Flare/Death/Toad.
            Assert.Equal(4900, TotalForSkillset("Black Magicks"));
        }

        [Fact]
        public void Summon_Total()
        {
            // 14 summons; Zodiark counts as 0 (cost sentinel).
            Assert.Equal(8400, TotalForSkillset("Summon"));
        }

        [Fact]
        public void TimeMagicks_Total()
        {
            Assert.Equal(5530, TotalForSkillset("Time Magicks"));
        }

        [Fact]
        public void MartialArts_Total()
        {
            Assert.Equal(2700, TotalForSkillset("Martial Arts"));
        }

        [Fact]
        public void Iaido_Total()
        {
            // Ashura → Chirijiraden: 100+200+…+1000 = 5500.
            Assert.Equal(5500, TotalForSkillset("Iaido"));
        }

        [Fact]
        public void Darkness_Total()
        {
            // 4 abilities.
            Assert.Equal(2700, TotalForSkillset("Darkness"));
        }

        [Fact]
        public void Mettle_Total()
        {
            // Ramza-unique extended skillset (Fundaments 4 + 5 Mettle-only).
            // Focus/Rush/ThrowStone/Salve + Tailwind/Chant/Steel/Shout/Ultima.
            Assert.Equal(5870, TotalForSkillset("Mettle"));
        }

        [Fact]
        public void Jump_Total_NonZero_AfterBackfill()
        {
            // Session 47: Jump costs were backfilled. Sum of all 12 per-level
            // abilities should be > 0. Pin exact total.
            //   Horizontal: 150+350+550+800+1100 = 2950
            //   Vertical:   100+250+400+550+700+1000+1500 = 4500
            //   Total:      7450
            Assert.Equal(7450, TotalForSkillset("Jump"));
        }

        [Fact]
        public void HolySword_Total_IsZero()
        {
            // Intentionally uncovered — Agrias's class, story-learned only.
            Assert.Equal(0, TotalForSkillset("Holy Sword"));
        }

        [Fact]
        public void AllPinnedSkillsets_AreNonNegative()
        {
            // Sanity: no skillset should have a negative total. Catches
            // accidental negative cost entries.
            string[] pinned = {
                "Items", "Arts of War", "Aim", "White Magicks", "Black Magicks",
                "Summon", "Time Magicks", "Martial Arts", "Iaido",
                "Darkness", "Mettle", "Jump", "Holy Sword",
            };
            foreach (var s in pinned)
                Assert.True(TotalForSkillset(s) >= 0, $"Skillset '{s}' has negative total");
        }
    }
}
