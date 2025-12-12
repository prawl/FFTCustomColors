using System;
using FFTColorMod.Configuration;

namespace FFTColorMod
{
    public class StoryCharacterThemeManager
    {
        private OrlandeauColorScheme _currentOrlandeauTheme = OrlandeauColorScheme.thunder_god;

        public OrlandeauColorScheme GetCurrentOrlandeauTheme()
        {
            return _currentOrlandeauTheme;
        }

        public void SetCurrentOrlandeauTheme(OrlandeauColorScheme theme)
        {
            _currentOrlandeauTheme = theme;
        }

        public OrlandeauColorScheme CycleOrlandeauTheme()
        {
            var values = Enum.GetValues<OrlandeauColorScheme>();
            var currentIndex = Array.IndexOf(values, _currentOrlandeauTheme);
            var nextIndex = (currentIndex + 1) % values.Length;
            _currentOrlandeauTheme = values[nextIndex];
            return _currentOrlandeauTheme;
        }
    }

    public class F2ThemeHandler
    {
        private readonly StoryCharacterThemeManager _storyManager = new();

        public string HandleF2Press(string spriteName)
        {
            // Check if this is Orlandeau
            if (spriteName.Contains("oru"))
            {
                var theme = _storyManager.CycleOrlandeauTheme();
                return $"sprites_orlandeau_{theme.ToString().ToLower()}";
            }

            // For generic sprites, return standard theme cycling (not implemented here)
            return "sprites_original";
        }
    }
}