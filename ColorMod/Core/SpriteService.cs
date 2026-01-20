using System;
using System.IO;
using System.Linq;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Interfaces;

namespace FFTColorCustomizer.Core
{
    /// <summary>
    /// Service for managing sprite operations
    /// </summary>
    public class SpriteService : ISpriteService
    {
        private readonly IPathResolver _pathResolver;
        private readonly ILogger _logger;

        public SpriteService(IPathResolver pathResolver, ILogger logger)
        {
            _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public void CopySprites(string theme, string character)
        {
            if (string.IsNullOrWhiteSpace(theme))
                throw new ArgumentException("Theme cannot be empty", nameof(theme));

            if (string.IsNullOrWhiteSpace(character))
                throw new ArgumentException("Character cannot be empty", nameof(character));

            try
            {
                var spriteFileName = GetSpriteNameForCharacter(character);
                if (string.IsNullOrEmpty(spriteFileName))
                {
                    _logger.LogWarning($"Could not determine sprite file name for {character}");
                    return;
                }

                var spritePath = _pathResolver.GetSpritePath(character, theme, spriteFileName + ".bin");
                if (!string.IsNullOrEmpty(spritePath) && File.Exists(spritePath))
                {
                    // Copy sprite to appropriate location
                    var destinationPath = GetDestinationPath(character);
                    if (!string.IsNullOrEmpty(destinationPath))
                    {
                        var destDir = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }

                        File.Copy(spritePath, destinationPath, true);
                        _logger.Log($"Copied sprite for {character} with theme {theme}");
                    }
                }
                else
                {
                    _logger.LogWarning($"Sprite file not found for {character} with theme {theme}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to copy sprites for {character}: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public void ClearSprites()
        {
            try
            {
                var dataPath = _pathResolver.GetDataPath("sprites");
                if (Directory.Exists(dataPath))
                {
                    var files = Directory.GetFiles(dataPath, "*.bin", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Failed to delete sprite file {file}: {ex.Message}");
                        }
                    }
                    _logger.Log("Cleared all sprite files");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to clear sprites: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public void ApplySpriteConfiguration(Config config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            try
            {
                // Apply sprites based on configuration (skip indexers)
                var properties = typeof(Config).GetProperties();
                foreach (var property in properties)
                {
                    // Skip indexers (properties with parameters)
                    if (property.GetIndexParameters().Length > 0)
                        continue;

                    if (property.PropertyType == typeof(string))
                    {
                        var themeName = property.GetValue(config) as string;
                        if (!string.IsNullOrWhiteSpace(themeName))
                        {
                            var characterName = property.Name.Replace("_", "");
                            CopySprites(themeName, characterName);
                        }
                    }
                }
                _logger.Log("Applied sprite configuration");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to apply sprite configuration: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public void LoadDynamicSprites(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be empty", nameof(path));

            try
            {
                if (!Directory.Exists(path))
                {
                    _logger.LogWarning($"Dynamic sprite path does not exist: {path}");
                    return;
                }

                var spriteFiles = Directory.GetFiles(path, "*.bin", SearchOption.AllDirectories);
                _logger.Log($"Found {spriteFiles.Length} dynamic sprite files to load");

                foreach (var file in spriteFiles)
                {
                    try
                    {
                        var fileName = Path.GetFileNameWithoutExtension(file);
                        var destPath = _pathResolver.GetDataPath($"sprites/{fileName}.bin");
                        var destDir = Path.GetDirectoryName(destPath);

                        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }

                        File.Copy(file, destPath, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to load dynamic sprite {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to load dynamic sprites: {ex.Message}");
            }
        }

        private string GetDestinationPath(string character)
        {
            // Map character to appropriate destination path
            var spriteName = GetSpriteNameForCharacter(character);
            if (!string.IsNullOrEmpty(spriteName))
            {
                return _pathResolver.GetDataPath($"sprites/{spriteName}.bin");
            }
            return null;
        }

        private string GetSpriteNameForCharacter(string character)
        {
            // Map character names to sprite file names
            // This is a simplified mapping - in production this would be more comprehensive
            var normalizedName = character.ToLowerInvariant().Replace("_male", "_m").Replace("_female", "_f");

            // Handle special cases
            return normalizedName switch
            {
                "knight_m" => "battle_knight_m_spr",
                "knight_f" => "battle_knight_f_spr",
                "squire_m" => "battle_squire_m_spr",
                "squire_f" => "battle_squire_f_spr",
                "archer_m" => "battle_archer_m_spr",
                "archer_f" => "battle_archer_f_spr",
                "agrias" => "battle_aguri_spr",
                "orlandeau" => "battle_oru_spr",
                "cloud" => "battle_cloud_spr",
                _ => $"battle_{normalizedName}_spr"
            };
        }
    }
}
