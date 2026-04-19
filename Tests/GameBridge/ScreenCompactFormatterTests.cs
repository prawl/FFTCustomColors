using FFTColorCustomizer.GameBridge;
using FFTColorCustomizer.Utilities;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Tests for <see cref="ScreenCompactFormatter.FormatHeader"/> — the
    /// pure compact-line renderer extracted from fft.sh in session 47.
    /// Each test pins the expected render shape for a canonical screen
    /// combination so the shell can delegate without guessing.
    /// </summary>
    public class ScreenCompactFormatterTests
    {
        [Fact]
        public void Null_RendersAsUnknown()
        {
            Assert.Equal("[Unknown]", ScreenCompactFormatter.FormatHeader(null));
        }

        [Fact]
        public void UnknownName_RendersAsBracketedName()
        {
            var screen = new DetectedScreen { Name = "WorldMap" };
            var line = ScreenCompactFormatter.FormatHeader(screen);
            Assert.StartsWith("[WorldMap]", line);
        }

        [Fact]
        public void WorldMap_WithLocation_RendersLocString()
        {
            var screen = new DetectedScreen
            {
                Name = "WorldMap",
                Location = 9,
                LocationName = "Dorter",
                UI = "Move",
            };
            var line = ScreenCompactFormatter.FormatHeader(screen, status: "completed");
            Assert.Equal("[WorldMap] loc=9(Dorter) ui=Move status=completed", line);
        }

        [Fact]
        public void WorldMap_WithObjective_RendersObjectiveString()
        {
            var screen = new DetectedScreen
            {
                Name = "WorldMap",
                Location = 26,
                LocationName = "TheSiedgeWeald",
                                StoryObjective = 18,
                StoryObjectiveName = "OrbonneMonastery",
            };
            var line = ScreenCompactFormatter.FormatHeader(screen);
            Assert.Contains("loc=26(TheSiedgeWeald)", line);
            Assert.Contains("objective=18(OrbonneMonastery)", line);
        }

        [Fact]
        public void WorldMap_NoObjective_OmitsObjective()
        {
            var screen = new DetectedScreen
            {
                Name = "WorldMap",
                Location = 0,
                LocationName = "Lesalia",
            };
            var line = ScreenCompactFormatter.FormatHeader(screen);
            Assert.DoesNotContain("objective=", line);
        }

        [Fact]
        public void BattleMyTurn_WithActiveUnitSummary_RendersSummary()
        {
            var screen = new DetectedScreen
            {
                Name = "BattleMyTurn",
                ActiveUnitSummary = "Kenrick(White Mage) (9,9) HP=437/437",
                UI = "Wait",
            };
            var line = ScreenCompactFormatter.FormatHeader(screen);
            Assert.Equal("[BattleMyTurn] Kenrick(White Mage) (9,9) HP=437/437 ui=Wait", line);
        }

        [Fact]
        public void BattleMyTurn_NoSummary_FallsBackToNameAndJob()
        {
            var screen = new DetectedScreen
            {
                Name = "BattleMyTurn",
                ActiveUnitName = "Ramza",
                ActiveUnitJob = "Gallant Knight",
                UI = "Move",
            };
            var line = ScreenCompactFormatter.FormatHeader(screen);
            Assert.Contains("Ramza(Gallant Knight)", line);
            Assert.Contains("ui=Move", line);
        }

        [Fact]
        public void BattleScreens_DoNotEmitLoc()
        {
            // loc= is a world-side concept. Battle screens show active unit
            // info instead.
            var screen = new DetectedScreen
            {
                Name = "BattleMoving",
                Location = 26,
                LocationName = "TheSiedgeWeald",
            };
            var line = ScreenCompactFormatter.FormatHeader(screen);
            Assert.DoesNotContain("loc=", line);
        }

        [Fact]
        public void WorldSideScreens_OmitBattleFields()
        {
            var screen = new DetectedScreen
            {
                Name = "PartyMenuUnits",
                ActiveUnitName = "Ramza",
                ActiveUnitJob = "Gallant Knight",
                UI = "Ramza",
            };
            var line = ScreenCompactFormatter.FormatHeader(screen);
            // Active unit banner is battle-only; world-side uses the UI field
            // to render the hovered unit.
            Assert.DoesNotContain("(Gallant Knight)", line);
            Assert.Contains("ui=Ramza", line);
        }

        [Fact]
        public void EmptyFields_OmitLabels()
        {
            // No status / no ui → no "status=" / "ui=" substrings.
            var screen = new DetectedScreen { Name = "Cutscene" };
            var line = ScreenCompactFormatter.FormatHeader(screen);
            Assert.Equal("[Cutscene]", line);
        }
    }
}
