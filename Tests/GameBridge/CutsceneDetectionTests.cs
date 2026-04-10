using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class CutsceneDetectionTests
    {
        [Fact]
        public void DetectScreen_Cutscene_WithEventId_ReturnsCutscene()
        {
            // During cutscenes: location=255, not in battle (no unit slots),
            // but eventId > 0 distinguishes from title screen
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 0, slot9: 0,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false, eventId: 10);

            Assert.Equal("Cutscene", result);
        }

        [Fact]
        public void DetectScreen_TitleScreen_NoEventId_ReturnsTitleScreen()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 0, slot9: 0,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false, eventId: 0);

            Assert.Equal("TitleScreen", result);
        }

        [Fact]
        public void DetectScreen_TitleScreen_EventIdUninitializedSentinel_ReturnsTitleScreen()
        {
            // On a freshly launched game sitting at the title screen, the eventId
            // memory slot reads as 0xFFFF (65535) — an uninitialized sentinel, not
            // a real event. Previously this was misclassified as Cutscene.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 0, slot9: 0,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false, eventId: 65535);

            Assert.Equal("TitleScreen", result);
        }

        [Fact]
        public void DetectScreen_BattleCutscene_WithEventId_ReturnsBattle()
        {
            // Pre-battle cutscenes with unit slots populated — currently detected as Battle.
            // TODO: Distinguish pre-battle cutscenes from active battle using eventId + battleMode.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false, eventId: 4);

            // For now, in-battle cutscenes show as Battle_MyTurn (battleTeam=0, acted=0, moved=0)
            Assert.Equal("Battle_MyTurn", result);
        }
    }
}
