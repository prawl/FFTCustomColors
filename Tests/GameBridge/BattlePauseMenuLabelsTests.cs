using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class BattlePauseMenuLabelsTests
    {
        [Theory]
        [InlineData(0, "Data")]
        [InlineData(1, "Retry")]
        [InlineData(2, "Load")]
        [InlineData(3, "Settings")]
        [InlineData(4, "Return to World Map")]
        [InlineData(5, "Return to Title Screen")]
        public void Returns_CorrectLabel_For_ValidRows(int row, string expected)
        {
            Assert.Equal(expected, BattlePauseMenuLabels.ForRow(row));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(6)]
        [InlineData(100)]
        [InlineData(255)]
        public void Returns_Null_For_OutOfRangeRows(int row)
        {
            Assert.Null(BattlePauseMenuLabels.ForRow(row));
        }

        [Fact]
        public void Pause_Menu_Does_Not_Include_Save()
        {
            // Regression guard: the "SaveSlotPicker from BattlePaused" TODO
            // was closed in session 44 as a myth. If a future refactor
            // re-introduces Save here, this test will catch it.
            for (int r = 0; r <= 5; r++)
            {
                var label = BattlePauseMenuLabels.ForRow(r);
                Assert.NotEqual("Save", label);
            }
        }
    }
}
