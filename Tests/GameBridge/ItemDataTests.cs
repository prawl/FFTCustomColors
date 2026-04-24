using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class ItemDataTests
    {
        [Fact]
        public void GetAttackRange_UnknownId_Returns1()
        {
            // Unknown equipment ID should default to melee range 1
            Assert.Equal(1, ItemData.GetAttackRange(9999));
        }

        [Fact]
        public void GetAttackRange_EmptySlot_Returns1()
        {
            // 0xFF and 0xFFFF are empty equipment slots
            Assert.Equal(1, ItemData.GetAttackRange(0xFF));
            Assert.Equal(1, ItemData.GetAttackRange(0xFFFF));
        }

        [Fact]
        public void GetAttackRange_MeleeWeapon_Returns1()
        {
            // Dagger (FFTPatcher ID 1) has Range=1
            Assert.Equal(1, ItemData.GetAttackRange(1));
        }

        [Fact]
        public void GetAttackRange_Bow_Returns5()
        {
            // Longbow (FFTPatcher ID 83) has Range=5
            Assert.Equal(5, ItemData.GetAttackRange(83));
        }

        [Fact]
        public void GetAttackRange_Gun_Returns8()
        {
            // Romandan Pistol (FFTPatcher ID 71) has Range=8
            Assert.Equal(8, ItemData.GetAttackRange(71));
        }

        [Fact]
        public void GetAttackRange_Crossbow_Returns4()
        {
            // Bowgun (FFTPatcher ID 77) has Range=4
            Assert.Equal(4, ItemData.GetAttackRange(77));
        }

        [Fact]
        public void GetAttackRange_NonWeapon_Returns1()
        {
            // Shield (FFTPatcher ID 130 = Escutcheon) is not a weapon
            Assert.Equal(1, ItemData.GetAttackRange(130));
        }

        [Fact]
        public void GetWeaponRangeFromEquipment_FirstWeaponDeterminesRange()
        {
            // Longbow (83) is the first weapon → range 5
            var equipment = new List<int> { 83, 130, 156 };
            Assert.Equal(5, ItemData.GetWeaponRangeFromEquipment(equipment));
        }

        [Fact]
        public void GetWeaponRangeFromEquipment_SkipsNonWeapons()
        {
            // Shield (130) first, then Dagger (1) → range 1 from Dagger
            var equipment = new List<int> { 130, 1 };
            Assert.Equal(1, ItemData.GetWeaponRangeFromEquipment(equipment));
        }

        [Fact]
        public void GetWeaponRangeFromEquipment_NullOrEmpty_Returns1()
        {
            Assert.Equal(1, ItemData.GetWeaponRangeFromEquipment(null));
            Assert.Equal(1, ItemData.GetWeaponRangeFromEquipment(new List<int>()));
        }

        [Fact]
        public void GetWeaponRangeFromEquipment_AllNonWeapons_Returns1()
        {
            // All armor/accessories → melee range
            var equipment = new List<int> { 130, 156, 185 };
            Assert.Equal(1, ItemData.GetWeaponRangeFromEquipment(equipment));
        }

        [Fact]
        public void GetWeaponRangeFromEquipment_GunInSlot0_Returns8()
        {
            // Romandan Pistol (71) first → range 8
            var equipment = new List<int> { 71 };
            Assert.Equal(8, ItemData.GetWeaponRangeFromEquipment(equipment));
        }

        [Fact]
        public void AttackAbility_UsesWeaponRange_ForTargetTiles()
        {
            // Attack with a gun (range 8) should produce tiles at distances 1-8, not just 4 adjacent
            var map = new MapData { Width = 20, Height = 20, Tiles = new MapTile[20, 20] };
            for (int x = 0; x < 20; x++)
                for (int y = 0; y < 20; y++)
                    map.Tiles[x, y] = new MapTile { Height = 0, NoWalk = false };

            int weaponRange = 8; // gun
            var attackInfo = new ActionAbilityInfo(
                ActionAbilityLookup.ATTACK_ID, "Attack", 0,
                weaponRange.ToString(), 0, 1, 0, "enemy",
                "Attacks with the equipped weapon");

            var tiles = AbilityTargetCalculator.GetValidTargetTiles(10, 10, attackInfo, map);

            // Should include tiles at range 8 (e.g. 10,2 is distance 8 from 10,10)
            Assert.Contains((10, 2), tiles);
            // Should NOT include the caster tile (enemy-target)
            Assert.DoesNotContain((10, 10), tiles);
            // Should have many more than 4 tiles
            Assert.True(tiles.Count > 4, $"Expected >4 tiles for range 8, got {tiles.Count}");
        }

        [Fact]
        public void BuildAttackAbilityInfo_UsesEquipmentRange()
        {
            // Gun-wielding unit: Attack should have range 8
            var gun = new List<int> { 71 }; // Romandan Pistol
            var info = ItemData.BuildAttackAbilityInfo(gun);
            Assert.Equal("8", info.HRange);
            Assert.Equal("Attack", info.Name);
            Assert.Equal("enemy", info.Target);
        }

        [Fact]
        public void BuildAttackAbilityInfo_NoEquipment_Range1()
        {
            var info = ItemData.BuildAttackAbilityInfo(null);
            Assert.Equal("1", info.HRange);
        }

        [Fact]
        public void BuildAttackAbilityInfo_MeleeWeapon_Range1()
        {
            var sword = new List<int> { 19 }; // Broadsword
            var info = ItemData.BuildAttackAbilityInfo(sword);
            Assert.Equal("1", info.HRange);
        }

        [Fact]
        public void BuildAttackAbilityInfo_Gun_MinRange2()
        {
            // Guns can't hit adjacent tiles — min range is 2, not 3.
            // In FFT, guns skip the tile right next to you but CAN hit 2 tiles away.
            var gun = new List<int> { 71 }; // Romandan Pistol
            var info = ItemData.BuildAttackAbilityInfo(gun);
            Assert.Equal(2, info.MinRange);
        }

        [Fact]
        public void BuildAttackAbilityInfo_Bow_MinRange2()
        {
            // Bows skip adjacent tile — min range 2 (same as guns).
            var bow = new List<int> { 83 }; // Longbow
            var info = ItemData.BuildAttackAbilityInfo(bow);
            Assert.Equal(2, info.MinRange);
        }

        [Fact]
        public void BuildAttackAbilityInfo_Crossbow_MinRange3()
        {
            // Crossbows have min range 3 and max range 4.
            var crossbow = new List<int> { 77 }; // Bowgun
            var info = ItemData.BuildAttackAbilityInfo(crossbow);
            Assert.Equal(3, info.MinRange);
        }

        [Fact]
        public void BuildAttackAbilityInfo_MeleeWeapon_NoMinRange()
        {
            var sword = new List<int> { 19 }; // Broadsword
            var info = ItemData.BuildAttackAbilityInfo(sword);
            Assert.Equal(0, info.MinRange);
        }

        /// <summary>
        /// Session 51: fixed in BuildAttackAbilityInfo — ranged weapons
        /// (bow/gun/crossbow) now get VRange=99 (unlimited vertical reach),
        /// matching the game's arc-trajectory behavior. Melee weapons keep
        /// VRange=0 so they fall back to caster Jump in AbilityTargetCalculator
        /// (which correctly caps melee reach).
        /// </summary>
        [Fact]
        public void BuildAttackAbilityInfo_Bow_VRange99_Unlimited()
        {
            var bow = new List<int> { 83 }; // Longbow
            var info = ItemData.BuildAttackAbilityInfo(bow);
            Assert.Equal(99, info.VRange);
        }

        [Fact]
        public void BuildAttackAbilityInfo_Gun_VRange99_Unlimited()
        {
            var gun = new List<int> { 71 }; // Romandan Pistol
            var info = ItemData.BuildAttackAbilityInfo(gun);
            Assert.Equal(99, info.VRange);
        }

        [Fact]
        public void BuildAttackAbilityInfo_MeleeSword_VRange0_FallsBackToJump()
        {
            var sword = new List<int> { 19 }; // Broadsword
            var info = ItemData.BuildAttackAbilityInfo(sword);
            Assert.Equal(0, info.VRange);
        }

        [Fact]
        public void BuildAttackAbilityInfo_NoEquipment_VRange0()
        {
            var info = ItemData.BuildAttackAbilityInfo(null);
            Assert.Equal(0, info.VRange);
        }

        // --- GetEquippedWeapon / ComposeWeaponTag (S60) ---

        [Fact]
        public void GetEquippedWeapon_FindsFirstWeapon()
        {
            // Dagger (1) is the first weapon — returned regardless of other slots.
            var equipment = new List<int> { 130, 1, 156 }; // Shield, Dagger, Grand Helm
            var weapon = ItemData.GetEquippedWeapon(equipment);
            Assert.NotNull(weapon);
            Assert.Equal("Dagger", weapon!.Name);
        }

        [Fact]
        public void GetEquippedWeapon_NullOrEmpty_ReturnsNull()
        {
            Assert.Null(ItemData.GetEquippedWeapon(null));
            Assert.Null(ItemData.GetEquippedWeapon(new List<int>()));
        }

        [Fact]
        public void GetEquippedWeapon_UnarmedWithArmorOnly_ReturnsNull()
        {
            // Only non-weapon items — returns null (unarmed).
            var equipment = new List<int> { 130, 156, 185 };
            Assert.Null(ItemData.GetEquippedWeapon(equipment));
        }

        [Fact]
        public void ComposeWeaponTag_UnarmedOrUnknown_ReturnsEmpty()
        {
            Assert.Equal("", ItemData.ComposeWeaponTag(null));
            Assert.Equal("", ItemData.ComposeWeaponTag(new List<int>()));
            Assert.Equal("", ItemData.ComposeWeaponTag(new List<int> { 9999 })); // unknown id
        }

        [Fact]
        public void ComposeWeaponTag_WeaponWithoutOnHitEffect_ReturnsNameOnly()
        {
            // Broadsword (19) has no AttackEffects set.
            Assert.Equal("Broadsword", ItemData.ComposeWeaponTag(new List<int> { 19 }));
        }

        [Fact]
        public void ComposeWeaponTag_ChaosBlade_AppendsOnHitEffect()
        {
            // Chaos Blade (37) has AttackEffects = "On hit: chance to add Stone".
            // The tag should surface the on-hit text (stripping the redundant
            // "On hit: " prefix) so Claude knows a basic attack has a petrify
            // proc chance.
            var tag = ItemData.ComposeWeaponTag(new List<int> { 37 });
            Assert.Equal("Chaos Blade onHit:chance to add Stone", tag);
        }
    }
}
