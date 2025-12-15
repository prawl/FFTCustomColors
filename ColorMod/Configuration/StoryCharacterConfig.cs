using System;
using System.Collections.Generic;
using System.Reflection;

namespace FFTColorMod.Configuration
{
    /// <summary>
    /// Registry for managing story character configurations
    /// Provides a centralized way to handle story character color schemes
    /// </summary>
    public class StoryCharacterConfig
    {
        private readonly Config _config;
        private readonly Dictionary<string, IStoryCharacterColorScheme> _characterSchemes;

        public StoryCharacterConfig(Config config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _characterSchemes = new Dictionary<string, IStoryCharacterColorScheme>();
            InitializeCharacterSchemes();
        }

        private void InitializeCharacterSchemes()
        {
            // Register all story characters with their color scheme accessors
            RegisterCharacter("Agrias",
                () => _config.Agrias,
                value => _config.Agrias = (AgriasColorScheme)value);

            RegisterCharacter("Orlandeau",
                () => _config.Orlandeau,
                value => _config.Orlandeau = (OrlandeauColorScheme)value);

            RegisterCharacter("Cloud",
                () => _config.Cloud,
                value => _config.Cloud = (CloudColorScheme)value);

            RegisterCharacter("Mustadio",
                () => _config.Mustadio,
                value => _config.Mustadio = (MustadioColorScheme)value);

            RegisterCharacter("Reis",
                () => _config.Reis,
                value => _config.Reis = (ReisColorScheme)value);

            RegisterCharacter("Malak",
                () => _config.Malak,
                value => _config.Malak = (MalakColorScheme)value);

            RegisterCharacter("Rafa",
                () => _config.Rafa,
                value => _config.Rafa = (RafaColorScheme)value);

            RegisterCharacter("Delita",
                () => _config.Delita,
                value => _config.Delita = (DelitaColorScheme)value);

            RegisterCharacter("Alma",
                () => _config.Alma,
                value => _config.Alma = (AlmaColorScheme)value);

            RegisterCharacter("Wiegraf",
                () => _config.Wiegraf,
                value => _config.Wiegraf = (WiegrafColorScheme)value);

            RegisterCharacter("Celia",
                () => _config.Celia,
                value => _config.Celia = (CeliaColorScheme)value);

            RegisterCharacter("Lettie",
                () => _config.Lettie,
                value => _config.Lettie = (LettieColorScheme)value);

            RegisterCharacter("Ovelia",
                () => _config.Ovelia,
                value => _config.Ovelia = (OveliaColorScheme)value);

            RegisterCharacter("Simon",
                () => _config.Simon,
                value => _config.Simon = (SimonColorScheme)value);

            RegisterCharacter("Gaffgarion",
                () => _config.Gaffgarion,
                value => _config.Gaffgarion = (GaffgarionColorScheme)value);

            RegisterCharacter("Elmdore",
                () => _config.Elmdore,
                value => _config.Elmdore = (ElmdoreColorScheme)value);

            RegisterCharacter("Vormav",
                () => _config.Vormav,
                value => _config.Vormav = (VormavColorScheme)value);

            RegisterCharacter("Zalbag",
                () => _config.Zalbag,
                value => _config.Zalbag = (ZalbagColorScheme)value);

            RegisterCharacter("Zalmo",
                () => _config.Zalmo,
                value => _config.Zalmo = (ZalmoColorScheme)value);
        }

        private void RegisterCharacter<T>(string name, Func<T> getter, Action<T> setter) where T : Enum
        {
            _characterSchemes[name] = new StoryCharacterColorScheme<T>(getter, setter);
        }

        /// <summary>
        /// Gets the color scheme for a specific story character
        /// </summary>
        public Enum GetColorScheme(string characterName)
        {
            if (_characterSchemes.TryGetValue(characterName, out var scheme))
            {
                return scheme.GetValue();
            }
            return null;
        }

        /// <summary>
        /// Sets the color scheme for a specific story character
        /// </summary>
        public void SetColorScheme(string characterName, Enum value)
        {
            if (_characterSchemes.TryGetValue(characterName, out var scheme))
            {
                scheme.SetValue(value);
            }
        }

