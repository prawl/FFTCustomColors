using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// The theme manager, now a thin layer over the theme service.
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
