using FFTColorMod.Utilities;

namespace FFTColorMod.Services
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
    }
}