using FFTColorCustomizer.Services;

namespace FFTColorCustomizer.Configuration
{
    /// <summary>
    /// The configuration manager, now a thin layer over the configuration service.
    /// </summary>
    public class ConfigurationManager : ConfigurationManagerAdapter
    {
        public ConfigurationManager(string configPath) : base(configPath)
        {
        }

        public ConfigurationManager(string configPath, JobClassDefinitionService jobClassService)
            : base(configPath, jobClassService)
        {
        }
    }
}
