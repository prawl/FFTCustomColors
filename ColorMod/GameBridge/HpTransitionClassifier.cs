using System;

namespace FFTColorCustomizer.GameBridge
{
    public enum HpTransitionEvent
    {
        /// <summary>No hook fires — unchanged HP, or read failure.</summary>
        None,
        /// <summary>HP dropped but target still alive — OnDamageDealt.</summary>
        Damage,
        /// <summary>HP dropped to 0 or below — OnDamageDealt + OnKill.</summary>
        Kill,
        /// <summary>HP went from positive to higher positive — OnHeal.</summary>
        Heal,
        /// <summary>HP went from &lt;=0 to positive — OnRaise.</summary>
        Raise,
    }

    /// <summary>
    /// Pure classifier for pre/post HP deltas. Drives which
    /// BattleStatTracker hook should fire after a battle_attack /
    /// battle_ability completes and ReadLiveHp returns the post-action HP.
    /// </summary>
    public static class HpTransitionClassifier
    {
        // ReadLiveHp returns -1 specifically as "not found" sentinel —
        // distinguish from overkill (which can leave HP at e.g. -20) by
        // only treating exactly -1 as read failure.
        private const int ReadFailureSentinel = -1;

        public static HpTransitionEvent Classify(int preHp, int postHp)
        {
            if (preHp == ReadFailureSentinel || postHp == ReadFailureSentinel)
                return HpTransitionEvent.None;

            if (preHp == postHp) return HpTransitionEvent.None;

            // Raise: pre dead (<=0), post alive.
            if (preHp <= 0 && postHp > 0) return HpTransitionEvent.Raise;

            // Kill: HP dropped from alive into <=0 (including overkill).
            if (preHp > 0 && postHp <= 0) return HpTransitionEvent.Kill;

            // Regular damage / heal while alive.
            if (postHp < preHp) return HpTransitionEvent.Damage;
            if (postHp > preHp) return HpTransitionEvent.Heal;

            return HpTransitionEvent.None;
        }

        /// <summary>
        /// Positive magnitude of HP change. Overkill damage is clamped to
        /// preHp so stat tracker totals match actual HP consumed.
        /// </summary>
        public static int Magnitude(int preHp, int postHp)
        {
            if (preHp == ReadFailureSentinel || postHp == ReadFailureSentinel)
                return 0;
            int delta = postHp - preHp;
            if (delta < 0)
            {
                // Damage path: clamp overkill to preHp.
                return Math.Min(preHp, -delta);
            }
            return delta;
        }
    }
}
