using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// ThemeManager now uses the new service architecture internally
    /// while maintaining backward compatibility with existing code
    /// </summary>
    public class ThemeManager : ThemeManagerAdapter
    {
        public ThemeManager(string sourcePath, string modPath) : base(sourcePath, modPath)
        {
        }

        // Make methods virtual for testing
        public new virtual void CycleRamzaTheme() => base.CycleRamzaTheme();
        public new virtual void CycleOrlandeauTheme() => base.CycleOrlandeauTheme();
        public new virtual void CycleAgriasTheme() => base.CycleAgriasTheme();
        public new virtual void CycleCloudTheme() => base.CycleCloudTheme();
    }
}
