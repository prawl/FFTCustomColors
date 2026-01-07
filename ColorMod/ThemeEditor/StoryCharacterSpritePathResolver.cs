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
    }
}
