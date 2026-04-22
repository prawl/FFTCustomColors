using FFTColorCustomizer.GameBridge;
using FFTColorCustomizer.Utilities;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// When the active unit attacks an enemy with Counter, the reaction
// fires and can bring the active unit's own HP to 0. Without explicit
// detection, battle_attack returns "HIT" on the target but then
// battle_wait tries to run on a dead unit, confuses the menu, and
// stalls. Pure classifier: given an action's pre/post active-unit HP,
// decide whether the caller should short-circuit (unit KO'd, turn is
// effectively over).
public class CounterAttackKoClassifierTests
{
    [Fact]
    public void ActiveHp_Unchanged_NotKod()
    {
        var pre = new PostActionState { Hp = 500, MaxHp = 500 };
        var post = new PostActionState { Hp = 500, MaxHp = 500 };
        Assert.False(CounterAttackKoClassifier.IsActiveUnitKod(pre, post));
    }

    [Fact]
    public void ActiveHp_Reduced_ButAlive_NotKod()
    {
        var pre = new PostActionState { Hp = 500, MaxHp = 500 };
        var post = new PostActionState { Hp = 120, MaxHp = 500 };
        Assert.False(CounterAttackKoClassifier.IsActiveUnitKod(pre, post));
    }

    [Fact]
    public void ActiveHp_DroppedToZero_FromCounter_IsKod()
    {
        // Classic Counter-kill scenario: active unit hits an enemy with
        // Counter reaction, the reaction fires, active unit dies.
        var pre = new PostActionState { Hp = 80, MaxHp = 500 };
        var post = new PostActionState { Hp = 0, MaxHp = 500 };
        Assert.True(CounterAttackKoClassifier.IsActiveUnitKod(pre, post));
    }

    [Fact]
    public void ActiveHp_Overkilled_IsKod()
    {
        var pre = new PostActionState { Hp = 30, MaxHp = 500 };
        var post = new PostActionState { Hp = -20, MaxHp = 500 };
        Assert.True(CounterAttackKoClassifier.IsActiveUnitKod(pre, post));
    }

    [Fact]
    public void NullPre_ReturnsFalse()
    {
        // Can't decide without a baseline — don't classify as KO.
        var post = new PostActionState { Hp = 0, MaxHp = 500 };
        Assert.False(CounterAttackKoClassifier.IsActiveUnitKod(null, post));
    }

    [Fact]
    public void NullPost_ReturnsFalse()
    {
        var pre = new PostActionState { Hp = 500, MaxHp = 500 };
        Assert.False(CounterAttackKoClassifier.IsActiveUnitKod(pre, null));
    }

    [Fact]
    public void StartedDead_StayedDead_NotClassifiedAsNewKo()
    {
        // Pre-KO'd unit (shouldn't happen in practice but be safe):
        // transitioning from 0 to 0 isn't a fresh KO event.
        var pre = new PostActionState { Hp = 0, MaxHp = 500 };
        var post = new PostActionState { Hp = 0, MaxHp = 500 };
        Assert.False(CounterAttackKoClassifier.IsActiveUnitKod(pre, post));
    }
}
