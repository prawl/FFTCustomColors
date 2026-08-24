using System.Drawing;
using System.Linq;
using FFTColorCustomizer.ThemeEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FFTColorCustomizer.Tools.ThemeBaker;

/// <summary>
/// Bakes the hand-authored built-in theme recipes under
/// ColorMod/Data/BuiltInThemes/NPC/&lt;Character&gt;.json into the sprite bins the mod ships,
/// and registers each theme in ColorMod/Data/StoryCharacters.json.
///
/// The recipe is the ONLY hand-edited artifact. The bins are generated; never edit one by hand.
/// Baking reuses the production theme-editor classes (SectionMappingLoader, PaletteModifier,
/// RelativeShadeGenerator), so a bin baked here is byte-identical to one saved from the editor.
/// NpcBuiltInThemeContractTests re-bakes every recipe and compares, so drift goes red.
///
/// Usage:  dotnet run --project tools/ThemeBaker [Character ...]
///         (no arguments bakes every recipe on disk)
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var repoRoot = FindRepoRoot();
            var recipeDir = Path.Combine(repoRoot, "ColorMod", "Data", "BuiltInThemes", "NPC");

            var files = Directory.Exists(recipeDir)
                ? Directory.GetFiles(recipeDir, "*.json").OrderBy(f => f).ToList()
                : new List<string>();
            if (args.Length > 0)
            {
                var wanted = args.Select(a => a.ToLowerInvariant()).ToHashSet();
                files = files
                    .Where(f => wanted.Contains(Path.GetFileNameWithoutExtension(f).ToLowerInvariant()))
                    .ToList();

            }

            var registry = LoadRegistry(repoRoot, out var registryPath);
            var baked = 0;

            foreach (var file in files)
                baked += BakeRecipeFile(repoRoot, file, registry);

            // CC-31 harvested themes: the game's own unused palette slots, promoted verbatim.
            // A different job from the designed recipes above, so it lives in its own file.
            baked += HarvestBaker.BakeAll(repoRoot, registry, args);

            SaveRegistry(registryPath, registry);

            Console.WriteLine($"Baked {baked} theme(s) from {files.Count} recipe file(s).");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ThemeBaker failed: {ex.Message}");
            return 1;
        }
    }

    private static int BakeRecipeFile(string repoRoot, string recipeFile, JObject registry)
    {
        var recipe = JObject.Parse(File.ReadAllText(recipeFile));
        var character = (string?)recipe["character"]
            ?? throw new InvalidOperationException($"{recipeFile} has no \"character\"");

        var mappingPath = Path.Combine(
            repoRoot, "ColorMod", "Data", "SectionMappings", "NPC", $"{character}.json");
        if (!File.Exists(mappingPath))
            throw new FileNotFoundException($"No section mapping for {character}: {mappingPath}");

        var mapping = SectionMappingLoader.LoadFromFile(mappingPath);
        var unitPath = Path.Combine(
            repoRoot, "ColorMod", "FFTIVC", "data", "enhanced", "fftpack", "unit");

        // EVERY sprite the character owns, not just the first. Alma draws from three
        // bins (aruma, gyumu, h82) and ConfigBasedSpriteManager applies all of them,
        // so baking only mapping.Sprite would ship a theme that silently reverts to
        // vanilla in two of her three appearances.
        foreach (var sprite in mapping.Sprites)
        {
            var bin = StoryCharacterSpritePathResolver.ResolveSpritePath(unitPath, character, sprite);
            if (!File.Exists(bin))
                throw new FileNotFoundException($"Original sprite missing for {character}: {bin}");
        }

        var themes = (JArray?)recipe["themes"]
            ?? throw new InvalidOperationException($"{recipeFile} has no \"themes\" array");

        var count = 0;
        foreach (var theme in themes.OfType<JObject>())
        {
            var themeName = (string?)theme["name"]
                ?? throw new InvalidOperationException($"{character}: a theme has no \"name\"");
            var sections = (JObject?)theme["sections"]
                ?? throw new InvalidOperationException($"{character}/{themeName}: no \"sections\"");

            BakeTheme(unitPath, character, themeName, mapping, sections);
            RegisterTheme(registry, character, themeName);
            count++;
        }

        return count;
    }

    private static void BakeTheme(
        string unitPath, string character, string themeName,
        SectionMapping mapping, JObject sections)
    {
        var outDir = Path.Combine(unitPath, $"sprites_{character.ToLowerInvariant()}_{themeName}");
        Directory.CreateDirectory(outDir);

        foreach (var sprite in mapping.Sprites)
        {
            var originalBin = StoryCharacterSpritePathResolver.ResolveSpritePath(
                unitPath, character, sprite);

            var modifier = new PaletteModifier();
            modifier.LoadTemplate(originalBin, character);

            foreach (var entry in sections.Properties())
            {
                var section = mapping.Sections.SingleOrDefault(s => s.Name == entry.Name)
                    ?? throw new InvalidOperationException(
                        $"{character}/{themeName}: section \"{entry.Name}\" is not in the section mapping. " +
                        $"Known sections: {string.Join(", ", mapping.Sections.Select(s => s.Name))}");

                var hex = (string?)entry.Value
                    ?? throw new InvalidOperationException(
                        $"{character}/{themeName}: section \"{entry.Name}\" has no colour");

                modifier.ApplySectionColor(section, ColorTranslator.FromHtml(hex));
            }

            modifier.SaveToFile(Path.Combine(outDir, sprite));
        }

        Console.WriteLine($"  {character}/{themeName} -> {Path.GetFileName(outDir)}/"
                          + string.Join(", ", mapping.Sprites));
    }

    /// <summary>
    /// Appends the theme to the character's availableThemes if it is not already listed.
    /// "original" always stays first; new themes append in bake order.
    /// </summary>
    internal static void RegisterTheme(JObject registry, string character, string themeName)
    {
        var characters = (JArray?)registry["characters"]
            ?? throw new InvalidOperationException("StoryCharacters.json has no \"characters\" array");

        var definition = characters.OfType<JObject>()
            .FirstOrDefault(c => (string?)c["name"] == character)
            ?? throw new InvalidOperationException(
                $"{character} is not in StoryCharacters.json; add the registry entry first");

        var available = (JArray?)definition["availableThemes"]
            ?? throw new InvalidOperationException($"{character} has no availableThemes array");

        if (available.Any(t => (string?)t == themeName))
            return;

        available.Add(themeName);
    }

    private static JObject LoadRegistry(string repoRoot, out string registryPath)
    {
        registryPath = Path.Combine(repoRoot, "ColorMod", "Data", "StoryCharacters.json");
        return JObject.Parse(File.ReadAllText(registryPath));
    }

    /// <summary>
    /// Writes the registry back in the file's existing shape: two-space indent, CRLF line
    /// endings, and no trailing newline. Matching this keeps the diff to the lines that
    /// actually changed instead of reformatting all 456 of them.
    /// </summary>
    private static void SaveRegistry(string registryPath, JObject registry)
    {
        var json = JsonConvert.SerializeObject(registry, Formatting.Indented);
        json = json.Replace("\r\n", "\n").Replace("\n", "\r\n");
        File.WriteAllText(registryPath, json);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ColorMod", "Data", "StoryCharacters.json")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "Could not find the repo root (no ColorMod/Data/StoryCharacters.json above the working directory)");
    }
}
