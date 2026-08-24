using System.Linq;
using FFTColorCustomizer.ThemeEditor;
using Newtonsoft.Json.Linq;

namespace FFTColorCustomizer.Tools.ThemeBaker;

/// <summary>
/// Bakes the CC-31 harvested theme recipes under
/// ColorMod/Data/BuiltInThemes/Harvested/&lt;Character&gt;.json.
///
/// A harvested theme is NOT designed: it promotes one of the game's own unused palette slots
/// into slot 0 verbatim, so the colours are the original artists' work. That makes this a
/// different job from the recipe baker in Program.cs, which generates shades from a chosen base
/// colour, and the two are kept apart on purpose.
///
/// Two consequences worth knowing. First, no section mapping is involved at all, so this works
/// for Agrias (whose mapping lives under SectionMappings/Story) exactly as it does for the NPCs.
/// The sprite list comes from StoryCharacters.json instead. Second, because it is a byte move
/// there is no quantisation and no shade generation, so none of the CC-33 colour-conversion
/// hazards apply.
///
/// HarvestedThemeContractTests re-bakes every recipe here and compares bytes, so drift goes red.
/// </summary>
internal static class HarvestBaker
{
    internal static int BakeAll(string repoRoot, JObject registry, string[] wantedCharacters)
    {
        var recipeDir = Path.Combine(repoRoot, "ColorMod", "Data", "BuiltInThemes", "Harvested");
        if (!Directory.Exists(recipeDir))
            return 0;

        var files = Directory.GetFiles(recipeDir, "*.json").OrderBy(f => f).ToList();
        if (wantedCharacters.Length > 0)
        {
            var wanted = wantedCharacters.Select(a => a.ToLowerInvariant()).ToHashSet();
            files = files
                .Where(f => wanted.Contains(Path.GetFileNameWithoutExtension(f).ToLowerInvariant()))
                .ToList();
        }

        var baked = 0;
        foreach (var file in files)
            baked += BakeRecipeFile(repoRoot, file, registry);
        return baked;
    }

    private static int BakeRecipeFile(string repoRoot, string recipeFile, JObject registry)
    {
        var recipe = JObject.Parse(File.ReadAllText(recipeFile));
        var character = (string?)recipe["character"]
            ?? throw new InvalidOperationException($"{recipeFile} has no \"character\"");

        var unitPath = Path.Combine(
            repoRoot, "ColorMod", "FFTIVC", "data", "enhanced", "fftpack", "unit");

        var stems = SpriteStemsFor(registry, character);
        var themes = (JArray?)recipe["themes"]
            ?? throw new InvalidOperationException($"{recipeFile} has no \"themes\" array");

        var count = 0;
        foreach (var theme in themes.OfType<JObject>())
        {
            var themeName = (string?)theme["name"]
                ?? throw new InvalidOperationException($"{character}: a theme has no \"name\"");
            var sourceSlot = (int?)theme["sourceSlot"]
                ?? throw new InvalidOperationException(
                    $"{character}/{themeName}: harvested themes need a \"sourceSlot\"");

            BakeTheme(unitPath, character, themeName, sourceSlot, stems);
            Program.RegisterTheme(registry, character, themeName);
            count++;
        }

        return count;
    }

    private static void BakeTheme(
        string unitPath, string character, string themeName, int sourceSlot, List<string> stems)
    {
        var outDir = Path.Combine(unitPath, $"sprites_{character.ToLowerInvariant()}_{themeName}");
        Directory.CreateDirectory(outDir);

        foreach (var stem in stems)
        {
            var binName = $"battle_{stem}_spr.bin";
            var originalBin = StoryCharacterSpritePathResolver.ResolveSpritePath(
                unitPath, character, binName);

            if (!File.Exists(originalBin))
                throw new FileNotFoundException(
                    $"Original sprite missing for {character}: {originalBin}");

            var data = File.ReadAllBytes(originalBin);

            // A slot that already matches slot 0 would ship a dropdown entry that changes nothing
            // a player can see. The census behind CC-31 found three such candidates, so this is a
            // real failure mode and not a theoretical one.
            if (!PaletteHarvester.PromoteSlotToZero(data, sourceSlot))
                throw new InvalidOperationException(
                    $"{character}/{themeName}: slot {sourceSlot} of {binName} is identical to slot 0, "
                    + "so this harvest would be a theme that changes nothing. Pick another slot.");

            File.WriteAllBytes(Path.Combine(outDir, binName), data);
        }

        Console.WriteLine($"  {character}/{themeName} <- slot {sourceSlot} -> "
                          + $"{Path.GetFileName(outDir)}/{string.Join(", ", stems)}");
    }

    private static List<string> SpriteStemsFor(JObject registry, string character)
    {
        var definition = ((JArray?)registry["characters"])?.OfType<JObject>()
            .FirstOrDefault(c => (string?)c["name"] == character)
            ?? throw new InvalidOperationException(
                $"{character} is not in StoryCharacters.json; add the registry entry first");

        var names = (JArray?)definition["spriteNames"]
            ?? throw new InvalidOperationException($"{character} has no spriteNames array");

        var stems = names.Select(n => (string?)n).Where(n => !string.IsNullOrEmpty(n)).ToList()!;
        if (stems.Count == 0)
            throw new InvalidOperationException($"{character} has an empty spriteNames array");

        return stems!;
    }
}
