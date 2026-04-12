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
        public void DetectScreen_BattleCutscene_WithEventId_ReturnsBattleDialogue()
        {
            // Mid-battle dialogue: unit slots populated + eventId active + battleMode=0.
            // Real event IDs are small (< 200), from event script files like event004.en.mes.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false, eventId: 4);

            Assert.Equal("Battle_Dialogue", result);
        }

        [Theory]
        [InlineData(401)]  // nameId for a character, not a real event
        [InlineData(256)]  // any value >= 200 is a nameId, not an event script
        [InlineData(500)]
        public void DetectScreen_AttackAnimation_HighEventId_ShouldNotReturnBattleDialogue(int nameIdAsEventId)
        {
            // During attack animations, battleMode drops to 0 and the eventId address
            // (0x14077CA94) actually holds the active unit's nameId (e.g. 401).
            // This was misdetected as Battle_Dialogue. High "eventId" values (>= 200)
            // are nameIds, not real event script IDs.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false, eventId: nameIdAsEventId);

            Assert.NotEqual("Battle_Dialogue", result);
            Assert.NotEqual("Cutscene", result);
            Assert.StartsWith("Battle", result);
        }
    }
}
