using System;
using FFTColorMod.Configuration;

namespace FFTColorMod
{
    public class StoryCharacterThemeManager
    {
        private OrlandeauColorScheme _currentOrlandeauTheme = OrlandeauColorScheme.original;
        private AgriasColorScheme _currentAgriasTheme = AgriasColorScheme.original;
        private CloudColorScheme _currentCloudTheme = CloudColorScheme.original;
        private MustadioColorScheme _currentMustadioTheme = MustadioColorScheme.original;
        private ReisColorScheme _currentReisTheme = ReisColorScheme.original;
        private DelitaColorScheme _currentDelitaTheme = DelitaColorScheme.original;
        private AlmaColorScheme _currentAlmaTheme = AlmaColorScheme.original;
        private WiegrafColorScheme _currentWiegrafTheme = WiegrafColorScheme.original;

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

        public AgriasColorScheme GetCurrentAgriasTheme()
        {
            return _currentAgriasTheme;
        }

        public void SetCurrentAgriasTheme(AgriasColorScheme theme)
        {
            _currentAgriasTheme = theme;
        }

        public AgriasColorScheme CycleAgriasTheme()
        {
            var values = Enum.GetValues<AgriasColorScheme>();
            var currentIndex = Array.IndexOf(values, _currentAgriasTheme);
            var nextIndex = (currentIndex + 1) % values.Length;
            _currentAgriasTheme = values[nextIndex];
            return _currentAgriasTheme;
        }

        public CloudColorScheme GetCurrentCloudTheme()
        {
            return _currentCloudTheme;
        }

        public void SetCurrentCloudTheme(CloudColorScheme theme)
        {
            _currentCloudTheme = theme;
        }

        public CloudColorScheme CycleCloudTheme()
        {
            var values = Enum.GetValues<CloudColorScheme>();
            var currentIndex = Array.IndexOf(values, _currentCloudTheme);
            var nextIndex = (currentIndex + 1) % values.Length;
            _currentCloudTheme = values[nextIndex];
            return _currentCloudTheme;
        }

        // Mustadio methods
        public MustadioColorScheme GetCurrentMustadioTheme() => _currentMustadioTheme;
        public void SetCurrentMustadioTheme(MustadioColorScheme theme) => _currentMustadioTheme = theme;
        public MustadioColorScheme CycleMustadioTheme()
        {
            var values = Enum.GetValues<MustadioColorScheme>();
            var currentIndex = Array.IndexOf(values, _currentMustadioTheme);
            var nextIndex = (currentIndex + 1) % values.Length;
            _currentMustadioTheme = values[nextIndex];
            return _currentMustadioTheme;
        }

        // Reis methods
        public ReisColorScheme GetCurrentReisTheme() => _currentReisTheme;
        public void SetCurrentReisTheme(ReisColorScheme theme) => _currentReisTheme = theme;
        public ReisColorScheme CycleReisTheme()
        {
            var values = Enum.GetValues<ReisColorScheme>();
            var currentIndex = Array.IndexOf(values, _currentReisTheme);
            var nextIndex = (currentIndex + 1) % values.Length;
            _currentReisTheme = values[nextIndex];
            return _currentReisTheme;
        }


        // Delita methods
        public DelitaColorScheme GetCurrentDelitaTheme() => _currentDelitaTheme;
        public void SetCurrentDelitaTheme(DelitaColorScheme theme) => _currentDelitaTheme = theme;
        public DelitaColorScheme CycleDelitaTheme()
        {
            var values = Enum.GetValues<DelitaColorScheme>();
            var currentIndex = Array.IndexOf(values, _currentDelitaTheme);
            var nextIndex = (currentIndex + 1) % values.Length;
            _currentDelitaTheme = values[nextIndex];
            return _currentDelitaTheme;
        }

        // Alma methods
        public AlmaColorScheme GetCurrentAlmaTheme() => _currentAlmaTheme;
        public void SetCurrentAlmaTheme(AlmaColorScheme theme) => _currentAlmaTheme = theme;
        public AlmaColorScheme CycleAlmaTheme()
        {
            var values = Enum.GetValues<AlmaColorScheme>();
            var currentIndex = Array.IndexOf(values, _currentAlmaTheme);
            var nextIndex = (currentIndex + 1) % values.Length;
            _currentAlmaTheme = values[nextIndex];
            return _currentAlmaTheme;
        }

        // Wiegraf methods
        public WiegrafColorScheme GetCurrentWiegrafTheme() => _currentWiegrafTheme;
        public void SetCurrentWiegrafTheme(WiegrafColorScheme theme) => _currentWiegrafTheme = theme;
        public WiegrafColorScheme CycleWiegrafTheme()
        {
            var values = Enum.GetValues<WiegrafColorScheme>();
            var currentIndex = Array.IndexOf(values, _currentWiegrafTheme);
            var nextIndex = (currentIndex + 1) % values.Length;
            _currentWiegrafTheme = values[nextIndex];
            return _currentWiegrafTheme;
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