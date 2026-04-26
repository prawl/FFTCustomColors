using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Hand-curated per-(eventId, boxIdx) speaker overrides. Used in place
    /// of the live-pointer read which is parked pending a working widget
    /// locator (see project_dialogue_speaker_pointer.md for the memory-hunt
    /// state).
    ///
    /// <para>The .mes file only marks scene-opener speakers via 0xE3 0x08
    /// tags and inherits null for mid-scene rotations — so the bridge
    /// renders most boxes as <c>[narrator]</c>. This table fills the gaps
    /// using a combination of (a) explicit .mes E3 08 tags carried forward,
    /// (b) user confirmation during the 2026-04-26 walkthrough, and (c)
    /// inference from .mes content (who's addressed, who's responding).
    /// </para>
    ///
    /// <para>To add a new event: play through it, note who speaks each box
    /// from the on-screen portrait, and append the entry below. The bridge
    /// renderer prefers an override over both the .mes-decoded speaker AND
    /// the live-pointer read.</para>
    /// </summary>
    public static class DialogueSpeakerOverrides
    {
        // event 045 — Eagrose Castle interrogation (Dycedarg / Ramza /
        // Delita) → Larg's audience. 29 paginated boxes from
        // event045.en.mes (sentence-char-balanced pagination of 20
        // FE-bounded segments). User confirmed boxes 0/3/7/8/9/14/16
        // during the 2026-04-26 hunt; the rest is derived from .mes
        // content (who is addressed / who's responding) and session-1
        // memory hunt findings.
        private static readonly string[] Event045 = new[]
        {
            "Lord Dycedarg Beoulve",   // 0  — "What madness possessed you..." (USER ✓)
            "Ramza",                   // 1  — "..." (E0 placeholder; player silence)
            "Lord Dycedarg Beoulve",   // 2  — "Silence is not the answer I seek."
            "Delita",                  // 3  — "'Twas I forced him to go." (.mes E3 08 ✓)
            "Lord Dycedarg Beoulve",   // 4  — "Was that the way of it, Brother?" (USER ✓)
            "Ramza",                   // 5  — "No...I went of my own choosing."
            "Delita",                  // 6  — "'Tis Ramza's noble disposition...my lord."
            "Ramza",                   // 7  — "You needn't be false on my behalf, Delita." (USER ✓)
            "Lord Dycedarg Beoulve",   // 8  — "Might I pose a question, Ramza?" (USER ✓)
            "Lord Dycedarg Beoulve",   // 9  — "Adherence to the rule of law..." (USER ✓ paginated)
            "Lord Dycedarg Beoulve",   // 10 — "Is your intent to live up to your name—or..." (paginated)
            "Ramza",                   // 11 — "...Forgive me, Lord Brother."
            "Man's Voice",             // 12 — "I believe the point is made..." (.mes E3 08 ✓)
            "Well-dressed Man",        // 13 — "You must not let the how of it..." (paginated, off-screen)
            "Well-dressed Man",        // 14 — "It is the way of young men..." (USER ✓)
            "Lord Dycedarg Beoulve",   // 15 — "To coddle them is to do them disservice..."
            "Duke Larg",               // 16 — "So, you are Lord Dycedarg's younger brother." (.mes E3 08 ✓ + USER)
            "Duke Larg",               // 17 — "Rise, son of Gallionne." (paginated)
            "Duke Larg",               // 18 — "Indeed, the resemblance..." (paginated continuation)
            "Duke Larg",               // 19 — "Our campaign against the Corpse Brigade..." (paginated)
            "Duke Larg",               // 20 — "I will permit you to..." (paginated continuation)
            "Ramza",                   // 21 — "Very well, Lord Brother."
            "Ramza",                   // 22 — "My apologies, Your Grace."
            "Duke Larg",               // 23 — "It was not of your doing, Dycedarg." (paginated)
            "Duke Larg",               // 24 — "In truth, it serves only to show..." (paginated)
            "Duke Larg",               // 25 — "the caliber of the men..." (paginated)
            "Duke Larg",               // 26 — "...he has gathered around him." (paginated)
            "Duke Larg",               // 27 — "The king's life hangs by a thread."
            "Lord Dycedarg Beoulve",   // 28 — "Indeed, my dear friend. I trust you will not fail me."
        };

        private static readonly Dictionary<int, string[]> _events = new()
        {
            [45] = Event045,
        };

        /// <summary>
        /// Returns the curated speaker for the given (eventId, boxIdx) or
        /// null when no override exists.
        /// </summary>
        public static string? Get(int eventId, int boxIdx)
        {
            if (boxIdx < 0) return null;
            if (!_events.TryGetValue(eventId, out var table)) return null;
            if (boxIdx >= table.Length) return null;
            return table[boxIdx];
        }
    }
}
