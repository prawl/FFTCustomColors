using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pure function: given an ability name, decide whether this ability
    /// auto-ends the casting unit's turn (in FFT canon). When a helper sees
    /// one of these, it should NOT prompt Claude to press Wait — the engine
    /// has already ended the turn.
    ///
    /// Session 32 scope: Jump (Dragoon) is the only standard case. Future
    /// additions (if any — suicide abilities like Ultima Demon's Grand Cross
    /// don't auto-end, the unit just dies) extend the allow-list.
    /// </summary>
    public class AutoEndTurnAbilitiesTests
    {
        [Fact]
        public void Jump_IsAutoEndTurn()
        {
            Assert.True(AutoEndTurnAbilities.IsAutoEndTurn("Jump"));
        }

        [Fact]
        public void Attack_IsNotAutoEndTurn()
        {
            // Basic attack leaves the turn open for Move/Wait.
            Assert.False(AutoEndTurnAbilities.IsAutoEndTurn("Attack"));
        }

        [Fact]
        public void Fire_IsNotAutoEndTurn()
        {
            // Cast-time abilities queue but don't auto-end; unit still needs Wait.
            Assert.False(AutoEndTurnAbilities.IsAutoEndTurn("Fire"));
        }

        [Fact]
        public void Cure_IsNotAutoEndTurn()
        {
            Assert.False(AutoEndTurnAbilities.IsAutoEndTurn("Cure"));
        }

        [Fact]
        public void EmptyName_IsNotAutoEndTurn()
        {
            Assert.False(AutoEndTurnAbilities.IsAutoEndTurn(""));
        }

        [Fact]
        public void NullName_IsNotAutoEndTurn()
        {
            Assert.False(AutoEndTurnAbilities.IsAutoEndTurn(null));
        }

        [Fact]
        public void CaseInsensitive()
        {
            Assert.True(AutoEndTurnAbilities.IsAutoEndTurn("jump"));
            Assert.True(AutoEndTurnAbilities.IsAutoEndTurn("JUMP"));
            Assert.True(AutoEndTurnAbilities.IsAutoEndTurn("Jump"));
        }

        [Fact]
        public void TrimsWhitespace()
        {
            // Defensive: in case a caller passes a name with whitespace.
            Assert.True(AutoEndTurnAbilities.IsAutoEndTurn("  Jump  "));
        }
    }
}
