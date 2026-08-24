using System;
using System.Linq;
using FFTColorCustomizer.ThemeEditor;
using Xunit;

namespace FFTColorCustomizer.Tests.ThemeEditor
{
    /// <summary>
    /// CC-31: the game ships unused colour schemes of its own in palette slots 1-7 of a unit bin.
    /// Harvesting one means promoting slot N over slot 0, which is a verbatim byte move: no
    /// quantisation, no shade generation, so the result is exactly what the game's own artists
    /// authored. These tests pin that it stays a byte move and never becomes a colour transform.
    /// </summary>
    public class PaletteHarvesterTests
    {
        private const int Block = 512;
        private const int Stride = 32;

        /// <summary>A bin whose every palette slot is filled with a distinct recognisable pattern.</summary>
        private static byte[] MakeBin(int extraPixelBytes = 64)
        {
            var data = new byte[Block + extraPixelBytes];
            for (int slot = 0; slot < 16; slot++)
                for (int entry = 0; entry < 16; entry++)
                {
                    // Distinct per (slot, entry) so any misplaced copy is detectable.
                    ushort v = (ushort)((slot << 8) | (entry << 2) | 0x0001);
                    data[slot * Stride + entry * 2] = (byte)(v & 0xFF);
                    data[slot * Stride + entry * 2 + 1] = (byte)(v >> 8);
                }
            for (int i = 0; i < extraPixelBytes; i++)
                data[Block + i] = (byte)(0xA0 + (i % 16));
            return data;
        }

        private static byte[] Slot(byte[] data, int slot) =>
            data.Skip(slot * Stride).Take(Stride).ToArray();

        [Fact]
        public void PromoteSlotToZero_copies_the_source_slot_verbatim_over_slot_zero()
        {
            var data = MakeBin();
            var expected = Slot(data, 3);

            var changed = PaletteHarvester.PromoteSlotToZero(data, 3);

            Assert.True(changed);
            Assert.Equal(expected, Slot(data, 0));
        }

        [Fact]
        public void PromoteSlotToZero_leaves_the_source_slot_in_place()
        {
            var data = MakeBin();
            var expected = Slot(data, 3);

            PaletteHarvester.PromoteSlotToZero(data, 3);

            Assert.Equal(expected, Slot(data, 3));
        }

        [Fact]
        public void PromoteSlotToZero_leaves_every_other_palette_slot_untouched()
        {
            var data = MakeBin();
            var before = Enumerable.Range(0, 16).Select(s => Slot(data, s)).ToArray();

            PaletteHarvester.PromoteSlotToZero(data, 3);

            for (int slot = 1; slot < 16; slot++)
                Assert.Equal(before[slot], Slot(data, slot));
        }

        [Fact]
        public void PromoteSlotToZero_leaves_the_pixel_block_byte_identical()
        {
            var data = MakeBin();
            var before = data.Skip(Block).ToArray();

            PaletteHarvester.PromoteSlotToZero(data, 3);

            Assert.Equal(before, data.Skip(Block).ToArray());
        }

        [Fact]
        public void PromoteSlotToZero_is_idempotent()
        {
            var once = MakeBin();
            PaletteHarvester.PromoteSlotToZero(once, 3);
            var afterOne = (byte[])once.Clone();

            PaletteHarvester.PromoteSlotToZero(once, 3);

            Assert.Equal(afterOne, once);
        }

        [Fact]
        public void PromoteSlotToZero_reports_no_change_when_the_source_already_equals_slot_zero()
        {
            var data = MakeBin();
            Array.Copy(data, 3 * Stride, data, 0, Stride);

            Assert.False(PaletteHarvester.PromoteSlotToZero(data, 3));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(16)]
        [InlineData(-1)]
        public void PromoteSlotToZero_rejects_a_slot_outside_one_to_fifteen(int slot)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PaletteHarvester.PromoteSlotToZero(MakeBin(), slot));
        }

        [Fact]
        public void PromoteSlotToZero_rejects_a_buffer_too_short_to_hold_a_palette_block()
        {
            Assert.Throws<ArgumentException>(
                () => PaletteHarvester.PromoteSlotToZero(new byte[Block - 1], 3));
        }

        [Fact]
        public void PromoteSlotToZero_rejects_a_null_buffer()
        {
            Assert.Throws<ArgumentNullException>(() => PaletteHarvester.PromoteSlotToZero(null, 3));
        }
    }
}
