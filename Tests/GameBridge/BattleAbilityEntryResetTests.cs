using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class BattleAbilityEntryResetTests
    {
        // --- EscapeCountToMyTurn: how many Escape keys from the current screen?

        [Theory]
        [InlineData("BattleMyTurn", 0)]
        [InlineData("BattleActing", 0)]  // partially-acted (post-move, pre-action); action menu still open
        public void EscapeCount_AlreadyAtMyTurn_IsZero(string screen, int expected)
        {
            Assert.Equal(expected, BattleAbilityEntryReset.EscapeCountToMyTurn(screen));
        }

        [Fact]
        public void EscapeCount_FromBattleAbilities_IsOne()
        {
            // One Escape from the submenu returns to BattleMyTurn.
            Assert.Equal(1, BattleAbilityEntryReset.EscapeCountToMyTurn("BattleAbilities"));
        }

        [Theory]
        [InlineData("Battle_WhiteMagicks")]
        [InlineData("Battle_BlackMagicks")]
        [InlineData("Battle_TimeMagicks")]
        [InlineData("Battle_Mettle")]
        [InlineData("Battle_Items")]
        [InlineData("Battle_Jump")]
        [InlineData("Battle_Punch_Art")]
        public void EscapeCount_FromSkillsetList_IsTwo(string skillsetScreen)
        {
            // Two Escapes: ability list → submenu → BattleMyTurn.
            Assert.Equal(2, BattleAbilityEntryReset.EscapeCountToMyTurn(skillsetScreen));
        }

        [Theory]
        [InlineData("BattleAttacking")]
        [InlineData("BattleCasting")]
        public void EscapeCount_FromTargeting_IsThree(string screen)
        {
            // Three Escapes: targeting → ability list → submenu → BattleMyTurn.
            Assert.Equal(3, BattleAbilityEntryReset.EscapeCountToMyTurn(screen));
        }

        [Fact]
        public void EscapeCount_NullOrUnknownScreen_IsZero()
        {
            // Defensive default — caller validates screen state separately before invoking.
            Assert.Equal(0, BattleAbilityEntryReset.EscapeCountToMyTurn(null));
            Assert.Equal(0, BattleAbilityEntryReset.EscapeCountToMyTurn(""));
            Assert.Equal(0, BattleAbilityEntryReset.EscapeCountToMyTurn("WorldMap"));
            Assert.Equal(0, BattleAbilityEntryReset.EscapeCountToMyTurn("Cutscene"));
        }

        // --- IsResetableBattleScreen: should the caller run the reset at all?

        [Theory]
        [InlineData("BattleMyTurn", true)]
        [InlineData("BattleActing", true)]
        [InlineData("BattleAbilities", true)]
        [InlineData("Battle_WhiteMagicks", true)]
        [InlineData("Battle_Mettle", true)]
        [InlineData("BattleAttacking", true)]
        [InlineData("BattleCasting", true)]
        [InlineData("WorldMap", false)]
        [InlineData("Cutscene", false)]
        [InlineData("TitleScreen", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsResetableBattleScreen_MatchesExpected(string? screen, bool expected)
        {
            Assert.Equal(expected, BattleAbilityEntryReset.IsResetableBattleScreen(screen));
        }

        // --- PlanSequence: the action list for dry-run / logging

        [Fact]
        public void PlanSequence_MyTurn_IsEmpty()
        {
            Assert.Empty(BattleAbilityEntryReset.PlanSequence("BattleMyTurn"));
        }

        [Fact]
        public void PlanSequence_Submenu_IsOneEscape()
        {
            var seq = BattleAbilityEntryReset.PlanSequence("BattleAbilities");
            Assert.Single(seq);
            Assert.Equal("Escape", seq[0]);
        }

        [Fact]
        public void PlanSequence_SkillsetList_IsTwoEscapes()
        {
            var seq = BattleAbilityEntryReset.PlanSequence("Battle_TimeMagicks");
            Assert.Equal(2, seq.Count);
            Assert.All(seq, s => Assert.Equal("Escape", s));
        }

        [Fact]
        public void PlanSequence_Targeting_IsThreeEscapes()
        {
            var seq = BattleAbilityEntryReset.PlanSequence("BattleCasting");
            Assert.Equal(3, seq.Count);
            Assert.All(seq, s => Assert.Equal("Escape", s));
        }
    }
}
