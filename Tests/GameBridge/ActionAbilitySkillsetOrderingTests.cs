using System.Linq;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pin the skillset ability ordering used by the ability-list cursor nav. Session 33
    /// TODO flagged a bug where `battle_ability "Aurablast"` selected Pummel — caused by
    /// the nav code pressing Down N times to reach index N, where N came from the hardcoded
    /// skillset order in `ActionAbilityLookup.Skillsets`. These tests lock the order so a
    /// future reshuffle (e.g. to alphabetize or group by cost) would break the test before
    /// it ships a silent off-by-one in battle.
    /// </summary>
    public class ActionAbilitySkillsetOrderingTests
    {
        [Fact]
        public void MartialArts_OrderingIsStable()
        {
            var abilities = ActionAbilityLookup.GetSkillsetAbilities("Martial Arts");
            Assert.NotNull(abilities);
            var names = abilities!.Select(a => a.Name).ToList();
            // Expected order from AbilityData source, pinned so nav math stays correct.
            Assert.Equal("Cyclone", names[0]);
            Assert.Equal("Pummel", names[1]);
            Assert.Equal("Aurablast", names[2]);
            Assert.Equal("Shockwave", names[3]);
            Assert.Equal("Doom Fist", names[4]);
            Assert.Equal("Purification", names[5]);
            Assert.Equal("Chakra", names[6]);
            Assert.Equal("Revive", names[7]);
        }

        [Fact]
        public void BlackMagicks_FireBlizzardThunder_InOrder()
        {
            var abilities = ActionAbilityLookup.GetSkillsetAbilities("Black Magicks");
            Assert.NotNull(abilities);
            var names = abilities!.Select(a => a.Name).ToList();
            // Fire tier, Thunder tier, Blizzard tier — each triplet contiguous.
            int fireIdx = names.IndexOf("Fire");
            int fireraIdx = names.IndexOf("Fira");
            int firagaIdx = names.IndexOf("Firaga");
            Assert.True(fireIdx >= 0);
            Assert.Equal(fireIdx + 1, fireraIdx);
            Assert.Equal(fireraIdx + 1, firagaIdx);
        }

        [Fact]
        public void MartialArts_IndexOfAurablast_Is2()
        {
            // Direct pin for the reported bug: Aurablast must resolve to index 2.
            var abilities = ActionAbilityLookup.GetSkillsetAbilities("Martial Arts");
            Assert.NotNull(abilities);
            int idx = abilities!.FindIndex(a => a.Name == "Aurablast");
            Assert.Equal(2, idx);
        }

        [Fact]
        public void MartialArts_IndexOfPummel_Is1()
        {
            var abilities = ActionAbilityLookup.GetSkillsetAbilities("Martial Arts");
            Assert.NotNull(abilities);
            int idx = abilities!.FindIndex(a => a.Name == "Pummel");
            Assert.Equal(1, idx);
        }

        [Fact]
        public void UnknownSkillset_ReturnsNull()
        {
            Assert.Null(ActionAbilityLookup.GetSkillsetAbilities("Not A Real Skillset"));
        }

        [Fact]
        public void GetById_Aurablast_0x79_ReturnsAurablast()
        {
            var info = ActionAbilityLookup.GetById(0x79);
            Assert.NotNull(info);
            Assert.Equal("Aurablast", info!.Name);
        }

        [Fact]
        public void MartialArts_NamesUnique()
        {
            // Guard: duplicate names would break name→index resolution during nav.
            var abilities = ActionAbilityLookup.GetSkillsetAbilities("Martial Arts");
            Assert.NotNull(abilities);
            var names = abilities!.Select(a => a.Name).ToList();
            Assert.Equal(names.Count, names.Distinct().Count());
        }
    }
}
