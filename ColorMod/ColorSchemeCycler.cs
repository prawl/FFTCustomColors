using System.Collections.Generic;

namespace FFTColorMod
{
    public class ColorSchemeCycler
    {
        private string _currentScheme = "original";

        private readonly List<string> _schemes = new List<string>
        {
            "original",
            "corpse_brigade",
            "lucavi",
            "northern_sky",
            "smoke",
            "southern_sky"
        };

        public void SetCurrentScheme(string scheme)
        {
            _currentScheme = scheme;
        }

        public string GetNextScheme()
        {
            // TLDR: Return next color scheme in cycle
            int currentIndex = _schemes.IndexOf(_currentScheme);
            if (currentIndex == -1 || currentIndex == _schemes.Count - 1)
            {
                return _schemes[0];
            }
            return _schemes[currentIndex + 1];
        }

        public List<string> GetAvailableSchemes()
        {
            // TLDR: Return list of available color schemes
            return new List<string>(_schemes);
        }

        public string GetCurrentScheme()
        {
            // TLDR: Return the current color scheme
            return _currentScheme;
        }
    }
}