using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class ArmorEquippabilityTests
    {
        [Theory]
        [InlineData("Knight", "Armor", true)]
        [InlineData("Dragoon", "Armor", true)]
        [InlineData("Samurai", "Armor", true)]
        [InlineData("Templar", "Armor", true)]
        [InlineData("White Mage", "Armor", false)]
        [InlineData("Monk", "Armor", false)]
        [InlineData("Archer", "Armor", false)]
        public void Armor_Equippability(string job, string armor, bool expected)
        {
            Assert.Equal(expected, ArmorEquippability.CanJobEquip(job, armor));
        }

        [Theory]
        [InlineData("Knight", "Helmet", true)]
        [InlineData("Dragoon", "Helmet", true)]
        [InlineData("Divine Knight", "Helmet", true)]
        [InlineData("White Mage", "Helmet", false)]
        [InlineData("Templar", "Helmet", false)] // Wiki: Templar NOT in helmet list
        public void Helmet_Equippability(string job, string armor, bool expected)
        {
            Assert.Equal(expected, ArmorEquippability.CanJobEquip(job, armor));
        }

        [Theory]
        [InlineData("White Mage", "Robe", true)]
        [InlineData("Summoner", "Robe", true)]
        [InlineData("Arithmetician", "Robe", true)]
        [InlineData("Skyseer", "Robe", true)]
        [InlineData("Netherseer", "Robe", true)]
        [InlineData("Monk", "Robe", false)]
        [InlineData("Archer", "Robe", false)]
        public void Robe_Equippability(string job, string armor, bool expected)
        {
            Assert.Equal(expected, ArmorEquippability.CanJobEquip(job, armor));
        }

        [Theory]
        [InlineData("Knight", "Shield", true)]
        [InlineData("Archer", "Shield", true)]
        [InlineData("Geomancer", "Shield", true)]
        [InlineData("Gallant Knight", "Shield", true)]
        [InlineData("White Mage", "Shield", false)]
        [InlineData("Monk", "Shield", false)]
        public void Shield_Equippability(string job, string armor, bool expected)
        {
            Assert.Equal(expected, ArmorEquippability.CanJobEquip(job, armor));
        }

        // Clothes: exclusion list — most jobs CAN equip.
        [Theory]
        [InlineData("Monk", "Clothes", true)]
        [InlineData("White Mage", "Clothes", true)]
        [InlineData("Archer", "Clothes", true)]
        [InlineData("Thief", "Clothes", true)]
        [InlineData("Knight", "Clothes", false)]
        [InlineData("Dragoon", "Clothes", false)]
        [InlineData("Samurai", "Clothes", false)]
        [InlineData("Mime", "Clothes", false)]
        [InlineData("Dragonkin", "Clothes", false)]
        public void Clothes_Equippability(string job, string armor, bool expected)
        {
            Assert.Equal(expected, ArmorEquippability.CanJobEquip(job, armor));
        }

        // Hat: exclusion list — most jobs CAN equip.
        [Theory]
        [InlineData("White Mage", "Hat", true)]
        [InlineData("Black Mage", "Hat", true)]
        [InlineData("Thief", "Hat", true)]
        [InlineData("Archer", "Hat", true)]
        [InlineData("Knight", "Hat", false)]
        [InlineData("Monk", "Hat", false)]
        [InlineData("Dragoon", "Hat", false)]
        [InlineData("Dark Knight", "Hat", false)]
        [InlineData("Divine Knight", "Hat", false)]
        public void Hat_Equippability(string job, string armor, bool expected)
        {
            Assert.Equal(expected, ArmorEquippability.CanJobEquip(job, armor));
        }

        [Fact]
        public void CanJobEquip_CaseInsensitive()
        {
            Assert.True(ArmorEquippability.CanJobEquip("knight", "armor"));
            Assert.True(ArmorEquippability.CanJobEquip("KNIGHT", "ARMOR"));
        }

        [Fact]
        public void CanJobEquip_EmptyOrNull_ReturnsFalse()
        {
            Assert.False(ArmorEquippability.CanJobEquip("", "Armor"));
            Assert.False(ArmorEquippability.CanJobEquip("Knight", ""));
            Assert.False(ArmorEquippability.CanJobEquip(null!, "Armor"));
            Assert.False(ArmorEquippability.CanJobEquip("Knight", null!));
        }

        [Fact]
        public void CanJobEquip_UnknownArmor_ReturnsFalse()
        {
            Assert.False(ArmorEquippability.CanJobEquip("Knight", "Cape"));
        }

        [Fact]
        public void AllArmorTypes_Contains6Types()
        {
            // Armor, Helmet, Robe, Shield (inclusive) + Clothes, Hat (exclusive) = 6.
            Assert.Equal(6, ArmorEquippability.AllArmorTypes.Count);
        }
    }
}
