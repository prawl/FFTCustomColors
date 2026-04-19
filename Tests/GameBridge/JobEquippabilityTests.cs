using FFTColorCustomizer.GameBridge;
using System.Linq;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Reverse lookup on top of WeaponEquippability + ArmorEquippability.
    /// Given a job name, returns all equipment types the job can inherently
    /// equip (no Equip-* support skill).
    ///
    /// Use case (TODO §10.6 per-job equippability): EquippableWeapons picker
    /// needs to render "greyed-out" hints for items the viewed unit's job
    /// can't use. Without a reverse lookup, callers would iterate every
    /// weapon/armor type and ask CanJobEquip — cleaner to expose one call.
    /// </summary>
    public class JobEquippabilityTests
    {
        [Fact]
        public void Knight_EquipsCoreKnightGear()
        {
            var types = JobEquippability.GetEquipmentTypes("Knight");

            // Knight's the canonical heavy class: Knight's Sword + Sword +
            // Armor + Helmet + Robe + Shield. Clothes/Hats are excluded.
            Assert.Contains("Knight's Sword", types);
            Assert.Contains("Sword", types);
            Assert.Contains("Armor", types);
            Assert.Contains("Helmet", types);
            Assert.Contains("Shield", types);
            Assert.Contains("Robe", types);
            Assert.DoesNotContain("Clothes", types);
            Assert.DoesNotContain("Hat", types);
            // Knight doesn't equip magic/ranged weapons natively.
            Assert.DoesNotContain("Rod", types);
            Assert.DoesNotContain("Staff", types);
            Assert.DoesNotContain("Bow", types);
        }

        [Fact]
        public void WhiteMage_EquipsCasterGear()
        {
            var types = JobEquippability.GetEquipmentTypes("White Mage");

            Assert.Contains("Staff", types);
            Assert.Contains("Robe", types);
            Assert.Contains("Clothes", types);
            Assert.Contains("Hat", types);
            // White mages don't equip heavy armor or weapons.
            Assert.DoesNotContain("Armor", types);
            Assert.DoesNotContain("Helmet", types);
            Assert.DoesNotContain("Knight's Sword", types);
            Assert.DoesNotContain("Shield", types);
        }

        [Fact]
        public void OnionKnight_EquipsAlmostEverything()
        {
            // Onion Knight is in nearly every weapon's list + most armor
            // lists. This test pins the "meta-class" shape per the Wiki.
            var types = JobEquippability.GetEquipmentTypes("Onion Knight");
            // Should include all 4 main armor types + many weapon types.
            Assert.Contains("Armor", types);
            Assert.Contains("Helmet", types);
            Assert.Contains("Robe", types);
            Assert.Contains("Shield", types);
            Assert.True(types.Count >= 20,
                $"Onion Knight equips {types.Count} types, expected ≥20");
        }

        [Fact]
        public void GallantKnight_EquipsShield_NotArmor()
        {
            // Ramza's unique class: per Wiki, Gallant Knight is in the
            // Shield list but NOT in the Armor/Helmet lists (he wears
            // Robes/Clothes, not heavy).
            var types = JobEquippability.GetEquipmentTypes("Gallant Knight");
            Assert.Contains("Shield", types);
            Assert.Contains("Knight's Sword", types);
            Assert.DoesNotContain("Armor", types);
            Assert.DoesNotContain("Helmet", types);
        }

        [Fact]
        public void Dancer_BagAndCloth_ButNotKnightsSword()
        {
            var types = JobEquippability.GetEquipmentTypes("Dancer");
            Assert.Contains("Bag", types);
            Assert.Contains("Cloth", types);
            Assert.Contains("Knife", types); // per Wiki weapons list
            Assert.DoesNotContain("Knight's Sword", types);
            Assert.DoesNotContain("Armor", types);
        }

        [Fact]
        public void UnknownJob_ReturnsEmptyForWeapons_ButHatClothesStillIncluded()
        {
            // An unknown job name isn't in any weapon's inclusive list, so
            // weapons return empty. For exclusion lists (Hat, Clothes) an
            // unknown job is "not excluded" — so technically gets them.
            // This pins the table semantics; whether a caller WANTS a
            // catch-all for unknown jobs is their choice.
            var types = JobEquippability.GetEquipmentTypes("NotARealJob");
            Assert.DoesNotContain("Sword", types);
            Assert.DoesNotContain("Armor", types);
            // Exclusion-list armors default to "allowed" for unknown jobs.
            Assert.Contains("Hat", types);
            Assert.Contains("Clothes", types);
        }

        [Fact]
        public void GetEquipmentTypes_EmptyOrNull_ReturnsEmpty()
        {
            Assert.Empty(JobEquippability.GetEquipmentTypes(""));
            Assert.Empty(JobEquippability.GetEquipmentTypes(null!));
        }

        [Fact]
        public void GetEquipmentTypes_CaseInsensitive()
        {
            var lower = JobEquippability.GetEquipmentTypes("knight");
            var upper = JobEquippability.GetEquipmentTypes("KNIGHT");
            var title = JobEquippability.GetEquipmentTypes("Knight");
            Assert.Equal(title.OrderBy(x => x), lower.OrderBy(x => x));
            Assert.Equal(title.OrderBy(x => x), upper.OrderBy(x => x));
        }
    }
}
