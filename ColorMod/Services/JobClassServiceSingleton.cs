namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Singleton instance of JobClassDefinitionService for global access.
    /// Thread-safe implementation using double-checked locking pattern.
    /// </summary>
    public static class JobClassServiceSingleton
    {
        private static readonly object _lock = new object();
        private static JobClassDefinitionService? _instance;
        private static string? _modPath;

        public static JobClassDefinitionService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new JobClassDefinitionService(_modPath);
                        }
                    }
                }
                return _instance;
            }
        }

        public static void Initialize(string modPath = null)
        {
            lock (_lock)
            {
                _modPath = modPath;
                _instance = new JobClassDefinitionService(modPath);
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
