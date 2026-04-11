using FFTColorCustomizer.GameBridge;
using Xunit;
using System.Collections.Generic;
using System.Linq;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class AbilityTargetCalculatorTests
    {
        /// <summary>Build a flat N×N walkable map with every tile at the same height.</summary>
        private static MapData FlatMap(int size, int height = 0)
        {
            var map = new MapData { Width = size, Height = size, Tiles = new MapTile[size, size] };
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                    map.Tiles[x, y] = new MapTile { Height = height, NoWalk = false };
            return map;
        }

        [Fact]
        public void IsPointTarget_ExcludesSelfHRange()
        {
            var ability = new ActionAbilityInfo(0, "Focus", 0, "Self", 0, 1, 0, "self", "");
            Assert.False(AbilityTargetCalculator.IsPointTarget(ability));
        }

        [Fact]
        public void IsPointTarget_ExcludesNonUnitAoE()
        {
            var ability = new ActionAbilityInfo(0, "Fire", 0, "4", 99, 2, 0, "enemy/AoE", "");
            Assert.False(AbilityTargetCalculator.IsPointTarget(ability));
        }

        [Fact]
        public void IsPointTarget_AcceptsSingleTileRanged()
        {
            var ability = new ActionAbilityInfo(0, "Rush", 0, "1", 1, 1, 0, "enemy", "");
            Assert.True(AbilityTargetCalculator.IsPointTarget(ability));
        }

        [Fact]
        public void GetValidTargetTiles_EnemyRange1_ExcludesCasterTile()
        {
            // Caster at (5,5), HR=1 enemy-target ability.
            // Valid = 4 cardinal neighbors (caster tile excluded — can't Rush yourself).
            var map = FlatMap(10);
            var ability = new ActionAbilityInfo(0, "Rush", 0, "1", 99, 1, 0, "enemy", "");
            var tiles = AbilityTargetCalculator.GetValidTargetTiles(5, 5, ability, map);
            Assert.Equal(4, tiles.Count);
            Assert.DoesNotContain((5, 5), tiles);
            Assert.Contains((4, 5), tiles);
            Assert.Contains((6, 5), tiles);
            Assert.Contains((5, 4), tiles);
            Assert.Contains((5, 6), tiles);
        }

        [Fact]
        public void GetValidTargetTiles_AllyRange1_IncludesCasterTile()
        {
            // Ally-target abilities (Potion, Chant, Salve) can target the caster.
            // Caster at (5,5), HR=1 → 4 cardinal neighbors + self = 5 tiles.
            var map = FlatMap(10);
            var ability = new ActionAbilityInfo(0, "Chant", 0, "1", 99, 1, 0, "ally", "");
            var tiles = AbilityTargetCalculator.GetValidTargetTiles(5, 5, ability, map);
            Assert.Equal(5, tiles.Count);
            Assert.Contains((5, 5), tiles);
            Assert.Contains((4, 5), tiles);
            Assert.Contains((6, 5), tiles);
            Assert.Contains((5, 4), tiles);
            Assert.Contains((5, 6), tiles);
        }

        [Fact]
        public void GetValidTargetTiles_AllyRange4_IncludesCasterTile()
        {
            // Full HR=4 diamond on a flat open map = 41 tiles (includes caster for ally cast).
            var map = FlatMap(20);
            var ability = new ActionAbilityInfo(0, "Potion", 0, "4", 99, 1, 0, "ally", "");
            var tiles = AbilityTargetCalculator.GetValidTargetTiles(10, 10, ability, map);
            Assert.Equal(41, tiles.Count);
            Assert.Contains((10, 10), tiles);
            Assert.Contains((6, 10), tiles);
            Assert.Contains((14, 10), tiles);
            Assert.DoesNotContain((5, 10), tiles);
            Assert.DoesNotContain((15, 10), tiles);
        }

        [Fact]
        public void GetValidTargetTiles_EnemyClippedToMapBounds()
        {
            // Enemy-target at corner (0,0), HR=4 quadrant = 15 tiles minus caster = 14.
            var map = FlatMap(10);
            var ability = new ActionAbilityInfo(0, "Throw Stone", 0, "4", 99, 1, 0, "enemy", "");
            var tiles = AbilityTargetCalculator.GetValidTargetTiles(0, 0, ability, map);
            Assert.Equal(14, tiles.Count);
            Assert.DoesNotContain((0, 0), tiles);
            Assert.Contains((4, 0), tiles);
            Assert.Contains((0, 4), tiles);
            Assert.DoesNotContain((-1, 0), tiles);
        }

        [Fact]
        public void GetValidTargetTiles_UnwalkableTilesExcluded()
        {
            // A single NoWalk tile inside the diamond should not appear in the result.
            var map = FlatMap(10);
            map.Tiles[5, 6] = new MapTile { Height = 0, NoWalk = true };
            var ability = new ActionAbilityInfo(0, "Rush", 0, "1", 99, 1, 0, "enemy", "");
            var tiles = AbilityTargetCalculator.GetValidTargetTiles(5, 5, ability, map);
            Assert.DoesNotContain((5, 6), tiles);
            // Still get the other 3 neighbors
            Assert.Equal(3, tiles.Count);
        }

        [Fact]
        public void GetValidTargetTiles_VRangeFiltersTallerTiles()
        {
            // VR=1 blocks tiles whose height differs by more than 1 from the caster.
            var map = FlatMap(10, height: 5);
            // Raise one tile within HR range way above the caster
            map.Tiles[7, 5] = new MapTile { Height = 20 };
            var ability = new ActionAbilityInfo(0, "Rush", 0, "3", 1, 1, 0, "enemy", "");
            var tiles = AbilityTargetCalculator.GetValidTargetTiles(5, 5, ability, map);
            // (7,5) is HR=2 but ΔZ=15 > VR=1 — excluded
            Assert.DoesNotContain((7, 5), tiles);
            // (6,5) is HR=1 flat — included
            Assert.Contains((6, 5), tiles);
        }

        [Fact]
        public void GetValidTargetTiles_VRange99_IgnoresElevation()
        {
            // VR=99 should not filter anything regardless of height differences.
            var map = FlatMap(10, height: 5);
            map.Tiles[7, 5] = new MapTile { Height = 99 };
            var ability = new ActionAbilityInfo(0, "Throw Stone", 0, "4", 99, 1, 0, "enemy", "");
            var tiles = AbilityTargetCalculator.GetValidTargetTiles(5, 5, ability, map);
            Assert.Contains((7, 5), tiles);
        }

        [Fact]
        public void GetValidTargetTiles_NullMap_ReturnsEmpty()
        {
            var ability = new ActionAbilityInfo(0, "Rush", 0, "1", 1, 1, 0, "enemy", "");
            var tiles = AbilityTargetCalculator.GetValidTargetTiles(5, 5, ability, null);
            Assert.Empty(tiles);
        }

        [Fact]
        public void GetValidTargetTiles_RadiusAoE_ReturnsCenterCandidates()
        {
            // Radius AoE abilities (AoE>1) now return the set of valid CENTER tiles,
            // not an empty set. Each center is a place the caster can aim the splash.
            var map = FlatMap(20);
            var ability = new ActionAbilityInfo(0, "Fire", 0, "4", 99, 2, 1, "enemy/AoE", "");
            var tiles = AbilityTargetCalculator.GetValidTargetTiles(10, 10, ability, map);
            // Same math as point-target: 41-tile diamond (HR=4) PLUS caster tile
            // included (radius casts can center on self).
            Assert.Equal(41, tiles.Count);
            Assert.Contains((10, 10), tiles);
            Assert.Contains((6, 10), tiles);
            Assert.Contains((14, 10), tiles);
            Assert.DoesNotContain((5, 10), tiles);
        }

        [Fact]
        public void IsRadiusTarget_AcceptsAoE2NumericRange()
        {
            var fira = new ActionAbilityInfo(0, "Fira", 12, "4", 99, 2, 2, "enemy/AoE", "");
            Assert.True(AbilityTargetCalculator.IsRadiusTarget(fira));
            Assert.False(AbilityTargetCalculator.IsPointTarget(fira));
        }

        [Fact]
        public void IsRadiusTarget_RejectsPointTarget()
        {
            var rush = new ActionAbilityInfo(0, "Rush", 0, "1", 99, 1, 0, "enemy", "");
            Assert.False(AbilityTargetCalculator.IsRadiusTarget(rush));
            Assert.True(AbilityTargetCalculator.IsPointTarget(rush));
        }

        [Fact]
        public void IsRadiusTarget_RejectsSelfCast()
        {
            var cyclone = new ActionAbilityInfo(0, "Cyclone", 0, "Self", 0, 2, 0, "enemy/AoE", "");
            Assert.False(AbilityTargetCalculator.IsRadiusTarget(cyclone));
        }

        [Fact]
        public void GetSplashTiles_AoE2_ReturnsFiveTilePlus()
        {
            // AoE=2 = radius 1 = 5-tile plus shape (center + 4 cardinal neighbors).
            var map = FlatMap(10);
            var ability = new ActionAbilityInfo(0, "Fira", 12, "4", 99, 2, 2, "enemy/AoE", "");
            var splash = AbilityTargetCalculator.GetSplashTiles(5, 5, ability, map);
            Assert.Equal(5, splash.Count);
            Assert.Contains((5, 5), splash);
            Assert.Contains((4, 5), splash);
            Assert.Contains((6, 5), splash);
            Assert.Contains((5, 4), splash);
            Assert.Contains((5, 6), splash);
        }

        [Fact]
        public void GetSplashTiles_AoE3_ReturnsThirteenTileDiamond()
        {
            // AoE=3 = radius 2 = 13-tile diamond.
            var map = FlatMap(10);
            var ability = new ActionAbilityInfo(0, "Curaja", 20, "4", 99, 3, 3, "ally/AoE", "");
            var splash = AbilityTargetCalculator.GetSplashTiles(5, 5, ability, map);
            Assert.Equal(13, splash.Count);
            // Center and all 4 radius-1 neighbors
            Assert.Contains((5, 5), splash);
            // Radius 2 corners
            Assert.Contains((3, 5), splash);
            Assert.Contains((7, 5), splash);
            Assert.Contains((5, 3), splash);
            Assert.Contains((5, 7), splash);
            // Radius 2 diagonals are IN (taxicab ≤ 2)
            Assert.Contains((4, 4), splash);
            Assert.Contains((6, 6), splash);
            // Outside radius
            Assert.DoesNotContain((3, 4), splash); // taxi=3
        }

        [Fact]
        public void GetSplashTiles_HoE_FiltersTallTiles()
        {
            // HoE=0 means splash only hits tiles at the same elevation as the center.
            var map = FlatMap(10, height: 5);
            map.Tiles[5, 6] = new MapTile { Height = 20 }; // way above the center
            var ability = new ActionAbilityInfo(0, "Fira", 12, "4", 99, 2, 0, "enemy/AoE", "");
            var splash = AbilityTargetCalculator.GetSplashTiles(5, 5, ability, map);
            // (5,6) excluded by HoE filter
            Assert.DoesNotContain((5, 6), splash);
            // Other 4 neighbors still present
            Assert.Equal(4, splash.Count);
        }

        [Fact]
        public void GetSplashTiles_UnwalkableTilesExcluded()
        {
            var map = FlatMap(10);
            map.Tiles[4, 5] = new MapTile { Height = 0, NoWalk = true };
            var ability = new ActionAbilityInfo(0, "Fira", 12, "4", 99, 2, 2, "enemy/AoE", "");
            var splash = AbilityTargetCalculator.GetSplashTiles(5, 5, ability, map);
            Assert.DoesNotContain((4, 5), splash);
            Assert.Equal(4, splash.Count);
        }

        [Fact]
        public void GetSplashTiles_PointTargetAbility_ReturnsEmpty()
        {
            var map = FlatMap(10);
            var rush = new ActionAbilityInfo(0, "Rush", 0, "1", 99, 1, 0, "enemy", "");
            Assert.Empty(AbilityTargetCalculator.GetSplashTiles(5, 5, rush, map));
        }

        [Fact]
        public void GetSplashTiles_ClippedToMapBounds()
        {
            // Center at corner (0,0): 5-tile plus clips to 3 in-bounds tiles.
            var map = FlatMap(10);
            var ability = new ActionAbilityInfo(0, "Fira", 12, "4", 99, 2, 2, "enemy/AoE", "");
            var splash = AbilityTargetCalculator.GetSplashTiles(0, 0, ability, map);
            Assert.Equal(3, splash.Count);
            Assert.Contains((0, 0), splash);
            Assert.Contains((1, 0), splash);
            Assert.Contains((0, 1), splash);
        }

        [Fact]
        public void GetValidTargetTiles_IneligibleAbility_ReturnsEmpty()
        {
            // Non-numeric HRange (e.g. "Self") is still rejected.
            var map = FlatMap(10);
            var cyclone = new ActionAbilityInfo(0, "Cyclone", 0, "Self", 0, 2, 0, "enemy/AoE", "");
            var tiles = AbilityTargetCalculator.GetValidTargetTiles(5, 5, cyclone, map);
            Assert.Empty(tiles);
        }
    }
}
