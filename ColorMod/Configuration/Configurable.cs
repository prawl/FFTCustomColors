using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Reloaded.Mod.Interfaces;
using Newtonsoft.Json;

namespace FFTColorMod.Configuration
{
    public class Configurable<TParentType> : IUpdatableConfigurable where TParentType : Configurable<TParentType>, new()
    {
        // Default Serialization Options
        public static JsonSerializerOptions SerializerOptions { get; } = new JsonSerializerOptions()
        {
            Converters = { new ReflectionBasedSystemTextJsonConverter(), new JsonStringEnumConverter() },
            WriteIndented = true
        };

        /* Events */

        /// <summary>
        /// Automatically executed when the external configuration file is updated.
        /// Passes a new instance of the configuration as parameter.
        /// </summary>
        [Browsable(false)]
        public event Action<IUpdatableConfigurable>? ConfigurationUpdated;

        /* Class Properties */

        /// <summary>
        /// Full path to the configuration file.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        [Browsable(false)]
        public string? FilePath { get; set; }

        /// <summary>
        /// The name of the configuration file.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        [Browsable(false)]
        public string? ConfigName { get; set; }

        /// <summary>
        /// Receives events on whenever the file is actively changed or updated.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        [Browsable(false)]
        private FileSystemWatcher? ConfigWatcher { get; set; }

        /* Construction */

        public Configurable() { }

        /// <summary>
        /// Creates configuration from a file.
        /// </summary>
        public static TParentType FromFile(string filePath, string configName)
        {
            var result = File.Exists(filePath)
                ? LoadFromFile(filePath)
                : new TParentType();

            result.FilePath = filePath;
            result.ConfigName = configName;

            // Only save if the file doesn't exist (to create default config)
            if (!File.Exists(filePath))
            {
                result.Save();
            }

            result.EnableFileWatcher();

            return result;
        }

        private static TParentType LoadFromFile(string filePath)
        {
            try
            {
                var jsonText = File.ReadAllText(filePath);
                var config = System.Text.Json.JsonSerializer.Deserialize<TParentType>(jsonText, SerializerOptions);
                return config ?? new TParentType();
            }
            catch
            {
                return new TParentType();
            }
        }

        /// <summary>
        /// Saves the configuration to file.
        /// </summary>
        public void Save()
        {
            if (string.IsNullOrEmpty(FilePath))
                return;

            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            // Create a copy without FilePath and ConfigName for serialization
            var copy = (TParentType)Activator.CreateInstance(GetType())!;
            var properties = GetType().GetProperties();
            foreach (var prop in properties)
            {
                if (prop.Name != "FilePath" && prop.Name != "ConfigName" && prop.CanWrite && prop.CanRead)
                {
                    prop.SetValue(copy, prop.GetValue(this));
                }
            }

            var json = System.Text.Json.JsonSerializer.Serialize(copy, GetType(), SerializerOptions);
            File.WriteAllText(FilePath, json);
        }

        /// <summary>
        /// Action property for IConfigurable interface compatibility.
        /// </summary>
        Action IConfigurable.Save => Save;

        private void EnableFileWatcher()
        {
            if (string.IsNullOrEmpty(FilePath))
                return;

            ConfigWatcher?.Dispose();
            ConfigWatcher = new FileSystemWatcher(Path.GetDirectoryName(FilePath)!, Path.GetFileName(FilePath));
            ConfigWatcher.Changed += (sender, e) => OnFileChanged();
            ConfigWatcher.EnableRaisingEvents = true;
        }

        private void OnFileChanged()
        {
            try
            {
                var newConfig = LoadFromFile(FilePath!);
                newConfig.FilePath = FilePath;
                newConfig.ConfigName = ConfigName;
                ConfigurationUpdated?.Invoke(newConfig);
            }
            catch { }
        }
    }
}