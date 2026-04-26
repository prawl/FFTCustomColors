using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Items skillset has R=4 (Chemist primary range) baked into the
    /// ability metadata. When the user runs Items as a SECONDARY skillset
    /// (any class with Items secondary), every item nerfs to R=1 unless
    /// they have the "Throw Items" support ability. Live-flagged
    /// 2026-04-26 playtest at Siedge Weald: Ramza(Knight) with Items
    /// secondary saw the bridge surface Phoenix Down `R:4 → (6,0)<Skeleton [KO]>`
    /// at d=3 and the action consumed without effect — the in-game
    /// range was actually 1.
    /// </summary>
    public class ItemRangeAdjusterTests
    {
        [Fact]
        public void Items_AsPrimary_KeepsRangeFour()
        {
            // Chemist class — Items is primary. Full R=4 applies.
            var range = ItemRangeAdjuster.Adjust(
                skillsetName: "Items",
                originalHRange: "4",
                primarySkillset: "Items",
                supportAbility: null);
            Assert.Equal("4", range);
        }

        [Fact]
        public void Items_AsSecondary_NoThrowItems_NerfsToOne()
        {
            // Non-Chemist with Items secondary: nerfed to R=1.
            var range = ItemRangeAdjuster.Adjust(
                skillsetName: "Items",
                originalHRange: "4",
                primarySkillset: "Arts of War",
                supportAbility: null);
            Assert.Equal("1", range);
        }

        [Fact]
        public void Items_AsSecondary_WithThrowItems_KeepsRangeFour()
        {
            // Non-Chemist with Items secondary + "Throw Items" support: full R=4.
            var range = ItemRangeAdjuster.Adjust(
                skillsetName: "Items",
                originalHRange: "4",
                primarySkillset: "Arts of War",
                supportAbility: "Throw Items");
            Assert.Equal("4", range);
        }

        [Fact]
        public void Items_AsSecondary_WithUnrelatedSupport_NerfsToOne()
        {
            // Other support abilities don't grant the range bonus.
            var range = ItemRangeAdjuster.Adjust(
                skillsetName: "Items",
                originalHRange: "4",
                primarySkillset: "Arts of War",
                supportAbility: "Reequip");
            Assert.Equal("1", range);
        }

        [Fact]
        public void NonItemsSkillset_RangeUnchanged()
        {
            // Range adjustment only applies to the Items skillset.
            var range = ItemRangeAdjuster.Adjust(
                skillsetName: "Black Magicks",
                originalHRange: "4",
                primarySkillset: "Arts of War",
                supportAbility: null);
            Assert.Equal("4", range);
        }

        [Fact]
        public void NonItemsSkillset_ChemistPrimary_RangeUnchanged()
        {
            // Even if the unit IS a Chemist, non-Items skillsets aren't affected.
            var range = ItemRangeAdjuster.Adjust(
                skillsetName: "White Magicks",
                originalHRange: "3",
                primarySkillset: "Items",
                supportAbility: null);
            Assert.Equal("3", range);
        }

        [Fact]
        public void Items_NullSkillsets_NoCrash_NerfsToOne()
        {
            // Defensive: if primary skillset can't be resolved, default to
            // nerfed range (safer — agent sees R=1 rather than the optimistic R=4).
            var range = ItemRangeAdjuster.Adjust(
                skillsetName: "Items",
                originalHRange: "4",
                primarySkillset: null,
                supportAbility: null);
            Assert.Equal("1", range);
        }

        [Fact]
        public void Items_SelfRange_StillSelf()
        {
            // Self-only abilities (HRange="Self") aren't ranged — the adjustment
            // shouldn't touch them. (No Items abilities are Self today, but the
            // helper should be defensive.)
            var range = ItemRangeAdjuster.Adjust(
                skillsetName: "Items",
                originalHRange: "Self",
                primarySkillset: "Arts of War",
                supportAbility: null);
            Assert.Equal("Self", range);
        }
    }
}
