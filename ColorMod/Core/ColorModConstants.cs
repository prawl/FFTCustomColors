namespace FFTColorCustomizer.Core
{
    /// <summary>
    /// Centralized constants for the FFT Color Mod
    /// </summary>
    public static class ColorModConstants
    {
        // Default values
        public const string DefaultTheme = "original";
        public const string DefaultCategory = "Generic Characters";

        // File and directory names
        public const string ConfigFileName = "Config.json";
        public const string ModConfigFileName = "ModConfig.json";
        public const string DataDirectory = "Data";
        public const string UserDirectory = "User";
        public const string ModsDirectory = "Mods";

        // Sprite paths
        public const string FFTIVCPath = "FFTIVC";
        public const string DataPath = "data";
        public const string EnhancedPath = "enhanced";
        public const string FFTPackPath = "fftpack";
        public const string UnitPath = "unit";
        public const string SpritesRelativePath = @"FFTIVC\data\enhanced\fftpack\unit";
        public const string SpritesPrefix = "sprites_";
        public const string SpriteFileExtension = ".bin";
        public const string OutFileExtension = "_out.bin";
        public const string SprFileExtension = "_spr.bin";

        // File patterns
        public const string BattlePrefix = "battle_";
        public const string PreviewPrefix = "preview_";
        public const string BitmapExtension = ".bmp";
        public const string JsonExtension = ".json";

        // JSON file names
        public const string StoryCharactersFile = "StoryCharacters.json";
        public const string JobClassesFile = "JobClasses.json";

        // Property suffixes
        public const string MaleSuffix = "_Male";
        public const string FemaleSuffix = "_Female";

        // Mod metadata
        public const string ModId = "FFTColorCustomizer";
        public const string ModName = "FFT Color Customizer";
        public const string ModAuthor = "Paxtrick";
        public const string ModNamespace = "paxtrick.fft.colorcustomizer";

        // Development paths (should not be used in production - use mod directory instead)
        // This is now deprecated and should not be used
        // public const string DevSourcePath = @"C:\Users\ptyRa\Dev\FFTColorCustomizer\ColorMod";

        // Common directory names
        public const string PreviewsDirectory = "Previews";

        // Logging
        public const string LogPrefix = "[FFT Color Mod]";

        // UI Constants
        public const int PreviewImageWidth = 64;
        public const int PreviewImageHeight = 64;
        public const int RowHeight = 40;
        public const int LabelWidth = 120;
        public const int DropdownWidth = 150;

        // Theme names (commonly used)
        public const string OriginalTheme = "original";
        public const string LucaviTheme = "lucavi";
        public const string CorpseBrigadeTheme = "corpse_brigade";
        public const string VampyreTheme = "vampyre";

        // Error messages
        public const string ConfigNotFoundError = "Configuration file not found";
        public const string InvalidThemeError = "Invalid theme specified";
        public const string SpriteNotFoundError = "Sprite file not found";
        public const string DirectoryNotFoundError = "Directory not found";

        // Success messages
        public const string ConfigSavedSuccess = "Configuration saved successfully";
        public const string ThemeAppliedSuccess = "Theme applied successfully";
        public const string ModLoadedSuccess = "Mod loaded successfully";
    }
}
