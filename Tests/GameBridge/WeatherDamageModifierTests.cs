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

        // Session 34: edge-case hardening.

        [Theory]
        [InlineData(" ", "Fire")]
        [InlineData("   ", "Fire")]
        [InlineData("Rain", " ")]
        [InlineData("Rain", "   ")]
        public void GetMultiplier_WhitespaceOnly_Returns1(string weather, string element)
        {
            // Whitespace-only inputs should pass through harmlessly. Current impl
            // checks IsNullOrEmpty — whitespace slips through the null guard but
            // then fails the dictionary lookup, yielding the 1.0 pass-through.
            // Pin that behavior so a future trim-input refactor stays neutral.
            Assert.Equal(1.0, WeatherDamageModifier.GetMultiplier(weather, element));
        }

        [Theory]
        [InlineData("Clear", "Fire")]
        [InlineData("Clear", "Lightning")]
        [InlineData("Clear", "Ice")]
        [InlineData("Clear", "Wind")]
        [InlineData("Clear", "Earth")]
        [InlineData("Clear", "Water")]
        [InlineData("Clear", "Holy")]
        [InlineData("Clear", "Dark")]
        public void ClearWeather_NoEffect_AllEightElements(string weather, string element)
        {
            // Clear is registered as an empty-effect weather. Sweep every canonical
            // FFT element to pin that no accidental entry sneaks in.
            Assert.Equal(1.0, WeatherDamageModifier.GetMultiplier(weather, element));
        }

        [Theory]
        [InlineData("Rain", "Fire", 0.75)]
        [InlineData("Rain", "Lightning", 1.25)]
        [InlineData("Snow", "Fire", 0.75)]
        [InlineData("Snow", "Ice", 1.25)]
        [InlineData("Thunderstorm", "Lightning", 1.25)]
        public void FormatMarker_RoundTripsWithGetMultiplier(string weather, string element, double expectedMultiplier)
        {
            // Round-trip invariant: GetMultiplier != 1.0 ⇔ FormatMarker != null.
            // Also pin the sign: >1 → '+', <1 → '-'.
            double m = WeatherDamageModifier.GetMultiplier(weather, element);
            Assert.Equal(expectedMultiplier, m);
            string? marker = WeatherDamageModifier.FormatMarker(weather, element);
            Assert.NotNull(marker);
            Assert.Equal(m > 1.0 ? '+' : '-', marker![0]);
        }

        [Fact]
        public void FormatMarker_IsNull_IffMultiplierIs1()
        {
            // Pin the inverse direction of the round-trip: any (weather, element)
            // that produces 1.0 must produce a null marker. Sweep a mixed set.
            var cases = new (string w, string e)[]
            {
                ("Clear", "Fire"),
                ("Sunny", "Lightning"),
                ("Rain", "Holy"),
                ("Snow", "Earth"),
                ("Thunderstorm", "Water"),
                ("Unknown", "Fire"),
            };
            foreach (var (w, e) in cases)
            {
                double m = WeatherDamageModifier.GetMultiplier(w, e);
                string? marker = WeatherDamageModifier.FormatMarker(w, e);
                if (m == 1.0) Assert.Null(marker);
                else Assert.NotNull(marker);
            }
        }

        [Fact]
        public void FormatMarker_ElementCaseInsensitive()
        {
            // Pin that varying element case doesn't drop the marker.
            Assert.Equal("+rain", WeatherDamageModifier.FormatMarker("Rain", "LIGHTNING"));
            Assert.Equal("+rain", WeatherDamageModifier.FormatMarker("Rain", "lightning"));
            Assert.Equal("-snow", WeatherDamageModifier.FormatMarker("Snow", "FIRE"));
        }

        [Fact]
        public void GetMultiplier_AllMultipliers_AreStrictlyPositive()
        {
            // Sanity: no registered multiplier should be <= 0 (would invert or
            // zero damage). Sweep every registered weather × every common element.
            foreach (var weather in new[] { "Clear", "Sunny", "Rain", "Snow", "Thunderstorm" })
            {
                foreach (var el in new[] { "Fire", "Lightning", "Ice", "Wind", "Earth", "Water", "Holy", "Dark" })
                {
                    double m = WeatherDamageModifier.GetMultiplier(weather, el);
                    Assert.True(m > 0, $"Weather={weather} Element={el} gave m={m}");
                }
            }
        }
    }
}
