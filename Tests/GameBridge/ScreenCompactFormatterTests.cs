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
        public void WorldMap_WithLocation_RendersUiBeforeLoc()
        {
            // ui= must always be the first key=value field after the screen
            // bracket so the agent can read the decision surface (current
            // submenu / mode) at a glance regardless of screen type. Battle
            // screens already had this; world screens used to put loc= first.
            var screen = new DetectedScreen
            {
                Name = "WorldMap",
                Location = 9,
                LocationName = "Dorter",
                UI = "Move",
            };
            var line = ScreenCompactFormatter.FormatHeader(screen, status: "completed");
            Assert.Equal("[WorldMap] ui=Move loc=9(Dorter) status=completed", line);
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
        public void BattleMyTurn_WithActiveUnitSummary_RendersUiBeforeUnit()
        {
            // ui= must always be the first key=value field after the screen
            // bracket — same rule as the world-side renderer. Battle used to
            // put the unit summary first; flipped so the decision surface
            // is at a fixed predictable position regardless of screen type.
            var screen = new DetectedScreen
            {
                Name = "BattleMyTurn",
                ActiveUnitSummary = "Kenrick(White Mage) (9,9) HP=437/437",
                UI = "Wait",
            };
            var line = ScreenCompactFormatter.FormatHeader(screen);
            Assert.Equal("[BattleMyTurn] ui=Wait Kenrick(White Mage) (9,9) HP=437/437", line);
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

        // Session 47 extension: gil + eventId.

        [Fact]
        public void WorldMap_Gil_RendersFormatted()
        {
            var screen = new DetectedScreen
            {
                Name = "Outfitter",
                Gil = 2605569,
            };
            var line = ScreenCompactFormatter.FormatHeader(screen);
            // Thousands separator per invariant culture.
            Assert.Contains("gil=2,605,569", line);
        }

        [Fact]
        public void WorldMap_GilZero_IsOmitted()
        {
            // 0 is the "unread" sentinel; don't render "gil=0".
            var screen = new DetectedScreen
            {
                Name = "Outfitter",
                Gil = 0,
            };
            var line = ScreenCompactFormatter.FormatHeader(screen);
            Assert.DoesNotContain("gil=", line);
        }

        [Fact]
        public void Cutscene_EventId_RendersWithLabel()
        {
            var screen = new DetectedScreen
            {
                Name = "Cutscene",
                EventId = 42,
            };
            var line = ScreenCompactFormatter.FormatHeader(screen);
            Assert.Contains("eventId=42", line);
        }

        [Fact]
        public void EventIdZero_IsOmitted()
        {
            var screen = new DetectedScreen
            {
                Name = "Cutscene",
                EventId = 0,
            };
            var line = ScreenCompactFormatter.FormatHeader(screen);
            Assert.DoesNotContain("eventId=", line);
        }

        [Fact]
        public void EventId_OutOfRange_IsOmitted()
        {
            // Sentinel / uninitialized values (0xFFFF etc.) are outside the
            // real-event range 1-399 and must not render.
            var screen = new DetectedScreen
            {
                Name = "Cutscene",
                EventId = 0xFFFF,
            };
            var line = ScreenCompactFormatter.FormatHeader(screen);
            Assert.DoesNotContain("eventId=", line);
        }
    }
}
