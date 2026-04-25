using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Filter spurious EquipmentAndAbilities detections that fire as a
    /// transient frame after key presses on GameOver / Battle* screens.
    /// Live-captured 2026-04-25 Siedge Weald GameOver: bridge sent Enter
    /// (battle_retry navigates the GameOver menu), first post-key detection
    /// returned EquipmentAndAbilities (the abilities-list ui rendered with
    /// the EqA fingerprint), then settled back to GameOver.
    ///
    /// EquipmentAndAbilities is only reachable from PartyMenuUnits/CharStatus.
    /// Direct transition from GameOver / Battle* / PostBattle screens isn't
    /// a real game flow, so when those states precede an EqA detection,
    /// it's a leak — hold the prior state.
    /// </summary>
    public class EqaLeakGuardTests
    {
        [Fact]
        public void Filter_NotEqA_PassesThrough()
        {
            Assert.Equal("WorldMap",
                EqaLeakGuard.Filter("WorldMap", "WorldMap"));
            Assert.Equal("CharacterStatus",
                EqaLeakGuard.Filter("PartyMenuUnits", "CharacterStatus"));
        }

        [Fact]
        public void Filter_EqAFromGameOver_HoldsPriorState()
        {
            Assert.Equal("GameOver",
                EqaLeakGuard.Filter("GameOver", "EquipmentAndAbilities"));
        }

        [Fact]
        public void Filter_EqAFromBattleStates_HoldsPriorState()
        {
            Assert.Equal("BattleMyTurn",
                EqaLeakGuard.Filter("BattleMyTurn", "EquipmentAndAbilities"));
            Assert.Equal("BattleEnemiesTurn",
                EqaLeakGuard.Filter("BattleEnemiesTurn", "EquipmentAndAbilities"));
            Assert.Equal("BattleVictory",
                EqaLeakGuard.Filter("BattleVictory", "EquipmentAndAbilities"));
            Assert.Equal("BattleDesertion",
                EqaLeakGuard.Filter("BattleDesertion", "EquipmentAndAbilities"));
        }

        [Fact]
        public void Filter_EqAFromLegitimateSource_PassesThrough()
        {
            // PartyMenuUnits → EqA is a real navigation; CharacterStatus →
            // EqA is also real. Don't filter these.
            Assert.Equal("EquipmentAndAbilities",
                EqaLeakGuard.Filter("PartyMenuUnits", "EquipmentAndAbilities"));
            Assert.Equal("EquipmentAndAbilities",
                EqaLeakGuard.Filter("CharacterStatus", "EquipmentAndAbilities"));
            Assert.Equal("EquipmentAndAbilities",
                EqaLeakGuard.Filter("EquipmentAndAbilities", "EquipmentAndAbilities"));
        }

        [Fact]
        public void Filter_EqAFromUnknownPrior_PassesThrough()
        {
            // No prior signal — accept the detection. Conservative; only
            // filter when we know the prior state can't legitimately
            // transition to EqA.
            Assert.Equal("EquipmentAndAbilities",
                EqaLeakGuard.Filter(null, "EquipmentAndAbilities"));
            Assert.Equal("EquipmentAndAbilities",
                EqaLeakGuard.Filter("", "EquipmentAndAbilities"));
        }
    }
}
