using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class ProjectileAbilityClassifierTests
    {
        [Fact]
        public void RangedAttack_IsProjectile()
        {
            // Archer with a bow: Attack ability with HRange=4.
            Assert.True(ProjectileAbilityClassifier.IsProjectile(null, "Attack", "4"));
        }

        [Fact]
        public void MeleeAttack_IsNotProjectile()
        {
            // Knight with a sword: Attack ability with HRange=1. Melee, no LoS.
            Assert.False(ProjectileAbilityClassifier.IsProjectile(null, "Attack", "1"));
        }

        [Fact]
        public void NinjaThrow_IsProjectile()
        {
            Assert.True(ProjectileAbilityClassifier.IsProjectile("Throw", "Knife", "4"));
            Assert.True(ProjectileAbilityClassifier.IsProjectile("Throw", "Shuriken", "5"));
        }

        [Fact]
        public void Spells_AreNotProjectiles()
        {
            Assert.False(ProjectileAbilityClassifier.IsProjectile("Black Magicks", "Fire", "5"));
            Assert.False(ProjectileAbilityClassifier.IsProjectile("White Magicks", "Cure", "4"));
            Assert.False(ProjectileAbilityClassifier.IsProjectile("Summon", "Ifrit", "4"));
            Assert.False(ProjectileAbilityClassifier.IsProjectile("Time Magicks", "Haste", "5"));
        }

        [Fact]
        public void Iaido_IsNotProjectile()
        {
            // Samurai Iaido manifests at target; not a traveling projectile.
            Assert.False(ProjectileAbilityClassifier.IsProjectile("Iaido", "Masamune", "3"));
        }

        [Fact]
        public void Items_AreNotProjectiles()
        {
            // Potion tosses HAVE been depicted as arcs, but gameplay-wise
            // are not LoS-blocked in FFT canon. Keep in the "spell-like" bucket.
            Assert.False(ProjectileAbilityClassifier.IsProjectile("Items", "Potion", "4"));
        }

        [Fact]
        public void NonNumericRange_IsNotProjectile()
        {
            // Self-target abilities have HRange="Self".
            Assert.False(ProjectileAbilityClassifier.IsProjectile(null, "Focus", "Self"));
        }

        [Fact]
        public void NullOrEmpty_IsNotProjectile()
        {
            Assert.False(ProjectileAbilityClassifier.IsProjectile(null, null, null));
            Assert.False(ProjectileAbilityClassifier.IsProjectile(null, "Attack", null));
            Assert.False(ProjectileAbilityClassifier.IsProjectile(null, "Attack", ""));
        }

        [Fact]
        public void CaseInsensitive_SkillsetAndAbility()
        {
            Assert.True(ProjectileAbilityClassifier.IsProjectile("THROW", "Knife", "3"));
            Assert.True(ProjectileAbilityClassifier.IsProjectile(null, "attack", "3"));
        }
    }
}
