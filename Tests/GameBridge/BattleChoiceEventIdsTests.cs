using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Known catalog of eventIds that produce a 2-option BattleChoice prompt.
    /// Each entry is a real scene observed in-game. Current signal-based
    /// detection (eventHasChoice + choiceModalFlag at ScreenDetectionLogic.cs:347)
    /// already classifies these correctly, but the explicit catalog serves
    /// two purposes:
    ///   1. Documentation — future sessions need to know which events have
    ///      been confirmed; new ones get catalogued here as they're encountered.
    ///   2. Regression pin — if the signal-based detection ever regresses,
    ///      the per-event tests fail visibly rather than silently routing
    ///      BattleChoice into BattleDialogue.
    ///
    /// Per TODO §0 session 44: user-approved approach. Adding new events to
    /// <see cref="BattleChoiceEventIds.KnownEventIds"/> requires confirming
    /// the event actually shows a 2-option modal (not just narration).
    /// </summary>
    public class BattleChoiceEventIdsTests
    {
        [Fact]
        public void KnownEventIds_Includes_MandaliaPlain_Event16()
        {
            // Session 44 captured: "1. Defeat the Brigade" / "2. Rescue the captive"
            Assert.Contains(16, BattleChoiceEventIds.KnownEventIds);
        }

        [Fact]
        public void KnownEventIds_AreAllInRealEventRange()
        {
            // eventId sentinel ranges: 1-399 for real events. 0 / 0xFFFF
            // are "no event." Every catalogued choice event must be in
            // the real range.
            foreach (var id in BattleChoiceEventIds.KnownEventIds)
            {
                Assert.True(id >= 1 && id < 400,
                    $"eventId {id} is outside the real-event range (1-399)");
            }
        }

        [Fact]
        public void IsKnownChoiceEvent_ReturnsTrue_ForCatalogued()
        {
            Assert.True(BattleChoiceEventIds.IsKnownChoiceEvent(16));
        }

        [Fact]
        public void IsKnownChoiceEvent_ReturnsFalse_ForUnknown()
        {
            // Event 2 (Orbonne opening) is pure narration, not a choice.
            Assert.False(BattleChoiceEventIds.IsKnownChoiceEvent(2));
            // Event 10 (Gariland pre-battle) also narration-only.
            Assert.False(BattleChoiceEventIds.IsKnownChoiceEvent(10));
            // Out-of-range sentinels.
            Assert.False(BattleChoiceEventIds.IsKnownChoiceEvent(0));
            Assert.False(BattleChoiceEventIds.IsKnownChoiceEvent(9999));
        }

        [Fact]
        public void Catalog_MandaliaEntry_HasExpectedLabels()
        {
            // S58 catalog extension: labels surface on BattleChoice ui=
            // render instead of raw "option 1" / "option 2".
            Assert.True(BattleChoiceEventIds.Catalog.TryGetValue(16, out var entry));
            Assert.Equal(16, entry!.EventId);
            Assert.Equal("Mandalia Plain", entry.Location);
            Assert.Equal("Defeat the Brigade", entry.OptionOne);
            Assert.Equal("Rescue the captive", entry.OptionTwo);
        }

        [Fact]
        public void OptionLabel_Cursor0_ReturnsOptionOne()
        {
            Assert.Equal("Defeat the Brigade", BattleChoiceEventIds.OptionLabel(16, 0));
        }

        [Fact]
        public void OptionLabel_Cursor1_ReturnsOptionTwo()
        {
            Assert.Equal("Rescue the captive", BattleChoiceEventIds.OptionLabel(16, 1));
        }

        [Fact]
        public void OptionLabel_UnknownEvent_ReturnsNull()
        {
            Assert.Null(BattleChoiceEventIds.OptionLabel(eventId: 9999, cursorRow: 0));
        }

        [Fact]
        public void OptionLabel_InvalidCursor_ReturnsNull()
        {
            Assert.Null(BattleChoiceEventIds.OptionLabel(eventId: 16, cursorRow: -1));
            Assert.Null(BattleChoiceEventIds.OptionLabel(eventId: 16, cursorRow: 2));
        }

        [Fact]
        public void OptionLabelOrGeneric_UnknownEvent_FallsBackToGeneric()
        {
            // Fallback renderer output — uncatalogued events still surface
            // SOMETHING for the ui= field rather than blank.
            Assert.Equal("Option 1", BattleChoiceEventIds.OptionLabelOrGeneric(9999, 0));
            Assert.Equal("Option 2", BattleChoiceEventIds.OptionLabelOrGeneric(9999, 1));
        }

        [Fact]
        public void OptionLabelOrGeneric_CataloguedEvent_UsesRealLabel()
        {
            Assert.Equal("Defeat the Brigade", BattleChoiceEventIds.OptionLabelOrGeneric(16, 0));
            Assert.Equal("Rescue the captive", BattleChoiceEventIds.OptionLabelOrGeneric(16, 1));
        }

        [Fact]
        public void OptionLabelOrGeneric_InvalidCursor_ReturnsQuestionMark()
        {
            Assert.Equal("?", BattleChoiceEventIds.OptionLabelOrGeneric(16, -1));
            Assert.Equal("?", BattleChoiceEventIds.OptionLabelOrGeneric(16, 5));
        }

        [Fact]
        public void KnownEventIds_StaysInSync_WithCatalog()
        {
            // Defensive pin: both exposures should agree.
            Assert.Equal(BattleChoiceEventIds.Catalog.Count, BattleChoiceEventIds.KnownEventIds.Count);
            foreach (var id in BattleChoiceEventIds.Catalog.Keys)
                Assert.Contains(id, BattleChoiceEventIds.KnownEventIds);
        }

        [Fact]
        public void Detection_ClassifiesKnownEvent_AsBattleChoice()
        {
            // Regression pin: when the runtime signals fire (eventHasChoice
            // from .mes scan + choiceModalFlag from memory), the detection
            // layer must classify this event as BattleChoice, not the
            // fallback BattleDialogue.
            foreach (var id in BattleChoiceEventIds.KnownEventIds)
            {
                var result = ScreenDetectionLogic.Detect(
                    party: 0, ui: 1, rawLocation: 24, slot0: 0xFFFFFFFFL, slot9: 0,
                    battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                    battleTeam: 0, battleActed: 1, battleMoved: 1,
                    encA: 0, encB: 0, isPartySubScreen: false,
                    eventId: id, locationMenuFlag: 1, eventHasChoice: true,
                    choiceModalFlag: 1);
                Assert.Equal("BattleChoice", result);
            }
        }
    }
}
