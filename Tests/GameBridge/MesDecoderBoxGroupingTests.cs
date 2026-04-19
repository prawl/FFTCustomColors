using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// Pins the rule that in-game dialogue BOXES are delimited by 0xF8,
// while 0xFE is a line-break WITHIN a box. The existing DialogueLine
// decoder flushes on both bytes (useful for preview output) but we
// need a separate "boxes" view to track "which bubble is on screen
// right now" — one advance = one 0xF8 boundary.
public class MesDecoderBoxGroupingTests
{
    [Fact]
    public void DecodeBoxes_SingleBox_WithNoLineBreak_ReturnsOneBox()
    {
        // "Hi" (0x1C 0x2C) — no boundaries at all.
        var bytes = new byte[] { 0x1C, 0x2C };
        var boxes = MesDecoder.DecodeBoxes(bytes);
        Assert.Single(boxes);
        Assert.Equal("Si", boxes[0].Text);
    }

    [Fact]
    public void DecodeBoxes_TwoBoxes_SeparatedByF8_ReturnsTwoBoxes()
    {
        // "A" 0xF8 "B"
        var bytes = new byte[] { 0x0A, 0xF8, 0x0B };
        var boxes = MesDecoder.DecodeBoxes(bytes);
        Assert.Equal(2, boxes.Count);
        Assert.Equal("A", boxes[0].Text);
        Assert.Equal("B", boxes[1].Text);
    }

    [Fact]
    public void DecodeBoxes_LineBreakInBoxIsNotBoxBoundary()
    {
        // "A" 0xFE "B" — all one box, rendered with a newline.
        var bytes = new byte[] { 0x0A, 0xFE, 0x0B };
        var boxes = MesDecoder.DecodeBoxes(bytes);
        Assert.Single(boxes);
        // Newline preserved so callers can render multi-line boxes as the game does.
        Assert.Contains("A", boxes[0].Text);
        Assert.Contains("B", boxes[0].Text);
    }

    [Fact]
    public void DecodeBoxes_PreservesSpeakerPerBox()
    {
        // 'B'=0x0B 'o'=0x32 'b'=0x25 'E'=0x0E 'v'=0x38 'e'=0x28
        // E3 08 <"Bob"> E3 00  "A"  0xF8  E3 08 <"Eve"> E3 00  "B"
        var bytes = new byte[]
        {
            0xE3, 0x08, 0x0B, 0x32, 0x25, 0xE3, 0x00, // speaker "Bob"
            0x0A,                                      // "A"
            0xF8,                                      // box boundary
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
    public void DecodeBoxes_SpeakerChange_ImplicitlyBoundsBox()
    {
        // E3 08 <"Bob"> E3 00  "A"  E3 08 <"Eve"> E3 00  "B"
        // No 0xF8 between, but speaker changes — each speaker gets
        // their own box. The game renders a new bubble when the
        // speaker changes regardless of F8 presence (Dorter event 34
        // starts with Swordsman: "I said I know naught of it" then
        // Knight: "Do not speak false to me" with no F8 between).
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
