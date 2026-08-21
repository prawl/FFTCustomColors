namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Singleton instance of JobClassDefinitionService for global access
    /// </summary>
    public static class JobClassServiceSingleton
    {
        private static JobClassDefinitionService _instance;

        public static JobClassDefinitionService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new JobClassDefinitionService();
                }
                return _instance;
            }
        }

        public static void Initialize(string modPath = null)
        {
            _instance = new JobClassDefinitionService(modPath);
        }

        /// <summary>
        /// Resets the singleton instance (mainly for testing), matching the Reset() every other
        /// service singleton exposes. Without it, a test that Initializes this to a temp folder
        /// leaves that instance live for whichever test reads Instance next (CC-26).
        /// </summary>
        public static void Reset()
        {
            _instance = null;
        }
    }
}
