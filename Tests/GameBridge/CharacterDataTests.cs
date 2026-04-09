using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class CharacterDataTests
    {
        [Fact]
        public void GetJobName_RamzaCh4_ReturnsGallantKnight()
        {
            // Roster job=3 is Ramza's Ch4 unique job.
            // The game displays "Gallant Knight", not "Heretic".
            Assert.Equal("Gallant Knight", CharacterData.GetJobName(3));
        }
    }
}
