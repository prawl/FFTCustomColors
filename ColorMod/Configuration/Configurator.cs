using System;
using System.IO;
using System.Threading;
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
        public bool TryRunCustomConfiguration()
        {
            Console.WriteLine("[FFT Color Mod] TryRunCustomConfiguration called!");

            // Use Windows Forms for better compatibility
            try
            {
                Console.WriteLine("[FFT Color Mod] Getting configuration...");
                Console.WriteLine($"[FFT Color Mod] ConfigFolder: {ConfigFolder}");

                // Load directly from the config file, not through the old system
                var configPath = Path.Combine(ConfigFolder!, "Config.json");
                Console.WriteLine($"[FFT Color Mod] Loading config directly from: {configPath}");

                var configManager = new ConfigurationManager(configPath);
                var config = configManager.LoadConfig();
                Console.WriteLine($"[FFT Color Mod] Config loaded - Squire_Male: {config.Squire_Male}");

                Console.WriteLine("[FFT Color Mod] Creating configuration form...");
                var formConfigPath = Path.Combine(ConfigFolder!, "Config.json");
                Console.WriteLine($"[FFT Color Mod] Will save to: {formConfigPath}");
                var configForm = new ConfigurationForm(config, formConfigPath);

                Console.WriteLine("[FFT Color Mod] Showing configuration form...");
                var result = configForm.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    Console.WriteLine("[FFT Color Mod] User clicked Save - DialogResult.OK received");
                    Console.WriteLine($"[FFT Color Mod] Config state before save - Squire_Male: {config.Squire_Male}");

                    // Always save directly to ensure consistency
                    var saveConfigPath = Path.Combine(ConfigFolder!, "Config.json");
                    Console.WriteLine($"[FFT Color Mod] Saving directly to: {saveConfigPath}");
                    var saveConfigManager = new ConfigurationManager(saveConfigPath);
                    saveConfigManager.SaveConfig(config);

                    // Don't call IConfigurable.Save here - it might trigger unwanted behavior
                    // The ConfigurationManager.SaveConfig already saved the file
                    Console.WriteLine("[FFT Color Mod] Config saved via ConfigurationManager");

                    // Notify Mod instance if it exists to update sprite manager
                    // This ensures the mod is aware of configuration changes made from Reloaded-II menu
                    Console.WriteLine("[FFT Color Mod] Configuration save process completed");
                }
                else
                {
                    Console.WriteLine($"[FFT Color Mod] User cancelled - DialogResult: {result}");
                }

                Console.WriteLine("[FFT Color Mod] Configuration window closed");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FFT Color Mod] Error showing configuration window: {ex}");
                Console.WriteLine($"[FFT Color Mod] Stack trace: {ex.StackTrace}");
                return false;
            }
        }
    }
}