using FFTColorCustomizer.Configuration;

namespace FFTColorCustomizer.Interfaces
{
    /// <summary>
    /// Service for managing sprite operations
    /// </summary>
    public interface ISpriteService
    {
        /// <summary>
        /// Copies sprite files for a specific theme and character
        /// </summary>
        void CopySprites(string theme, string character);

        /// <summary>
        /// Clears all sprite files
        /// </summary>
        void ClearSprites();

        /// <summary>
        /// Applies sprite configuration based on the config
        /// </summary>
        void ApplySpriteConfiguration(Config config);

        /// <summary>
        /// Loads dynamic sprites from a path
        /// </summary>
        void LoadDynamicSprites(string path);
    }
}
