using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class WeaponEquippabilityTests
    {
        [Theory]
        [InlineData("Gallant Knight", "Knight's Sword", true)]
        [InlineData("Knight", "Knight's Sword", true)]
        [InlineData("Dark Knight", "Knight's Sword", true)]
        [InlineData("Onion Knight", "Knight's Sword", true)]
        [InlineData("Holy Knight", "Knight's Sword", true)]
        [InlineData("Templar", "Knight's Sword", true)]
        [InlineData("Divine Knight", "Knight's Sword", true)]
        [InlineData("Squire", "Knight's Sword", false)]
        [InlineData("Chemist", "Knight's Sword", false)]
        [InlineData("White Mage", "Knight's Sword", false)]
        public void KnightSword_Equippability(string job, string weapon, bool expected)
        {
            Assert.Equal(expected, WeaponEquippability.CanJobEquip(job, weapon));
        }

        [Theory]
        [InlineData("Archer", "Bow", true)]
        [InlineData("Onion Knight", "Bow", true)]
        [InlineData("Sky Pirate", "Bow", true)]
        [InlineData("Knight", "Bow", false)]
        [InlineData("Squire", "Bow", false)]
        public void Bow_Equippability(string job, string weapon, bool expected)
        {
            Assert.Equal(expected, WeaponEquippability.CanJobEquip(job, weapon));
        }

        [Theory]
        [InlineData("White Mage", "Staff", true)]
        [InlineData("Time Mage", "Staff", true)]
        [InlineData("Summoner", "Staff", true)]
        [InlineData("Skyseer", "Staff", true)]
        [InlineData("Black Mage", "Staff", false)]
        public void Staff_Equippability(string job, string weapon, bool expected)
        {
            Assert.Equal(expected, WeaponEquippability.CanJobEquip(job, weapon));
        }

        [Theory]
        [InlineData("Black Mage", "Rod", true)]
        [InlineData("Summoner", "Rod", true)]
        [InlineData("Mystic", "Rod", true)]
        [InlineData("Netherseer", "Rod", true)]
        [InlineData("White Mage", "Rod", false)]
        public void Rod_Equippability(string job, string weapon, bool expected)
        {
            Assert.Equal(expected, WeaponEquippability.CanJobEquip(job, weapon));
        }

        [Theory]
        [InlineData("Squire", "Sword", true)]
        [InlineData("Knight", "Sword", true)]
        [InlineData("Geomancer", "Sword", true)]
        [InlineData("Onion Knight", "Sword", true)]
        [InlineData("White Mage", "Sword", false)]
        [InlineData("Monk", "Sword", false)]
        public void Sword_Equippability(string job, string weapon, bool expected)
        {
            Assert.Equal(expected, WeaponEquippability.CanJobEquip(job, weapon));
        }

        [Theory]
        [InlineData("Dragoon", "Polearm", true)]
        [InlineData("Sky Pirate", "Polearm", true)]
        [InlineData("Divine Knight", "Polearm", true)]
        [InlineData("Knight", "Polearm", false)]
        public void Polearm_Equippability(string job, string weapon, bool expected)
        {
            Assert.Equal(expected, WeaponEquippability.CanJobEquip(job, weapon));
        }

        [Theory]
        [InlineData("Dark Knight", "Fell Sword", true)]
        [InlineData("Onion Knight", "Fell Sword", true)]
        [InlineData("Gallant Knight", "Fell Sword", false)]
        public void FellSword_OnlyDarkKnightAndOnion(string job, string weapon, bool expected)
        {
            Assert.Equal(expected, WeaponEquippability.CanJobEquip(job, weapon));
        }

        [Theory]
        [InlineData("Bard", "Instrument", true)]
        [InlineData("Onion Knight", "Instrument", true)]
        [InlineData("Dancer", "Instrument", false)]
        public void Instrument_BardAndOnionOnly(string job, string weapon, bool expected)
        {
            Assert.Equal(expected, WeaponEquippability.CanJobEquip(job, weapon));
        }

        [Fact]
        public void CanJobEquip_CaseInsensitive()
        {
            Assert.True(WeaponEquippability.CanJobEquip("knight", "knight's sword"));
            Assert.True(WeaponEquippability.CanJobEquip("KNIGHT", "KNIGHT'S SWORD"));
        }

        [Fact]
        public void CanJobEquip_EmptyOrNull_ReturnsFalse()
        {
            Assert.False(WeaponEquippability.CanJobEquip("", "Sword"));
            Assert.False(WeaponEquippability.CanJobEquip("Knight", ""));
            Assert.False(WeaponEquippability.CanJobEquip(null!, "Sword"));
            Assert.False(WeaponEquippability.CanJobEquip("Knight", null!));
        }

        [Fact]
        public void CanJobEquip_UnknownWeapon_ReturnsFalse()
        {
            Assert.False(WeaponEquippability.CanJobEquip("Knight", "Lightsaber"));
        }

        [Fact]
        public void GetJobsFor_KnightsSword_Includes10Jobs()
        {
            var jobs = WeaponEquippability.GetJobsFor("Knight's Sword");
            Assert.Equal(10, jobs.Count);
        }

        [Fact]
        public void GetJobsFor_UnknownType_ReturnsEmpty()
        {
            Assert.Empty(WeaponEquippability.GetJobsFor("Lightsaber"));
        }

        [Fact]
        public void AllWeaponTypes_Contains19Types()
        {
            // Per Wiki: Axe, Bag, Book, Bow, Cloth, Crossbow, Fell Sword, Flail, Gun,
            // Instrument, Katana, Knife, Knight's Sword, Ninja Blade, Pole, Rod,
            // Polearm, Staff, Sword = 19.
            Assert.Equal(19, WeaponEquippability.AllWeaponTypes.Count);
        }

        [Fact]
        public void OnionKnight_EquipsNearlyEveryWeaponType()
        {
            // Onion Knight is in almost every weapon's list (all except Bag).
            int onionCount = 0;
            foreach (var type in WeaponEquippability.AllWeaponTypes)
            {
                if (WeaponEquippability.CanJobEquip("Onion Knight", type))
                    onionCount++;
            }
            Assert.True(onionCount >= 17, $"Onion Knight equips {onionCount} types, expected ≥17");
        }
    }
}
