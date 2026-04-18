using System;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure function: decide whether an ability fires a physical projectile
    /// that terrain can block. Used by the LoS annotator to decide when to
    /// compute line-of-sight clearance.
    ///
    /// FFT canon:
    ///   Physical projectiles (bow, crossbow, gun, thrown weapon) are blocked
    ///   by terrain between caster and target. These route through basic
    ///   "Attack" (when equipped with a ranged weapon) and the Ninja "Throw"
    ///   skillset.
    ///
    ///   Magic spells (Fire, Cure, Ifrit, Haste, etc.) fly over walls — NOT
    ///   LoS-blocked.
    ///
    ///   Samurai Iaido draws katana power over the battlefield — also not
    ///   terrain-blocked (the power manifests at the target, not a
    ///   traveling projectile).
    ///
    /// Rule: skillset name "Attack" (basic attack with any weapon, including
    /// ranged) OR "Throw" (Ninja) AND ability has a numeric HRange > 1.
    /// Melee Attack (HRange=1) needs no LoS check.
    /// </summary>
    public static class ProjectileAbilityClassifier
    {
        public static bool IsProjectile(string? skillsetName, string? abilityName, string? hRange)
        {
            if (string.IsNullOrEmpty(hRange)) return false;
            if (!int.TryParse(hRange, out var range) || range <= 1) return false;

            // Throw: Ninja's skillset, every ability is a thrown weapon.
            if (string.Equals(skillsetName, "Throw", StringComparison.OrdinalIgnoreCase))
                return true;

            // Basic Attack with a ranged weapon — HRange inherits from weapon.
            // The ability name itself is "Attack" regardless of weapon type.
            if (string.Equals(abilityName, "Attack", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }
    }
}
