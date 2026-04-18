using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pure function: given the current battle weather + an ability's element,
    /// return a damage multiplier (1.0 = no change, 1.25 = boosted, 0.75 =
    /// weakened). Returns 1.0 for non-elemental abilities and for weather
    /// states with no effect on the element.
    ///
    /// FFT canonical effects (PSX; IC-remaster not yet confirmed — all values
    /// flagged for later live verification):
    ///   Rain: Lightning × 1.25, Fire × 0.75
    ///   Thunderstorm: Lightning × 1.25
    ///   Snow: Ice × 1.25, Fire × 0.75
    ///   Clear/Sunny: no modifier
    /// </summary>
    public class WeatherDamageModifierTests
    {
        [Fact]
        public void Clear_Weather_NoModifier()
        {
            Assert.Equal(1.0, WeatherDamageModifier.GetMultiplier("Clear", "Fire"));
            Assert.Equal(1.0, WeatherDamageModifier.GetMultiplier("Clear", "Lightning"));
            Assert.Equal(1.0, WeatherDamageModifier.GetMultiplier("Clear", "Ice"));
        }

        [Fact]
        public void Rain_BoostsLightning()
        {
            Assert.Equal(1.25, WeatherDamageModifier.GetMultiplier("Rain", "Lightning"));
        }

        [Fact]
        public void Rain_WeakensFire()
        {
            Assert.Equal(0.75, WeatherDamageModifier.GetMultiplier("Rain", "Fire"));
        }

        [Fact]
        public void Rain_UnaffectedElements_NoModifier()
        {
            Assert.Equal(1.0, WeatherDamageModifier.GetMultiplier("Rain", "Ice"));
            Assert.Equal(1.0, WeatherDamageModifier.GetMultiplier("Rain", "Wind"));
            Assert.Equal(1.0, WeatherDamageModifier.GetMultiplier("Rain", "Holy"));
        }

        [Fact]
        public void Thunderstorm_BoostsLightning()
        {
            Assert.Equal(1.25, WeatherDamageModifier.GetMultiplier("Thunderstorm", "Lightning"));
        }

        [Fact]
        public void Snow_BoostsIce()
        {
            Assert.Equal(1.25, WeatherDamageModifier.GetMultiplier("Snow", "Ice"));
        }

        [Fact]
        public void Snow_WeakensFire()
        {
            Assert.Equal(0.75, WeatherDamageModifier.GetMultiplier("Snow", "Fire"));
        }

        [Fact]
        public void NonElementalAbility_NoModifier()
        {
            // Attack / Punch / etc. aren't element-typed. Multiplier is 1.0
            // regardless of weather.
            Assert.Equal(1.0, WeatherDamageModifier.GetMultiplier("Rain", null));
            Assert.Equal(1.0, WeatherDamageModifier.GetMultiplier("Snow", ""));
        }

        [Fact]
        public void UnknownWeather_NoModifier()
        {
            // Defensive: unknown weather name returns 1.0 (pass-through).
            Assert.Equal(1.0, WeatherDamageModifier.GetMultiplier("Solar Eclipse", "Fire"));
            Assert.Equal(1.0, WeatherDamageModifier.GetMultiplier(null, "Fire"));
        }

        [Fact]
        public void CaseInsensitive()
        {
            Assert.Equal(1.25, WeatherDamageModifier.GetMultiplier("rain", "lightning"));
            Assert.Equal(1.25, WeatherDamageModifier.GetMultiplier("RAIN", "LIGHTNING"));
            Assert.Equal(0.75, WeatherDamageModifier.GetMultiplier("Rain", "fire"));
        }

        [Fact]
        public void FormatMarker_BoostedElement_ReturnsBoostSigil()
        {
            // "+weather" for boosts, "-weather" for weakens.
            Assert.Equal("+rain", WeatherDamageModifier.FormatMarker("Rain", "Lightning"));
            Assert.Equal("-rain", WeatherDamageModifier.FormatMarker("Rain", "Fire"));
            Assert.Equal("+snow", WeatherDamageModifier.FormatMarker("Snow", "Ice"));
        }

        [Fact]
        public void FormatMarker_NoEffect_ReturnsNull()
        {
            Assert.Null(WeatherDamageModifier.FormatMarker("Clear", "Fire"));
            Assert.Null(WeatherDamageModifier.FormatMarker("Rain", "Holy"));
            Assert.Null(WeatherDamageModifier.FormatMarker("Unknown", "Fire"));
        }

        // Additional edge cases (session 33 batch 6).

        [Theory]
        [InlineData("Rain", "Holy")]
        [InlineData("Rain", "Dark")]
        [InlineData("Rain", "Water")]
        [InlineData("Rain", "Earth")]
        [InlineData("Snow", "Lightning")]
        [InlineData("Snow", "Holy")]
        [InlineData("Thunderstorm", "Fire")]
        [InlineData("Thunderstorm", "Ice")]
        public void GetMultiplier_NeutralElement_Returns1(string weather, string element)
        {
            Assert.Equal(1.0, WeatherDamageModifier.GetMultiplier(weather, element));
        }

        [Fact]
        public void GetMultiplier_NullWeatherNullElement_Returns1()
        {
            Assert.Equal(1.0, WeatherDamageModifier.GetMultiplier(null, null));
        }

        [Fact]
        public void GetMultiplier_EmptyStrings_Returns1()
        {
            Assert.Equal(1.0, WeatherDamageModifier.GetMultiplier("", ""));
            Assert.Equal(1.0, WeatherDamageModifier.GetMultiplier("", "Fire"));
            Assert.Equal(1.0, WeatherDamageModifier.GetMultiplier("Rain", ""));
        }

        [Fact]
        public void FormatMarker_WeatherNameIsLowercased()
        {
            // Marker format pin: weather name rendered lowercase regardless of input case.
            Assert.Equal("+rain", WeatherDamageModifier.FormatMarker("RAIN", "Lightning"));
            Assert.Equal("-snow", WeatherDamageModifier.FormatMarker("Snow", "Fire"));
        }

        [Fact]
        public void FormatMarker_NullInputs_ReturnsNull()
        {
            Assert.Null(WeatherDamageModifier.FormatMarker(null, null));
            Assert.Null(WeatherDamageModifier.FormatMarker(null, "Fire"));
            Assert.Null(WeatherDamageModifier.FormatMarker("Rain", null));
        }

        [Fact]
        public void SunnyWeather_NoModifier_ForAnyElement()
        {
            // Sunny is explicitly registered (empty effects). Any element → 1.0.
            foreach (var el in new[] { "Fire", "Lightning", "Ice", "Wind", "Earth", "Water", "Holy", "Dark" })
            {
                Assert.Equal(1.0, WeatherDamageModifier.GetMultiplier("Sunny", el));
            }
        }

        [Fact]
        public void BoostAndWeaken_AreSymmetric_WithinSameWeather()
        {
            // Rain boost 1.25, weaken 0.75 — the two multipliers are symmetric about 1.0.
            // (1.25 - 1.0) == (1.0 - 0.75). Pinning this makes future balance changes
            // visible in tests.
            double boost = WeatherDamageModifier.GetMultiplier("Rain", "Lightning");
            double weaken = WeatherDamageModifier.GetMultiplier("Rain", "Fire");
            Assert.Equal(boost - 1.0, 1.0 - weaken, 3); // 3 decimal places
        }
    }
}
