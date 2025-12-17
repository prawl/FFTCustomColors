using System.Collections.Generic;

namespace FFTColorCustomizer.Interfaces
{
    /// <summary>
    /// Provides centralized path resolution for all file system operations
    /// </summary>
    public interface IPathResolver
    {
        /// <summary>
        /// Gets the root directory of the mod installation
        /// </summary>
        string ModRootPath { get; }

        /// <summary>
        /// Gets the source directory (git repository) path
        /// </summary>
        string SourcePath { get; }

        /// <summary>
        /// Gets the user configuration directory path
        /// </summary>
        string UserConfigPath { get; }

        /// <summary>
        /// Resolves a path relative to the data directory
        /// </summary>
        string GetDataPath(string relativePath);

        /// <summary>
        /// Gets the path to a sprite file for a character and theme
        /// </summary>
        string GetSpritePath(string characterName, string themeName, string spriteFileName);

        /// <summary>
        /// Gets the path to the theme directory for a character
        /// </summary>
        string GetThemeDirectory(string characterName, string themeName);

        /// <summary>
        /// Gets the path to the configuration file
        /// </summary>
        string GetConfigPath();

        /// <summary>
        /// Gets the path to the preview image for a character and theme
        /// </summary>
        string GetPreviewImagePath(string characterName, string themeName);

        /// <summary>
        /// Tries multiple path candidates and returns the first existing one
        /// </summary>
        string ResolveFirstExistingPath(params string[] candidates);

        /// <summary>
        /// Gets all available theme directories for a character
        /// </summary>
        IEnumerable<string> GetAvailableThemes(string characterName);
    }
}
