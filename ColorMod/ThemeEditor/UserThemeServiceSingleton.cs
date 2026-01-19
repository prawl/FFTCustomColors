namespace FFTColorCustomizer.ThemeEditor
{
    /// <summary>
    /// Singleton instance of UserThemeService for global access.
    /// Thread-safe implementation using double-checked locking pattern.
    /// </summary>
    public static class UserThemeServiceSingleton
    {
        private static readonly object _lock = new object();
        private static UserThemeService? _instance;
        private static string? _modPath;

        public static UserThemeService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new UserThemeService(_modPath);
                        }
                    }
                }
                return _instance;
            }
        }

        public static void Initialize(string modPath)
        {
            lock (_lock)
            {
                _modPath = modPath;
                _instance = new UserThemeService(modPath);
            }
        }

        /// <summary>
        /// Resets the singleton instance (mainly for testing)
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _instance = null;
            }
        }
    }
}
