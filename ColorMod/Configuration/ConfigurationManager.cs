using FFTColorMod.Services;

namespace FFTColorMod.Configuration
{
    /// <summary>
    /// ConfigurationManager now uses the new service architecture internally
    /// while maintaining backward compatibility with existing code
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