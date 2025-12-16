using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FFTColorMod.Configuration
{
    /// <summary>
    /// Config class using dictionary-based storage for color schemes
    /// </summary>
    [Newtonsoft.Json.JsonConverter(typeof(ReflectionBasedConfigJsonConverter))]
    [JsonConverter(typeof(ReflectionBasedSystemTextJsonConverter))]
    public class Config : Configurable<Config>
    {
        // Dictionary to store all color schemes
        private Dictionary<string, ColorScheme> _colorSchemes = new();

        // Metadata for each job
        private static readonly Dictionary<string, JobMetadata> _jobMetadata = new()
        {
            // Squires
            ["Squire_Male"] = new JobMetadata("Generic Characters", "Male Squire", "Color scheme for all male squires", "SquireMale"),
            ["Squire_Female"] = new JobMetadata("Generic Characters", "Female Squire", "Color scheme for all female squires", "SquireFemale"),

            // Knights
            ["Knight_Male"] = new JobMetadata("Generic Characters", "Male Knight", "Color scheme for all male knights", "KnightMale"),
            ["Knight_Female"] = new JobMetadata("Generic Characters", "Female Knight", "Color scheme for all female knights", "KnightFemale"),

            // Monks
            ["Monk_Male"] = new JobMetadata("Generic Characters", "Male Monk", "Color scheme for all male monks", "MonkMale"),
            ["Monk_Female"] = new JobMetadata("Generic Characters", "Female Monk", "Color scheme for all female monks", "MonkFemale"),

            // Archers
            ["Archer_Male"] = new JobMetadata("Generic Characters", "Male Archer", "Color scheme for all male archers", "ArcherMale"),
            ["Archer_Female"] = new JobMetadata("Generic Characters", "Female Archer", "Color scheme for all female archers", "ArcherFemale"),

            // White Mages
            ["WhiteMage_Male"] = new JobMetadata("Generic Characters", "Male White Mage", "Color scheme for all male white mages", "WhiteMageMale"),
            ["WhiteMage_Female"] = new JobMetadata("Generic Characters", "Female White Mage", "Color scheme for all female white mages", "WhiteMageFemale"),

            // Black Mages
            ["BlackMage_Male"] = new JobMetadata("Generic Characters", "Male Black Mage", "Color scheme for all male black mages", "BlackMageMale"),
            ["BlackMage_Female"] = new JobMetadata("Generic Characters", "Female Black Mage", "Color scheme for all female black mages", "BlackMageFemale"),

            // Time Mages
            ["TimeMage_Male"] = new JobMetadata("Generic Characters", "Male Time Mage", "Color scheme for all male time mages", "TimeMageMale"),
            ["TimeMage_Female"] = new JobMetadata("Generic Characters", "Female Time Mage", "Color scheme for all female time mages", "TimeMageFemale"),

            // Summoners
            ["Summoner_Male"] = new JobMetadata("Generic Characters", "Male Summoner", "Color scheme for all male summoners", "SummonerMale"),
            ["Summoner_Female"] = new JobMetadata("Generic Characters", "Female Summoner", "Color scheme for all female summoners", "SummonerFemale"),

            // Thieves
            ["Thief_Male"] = new JobMetadata("Generic Characters", "Male Thief", "Color scheme for all male thieves", "ThiefMale"),
            ["Thief_Female"] = new JobMetadata("Generic Characters", "Female Thief", "Color scheme for all female thieves", "ThiefFemale"),

            // Mediators
            ["Mediator_Male"] = new JobMetadata("Generic Characters", "Male Mediator", "Color scheme for all male mediators", "MediatorMale"),
            ["Mediator_Female"] = new JobMetadata("Generic Characters", "Female Mediator", "Color scheme for all female mediators", "MediatorFemale"),

            // Mystics
            ["Mystic_Male"] = new JobMetadata("Generic Characters", "Male Mystic", "Color scheme for all male mystics", "MysticMale"),
            ["Mystic_Female"] = new JobMetadata("Generic Characters", "Female Mystic", "Color scheme for all female mystics", "MysticFemale"),

            // Geomancers
            ["Geomancer_Male"] = new JobMetadata("Generic Characters", "Male Geomancer", "Color scheme for all male geomancers", "GeomancerMale"),
            ["Geomancer_Female"] = new JobMetadata("Generic Characters", "Female Geomancer", "Color scheme for all female geomancers", "GeomancerFemale"),

            // Dragoons
            ["Dragoon_Male"] = new JobMetadata("Generic Characters", "Male Dragoon", "Color scheme for all male dragoons", "DragoonMale"),
            ["Dragoon_Female"] = new JobMetadata("Generic Characters", "Female Dragoon", "Color scheme for all female dragoons", "DragoonFemale"),

            // Samurai
            ["Samurai_Male"] = new JobMetadata("Generic Characters", "Male Samurai", "Color scheme for all male samurai", "SamuraiMale"),
            ["Samurai_Female"] = new JobMetadata("Generic Characters", "Female Samurai", "Color scheme for all female samurai", "SamuraiFemale"),

            // Ninjas
            ["Ninja_Male"] = new JobMetadata("Generic Characters", "Male Ninja", "Color scheme for all male ninjas", "NinjaMale"),
            ["Ninja_Female"] = new JobMetadata("Generic Characters", "Female Ninja", "Color scheme for all female ninjas", "NinjaFemale"),

            // Calculators
            ["Calculator_Male"] = new JobMetadata("Generic Characters", "Male Calculator", "Color scheme for all male calculators", "CalculatorMale"),
            ["Calculator_Female"] = new JobMetadata("Generic Characters", "Female Calculator", "Color scheme for all female calculators", "CalculatorFemale"),

            // Bards (Male only)
            ["Bard_Male"] = new JobMetadata("Generic Characters", "Male Bard", "Color scheme for all male bards", "BardMale"),

            // Dancers (Female only)
            ["Dancer_Female"] = new JobMetadata("Generic Characters", "Female Dancer", "Color scheme for all female dancers", "DancerFemale"),

            // Mimes
            ["Mime_Male"] = new JobMetadata("Generic Characters", "Male Mime", "Color scheme for all male mimes", "MimeMale"),
            ["Mime_Female"] = new JobMetadata("Generic Characters", "Female Mime", "Color scheme for all female mimes", "MimeFemale"),

            // Chemists
            ["Chemist_Male"] = new JobMetadata("Generic Characters", "Male Chemist", "Color scheme for all male chemists", "ChemistMale"),
            ["Chemist_Female"] = new JobMetadata("Generic Characters", "Female Chemist", "Color scheme for all female chemists", "ChemistFemale"),
        };

        public Config()
        {
            // Initialize all color schemes to original
            foreach (var key in _jobMetadata.Keys)
            {
                _colorSchemes[key] = ColorScheme.original;
            }
        }

        // Dictionary-based accessors
        public ColorScheme GetColorScheme(string jobKey)
        {
            return _colorSchemes.GetValueOrDefault(jobKey, ColorScheme.original);
        }

        public void SetColorScheme(string jobKey, ColorScheme value)
        {
            _colorSchemes[jobKey] = value;
        }

        public void SetColorSchemes(Dictionary<string, ColorScheme> updates)
        {
            foreach (var kvp in updates)
            {
                _colorSchemes[kvp.Key] = kvp.Value;
            }
        }

        public JobMetadata GetJobMetadata(string jobKey)
        {
            return _jobMetadata.GetValueOrDefault(jobKey);
        }

        public IEnumerable<string> GetAllJobKeys()
        {
            return _jobMetadata.Keys;
        }

        // Squires
        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Squire_Male
        {
            get => GetColorScheme("Squire_Male");
            set => SetColorScheme("Squire_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Squire_Female
        {
            get => GetColorScheme("Squire_Female");
            set => SetColorScheme("Squire_Female", value);
        }

        // Knights
        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Knight_Male
        {
            get => GetColorScheme("Knight_Male");
            set => SetColorScheme("Knight_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Knight_Female
        {
            get => GetColorScheme("Knight_Female");
            set => SetColorScheme("Knight_Female", value);
        }

        // Monks
        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Monk_Male
        {
            get => GetColorScheme("Monk_Male");
            set => SetColorScheme("Monk_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Monk_Female
        {
            get => GetColorScheme("Monk_Female");
            set => SetColorScheme("Monk_Female", value);
        }

        // Archers
        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Archer_Male
        {
            get => GetColorScheme("Archer_Male");
            set => SetColorScheme("Archer_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Archer_Female
        {
            get => GetColorScheme("Archer_Female");
            set => SetColorScheme("Archer_Female", value);
        }

        // White Mages
        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme WhiteMage_Male
        {
            get => GetColorScheme("WhiteMage_Male");
            set => SetColorScheme("WhiteMage_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme WhiteMage_Female
        {
            get => GetColorScheme("WhiteMage_Female");
            set => SetColorScheme("WhiteMage_Female", value);
        }

        // Black Mages
        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme BlackMage_Male
        {
            get => GetColorScheme("BlackMage_Male");
            set => SetColorScheme("BlackMage_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme BlackMage_Female
        {
            get => GetColorScheme("BlackMage_Female");
            set => SetColorScheme("BlackMage_Female", value);
        }

        // Time Mages
        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme TimeMage_Male
        {
            get => GetColorScheme("TimeMage_Male");
            set => SetColorScheme("TimeMage_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme TimeMage_Female
        {
            get => GetColorScheme("TimeMage_Female");
            set => SetColorScheme("TimeMage_Female", value);
        }

        // Summoners
        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Summoner_Male
        {
            get => GetColorScheme("Summoner_Male");
            set => SetColorScheme("Summoner_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Summoner_Female
        {
            get => GetColorScheme("Summoner_Female");
            set => SetColorScheme("Summoner_Female", value);
        }

        // Thieves
        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Thief_Male
        {
            get => GetColorScheme("Thief_Male");
            set => SetColorScheme("Thief_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Thief_Female
        {
            get => GetColorScheme("Thief_Female");
            set => SetColorScheme("Thief_Female", value);
        }

        // Mediators
        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Mediator_Male
        {
            get => GetColorScheme("Mediator_Male");
            set => SetColorScheme("Mediator_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Mediator_Female
        {
            get => GetColorScheme("Mediator_Female");
            set => SetColorScheme("Mediator_Female", value);
        }

        // Mystics
        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Mystic_Male
        {
            get => GetColorScheme("Mystic_Male");
            set => SetColorScheme("Mystic_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Mystic_Female
        {
            get => GetColorScheme("Mystic_Female");
            set => SetColorScheme("Mystic_Female", value);
        }

        // Geomancers
        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Geomancer_Male
        {
            get => GetColorScheme("Geomancer_Male");
            set => SetColorScheme("Geomancer_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Geomancer_Female
        {
            get => GetColorScheme("Geomancer_Female");
            set => SetColorScheme("Geomancer_Female", value);
        }

        // Dragoons
        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Dragoon_Male
        {
            get => GetColorScheme("Dragoon_Male");
            set => SetColorScheme("Dragoon_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Dragoon_Female
        {
            get => GetColorScheme("Dragoon_Female");
            set => SetColorScheme("Dragoon_Female", value);
        }

        // Samurai
        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Samurai_Male
        {
            get => GetColorScheme("Samurai_Male");
            set => SetColorScheme("Samurai_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Samurai_Female
        {
            get => GetColorScheme("Samurai_Female");
            set => SetColorScheme("Samurai_Female", value);
        }

        // Ninjas
        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Ninja_Male
        {
            get => GetColorScheme("Ninja_Male");
            set => SetColorScheme("Ninja_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Ninja_Female
        {
            get => GetColorScheme("Ninja_Female");
            set => SetColorScheme("Ninja_Female", value);
        }

        // Calculators
        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Calculator_Male
        {
            get => GetColorScheme("Calculator_Male");
            set => SetColorScheme("Calculator_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Calculator_Female
        {
            get => GetColorScheme("Calculator_Female");
            set => SetColorScheme("Calculator_Female", value);
        }

        // Bards (Male only)
        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Bard_Male
        {
            get => GetColorScheme("Bard_Male");
            set => SetColorScheme("Bard_Male", value);
        }

        // Dancers (Female only)
        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Dancer_Female
        {
            get => GetColorScheme("Dancer_Female");
            set => SetColorScheme("Dancer_Female", value);
        }

        // Mimes
        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Mime_Male
        {
            get => GetColorScheme("Mime_Male");
            set => SetColorScheme("Mime_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Mime_Female
        {
            get => GetColorScheme("Mime_Female");
            set => SetColorScheme("Mime_Female", value);
        }

        // Chemists
        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Chemist_Male
        {
            get => GetColorScheme("Chemist_Male");
            set => SetColorScheme("Chemist_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public ColorScheme Chemist_Female
        {
            get => GetColorScheme("Chemist_Female");
            set => SetColorScheme("Chemist_Female", value);
        }

        // Dynamic story character configuration
        private DynamicStoryCharacterConfig _dynamicCharacters;

        private DynamicStoryCharacterConfig DynamicCharacters
        {
            get
            {
                if (_dynamicCharacters == null)
                {
                    _dynamicCharacters = new DynamicStoryCharacterConfig();
                    // Sync existing properties to dynamic system
                    SyncToDynamicSystem();
                }
                return _dynamicCharacters;
            }
        }

        private void SyncToDynamicSystem()
        {
            if (_dynamicCharacters != null)
            {
                _dynamicCharacters.SetThemeObject("Agrias", Agrias);
                _dynamicCharacters.SetThemeObject("Orlandeau", Orlandeau);
                _dynamicCharacters.SetThemeObject("Cloud", Cloud);
                _dynamicCharacters.SetThemeObject("Mustadio", Mustadio);
                _dynamicCharacters.SetThemeObject("Reis", Reis);
                _dynamicCharacters.SetThemeObject("Delita", Delita);
                _dynamicCharacters.SetThemeObject("Alma", Alma);
            }
        }

        // Story Characters - these remain as-is since they use different enums
        public AgriasColorScheme Agrias { get; set; } = AgriasColorScheme.original;
        public OrlandeauColorScheme Orlandeau { get; set; } = OrlandeauColorScheme.original;
        public CloudColorScheme Cloud { get; set; } = CloudColorScheme.original;
        public MustadioColorScheme Mustadio { get; set; } = MustadioColorScheme.original;
        public ReisColorScheme Reis { get; set; } = ReisColorScheme.original;
        public DelitaColorScheme Delita { get; set; } = DelitaColorScheme.original;
        public AlmaColorScheme Alma { get; set; } = AlmaColorScheme.original;

        // Custom JSON serialization to maintain compatibility
        public Dictionary<string, object> ToJsonDictionary()
        {
            var result = new Dictionary<string, object>();

            // Add all generic character schemes
            foreach (var kvp in _jobMetadata)
            {
                var jsonPropertyName = kvp.Value.JsonPropertyName;
                var value = _colorSchemes[kvp.Key];
                result[jsonPropertyName] = value.ToString();
            }

            // Add all story characters
            result["Agrias"] = Agrias.ToString();
            result["Orlandeau"] = Orlandeau.ToString();
            result["Cloud"] = Cloud.ToString();
            result["Mustadio"] = Mustadio.ToString();
            result["Reis"] = Reis.ToString();
            result["Delita"] = Delita.ToString();
            result["Alma"] = Alma.ToString();

            return result;
        }

        /// <summary>
        /// Get a story character's theme dynamically by name
        /// </summary>
        public object GetStoryCharacterTheme(string characterName)
        {
            // First check if it's one of the existing hardcoded properties
            switch (characterName)
            {
                case "Agrias": return Agrias;
                case "Orlandeau": return Orlandeau;
                case "Cloud": return Cloud;
                case "Mustadio": return Mustadio;
                case "Reis": return Reis;
                case "Delita": return Delita;
                case "Alma": return Alma;
                default:
                    // Check dynamic system for new characters
                    return DynamicCharacters.GetThemeObject(characterName);
            }
        }

        /// <summary>
        /// Set a story character's theme dynamically by name
        /// </summary>
        public void SetStoryCharacterTheme(string characterName, object theme)
        {
            // Update both the property and dynamic system
            switch (characterName)
            {
                case "Agrias":
                    if (theme is AgriasColorScheme agrias) Agrias = agrias;
                    break;
                case "Orlandeau":
                    if (theme is OrlandeauColorScheme orlandeau) Orlandeau = orlandeau;
                    break;
                case "Cloud":
                    if (theme is CloudColorScheme cloud) Cloud = cloud;
                    break;
                case "Mustadio":
                    if (theme is MustadioColorScheme mustadio) Mustadio = mustadio;
                    break;
                case "Reis":
                    if (theme is ReisColorScheme reis) Reis = reis;
                    break;
                case "Delita":
                    if (theme is DelitaColorScheme delita) Delita = delita;
                    break;
                case "Alma":
                    if (theme is AlmaColorScheme alma) Alma = alma;
                    break;
            }

            // Always update dynamic system
            DynamicCharacters.SetThemeObject(characterName, theme);
        }

    }
}