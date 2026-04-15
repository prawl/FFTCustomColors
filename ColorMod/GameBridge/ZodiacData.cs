using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Zodiac signs for FFT units. Values are 0-indexed Aries..Pisces (0-11)
    /// with Serpentarius at 12 per the PSX/wiki canon.
    ///
    /// Story characters have fixed zodiacs — see
    /// FFTHandsFree/Wiki/ZodiacAndElements.md §"Story Character Zodiac Signs".
    /// This table covers them by nameId (same key the roster uses for story
    /// character identification). Ramza is player-chosen at game start and
    /// NOT covered here. Generics get random zodiacs at recruit and need a
    /// live memory read — follow-up work, tracked in TODO.
    /// </summary>
    public static class ZodiacData
    {
        public enum Sign
        {
            Aries = 0, Taurus = 1, Gemini = 2, Cancer = 3,
            Leo = 4, Virgo = 5, Libra = 6, Scorpio = 7,
            Sagittarius = 8, Capricorn = 9, Aquarius = 10, Pisces = 11,
            Serpentarius = 12,
        }

        /// <summary>Canonical display names, 0-indexed matching Sign enum.</summary>
        public static readonly string[] SignNames =
        {
            "Aries", "Taurus", "Gemini", "Cancer",
            "Leo", "Virgo", "Libra", "Scorpio",
            "Sagittarius", "Capricorn", "Aquarius", "Pisces",
            "Serpentarius",
        };

        /// <summary>
        /// Story-character zodiacs keyed by nameId. Only entries whose canon
        /// is confirmed via ZodiacAndElements.md. Delita, Ovelia, Boco etc.
        /// can be added when they become roster-visible.
        /// </summary>
        private static readonly Dictionary<int, Sign> _byNameId = new()
        {
            // NameIds from CharacterData.cs StoryCharacterName dictionary.
            // Canonical zodiacs per FFTHandsFree/Wiki/ZodiacAndElements.md.
            [2] = Sign.Sagittarius,   // Delita (guest)
            [5] = Sign.Leo,           // Alma
            [4] = Sign.Taurus,        // Ovelia (guest)
            [13] = Sign.Scorpio,      // Orlandeau (Cidolfus)
            [15] = Sign.Pisces,       // Reis
            [22] = Sign.Libra,        // Mustadio
            [26] = Sign.Gemini,       // Marach (Malak)
            [30] = Sign.Cancer,       // Agrias
            [31] = Sign.Libra,        // Beowulf
            [41] = Sign.Pisces,       // Rapha (Rafa)
            [42] = Sign.Capricorn,    // Meliadoul
            [50] = Sign.Aquarius,     // Cloud
            [117] = Sign.Gemini,      // Construct 8
        };

        /// <summary>
        /// Returns the canonical zodiac sign for a story character by nameId,
        /// or null if the character isn't in the story table (e.g. generic).
        /// </summary>
        public static Sign? GetByNameId(int nameId) =>
            _byNameId.TryGetValue(nameId, out var sign) ? sign : null;

        /// <summary>
        /// Returns the display name for a sign, or null if out of range.
        /// </summary>
        public static string? GetSignName(Sign sign)
        {
            int i = (int)sign;
            return (i >= 0 && i < SignNames.Length) ? SignNames[i] : null;
        }

        /// <summary>Compatibility multiplier between two signs per FFT rules.</summary>
        public enum Compatibility
        {
            Neutral,   // x1.0
            Good,      // x1.25 (same-gender on Good pair, or opposite gender on same sign)
            Best,      // x1.5 (opposite sign + opposite gender)
            Bad,       // x0.75
            Worst,     // x0.5 (opposite sign + same gender)
        }

        /// <summary>
        /// Opposite sign per FFT canon. Used for best/worst compatibility math.
        /// </summary>
        private static readonly Dictionary<Sign, Sign> _opposite = new()
        {
            [Sign.Aries] = Sign.Libra,
            [Sign.Libra] = Sign.Aries,
            [Sign.Taurus] = Sign.Scorpio,
            [Sign.Scorpio] = Sign.Taurus,
            [Sign.Gemini] = Sign.Sagittarius,
            [Sign.Sagittarius] = Sign.Gemini,
            [Sign.Cancer] = Sign.Capricorn,
            [Sign.Capricorn] = Sign.Cancer,
            [Sign.Leo] = Sign.Aquarius,
            [Sign.Aquarius] = Sign.Leo,
            [Sign.Virgo] = Sign.Pisces,
            [Sign.Pisces] = Sign.Virgo,
        };

        /// <summary>Returns the opposite sign, or null for Serpentarius.</summary>
        public static Sign? GetOpposite(Sign sign) =>
            _opposite.TryGetValue(sign, out var opp) ? opp : (Sign?)null;
    }
}
