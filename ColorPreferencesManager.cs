using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FFTColorMod;

public class ColorPreferencesManager
{
    private readonly string _configPath;

    public ColorPreferencesManager(string configPath)
    {
        _configPath = configPath;
    }

    public void SavePreferences(ColorScheme colorScheme)
    {
        // TLDR: Write color scheme name to file
        File.WriteAllText(_configPath, colorScheme.ToString());
    }

    public ColorScheme LoadPreferences()
    {
        // TLDR: Return default if file doesn't exist, otherwise parse saved value
        if (!File.Exists(_configPath))
        {
            return ColorScheme.Original;
        }

        var content = File.ReadAllText(_configPath);

        // TLDR: Handle empty or whitespace content
        if (string.IsNullOrWhiteSpace(content))
        {
            return ColorScheme.Original;
        }

        // TLDR: Try to parse, return default if invalid
        if (Enum.TryParse<ColorScheme>(content.Trim(), out var result))
        {
            return result;
        }

        return ColorScheme.Original;
    }

    public void SaveCharacterPreference(string characterName, ColorScheme colorScheme)
    {
        // TLDR: Save character-specific color in JSON dictionary
        var preferences = LoadAllCharacterPreferences();
        preferences[characterName] = colorScheme.ToString();

        var json = JsonSerializer.Serialize(preferences);
        File.WriteAllText(_configPath, json);
    }

    public ColorScheme LoadCharacterPreference(string characterName)
    {
        // TLDR: Load character-specific color from JSON dictionary
        var preferences = LoadAllCharacterPreferences();

        if (preferences.TryGetValue(characterName, out var colorString))
        {
            return Enum.Parse<ColorScheme>(colorString);
        }

        return ColorScheme.Original;
    }

    private Dictionary<string, string> LoadAllCharacterPreferences()
    {
        // TLDR: Load all character preferences from JSON file
        if (!File.Exists(_configPath))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        catch
        {
            // If file isn't valid JSON, return empty dictionary
            return new Dictionary<string, string>();
        }
    }
}