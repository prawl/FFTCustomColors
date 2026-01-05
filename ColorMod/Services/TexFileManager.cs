using System.Collections.Generic;
using System.IO;

namespace FFTColorCustomizer.Services
{
    public class TexFileManager
    {
        public List<string> GetTexFilesForCharacter(string characterName)
        {
            if (characterName == "RamzaChapter1")
            {
                return new List<string>
                {
                    "tex_830.bin",
                    "tex_831.bin"
                };
            }

            if (characterName == "RamzaChapter23")
            {
                return new List<string>
                {
                    "tex_832.bin",
                    "tex_833.bin"
                };
            }

            if (characterName == "RamzaChapter4")
            {
                return new List<string>
                {
                    "tex_834.bin",
                    "tex_835.bin"
                };
            }

            return new List<string>();
        }

        public string GetTexFilePathForTheme(string characterName, string themeName, string texFileName)
        {
            return $"system/ffto/g2d/themes/{themeName}/{texFileName}";
        }

        public bool UsesTexFiles(string characterName)
        {
            return characterName == "RamzaChapter1" ||
                   characterName == "RamzaChapter23" ||
                   characterName == "RamzaChapter4";
        }

        public void CopyTexFilesForTheme(string characterName, string themeName, string modPath)
        {
            if (!UsesTexFiles(characterName)) return;

            var sourcePath = Path.Combine(modPath, "ColorMod/FFTIVC/data/enhanced/system/ffto/g2d/themes", themeName);
            var destPath = Path.Combine(modPath, "ColorMod/FFTIVC/data/enhanced/system/ffto/g2d");

            Directory.CreateDirectory(destPath);

            // Get the tex files for this character/chapter
            var texFiles = GetTexFilesForCharacter(characterName);

            // Copy each tex file
            foreach (var texFile in texFiles)
            {
                var sourceFile = Path.Combine(sourcePath, texFile);
                if (File.Exists(sourceFile))
                {
                    File.Copy(sourceFile, Path.Combine(destPath, texFile), true);
                }
            }
        }
    }
}
