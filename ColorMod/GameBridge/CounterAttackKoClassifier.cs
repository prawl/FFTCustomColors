using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure classifier: did the active unit get KO'd as a side-effect of
    /// their own action (e.g. attacking an enemy with Counter reaction)?
    ///
    /// Used by battle_attack / battle_ability post-action to short-circuit
    /// the turn — when the active unit is dead, skipping normal "wait for
    /// user to press Wait" saves the helper from stalling on a menu that
    /// won't open for a dead unit.
    /// </summary>
    public static class CounterAttackKoClassifier
    {
        public static bool IsActiveUnitKod(PostActionState? pre, PostActionState? post)
        {
            if (pre == null || post == null) return false;
            // Require a real transition from alive (>0) to dead (<=0).
            return pre.Hp > 0 && post.Hp <= 0;
        }
    }
}
