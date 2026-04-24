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

        [Fact]
        public void Format_WithWeaponTag_AppendsBracketedTag()
        {
            // S60: surface the weapon in the active-unit banner so Claude
            // knows what they're swinging (range, on-hit effect).
            var result = ActiveUnitSummaryFormatter.Format(
                name: "Ramza", jobName: "Gallant Knight", x: 2, y: 1, hp: 719, maxHp: 719,
                weaponTag: "Chaos Blade onHit:Petrify");
            Assert.Equal("Ramza(Gallant Knight) (2,1) HP=719/719 [Chaos Blade onHit:Petrify]", result);
        }

        [Fact]
        public void Format_WithWeaponTagButNoHp_StillAppendsTag()
        {
            var result = ActiveUnitSummaryFormatter.Format(
                name: "Ramza", jobName: "Squire", x: 0, y: 0, hp: 0, maxHp: 0,
                weaponTag: "Iron Flail");
            Assert.Equal("Ramza(Squire) (0,0) [Iron Flail]", result);
        }

        [Fact]
        public void Format_WithEmptyWeaponTag_OmitsBrackets()
        {
            var result = ActiveUnitSummaryFormatter.Format(
                name: "Ramza", jobName: "Squire", x: 0, y: 0, hp: 100, maxHp: 100,
                weaponTag: "");
            Assert.Equal("Ramza(Squire) (0,0) HP=100/100", result);
        }

        [Fact]
        public void Format_WithNullWeaponTag_OmitsBrackets()
        {
            // Backward-compat — null tag behaves same as legacy no-arg call.
            var result = ActiveUnitSummaryFormatter.Format(
                name: "Ramza", jobName: "Squire", x: 0, y: 0, hp: 100, maxHp: 100,
                weaponTag: null);
            Assert.Equal("Ramza(Squire) (0,0) HP=100/100", result);
        }
    }
}
