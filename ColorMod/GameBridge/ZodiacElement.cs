using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Classical-element affinity for zodiac signs. Source:
    /// FFTHandsFree/Wiki/ZodiacAndElements.md § "Zodiac Compatibility Chart"
    /// — "Fire signs (Aries/Leo/Sagittarius) are Good with each other.
    /// Earth (Taurus/Virgo/Capricorn) Good with each other. Air
    /// (Gemini/Libra/Aquarius) Good with each other. Water
    /// (Cancer/Scorpio/Pisces) Good with each other."
    ///
    /// Same-element signs are Good with each other — the Element lookup
    /// + <see cref="ZodiacData.GetCompatibility"/> are consistent: if
    /// <c>GetElement(a) == GetElement(b)</c> and they're not the same sign,
    /// the pair is Good.
    ///
    /// Session 47: extracted so callers can render affinity decoratively
    /// (e.g. "Agrias (Cancer/Water)") and so downstream consumers can
    /// cross-check against the compatibility table.
    /// </summary>
    public enum ZodiacElement
    {
        None,   // Serpentarius — no element.
        Fire,
        Earth,
        Air,
        Water,
    }

    public static class ZodiacElementLookup
    {
        private static readonly Dictionary<ZodiacData.Sign, ZodiacElement> _byElement = new()
        {
            [ZodiacData.Sign.Aries] = ZodiacElement.Fire,
            [ZodiacData.Sign.Leo] = ZodiacElement.Fire,
            [ZodiacData.Sign.Sagittarius] = ZodiacElement.Fire,

            [ZodiacData.Sign.Taurus] = ZodiacElement.Earth,
            [ZodiacData.Sign.Virgo] = ZodiacElement.Earth,
            [ZodiacData.Sign.Capricorn] = ZodiacElement.Earth,

            [ZodiacData.Sign.Gemini] = ZodiacElement.Air,
            [ZodiacData.Sign.Libra] = ZodiacElement.Air,
            [ZodiacData.Sign.Aquarius] = ZodiacElement.Air,

            [ZodiacData.Sign.Cancer] = ZodiacElement.Water,
            [ZodiacData.Sign.Scorpio] = ZodiacElement.Water,
            [ZodiacData.Sign.Pisces] = ZodiacElement.Water,

            [ZodiacData.Sign.Serpentarius] = ZodiacElement.None,
        };

        public static ZodiacElement GetElement(ZodiacData.Sign sign)
        {
            return _byElement.TryGetValue(sign, out var element)
                ? element
                : ZodiacElement.None;
        }
    }
}
