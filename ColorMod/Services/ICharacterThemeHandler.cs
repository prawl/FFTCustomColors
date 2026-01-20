using System.Collections.Generic;
using FFTColorCustomizer.Configuration;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Strategy interface for character-specific theme handling.
    /// Each character type (standard, Ramza) implements this to encapsulate
    /// their unique theme application logic.
    /// </summary>
    public interface ICharacterThemeHandler
    {
        /// <summary>
        /// The character name this handler manages (e.g., "Orlandeau", "Ramza").
        /// </summary>
        string CharacterName { get; }

        /// <summary>
        /// Cycle to the next available theme for this character.
        /// </summary>
        /// <returns>The new theme name after cycling.</returns>
        string CycleTheme();

        /// <summary>
        /// Apply a specific theme to this character.
        /// </summary>
        /// <param name="themeName">The theme to apply.</param>
        void ApplyTheme(string themeName);

        /// <summary>
        /// Get the currently active theme for this character.
        /// </summary>
        string GetCurrentTheme();

        /// <summary>
        /// Get all available themes for this character.
        /// </summary>
        IEnumerable<string> GetAvailableThemes();

        /// <summary>
        /// Apply theme settings from a configuration object.
        /// </summary>
        void ApplyFromConfiguration(Config config);
    }

    /// <summary>
    /// Extended interface for characters with multi-chapter theme support (e.g., Ramza).
    /// </summary>
    public interface IMultiChapterCharacterHandler : ICharacterThemeHandler
    {
        /// <summary>
        /// Apply different themes per chapter.
        /// </summary>
        /// <param name="chapterThemes">Dictionary mapping chapter names to theme names.</param>
        void ApplyPerChapterThemes(Dictionary<string, string> chapterThemes);

        /// <summary>
        /// Get the chapter identifiers this character uses.
        /// </summary>
        string[] GetChapterNames();

        /// <summary>
        /// Get the current theme for a specific chapter.
        /// </summary>
        string GetChapterTheme(string chapterName);
    }
}
