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

        [Fact]
        public void SelfDestruct_IsAutoEndTurn()
        {
            // Bomb monster suicide attack — unit dies after use, turn ends automatically.
            Assert.True(AutoEndTurnAbilities.IsAutoEndTurn("Self-Destruct"));
        }

        [Fact]
        public void Wish_IsNotAutoEndTurn()
        {
            // Sacrificial heal behavior varies by version — don't assume auto-end
            // without live confirmation.
            Assert.False(AutoEndTurnAbilities.IsAutoEndTurn("Wish"));
        }

        [Fact]
        public void Ultima_IsNotAutoEndTurn()
        {
            // Canonical spell, just like Fire — no auto-end.
            Assert.False(AutoEndTurnAbilities.IsAutoEndTurn("Ultima"));
        }

        [Theory]
        [InlineData("Counter")]        // reaction, not active action
        [InlineData("Reraise")]        // passive trigger
        [InlineData("Critical: Quick")] // grants extra turn, not a user action
        [InlineData("Throw")]           // single action but unit keeps move + Wait
        [InlineData("Hi-Potion")]       // item, unit still needs Wait
        [InlineData("Phoenix Down")]    // item, unit still needs Wait
        [InlineData("Blood Price")]     // HP cost but turn continues in IC
        public void ReactionsAndItemsAndBloodPrice_AreNotAutoEndTurn(string name)
        {
            Assert.False(AutoEndTurnAbilities.IsAutoEndTurn(name));
        }

        /// <summary>
        /// Session 52 regression guardrail: the auto-end allow-list has exactly
        /// the ability names we've live-verified or canonically trust. Adding
        /// a new name (e.g. Wish once live-confirmed) must update this test —
        /// forces a reviewer to acknowledge what they're changing.
        ///
        /// Session-51 findings: Jump live-verified session 29. Self-Destruct
        /// hardcoded session 33 batch 2 (commit 0917e34) but NOT live-verified
        /// against a Bomb monster. If a future live test disagrees, pull
        /// Self-Destruct out of the set AND update this theory.
        /// </summary>
        [Theory]
        [InlineData("Jump")]
        [InlineData("Self-Destruct")]
        public void AllowList_ContainsExactlyKnownEntries(string name)
        {
            Assert.True(AutoEndTurnAbilities.IsAutoEndTurn(name));
        }

        [Theory]
        [InlineData("jumping")]         // substring of Jump — must not match
        [InlineData("Self Destruct")]   // space instead of hyphen
        [InlineData("SelfDestruct")]    // no hyphen
        [InlineData("Mega Flare")]      // summon attack, similar AOE but non-terminal
        [InlineData("Bio")]             // poison-DoT spell, NOT auto-end
        public void NamesSimilarToAllowList_DoNotMatch(string name)
        {
            Assert.False(AutoEndTurnAbilities.IsAutoEndTurn(name));
        }
    }
}
