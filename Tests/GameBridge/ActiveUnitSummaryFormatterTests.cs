using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class ActiveUnitSummaryFormatterTests
    {
        [Fact]
        public void Format_WithNameJobPositionHp_ReturnsFullSummary()
        {
            var result = ActiveUnitSummaryFormatter.Format(
                name: "Wilham", jobName: "Monk", x: 10, y: 10, hp: 477, maxHp: 477);
            Assert.Equal("Wilham(Monk) (10,10) HP=477/477", result);
        }

        [Fact]
        public void Format_WithNullName_DropsNameAndShowsJobOnly()
        {
            var result = ActiveUnitSummaryFormatter.Format(
                name: null, jobName: "Goblin", x: 5, y: 3, hp: 80, maxHp: 100);
            Assert.Equal("(Goblin) (5,3) HP=80/100", result);
        }

        [Fact]
        public void Format_WithNullJob_KeepsName()
        {
            var result = ActiveUnitSummaryFormatter.Format(
                name: "Ramza", jobName: null, x: 7, y: 4, hp: 300, maxHp: 320);
            Assert.Equal("Ramza (7,4) HP=300/320", result);
        }

        [Fact]
        public void Format_WithNoHp_OmitsHpField()
        {
            var result = ActiveUnitSummaryFormatter.Format(
                name: "Cloud", jobName: "Soldier", x: 0, y: 0, hp: 0, maxHp: 0);
            Assert.Equal("Cloud(Soldier) (0,0)", result);
        }

        [Fact]
        public void Format_WithNoNameOrJob_ReturnsEmpty()
        {
            var result = ActiveUnitSummaryFormatter.Format(
                name: null, jobName: null, x: 0, y: 0, hp: 0, maxHp: 0);
            Assert.Equal("", result);
        }
    }
}
