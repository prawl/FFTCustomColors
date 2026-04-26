using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Reject screen transitions that are physically impossible given the
    /// last-committed screen. Backstop for the if-else cascade in
    /// <see cref="ScreenDetectionLogic"/>: when memory reads at a transient
    /// produce a fingerprint matching e.g. <c>BattleCrystalMoveConfirm</c>
    /// from <c>BattleAttacking</c> (skipping the required <c>BattleVictory</c>
    /// step), this validator says "no, that can't happen — keep the old
    /// screen and try again on the next poll".
    ///
    /// Default-permit: transitions not in the blacklist are allowed (we
    /// don't want to break legit screen flows we haven't catalogued).
    /// </summary>
    public class ScreenTransitionValidatorTests
    {
        [Fact]
        public void NullPrevious_AlwaysValid()
        {
            // First-ever detection has no predecessor; can land on any screen.
            Assert.True(ScreenTransitionValidator.IsValidTransition(null, "BattleMyTurn"));
            Assert.True(ScreenTransitionValidator.IsValidTransition(null, "WorldMap"));
            Assert.True(ScreenTransitionValidator.IsValidTransition(null, "BattleVictory"));
        }

        [Fact]
        public void SameScreen_AlwaysValid()
        {
            // Re-classifying the same screen is a no-op transition.
            Assert.True(ScreenTransitionValidator.IsValidTransition("BattleMyTurn", "BattleMyTurn"));
            Assert.True(ScreenTransitionValidator.IsValidTransition("WorldMap", "WorldMap"));
        }

        [Fact]
        public void BattleAttackingToCrystalMove_Invalid()
        {
            // Live-flagged 2026-04-26 at Brigands' Den: bridge said
            // BattleAttacking → BattleAlliesTurn → BattleCrystalMoveConfirm
            // mid-battle. CrystalMoveConfirm is a post-victory reward
            // screen — only reachable FROM BattleVictory.
            Assert.False(ScreenTransitionValidator.IsValidTransition(
                "BattleAttacking", "BattleCrystalMoveConfirm"));
        }

        [Fact]
        public void BattleMyTurnToCrystalMove_Invalid()
        {
            Assert.False(ScreenTransitionValidator.IsValidTransition(
                "BattleMyTurn", "BattleCrystalMoveConfirm"));
        }

        [Fact]
        public void BattleEnemiesTurnToCrystalMove_Invalid()
        {
            Assert.False(ScreenTransitionValidator.IsValidTransition(
                "BattleEnemiesTurn", "BattleCrystalMoveConfirm"));
        }

        [Fact]
        public void BattleAlliesTurnToCrystalMove_Invalid()
        {
            Assert.False(ScreenTransitionValidator.IsValidTransition(
                "BattleAlliesTurn", "BattleCrystalMoveConfirm"));
        }

        [Fact]
        public void BattleVictoryToCrystalMove_Valid()
        {
            // The legit path: kill last enemy → Victory → CrystalMove reward.
            Assert.True(ScreenTransitionValidator.IsValidTransition(
                "BattleVictory", "BattleCrystalMoveConfirm"));
        }

        [Fact]
        public void CrystalMoveToCrystalMove_Valid()
        {
            // Stays on the screen until the player picks an item.
            Assert.True(ScreenTransitionValidator.IsValidTransition(
                "BattleCrystalMoveConfirm", "BattleCrystalMoveConfirm"));
        }

        [Fact]
        public void BattleVictoryFromMidBattle_Valid()
        {
            // Live-observed valid: the battle ends from the active turn.
            Assert.True(ScreenTransitionValidator.IsValidTransition("BattleMyTurn", "BattleVictory"));
            Assert.True(ScreenTransitionValidator.IsValidTransition("BattleAttacking", "BattleVictory"));
            Assert.True(ScreenTransitionValidator.IsValidTransition("BattleMoving", "BattleVictory"));
            Assert.True(ScreenTransitionValidator.IsValidTransition("BattleActing", "BattleVictory"));
            Assert.True(ScreenTransitionValidator.IsValidTransition("BattleEnemiesTurn", "BattleVictory"));
            Assert.True(ScreenTransitionValidator.IsValidTransition("BattleAlliesTurn", "BattleVictory"));
        }

        [Fact]
        public void WorldMapToBattleVictory_Invalid()
        {
            // Can't go from WorldMap directly to a battle terminal — the
            // battle has to start first. Live-flagged 2026-04-26 prior
            // playtests: a sticky gameOverFlag from a prior GameOver
            // can fire BattleVictory while back on the world map.
            Assert.False(ScreenTransitionValidator.IsValidTransition(
                "WorldMap", "BattleVictory"));
        }

        [Fact]
        public void WorldMapToBattleDesertion_Invalid()
        {
            Assert.False(ScreenTransitionValidator.IsValidTransition(
                "WorldMap", "BattleDesertion"));
        }

        [Fact]
        public void WorldMapToGameOver_Invalid()
        {
            Assert.False(ScreenTransitionValidator.IsValidTransition(
                "WorldMap", "GameOver"));
        }

        [Fact]
        public void BattleDialogueToWorldMap_Invalid()
        {
            // Live-flagged 2026-04-26: cutscene at Brigands' Den had
            // mid-cutscene transient WorldMap mis-tags while dialogue
            // was still on screen. WorldMap requires either a full
            // cutscene-end transition or a battle-end Advance flow.
            Assert.False(ScreenTransitionValidator.IsValidTransition(
                "BattleDialogue", "WorldMap"));
        }

        [Fact]
        public void CutsceneToWorldMap_Valid()
        {
            // The legit cutscene-end transition.
            Assert.True(ScreenTransitionValidator.IsValidTransition(
                "Cutscene", "WorldMap"));
        }

        [Fact]
        public void BattleVictoryToWorldMap_Valid()
        {
            // Post-victory Advance returns to WorldMap.
            Assert.True(ScreenTransitionValidator.IsValidTransition(
                "BattleVictory", "WorldMap"));
        }

        [Fact]
        public void BattleAttackingToBattleAttacking_Valid()
        {
            // Stays on BattleAttacking after a real miss — re-target prompt.
            Assert.True(ScreenTransitionValidator.IsValidTransition(
                "BattleAttacking", "BattleAttacking"));
        }

        [Fact]
        public void EmptyTo_AlwaysValid()
        {
            // Defensive: empty string treated like null (no last screen).
            Assert.True(ScreenTransitionValidator.IsValidTransition("", "BattleMyTurn"));
        }
    }
}
