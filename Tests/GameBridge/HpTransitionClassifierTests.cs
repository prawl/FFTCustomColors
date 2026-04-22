using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// Given (preHp, postHp) transitions that a post-action HP read produces,
// classify into the right BattleStatTracker hook:
//   HP↓ above 0            -> Damage (OnDamageDealt)
//   HP↓ reaching 0 or below -> Kill   (OnDamageDealt + OnKill)
//   HP↑ from 0 to positive -> Raise  (OnRaise)
//   HP↑ otherwise          -> Heal   (OnHeal)
//   HP unchanged           -> None   (no hook fires — miss / no-op)
//   postHp negative read   -> None   (read failed, skip)
//
// The classifier is pure — no StatTracker, no memory, no timing. Keeps
// the hook plumbing in NavigationActions dumb and the branching tested.
public class HpTransitionClassifierTests
{
    [Fact]
    public void PreEqualPost_ReturnsNone()
    {
        Assert.Equal(HpTransitionEvent.None, HpTransitionClassifier.Classify(preHp: 120, postHp: 120));
    }

    [Fact]
    public void HpDropped_ButAlive_ReturnsDamage()
    {
        Assert.Equal(HpTransitionEvent.Damage, HpTransitionClassifier.Classify(preHp: 500, postHp: 437));
    }

    [Fact]
    public void HpDropped_ToZero_ReturnsKill()
    {
        Assert.Equal(HpTransitionEvent.Kill, HpTransitionClassifier.Classify(preHp: 50, postHp: 0));
    }

    [Fact]
    public void HpDropped_Overkill_ReturnsKill()
    {
        // Some attacks overkill. Still a kill.
        Assert.Equal(HpTransitionEvent.Kill, HpTransitionClassifier.Classify(preHp: 80, postHp: -20));
    }

    [Fact]
    public void HpIncreased_FromAlive_ReturnsHeal()
    {
        Assert.Equal(HpTransitionEvent.Heal, HpTransitionClassifier.Classify(preHp: 200, postHp: 340));
    }

    [Fact]
    public void HpIncreased_FromZero_ReturnsRaise()
    {
        Assert.Equal(HpTransitionEvent.Raise, HpTransitionClassifier.Classify(preHp: 0, postHp: 1));
    }

    [Fact]
    public void HpIncreased_FromNegative_ReturnsRaise()
    {
        // Overkilled unit being raised — still a resurrection.
        Assert.Equal(HpTransitionEvent.Raise, HpTransitionClassifier.Classify(preHp: -30, postHp: 400));
    }

    [Fact]
    public void PostHpNegative_NotKill_IndicatesReadFailure()
    {
        // -1 is the ReadLiveHp "not found" sentinel. Classifier avoids
        // scoring a phantom kill when the read didn't land.
        Assert.Equal(HpTransitionEvent.None, HpTransitionClassifier.Classify(preHp: 500, postHp: -1));
    }

    [Fact]
    public void PreHpNegative_ReturnsNone()
    {
        // Pre-read failed — we don't know the starting state.
        Assert.Equal(HpTransitionEvent.None, HpTransitionClassifier.Classify(preHp: -1, postHp: 500));
    }

    [Fact]
    public void HpDelta_Magnitude_DamageCase()
    {
        // Amount helper — positive absolute for both damage and heal.
        Assert.Equal(63, HpTransitionClassifier.Magnitude(preHp: 500, postHp: 437));
    }

    [Fact]
    public void HpDelta_Magnitude_HealCase()
    {
        Assert.Equal(140, HpTransitionClassifier.Magnitude(preHp: 200, postHp: 340));
    }

    [Fact]
    public void HpDelta_Magnitude_OverkillClampsToPreHp()
    {
        // Attacker dealing 100 damage to 80-HP target gets credited for 80,
        // not 100 — lifetime damage stats match the actual HP consumed.
        Assert.Equal(80, HpTransitionClassifier.Magnitude(preHp: 80, postHp: -20));
    }
}
