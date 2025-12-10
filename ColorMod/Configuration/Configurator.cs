using System;
using System.IO;
using Reloaded.Mod.Interfaces;

namespace FFTColorMod.Configuration
{
    public class Configurator : IConfiguratorV3
    {
        private static ConfiguratorMixin _configuratorMixin = new ConfiguratorMixin();

        /// <summary>
        /// The folder where the modification files are stored.
        /// </summary>
        public string? ModFolder { get; private set; }

        /// <summary>
        /// Full path to the config folder.
        /// </summary>
        public string? ConfigFolder { get; private set; }

        /// <summary>
        /// Specifies additional information for the configurator.
        /// </summary>
        public ConfiguratorContext Context { get; private set; }

        /// <summary>
        /// Returns a list of configurations.
        /// </summary>
        public IUpdatableConfigurable[] Configurations => _configurations ?? MakeConfigurations();
        private IUpdatableConfigurable[]? _configurations;

        private IUpdatableConfigurable[] MakeConfigurations()
        {
            _configurations = _configuratorMixin.MakeConfigurations(ConfigFolder!);

            // Add self-updating to configurations.
            for (int x = 0; x < Configurations.Length; x++)
            {
                var xCopy = x;
                Configurations[x].ConfigurationUpdated += configurable =>
                {
                    Configurations[xCopy] = configurable;
                };
            }

            return _configurations;
        }

        public Configurator() { }

        public Configurator(string configDirectory) : this()
        {
            ConfigFolder = configDirectory;
        }

        /* Configurator V2 */

        /// <summary>
        /// Migrates from the old config location to the newer config location.
        /// </summary>
        /// <param name="oldDirectory">Old directory containing the mod configs.</param>
        /// <param name="newDirectory">New directory pointing to user config folder.</param>
        public void Migrate(string oldDirectory, string newDirectory)
        {
            // Performs migration of configurations from older to newer config directory.
            TryMoveFile("Config.json");

            void TryMoveFile(string fileName)
            {
                try
                {
                    var oldFilePath = Path.Combine(oldDirectory, fileName);
                    var newFilePath = Path.Combine(newDirectory, fileName);

                    if (File.Exists(oldFilePath) && !File.Exists(newFilePath))
                    {
                        Directory.CreateDirectory(newDirectory);
                        File.Move(oldFilePath, newFilePath);
                    }
                }
                catch (Exception) { }
            }
        }

        /* Configurator V3 */

        /// <summary>
        /// Sets the directory where the mod files are stored.
        /// </summary>
        /// <param name="modDirectory">Full directory path to the mod folder.</param>
        public IConfiguratorV3 SetModDirectory(string modDirectory)
        {
            ModFolder = modDirectory;
            return this;
        }

        /// <summary>
        /// Sets the directory where configs are stored.
        /// </summary>
        /// <param name="configDirectory">Full directory path to the config folder.</param>
        public IConfiguratorV3 SetConfigDirectory(string configDirectory)
        {
            ConfigFolder = configDirectory;
            return this;
        }

        /// <summary>
        /// Sets the config directory and migrates from another directory.
        /// </summary>
        /// <param name="configDirectory">Full directory path to the config folder.</param>
        /// <param name="oldDirectory">Old directory to migrate from.</param>
        public IConfiguratorV3 SetConfigDirectory(string configDirectory, string oldDirectory)
        {
            SetConfigDirectory(configDirectory);
            Migrate(oldDirectory, configDirectory);
            return this;
        }

        /// <summary>
        /// Sets a context to be used by the configurator.
        /// </summary>
        /// <param name="context">Context to be used.</param>
        void IConfiguratorV3.SetContext(in ConfiguratorContext context)
        {
            Context = context;
        }

        /// <summary>
        /// Returns a configuration with a specified index.
        /// </summary>
        public TType GetConfiguration<TType>(int index) where TType : class => (TType)(object)Configurations[index];

        /// <summary>
        /// Saves all configurations to disk.
        /// </summary>
        public void Save()
        {
            foreach (var config in Configurations)
            {
                // Call the Save action from IConfigurable interface
                (config as IConfigurable)?.Save?.Invoke();
            }
        }

        /* Configurator V2 */

        /// <summary>
        /// Sets the directory where configs are stored.
        /// </summary>
        /// <param name="configDirectory">Full directory path to the config folder.</param>
        void IConfiguratorV2.SetConfigDirectory(string configDirectory) => ConfigFolder = configDirectory;

        /* IConfiguratorV1 */

        /// <summary>
        /// Gets all configurations.
        /// </summary>
        public IConfigurable[] GetConfigurations() => Configurations;

        /// <summary>
        /// Sets the mod directory for the Configurator.
        /// </summary>
        void IConfiguratorV1.SetModDirectory(string modDirectory) => ModFolder = modDirectory;

        /// <summary>
        /// Tries to run a custom configuration menu.
        /// </summary>
        public bool TryRunCustomConfiguration() => false;
    }
}