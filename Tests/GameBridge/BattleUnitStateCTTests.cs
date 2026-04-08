using System.Text.Json;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class BattleUnitStateCTTests
    {
        [Fact]
        public void BattleUnitState_CT_SerializesWhenNonZero()
        {
            var unit = new BattleUnitState
            {
                Team = 0,
                Level = 10,
                Hp = 100,
                MaxHp = 100,
                CT = 85,
            };

            var json = JsonSerializer.Serialize(unit);
            Assert.Contains("\"ct\":85", json);
        }

        [Fact]
        public void BattleUnitState_CT_OmittedWhenZero()
        {
            var unit = new BattleUnitState
            {
                Team = 0,
                Level = 10,
                CT = 0,
            };

            var json = JsonSerializer.Serialize(unit);
            Assert.DoesNotContain("\"ct\"", json);
        }
    }
}
