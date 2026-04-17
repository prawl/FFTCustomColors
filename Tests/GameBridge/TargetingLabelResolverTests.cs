using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class TargetingLabelResolverTests
    {
        [Fact]
        public void Prefers_LastAbilityName_When_Set()
        {
            var result = TargetingLabelResolver.Resolve(
                lastAbilityName: "Fire",
                selectedAbility: "Cure",
                selectedItem: "Attack");
            Assert.Equal("Fire", result);
        }

        [Fact]
        public void Falls_Back_To_SelectedAbility_When_LastIsNull()
        {
            var result = TargetingLabelResolver.Resolve(
                lastAbilityName: null,
                selectedAbility: "Cure",
                selectedItem: "Attack");
            Assert.Equal("Cure", result);
        }

        [Fact]
        public void Falls_Back_To_SelectedItem_When_Others_Null()
        {
            var result = TargetingLabelResolver.Resolve(
                lastAbilityName: null,
                selectedAbility: null,
                selectedItem: "Attack");
            Assert.Equal("Attack", result);
        }

        [Fact]
        public void Returns_Null_When_All_Null()
        {
            var result = TargetingLabelResolver.Resolve(null, null, null);
            Assert.Null(result);
        }
    }
}
