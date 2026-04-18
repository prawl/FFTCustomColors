using System.Collections.Generic;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pure function: given an ability's element + a target unit's five affinity
    /// lists (absorb/null/half/weak/strengthen), return a one-word marker for the
    /// strongest matching affinity — or null if the ability is non-elemental or
    /// the target has no matching affinity.
    ///
    /// Priority when multiple apply (shield-combo cases): absorb > null > half >
    /// weak > strengthen. Absorb is the most-load-bearing decision aid; weak is
    /// the next. Strengthen is an outgoing-damage boost so it matters less
    /// against this target (it's listed so Claude can still notice the unit
    /// amplifies the element on its own attacks).
    /// </summary>
    public class ElementAffinityAnnotatorTests
    {
        [Fact]
        public void NonElementalAbility_ReturnsNull()
        {
            var result = ElementAffinityAnnotator.ComputeMarker(
                abilityElement: null,
                absorb: new() { "Fire" }, nullList: null, half: null, weak: null, strengthen: null);
            Assert.Null(result);
        }

        [Fact]
        public void NoAffinityLists_ReturnsNull()
        {
            var result = ElementAffinityAnnotator.ComputeMarker("Fire", null, null, null, null, null);
            Assert.Null(result);
        }

        [Fact]
        public void WeakToAbilityElement_ReturnsWeak()
        {
            var result = ElementAffinityAnnotator.ComputeMarker(
                abilityElement: "Fire",
                absorb: null, nullList: null, half: null,
                weak: new() { "Fire", "Ice" }, strengthen: null);
            Assert.Equal("weak", result);
        }

        [Fact]
        public void AbsorbsAbilityElement_ReturnsAbsorb()
        {
            var result = ElementAffinityAnnotator.ComputeMarker(
                abilityElement: "Holy",
                absorb: new() { "Holy" }, nullList: null, half: null, weak: null, strengthen: null);
            Assert.Equal("absorb", result);
        }

        [Fact]
        public void NullifiesAbilityElement_ReturnsNull_Marker()
        {
            var result = ElementAffinityAnnotator.ComputeMarker(
                abilityElement: "Fire",
                absorb: null, nullList: new() { "Fire" }, half: null, weak: null, strengthen: null);
            Assert.Equal("null", result);
        }

        [Fact]
        public void HalvesAbilityElement_ReturnsHalf()
        {
            var result = ElementAffinityAnnotator.ComputeMarker(
                abilityElement: "Ice",
                absorb: null, nullList: null, half: new() { "Ice" }, weak: null, strengthen: null);
            Assert.Equal("half", result);
        }

        [Fact]
        public void StrengthensAbilityElement_ReturnsStrengthen()
        {
            var result = ElementAffinityAnnotator.ComputeMarker(
                abilityElement: "Fire",
                absorb: null, nullList: null, half: null, weak: null,
                strengthen: new() { "Fire" });
            Assert.Equal("strengthen", result);
        }

        [Fact]
        public void AbilityElementNotInAnyList_ReturnsNull()
        {
            var result = ElementAffinityAnnotator.ComputeMarker(
                abilityElement: "Fire",
                absorb: new() { "Ice" }, nullList: new() { "Holy" }, half: new() { "Earth" },
                weak: new() { "Water" }, strengthen: new() { "Wind" });
            Assert.Null(result);
        }

        [Fact]
        public void PriorityOrder_AbsorbBeatsWeak()
        {
            // Contrived but possible — a unit could have overlapping fields via
            // conflicting gear. Absorb is the more decision-altering fact.
            var result = ElementAffinityAnnotator.ComputeMarker(
                abilityElement: "Fire",
                absorb: new() { "Fire" }, nullList: null, half: null,
                weak: new() { "Fire" }, strengthen: null);
            Assert.Equal("absorb", result);
        }

        [Fact]
        public void PriorityOrder_NullBeatsHalf()
        {
            var result = ElementAffinityAnnotator.ComputeMarker(
                abilityElement: "Fire",
                absorb: null, nullList: new() { "Fire" }, half: new() { "Fire" },
                weak: null, strengthen: null);
            Assert.Equal("null", result);
        }

        [Fact]
        public void PriorityOrder_HalfBeatsWeak()
        {
            // Defensive half overrides weak when both are set on same element.
            var result = ElementAffinityAnnotator.ComputeMarker(
                abilityElement: "Fire",
                absorb: null, nullList: null, half: new() { "Fire" },
                weak: new() { "Fire" }, strengthen: null);
            Assert.Equal("half", result);
        }

        [Fact]
        public void PriorityOrder_WeakBeatsStrengthen()
        {
            var result = ElementAffinityAnnotator.ComputeMarker(
                abilityElement: "Fire",
                absorb: null, nullList: null, half: null,
                weak: new() { "Fire" }, strengthen: new() { "Fire" });
            Assert.Equal("weak", result);
        }

        [Fact]
        public void CaseInsensitive_ElementName()
        {
            var result = ElementAffinityAnnotator.ComputeMarker(
                abilityElement: "fire",  // lowercase
                absorb: null, nullList: null, half: null,
                weak: new() { "Fire" }, strengthen: null);
            Assert.Equal("weak", result);
        }

        [Fact]
        public void EmptyAbilityElement_ReturnsNull()
        {
            var result = ElementAffinityAnnotator.ComputeMarker(
                abilityElement: "",
                absorb: new() { "Fire" }, nullList: null, half: null, weak: null, strengthen: null);
            Assert.Null(result);
        }
    }
}
