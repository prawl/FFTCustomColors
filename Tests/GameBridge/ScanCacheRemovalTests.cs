using System.Reflection;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class ScanCacheRemovalTests
    {
        [Theory]
        [InlineData("HasCachedScan")]
        [InlineData("CachedScanResponse")]
        public void BattleTurnTracker_ShouldNotHaveScanCacheProperties(string propName)
        {
            var prop = typeof(BattleTurnTracker).GetProperty(propName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.Null(prop);
        }

        [Theory]
        [InlineData("CacheScanResponse")]
        [InlineData("InvalidateCache")]
        public void BattleTurnTracker_ShouldNotHaveScanCacheMethods(string methodName)
        {
            var method = typeof(BattleTurnTracker).GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.Null(method);
        }
    }
}