        /// <summary>
        /// Gets the sprite path format for a story character's color scheme
        /// </summary>
        public string GetSpritePathFormat(string characterName)
        {
            // Directly read from config properties to get current values
            // Handle special formatting for certain characters
            switch (characterName)
            {
                case "Agrias":
                    return _config.Agrias.GetDescription();

                case "Orlandeau":
                    if (_config.Orlandeau == OrlandeauColorScheme.original)
                        return "sprites_original";
                    return $"sprites_orlandeau_{_config.Orlandeau.ToString().ToLower()}";

                case "Cloud":
                    if (_config.Cloud == CloudColorScheme.original)
                        return "sprites_original";
                    return $"sprites_cloud_{_config.Cloud.ToString().ToLower()}";

                case "Mustadio":
                    if (_config.Mustadio == MustadioColorScheme.original)
                        return "sprites_original";
                    return $"sprites_{_config.Mustadio.ToString().ToLower()}";

                case "Reis":
                    if (_config.Reis == ReisColorScheme.original)
                        return "sprites_original";
                    return $"sprites_{_config.Reis.ToString().ToLower()}";

                case "Malak":
                    if (_config.Malak == MalakColorScheme.original)
                        return "sprites_original";
                    return $"sprites_{_config.Malak.ToString().ToLower()}";

                case "Rafa":
                    if (_config.Rafa == RafaColorScheme.original)
                        return "sprites_original";
                    return $"sprites_{_config.Rafa.ToString().ToLower()}";

                case "Delita":
                    if (_config.Delita == DelitaColorScheme.original)
                        return "sprites_original";
                    return $"sprites_{_config.Delita.ToString().ToLower()}";

                case "Alma":
                    if (_config.Alma == AlmaColorScheme.original)
                        return "sprites_original";
                    return $"sprites_{_config.Alma.ToString().ToLower()}";

                case "Wiegraf":
                    if (_config.Wiegraf == WiegrafColorScheme.original)
                        return "sprites_original";
                    return $"sprites_{_config.Wiegraf.ToString().ToLower()}";

                case "Celia":
                    if (_config.Celia == CeliaColorScheme.original)
                        return "sprites_original";
                    return $"sprites_{_config.Celia.ToString().ToLower()}";

                case "Lettie":
                    if (_config.Lettie == LettieColorScheme.original)
                        return "sprites_original";
                    return $"sprites_{_config.Lettie.ToString().ToLower()}";

                // These characters don't have GetDescription extensions yet
                case "Ovelia":
                case "Simon":
                case "Gaffgarion":
                case "Elmdore":
                case "Vormav":
                case "Zalbag":
                case "Zalmo":
                    return "sprites_original";

                default:
                    return "sprites_original";
            }
        }

        /// <summary>
        /// Resets all story characters to their original color schemes
        /// </summary>
        public void ResetAll()
        {
            _config.Agrias = AgriasColorScheme.original;
            _config.Orlandeau = OrlandeauColorScheme.original;
            _config.Cloud = CloudColorScheme.original;
            _config.Mustadio = MustadioColorScheme.original;
            _config.Reis = ReisColorScheme.original;
            _config.Malak = MalakColorScheme.original;
            _config.Rafa = RafaColorScheme.original;
            _config.Delita = DelitaColorScheme.original;
            _config.Alma = AlmaColorScheme.original;
            _config.Wiegraf = WiegrafColorScheme.original;
            _config.Celia = CeliaColorScheme.original;
            _config.Lettie = LettieColorScheme.original;
            _config.Ovelia = OveliaColorScheme.original;
            _config.Simon = SimonColorScheme.original;
            _config.Gaffgarion = GaffgarionColorScheme.original;
            _config.Elmdore = ElmdoreColorScheme.original;
            _config.Vormav = VormavColorScheme.original;
            _config.Zalbag = ZalbagColorScheme.original;
            _config.Zalmo = ZalmoColorScheme.original;
        }

        /// <summary>
        /// Gets all registered story character names
        /// </summary>
        public IEnumerable<string> GetAllCharacterNames()
        {
            return _characterSchemes.Keys;
        }

        // Helper interface and class for managing different enum types
        private interface IStoryCharacterColorScheme
        {
            Enum GetValue();
            void SetValue(Enum value);
        }

        private class StoryCharacterColorScheme<T> : IStoryCharacterColorScheme where T : Enum
        {
            private readonly Func<T> _getter;
            private readonly Action<T> _setter;

            public StoryCharacterColorScheme(Func<T> getter, Action<T> setter)
            {
                _getter = getter;
                _setter = setter;
            }

            public Enum GetValue() => _getter();
            public void SetValue(Enum value) => _setter((T)value);
        }
    }
}