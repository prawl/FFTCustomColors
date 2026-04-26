using System.IO;
using System.Linq;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pins the .mes-decoder fixes shipped 2026-04-26 after a live
    /// walk-through of event 045 (Eagrose Castle):
    /// - 0xE0 and 0xDA-0x68 are player-name placeholders, not control bytes.
    ///   Without substitution the bridge prints "Might I pose a question, ?".
    /// - The trailing 0xFE-run length tells the renderer how many bubbles to
    ///   show for a segment. FE×1 → 1 bubble, FE×3 → 3 bubbles. The text is
    ///   distributed into bubbles by sentence count using ceiling division
    ///   (5 sentences across 3 bubbles → 2-2-1 — matches what the game
    ///   actually shows for event 045's "Might I pose a question…" segment).
    /// - 0xF8 (single or run-of-N) is intra-bubble whitespace — NOT a bubble
    ///   break. The earlier "F8×2 = bubble boundary" rule was wrong; the
    ///   F8×2 inside event 045 segment 0x0203 sits inside a single bubble
    ///   that the user verified on screen.
    /// </summary>
    public class MesDecoderPlaceholderTests
    {
        // --- Placeholder substitution ---

        [Fact]
        public void DecodeBoxes_E0_SubstitutedWithPlayerName()
        {
            // "A" 0xE0 "?"  →  "ARamza?"
            var bytes = new byte[] { 0x0A, 0xE0, 0x40 };
            var boxes = MesDecoder.DecodeBoxes(bytes);
            Assert.Single(boxes);
            Assert.Equal("ARamza?", boxes[0].Text);
        }

        [Fact]
        public void DecodeBoxes_DA68_SubstitutedWithEmDash()
        {
            // 0xDA 0x68 is an em-dash inserted by the renderer, NOT the
            // player name. Verified live 2026-04-26 at event 045 box 25
            // ("And let us not forget—they did save…") and the
            // "Is your intent to live up to your name—or to drag…" line.
            var bytes = new byte[] { 0x0A, 0xDA, 0x68, 0x0B };
            var boxes = MesDecoder.DecodeBoxes(bytes);
            Assert.Single(boxes);
            Assert.Equal("A—B", boxes[0].Text);
        }

        // --- F8 = intra-bubble whitespace (corrected rule) ---

        [Fact]
        public void DecodeBoxes_SingleF8_IsIntraBubbleWhitespace()
        {
            // "A" 0xF8 "B" — one bubble, F8 becomes a newline.
            var bytes = new byte[] { 0x0A, 0xF8, 0x0B };
            var boxes = MesDecoder.DecodeBoxes(bytes);
            Assert.Single(boxes);
        }

        [Fact]
        public void DecodeBoxes_F8Run_AlsoIntraBubbleWhitespace_NotBubbleBoundary()
        {
            // "A" 0xF8 0xF8 "B" — used to be split into 2 bubbles. The
            // event 045 walk-through proved F8×2 sits inside a single
            // bubble, so it must collapse to one box.
            var bytes = new byte[] { 0x0A, 0xF8, 0xF8, 0x0B };
            var boxes = MesDecoder.DecodeBoxes(bytes);
            Assert.Single(boxes);
        }

        // --- FE-count → bubble count via sentence pagination ---

        [Fact]
        public void DecodeBoxes_FE1_OneBubble_NoPagination()
        {
            // "Hello." 0xFE — one segment, one bubble.
            // 'H'=0x11 'e'=0x28 'l'=0x2F 'l'=0x2F 'o'=0x32 '.'=0x78
            var bytes = new byte[] { 0x11, 0x28, 0x2F, 0x2F, 0x32, 0x78, 0xFE };
            var boxes = MesDecoder.DecodeBoxes(bytes);
            Assert.Single(boxes);
            Assert.Equal("Hello.", boxes[0].Text);
        }

        [Fact]
        public void DecodeBoxes_FE3_PaginatesSegmentIntoThreeBubbles()
        {
            // Three sentences, FE×3 → 3 bubbles, one sentence each.
            // Bytes for "Aa. Bb. Cc."
            // 'A'=0x0A 'a'=0x24 '.'=0x78 ' '=0x88 'B'=0x0B 'b'=0x25 'C'=0x0C 'c'=0x26
            var bytes = new byte[]
            {
                0x0A, 0x24, 0x78, 0x88, // "Aa. "
                0x0B, 0x25, 0x78, 0x88, // "Bb. "
                0x0C, 0x26, 0x78,       // "Cc."
                0xFE, 0xFE, 0xFE,       // FE×3 → 3 bubbles
            };
            var boxes = MesDecoder.DecodeBoxes(bytes);
            Assert.Equal(3, boxes.Count);
            Assert.Equal("Aa.", boxes[0].Text);
            Assert.Equal("Bb.", boxes[1].Text);
            Assert.Equal("Cc.", boxes[2].Text);
        }

        [Fact]
        public void DecodeBoxes_FE3_FiveSentences_DistributesAs2_2_1()
        {
            // 5 sentences, FE×3 → 2-2-1 distribution (ceil-division).
            // Each sentence is just "X.": A./B./C./D./E.
            var bytes = new byte[]
            {
                0x0A, 0x78, 0x88, // "A. "
                0x0B, 0x78, 0x88, // "B. "
                0x0C, 0x78, 0x88, // "C. "
                0x0D, 0x78, 0x88, // "D. "
                0x0E, 0x78,       // "E."
                0xFE, 0xFE, 0xFE,
            };
            var boxes = MesDecoder.DecodeBoxes(bytes);
            Assert.Equal(3, boxes.Count);
            Assert.Equal("A. B.", boxes[0].Text);
            Assert.Equal("C. D.", boxes[1].Text);
            Assert.Equal("E.", boxes[2].Text);
        }

        [Fact]
        public void DecodeBoxes_FENRun_LongerThanSentenceCount_CollapsesToSentenceCount()
        {
            // FE×2 with one sentence: the extra FE encodes a pause beat
            // after the bubble, NOT a second empty bubble. Same rule the
            // Dorter event 38 5×FE run relies on.
            var bytes = new byte[] { 0x0A, 0x78, 0xFE, 0xFE };
            var boxes = MesDecoder.DecodeBoxes(bytes);
            Assert.Single(boxes);
            Assert.Equal("A.", boxes[0].Text);
        }

        [Fact]
        public void DecodeBoxes_SegmentWithoutOwnSpeakerTag_HasNullSpeaker()
        {
            // Two FE-segments: the first has speaker "Bo", the second has
            // no speaker tag of its own. The .mes format doesn't encode a
            // "speaker continues" hint, so persisting the previous speaker
            // produces false confidence (event 045 boxes 4/5/7/8 all read
            // "Delita" from the inherited tag but were actually
            // Dycedarg/Ramza in-game). Emit null instead so the bridge
            // falls back to "narrator" rather than lying.
            var bytes = new byte[]
            {
                0xE3, 0x08, 0x0B, 0x32, 0xE3, 0x00, // speaker "Bo"
                0x21, 0xFE,                          // "W." + FE  (W=0x21)
                0x22, 0xFE,                          // "X." + FE  (X=0x22, no tag)
            };
            var boxes = MesDecoder.DecodeBoxes(bytes);
            Assert.Equal(2, boxes.Count);
            Assert.Equal("Bo", boxes[0].Speaker);
            Assert.Null(boxes[1].Speaker);
        }

        [Fact]
        public void Event045_Box4_NoLongerFalselyTaggedDelita()
        {
            // Box 4 ("Was that the way of it, Brother?…") inherits the
            // speaker tag from segment 3 (Delita) in the .mes data, but
            // the game shows Dycedarg as the speaker. With the "null on
            // inherited" rule the bridge no longer claims Delita.
            var path = FindEvent045();
            if (path == null) return;
            var bytes = File.ReadAllBytes(path);
            var boxes = MesDecoder.DecodeBoxes(bytes);

            var brotherBox = boxes.SingleOrDefault(b =>
                b.Text.Contains("Was that the way of it, Brother"));
            Assert.NotNull(brotherBox);
            Assert.Null(brotherBox!.Speaker);
        }

        [Fact]
        public void DecodeBoxes_PaginatedBubbles_InheritSegmentSpeaker()
        {
            // Speaker "Bo" + 3 sentences + FE×3 → all three bubbles tagged "Bo".
            var bytes = new byte[]
            {
                0xE3, 0x08, 0x0B, 0x32, 0xE3, 0x00, // speaker "Bo"
                0x0A, 0x78, 0x88,
                0x0B, 0x78, 0x88,
                0x0C, 0x78,
                0xFE, 0xFE, 0xFE,
            };
            var boxes = MesDecoder.DecodeBoxes(bytes);
            Assert.Equal(3, boxes.Count);
            Assert.All(boxes, b => Assert.Equal("Bo", b.Speaker));
        }

        // --- Live event045 walk-through pins (regression tests) ---

        private static string? FindEvent045()
        {
            string deployed = "c:/program files (x86)/steam/steamapps/common/FINAL FANTASY TACTICS - The Ivalice Chronicles/Reloaded/Mods/FFTColorCustomizer/claude_bridge/scripts/event045.en.mes";
            if (File.Exists(deployed)) return deployed;
            string src = "c:/Users/ptyRa/OneDrive/Desktop/Pac Files/0002.en/fftpack/text/event045.en.mes";
            return File.Exists(src) ? src : null;
        }

        [Fact]
        public void Event045_QuestionAndPurpose_AreOneBubble()
        {
            var path = FindEvent045();
            if (path == null) return;
            var bytes = File.ReadAllBytes(path);
            var boxes = MesDecoder.DecodeBoxes(bytes);

            // The user verified on screen that "Might I pose a question,
            // Ramza? What purpose do laws serve when even those who would
            // enforce them choose not to pay them heed?" is a single bubble.
            var bubble = boxes.SingleOrDefault(b =>
                b.Text.Contains("Might I pose a question") &&
                b.Text.Contains("What purpose do laws serve") &&
                b.Text.Contains("them heed?"));
            Assert.NotNull(bubble);
        }

        [Fact]
        public void Event045_AdherenceLine_IsItsOwnBubble()
        {
            var path = FindEvent045();
            if (path == null) return;
            var bytes = File.ReadAllBytes(path);
            var boxes = MesDecoder.DecodeBoxes(bytes);

            // "Adherence to the rule of law…example." is one game bubble
            // and must NOT be merged with the previous or next bubbles.
            var adherence = boxes.SingleOrDefault(b =>
                b.Text.Contains("Adherence to the rule of law"));
            Assert.NotNull(adherence);
            Assert.DoesNotContain("Might I pose", adherence!.Text);
            Assert.DoesNotContain("Is your intent", adherence.Text);
        }

        [Fact]
        public void Event045_IsYourIntent_IsItsOwnBubble()
        {
            var path = FindEvent045();
            if (path == null) return;
            var bytes = File.ReadAllBytes(path);
            var boxes = MesDecoder.DecodeBoxes(bytes);

            var intentBubble = boxes.SingleOrDefault(b =>
                b.Text.Contains("Is your intent to live up to your name"));
            Assert.NotNull(intentBubble);
            Assert.Contains("drag it with you through the mire", intentBubble!.Text);
        }

        [Fact]
        public void Event045_RamzaPlaceholdersInsideText_AreSubstituted()
        {
            var path = FindEvent045();
            if (path == null) return;
            var bytes = File.ReadAllBytes(path);
            var boxes = MesDecoder.DecodeBoxes(bytes);

            // 0xE0 placeholders → "Ramza" (e.g. "Might I pose a question,
            // Ramza?" and "'Tis Ramza's noble disposition…").
            var allText = string.Join(" ", boxes.Select(b => b.Text));
            Assert.Contains("Might I pose a question, Ramza?", allText);
            Assert.Contains("Ramza's noble disposition", allText);
        }

        [Fact]
        public void Event045_DA68PlaceholdersInsideText_BecomeEmDashes()
        {
            var path = FindEvent045();
            if (path == null) return;
            var bytes = File.ReadAllBytes(path);
            var boxes = MesDecoder.DecodeBoxes(bytes);
            var allText = string.Join(" ", boxes.Select(b => b.Text));

            // "And let us not forget—they did save…" (Larg's line).
            Assert.Contains("forget—they", allText);
            // "Is your intent to live up to your name—or…" (Dycedarg).
            Assert.Contains("name—or", allText);
            // The pre-fix "nameor" cramming and the wrong-fix "nameRamzaor"
            // must not appear.
            Assert.DoesNotContain("nameor", allText);
            Assert.DoesNotContain("nameRamzaor", allText);
        }
    }
}
