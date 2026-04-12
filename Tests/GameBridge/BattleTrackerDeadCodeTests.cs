using System.Reflection;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class BattleTrackerDeadCodeTests
    {
        // The old heap scanning methods were replaced by the static battle array
        // at 0x140893C00. They should be removed entirely.

        [Theory]
        [InlineData("ScanHeapForPositions")]
        [InlineData("RefreshPositionsFromKnownAddresses")]
        [InlineData("ReadPositionFromHeap")]
        public void BattleTracker_ShouldNotHaveDeadHeapScanningMethods(string methodName)
        {
            var method = typeof(BattleTracker).GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.Null(method);
        }

        [Theory]
        [InlineData("_heapAddresses")]
        [InlineData("_heapScanDone")]
        [InlineData("_lastHeapScan")]
        public void BattleTracker_ShouldNotHaveDeadHeapScanningFields(string fieldName)
        {
            var field = typeof(BattleTracker).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.Null(field);
        }

        [Theory]
        [InlineData("HeapOffX")]
        [InlineData("HeapOffY")]
        [InlineData("HeapOffTurnFlag")]
        [InlineData("HeapOffHp")]
        [InlineData("HeapOffMaxHp")]
        [InlineData("HeapOffMp")]
        [InlineData("HeapOffMaxMp")]
        public void BattleTracker_ShouldNotHaveDeadHeapOffsetConstants(string constName)
        {
            var field = typeof(BattleTracker).GetField(constName,
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.Null(field);
        }
    }
}
