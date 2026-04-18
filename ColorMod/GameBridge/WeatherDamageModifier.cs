using System;
using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure function: weather → element → damage multiplier mapping, plus a
    /// marker-string formatter for the shell ability render.
    ///
    /// Values are PSX-canonical (Rain boosts Lightning × 1.25, weakens Fire ×
    /// 0.75; Snow boosts Ice, weakens Fire; Thunderstorm boosts Lightning).
    /// IC remaster may rebalance — mark memory/project_weather_modifier_* if
    /// live verification finds different numbers.
    ///
    /// Consumer path (when the weather memory byte is located):
    ///   scan_move response annotates each elemental ability's damage with the
    ///   weather multiplier so Claude sees `Thunder +rain` / `Fire -rain` at
    ///   a glance.
    /// </summary>
    public static class WeatherDamageModifier
    {
        // element → multiplier, per named weather.
        private static readonly Dictionary<string, Dictionary<string, double>> _table = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Clear"] = new(),
            ["Sunny"] = new(),
            ["Rain"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Lightning"] = 1.25,
                ["Fire"] = 0.75,
            },
            ["Thunderstorm"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Lightning"] = 1.25,
            },
            ["Snow"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Ice"] = 1.25,
                ["Fire"] = 0.75,
            },
        };

        public static double GetMultiplier(string? weather, string? element)
        {
            if (string.IsNullOrEmpty(weather) || string.IsNullOrEmpty(element)) return 1.0;
            if (!_table.TryGetValue(weather, out var elems)) return 1.0;
            if (elems == null || !elems.TryGetValue(element, out var mult)) return 1.0;
            return mult;
        }

        /// <summary>
        /// Shell-friendly marker: "+rain" for boosts, "-rain" for weakens,
        /// null when no effect. Weather name lowercased for display.
        /// </summary>
        public static string? FormatMarker(string? weather, string? element)
        {
            double m = GetMultiplier(weather, element);
            if (m == 1.0 || string.IsNullOrEmpty(weather)) return null;
            string wLower = weather!.ToLowerInvariant();
            return m > 1.0 ? $"+{wLower}" : $"-{wLower}";
        }
    }
}
