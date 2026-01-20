using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Core;
using FFTColorCustomizer.Interfaces;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Registry that manages all character theme handlers.
    /// Provides a unified interface for theme operations across all story characters.
    /// Replaces the duplicate methods in ThemeManagerAdapter with a single,
    /// parameterized approach using the Strategy pattern.
    /// </summary>
    public class CharacterThemeHandlerRegistry
    {
        private readonly Dictionary<string, ICharacterThemeHandler> _handlers;
        private readonly IThemeService _themeService;
        private readonly StoryCharacterThemeManager _storyCharacterManager;
        private readonly string _sourcePath;
        private readonly string _modPath;

        // Cached character names loaded from StoryCharacters.json
        private readonly List<string> _standardCharacterNames;

        /// <summary>
        /// Standard characters that use simple sprite-based theming.
        /// Loaded from StoryCharacters.json, excluding Ramza chapters.
        /// </summary>
        public IReadOnlyList<string> StandardCharacterNames => _standardCharacterNames;

        public CharacterThemeHandlerRegistry(
            string sourcePath,
            string modPath,
            IThemeService themeService,
            StoryCharacterThemeManager storyCharacterManager)
        {
            _sourcePath = sourcePath;
            _modPath = modPath;
            _themeService = themeService;
            _storyCharacterManager = storyCharacterManager;
            _handlers = new Dictionary<string, ICharacterThemeHandler>(StringComparer.OrdinalIgnoreCase);

            // Load character names from StoryCharacters.json
            _standardCharacterNames = LoadStandardCharacterNames();

            RegisterAllHandlers();
        }

        /// <summary>
        /// Loads standard character names from StoryCharacters.json.
        /// Excludes Ramza chapters (RamzaChapter1, RamzaChapter23, RamzaChapter4).
        /// </summary>
        private List<string> LoadStandardCharacterNames()
        {
            var characters = new List<string>();
            var jsonPath = Path.Combine(_modPath, "Data", "StoryCharacters.json");

            try
            {
                if (File.Exists(jsonPath))
                {
                    var jsonContent = File.ReadAllText(jsonPath);
                    var document = JsonDocument.Parse(jsonContent);

                    if (document.RootElement.TryGetProperty("characters", out var charactersArray))
                    {
                        foreach (var character in charactersArray.EnumerateArray())
                        {
                            var name = character.GetProperty("name").GetString();
                            if (!string.IsNullOrEmpty(name) && !name.StartsWith("Ramza"))
                            {
                                characters.Add(name);
                            }
                        }
                    }

                    ModLogger.LogDebug($"Loaded {characters.Count} standard characters from StoryCharacters.json");
                }
                else
                {
                    ModLogger.LogWarning($"StoryCharacters.json not found at: {jsonPath}, using fallback list");
                    characters.AddRange(GetFallbackCharacterNames());
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"Error loading StoryCharacters.json: {ex.Message}, using fallback list");
                characters.AddRange(GetFallbackCharacterNames());
            }

            return characters;
        }

        /// <summary>
        /// Fallback character names if StoryCharacters.json cannot be loaded.
        /// </summary>
        private static IEnumerable<string> GetFallbackCharacterNames()
        {
            return new[] { "Orlandeau", "Agrias", "Cloud", "Mustadio", "Marach",
                           "Beowulf", "Meliadoul", "Rapha", "Reis" };
        }

        /// <summary>
        /// Registers all character handlers.
        /// </summary>
        private void RegisterAllHandlers()
        {
            // Register Ramza (special multi-chapter handler)
            RegisterHandler(new RamzaCharacterThemeHandler(
                _sourcePath, _modPath, _themeService, _storyCharacterManager));

            // Register all standard characters loaded from StoryCharacters.json
            foreach (var characterName in _standardCharacterNames)
            {
                RegisterHandler(new StandardCharacterThemeHandler(
                    characterName, _sourcePath, _modPath, _themeService, _storyCharacterManager));
            }

            ModLogger.LogDebug($"CharacterThemeHandlerRegistry: Registered {_handlers.Count} handlers");
        }

        /// <summary>
        /// Registers a character theme handler.
        /// </summary>
        public void RegisterHandler(ICharacterThemeHandler handler)
        {
            _handlers[handler.CharacterName] = handler;
        }

        /// <summary>
        /// Gets the handler for a specific character.
        /// </summary>
        /// <returns>The handler, or null if not found.</returns>
        public ICharacterThemeHandler GetHandler(string characterName)
        {
            return _handlers.TryGetValue(characterName, out var handler) ? handler : null;
        }

        /// <summary>
        /// Gets the multi-chapter handler for Ramza.
        /// </summary>
        public IMultiChapterCharacterHandler GetRamzaHandler()
        {
            return GetHandler("Ramza") as IMultiChapterCharacterHandler;
        }

        /// <summary>
        /// Checks if a character has a registered handler.
        /// </summary>
        public bool HasHandler(string characterName)
        {
            return _handlers.ContainsKey(characterName);
        }

        /// <summary>
        /// Gets all registered handler names.
        /// </summary>
        public IEnumerable<string> GetRegisteredCharacters()
        {
            return _handlers.Keys;
        }

        #region Unified Theme Operations

        /// <summary>
        /// Cycles the theme for any character.
        /// This is the primary method that replaces all character-specific cycle methods.
        /// </summary>
        /// <param name="characterName">The character name (e.g., "Ramza", "Orlandeau")</param>
        /// <returns>The new theme name after cycling.</returns>
        public string CycleTheme(string characterName)
        {
            var handler = GetHandler(characterName);
            if (handler == null)
            {
                ModLogger.LogWarning($"No handler registered for character: {characterName}");
                return "original";
            }

            return handler.CycleTheme();
        }

        /// <summary>
        /// Applies a theme to any character.
        /// </summary>
        /// <param name="characterName">The character name</param>
        /// <param name="themeName">The theme to apply</param>
        public void ApplyTheme(string characterName, string themeName)
        {
            var handler = GetHandler(characterName);
            if (handler == null)
            {
                ModLogger.LogWarning($"No handler registered for character: {characterName}");
                return;
            }

            handler.ApplyTheme(themeName);
        }

        /// <summary>
        /// Gets the current theme for a character.
        /// </summary>
        public string GetCurrentTheme(string characterName)
        {
            var handler = GetHandler(characterName);
            return handler?.GetCurrentTheme() ?? "original";
        }

        /// <summary>
        /// Gets available themes for a character.
        /// </summary>
        public IEnumerable<string> GetAvailableThemes(string characterName)
        {
            var handler = GetHandler(characterName);
            return handler?.GetAvailableThemes() ?? new[] { "original" };
        }

        /// <summary>
        /// Applies initial themes for all characters from stored configuration.
        /// </summary>
        public void ApplyInitialThemes()
        {
            ModLogger.LogDebug("CharacterThemeHandlerRegistry.ApplyInitialThemes() called");

            // Apply Ramza first (has special initialization)
            var ramzaHandler = GetHandler("Ramza");
            if (ramzaHandler != null)
            {
                var theme = _storyCharacterManager.GetCurrentTheme("RamzaChapter1");
                if (string.IsNullOrEmpty(theme))
                {
                    theme = _storyCharacterManager.GetCurrentTheme("Ramza");
                }
                ramzaHandler.ApplyTheme(theme ?? "original");
            }

            // Apply themes for all standard characters
            foreach (var characterName in _standardCharacterNames)
            {
                var handler = GetHandler(characterName);
                if (handler != null)
                {
                    var theme = _storyCharacterManager.GetCurrentTheme(characterName);
                    handler.ApplyTheme(theme ?? "original");
                }
            }
        }

        /// <summary>
        /// Applies themes from a configuration object for all characters.
        /// </summary>
        public void ApplyFromConfiguration(Config config)
        {
            foreach (var handler in _handlers.Values)
            {
                handler.ApplyFromConfiguration(config);
            }
        }

        #endregion

        #region Backward Compatibility

        /// <summary>
        /// Gets the underlying StoryCharacterThemeManager for backward compatibility.
        /// </summary>
        public StoryCharacterThemeManager GetStoryCharacterManager()
        {
            return _storyCharacterManager;
        }

        #endregion
    }
}
