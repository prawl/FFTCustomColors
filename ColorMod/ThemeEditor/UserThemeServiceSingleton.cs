namespace FFTColorCustomizer.ThemeEditor
{
    /// <summary>
    /// Singleton instance of UserThemeService for global access
    /// </summary>
    public static class UserThemeServiceSingleton
    {
        private static UserThemeService _instance;

        public static UserThemeService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new UserThemeService(null);
                }
                return _instance;
            }
        }

        public static void Initialize(string modPath)
        {
            _instance = new UserThemeService(modPath);
        }

        public static void Reset()
        {
            _instance = null;
        }
    }
}
