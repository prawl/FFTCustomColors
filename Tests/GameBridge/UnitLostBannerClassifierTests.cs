using System.Collections.Generic;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// When a player unit's deathCounter expires they crystallize
    /// (Treasure status appears, Dead drops). This is a major story
    /// event — the unit is permanently gone for this battle. The
    /// streaming narrator emits `> X gained Treasure / > X lost Dead`
    /// inline but those buried in a 30-line enemy dump are easy to
    /// miss. Emit a loud `=== UNIT LOST: ... ===` banner like
    /// TURN HANDOFF. Live-flagged 2026-04-26 P3 playtest: agent didn't
    /// notice Kenrick crystallized.
    /// </summary>
    public class UnitLostBannerClassifierTests
    {
        private static UnitScanDiff.ChangeEvent Status(
            string label, string team, List<string>? gained, List<string>? lost)
            => new(label, team, OldXY: (5, 5), NewXY: (5, 5),
                OldHp: 100, NewHp: 100,
                StatusesGained: gained, StatusesLost: lost,
                Kind: "status");

        [Fact]
        public void NoEvents_ReturnsNull()
        {
            Assert.Null(UnitLostBannerClassifier.BuildBanner(new List<UnitScanDiff.ChangeEvent>()));
        }

        [Fact]
        public void NullEvents_ReturnsNull()
        {
            Assert.Null(UnitLostBannerClassifier.BuildBanner(null));
        }

        [Fact]
        public void PlayerCrystallized_EmitsBanner()
        {
            // Kenrick crystallized — Treasure status gained.
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                Status("Kenrick", "PLAYER",
                    gained: new List<string> { "Treasure" },
                    lost: new List<string> { "Dead" }),
            };
            var banner = UnitLostBannerClassifier.BuildBanner(events);
            Assert.Equal("=== UNIT LOST: Kenrick crystallized (permanent for this battle) ===", banner);
        }

        [Fact]
        public void PlayerCrystal_EmitsBanner()
        {
            // Some scans report "Crystal" instead of "Treasure" — both
            // mean crystallization.
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                Status("Wilham", "PLAYER",
                    gained: new List<string> { "Crystal" },
                    lost: null),
            };
            var banner = UnitLostBannerClassifier.BuildBanner(events);
            Assert.Equal("=== UNIT LOST: Wilham crystallized (permanent for this battle) ===", banner);
        }

        [Fact]
        public void EnemyCrystallized_NoBanner()
        {
            // Enemies crystallizing is GOOD news — shouldn't get the
            // alarm-banner treatment.
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                Status("Knight", "ENEMY",
                    gained: new List<string> { "Treasure" },
                    lost: null),
            };
            Assert.Null(UnitLostBannerClassifier.BuildBanner(events));
        }

        [Fact]
        public void MultiplePlayerCrystals_BothInBanner()
        {
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                Status("Kenrick", "PLAYER",
                    gained: new List<string> { "Treasure" }, lost: null),
                Status("Wilham", "PLAYER",
                    gained: new List<string> { "Crystal" }, lost: null),
            };
            var banner = UnitLostBannerClassifier.BuildBanner(events);
            Assert.Equal("=== UNIT LOST: Kenrick crystallized + Wilham crystallized (permanent for this battle) ===", banner);
        }

        [Fact]
        public void NonStatusEvent_Ignored()
        {
            // Damage event without status changes — no banner.
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                new("Ramza", "PLAYER", null, null,
                    OldHp: 500, NewHp: 200,
                    StatusesGained: null, StatusesLost: null, Kind: "damaged"),
            };
            Assert.Null(UnitLostBannerClassifier.BuildBanner(events));
        }
    }
}
