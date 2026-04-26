using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// Pins the rule that in-game dialogue BOXES are delimited by 0xFE,
// while 0xF8 is an intra-bubble line wrap. Verified via live walk-through
// of Dorter event 38 (2026-04-19, 45 real bubbles): every real bubble
// boundary corresponds to a 0xFE in the raw .mes; every 0xF8 is a visual
// newline inside the same bubble. Speaker change (0xE3 0x08 ... 0xE3 0x00)
// is also an implicit bubble boundary.
//
// Runs of multiple 0xFE collapse into one boundary (the game uses 2-5
// consecutive FE bytes to add a pause/animation beat between bubbles).
public class MesDecoderBoxGroupingTests
{
    [Fact]
    public void DecodeBoxes_SingleBox_WithNoControlBytes_ReturnsOneBox()
    {
        var bytes = new byte[] { 0x1C, 0x2C };
        var boxes = MesDecoder.DecodeBoxes(bytes);
        Assert.Single(boxes);
        Assert.Equal("Si", boxes[0].Text);
    }

    [Fact]
    public void DecodeBoxes_TwoBoxes_SeparatedByFE_ReturnsTwoBoxes()
    {
        // "A" 0xFE "B"
        var bytes = new byte[] { 0x0A, 0xFE, 0x0B };
        var boxes = MesDecoder.DecodeBoxes(bytes);
        Assert.Equal(2, boxes.Count);
        Assert.Equal("A", boxes[0].Text);
        Assert.Equal("B", boxes[1].Text);
    }

    [Fact]
    public void DecodeBoxes_F8IsLineWrapWithinBox_NotBoundary()
    {
        // "A" 0xF8 "B" — single bubble, rendered with a line wrap.
        var bytes = new byte[] { 0x0A, 0xF8, 0x0B };
        var boxes = MesDecoder.DecodeBoxes(bytes);
        Assert.Single(boxes);
        Assert.Contains("A", boxes[0].Text);
        Assert.Contains("B", boxes[0].Text);
    }

    [Fact]
    public void DecodeBoxes_PreservesSpeakerPerBox()
    {
        // 'B'=0x0B 'o'=0x32 'b'=0x25 'E'=0x0E 'v'=0x39 'e'=0x28
        // E3 08 <"Bob"> E3 00  "A"  0xFE  E3 08 <"Eve"> E3 00  "B"
        var bytes = new byte[]
        {
            0xE3, 0x08, 0x0B, 0x32, 0x25, 0xE3, 0x00, // speaker "Bob"
            0x0A,                                      // "A"
            0xFE,                                      // box boundary
            0xE3, 0x08, 0x0E, 0x39, 0x28, 0xE3, 0x00, // speaker "Eve"
            0x0B,                                      // "B"
        };
        var boxes = MesDecoder.DecodeBoxes(bytes);
        Assert.Equal(2, boxes.Count);
        Assert.Equal("Bob", boxes[0].Speaker);
        Assert.Equal("Eve", boxes[1].Speaker);
    }

    [Fact]
    public void DecodeBoxes_EmptyInput_ReturnsEmptyList()
    {
        var boxes = MesDecoder.DecodeBoxes(new byte[0]);
        Assert.Empty(boxes);
    }

    [Fact]
    public void DecodeBoxes_ConsecutiveFE_CollapseToOneBoundary()
    {
        // 2+ consecutive 0xFE = one boundary (pause/animation beat, not
        // multiple empty bubbles).
        var bytes = new byte[]
        {
            0x0A,             // "A"
            0xFE, 0xFE,       // 2x FE = one boundary
            0x0B,             // "B"
        };
        var boxes = MesDecoder.DecodeBoxes(bytes);
        Assert.Equal(2, boxes.Count);
        Assert.Equal("A", boxes[0].Text);
        Assert.Equal("B", boxes[1].Text);
    }

    [Fact]
    public void DecodeBoxes_FiveFE_CollapseToOneBoundary()
    {
        // Dorter event 38 ends Argath's paragraph with 5 consecutive FE —
        // the game treats this as one boundary plus a beat, not 5 bubbles.
        var bytes = new byte[] { 0x0A, 0xFE, 0xFE, 0xFE, 0xFE, 0xFE, 0x0B };
        var boxes = MesDecoder.DecodeBoxes(bytes);
        Assert.Equal(2, boxes.Count);
    }

    [Fact]
    public void DecodeBoxes_F8ThenFE_F8IsLineWrap_FEIsBoundary()
    {
        // "Argath: We know you're of the Brigade. 'F8' There's no use hiding it. 'FE' ..."
        // = ONE bubble with a line-wrap, then the FE closes it.
        var bytes = new byte[]
        {
            0x0A,      // "A"
            0xF8,      // line wrap (stays in bubble)
            0x0B,      // "B"
            0xFE,      // bubble boundary
            0x0C,      // "C"
        };
        var boxes = MesDecoder.DecodeBoxes(bytes);
        Assert.Equal(2, boxes.Count);
        Assert.Contains("A", boxes[0].Text);
        Assert.Contains("B", boxes[0].Text);
        Assert.Equal("C", boxes[1].Text);
    }

    [Fact]
    public void DecodeBoxes_ConsecutiveF8_IsIntraBubbleWhitespace()
    {
        // Updated 2026-04-26: the older Zeklaus-event-40 reading that
        // F8×2 = bubble break was wrong. Live walk-through of event 045
        // (Eagrose Castle, segment 0x0203) showed F8×2 sitting INSIDE
        // a single bubble — so all 0xF8 runs decode as intra-bubble
        // whitespace and the bubble count comes from the trailing FE run.
        var bytes = new byte[] { 0x0A, 0xF8, 0xF8, 0x0B };
        var boxes = MesDecoder.DecodeBoxes(bytes);
        Assert.Single(boxes);
    }

    [Fact]
    public void DecodeBoxes_ThreeConsecutiveF8_AlsoIntraBubbleWhitespace()
    {
        var bytes = new byte[] { 0x0A, 0xF8, 0xF8, 0xF8, 0x0B };
        var boxes = MesDecoder.DecodeBoxes(bytes);
        Assert.Single(boxes);
    }

    [Fact]
    public void DecodeBoxes_SingleF8_StillLineWrap()
    {
        // Sanity: the fix for consecutive F8 must not break the single-F8 rule.
        var bytes = new byte[] { 0x0A, 0xF8, 0x0B };
        var boxes = MesDecoder.DecodeBoxes(bytes);
        Assert.Single(boxes);
    }

    [Fact]
    public void DecodeBoxes_SpeakerChange_ImplicitlyBoundsBox()
    {
        var bytes = new byte[]
        {
            0xE3, 0x08, 0x0B, 0x32, 0x25, 0xE3, 0x00, // speaker "Bob"
            0x0A,                                      // "A"
            0xE3, 0x08, 0x0E, 0x39, 0x28, 0xE3, 0x00, // speaker "Eve"
            0x0B,                                      // "B"
        };
        var boxes = MesDecoder.DecodeBoxes(bytes);
        Assert.Equal(2, boxes.Count);
        Assert.Equal("Bob", boxes[0].Speaker);
        Assert.Equal("A", boxes[0].Text);
        Assert.Equal("Eve", boxes[1].Speaker);
        Assert.Equal("B", boxes[1].Text);
    }
}
