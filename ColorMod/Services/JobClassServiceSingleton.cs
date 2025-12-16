namespace FFTColorMod.Services
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
    }
}