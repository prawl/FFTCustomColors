using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pure formatter for the HP-delta suffix appended to battle_ability's
    /// response.Info — mirrors the BattleAttack shape so Claude sees the
    /// same damage/heal/KO format for abilities as for basic attacks.
    ///
    /// Contract:
    ///   "" when delta can't be measured (cast-time ability, pre-HP read failed,
    ///       post-HP read failed, no change)
    ///   " — KO'd! (preHp→0/maxHp)"   when post reaches 0 from positive
    ///   " (preHp→postHp/maxHp)"      damage or heal (post != pre, post > 0)
    ///   " — revived (0→postHp/maxHp)" when pre=0 (dead) and post>0 (Phoenix Down etc.)
    /// </summary>
    public class AbilityHpDeltaFormatterTests
    {
        [Fact]
        public void NoChange_ReturnsEmpty()
        {
            Assert.Equal("", AbilityHpDeltaFormatter.Format(preHp: 500, postHp: 500, maxHp: 719));
        }

        [Fact]
        public void DamageButAlive_FormatsPreToPostOverMax()
        {
            Assert.Equal(" (500→400/719)",
                AbilityHpDeltaFormatter.Format(preHp: 500, postHp: 400, maxHp: 719));
        }

        [Fact]
        public void DamageToZero_EmitsKoMarker()
        {
            Assert.Equal(" — KO'd! (50→0/620)",
                AbilityHpDeltaFormatter.Format(preHp: 50, postHp: 0, maxHp: 620));
        }

        [Fact]
        public void HealOnAliveUnit_FormatsIncrease()
        {
            Assert.Equal(" (300→500/719)",
                AbilityHpDeltaFormatter.Format(preHp: 300, postHp: 500, maxHp: 719));
        }

        [Fact]
        public void PhoenixDownRevive_EmitsRevivedMarker()
        {
            Assert.Equal(" — revived (0→50/477)",
                AbilityHpDeltaFormatter.Format(preHp: 0, postHp: 50, maxHp: 477));
        }

        [Fact]
        public void PreHpNegative_ReturnsEmpty()
        {
            // ReadStaticArrayHpAt returns -1 when the unit / tile isn't found.
            // Defensive: no suffix when we can't establish a pre value.
            Assert.Equal("", AbilityHpDeltaFormatter.Format(preHp: -1, postHp: 500, maxHp: 719));
        }

        [Fact]
        public void PostHpNegative_ReturnsEmpty()
        {
            // ReadLiveHp returns -1 when the post-action memory scan fails.
            Assert.Equal("", AbilityHpDeltaFormatter.Format(preHp: 500, postHp: -1, maxHp: 719));
        }

        [Fact]
        public void MaxHpZero_ReturnsEmpty()
        {
            // Defensive: guard against divide-by-zero-style bad data.
            Assert.Equal("", AbilityHpDeltaFormatter.Format(preHp: 500, postHp: 400, maxHp: 0));
        }
    }
}
