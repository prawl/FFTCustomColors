using System.IO;

namespace FFTColorCustomizer.ThemeEditor
{
    public static class StoryCharacterSpritePathResolver
    {
        public static string ResolveSpritePath(string baseDirectory, string characterName, string spriteFileName)
        {
            var characterSpecificFolder = $"sprites_{characterName.ToLower()}_original";
            var characterSpecificPath = Path.Combine(baseDirectory, characterSpecificFolder);

            string spriteFolderName;
            if (Directory.Exists(characterSpecificPath))
            {
                spriteFolderName = characterSpecificFolder;
            }
            else
            {
                spriteFolderName = "sprites_original";
            }

            return Path.Combine(baseDirectory, spriteFolderName, spriteFileName);
        }

        /// <summary>
        /// Resolves the sprite path for WotL jobs (Dark Knight, Onion Knight).
        /// WotL sprites are stored in unit_psp/sprites_original instead of unit/sprites_original.
        /// </summary>
        public static string ResolveWotLSpritePath(string unitPspDirectory, string spriteFileName)
        {
            return Path.Combine(unitPspDirectory, "sprites_original", spriteFileName);
        }
    }
}
