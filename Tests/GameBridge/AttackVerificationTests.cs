using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class AttackVerificationTests
    {
        [Fact]
        public void VerifyAttack_HpDecreased_ReportsHit()
        {
            int preAttackHp = 393;
            int postAttackHp = 280;

            var result = AttackVerification.Evaluate(preAttackHp, postAttackHp);

            Assert.True(result.Hit);
            Assert.Equal(113, result.Damage);
            Assert.Equal(preAttackHp, result.HpBefore);
            Assert.Equal(postAttackHp, result.HpAfter);
        }

        [Fact]
        public void VerifyAttack_HpUnchanged_ReportsMiss()
        {
            int preAttackHp = 393;
            int postAttackHp = 393;

            var result = AttackVerification.Evaluate(preAttackHp, postAttackHp);

            Assert.False(result.Hit);
            Assert.Equal(0, result.Damage);
        }

        [Fact]
        public void VerifyAttack_HpDroppedToZero_ReportsKill()
        {
            int preAttackHp = 50;
            int postAttackHp = 0;

            var result = AttackVerification.Evaluate(preAttackHp, postAttackHp);

            Assert.True(result.Hit);
            Assert.True(result.Killed);
            Assert.Equal(50, result.Damage);
        }

        [Fact]
        public void VerifyAttack_HpIncreased_ReportsHeal()
        {
            int preAttackHp = 200;
            int postAttackHp = 250;

            var result = AttackVerification.Evaluate(preAttackHp, postAttackHp);

            Assert.False(result.Hit);
            Assert.True(result.Healed);
            Assert.Equal(50, result.HealAmount);
            Assert.Equal(0, result.Damage);
        }

        [Fact]
        public void VerifyAttack_HpDecreased_NotHealed()
        {
            var result = AttackVerification.Evaluate(393, 280);

            Assert.False(result.Healed);
            Assert.Equal(0, result.HealAmount);
        }

        [Fact]
        public void VerifyAttack_HpUnchanged_NotHealed()
        {
            var result = AttackVerification.Evaluate(393, 393);

            Assert.False(result.Healed);
            Assert.Equal(0, result.HealAmount);
        }
    }
}
