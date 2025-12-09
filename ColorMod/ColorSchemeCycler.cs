using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FFTColorMod
{
    public class ColorSchemeCycler
    {
        private string _currentScheme = "original";
        private readonly List<string> _schemes;

        public ColorSchemeCycler() : this(null)
        {
        }

        public ColorSchemeCycler(string spritesPath)
        {
            if (!string.IsNullOrEmpty(spritesPath) && Directory.Exists(spritesPath))
            {
                var detectedSchemes = Directory.GetDirectories(spritesPath)
                    .Where(d => Path.GetFileName(d).StartsWith("sprites_"))
                    .Select(d => Path.GetFileName(d).Replace("sprites_", ""))
                    .ToList();

                // Sort with "original" first, then everything else alphabetically
                _schemes = detectedSchemes
                    .OrderBy(s => s == "original" ? 0 : 1)
                    .ThenBy(s => s)
                    .ToList();
            }
            else
            {
                // Minimal fallback for testing when no directory exists
                _schemes = new List<string> { "original", "corpse_brigade", "lucavi", "northern_sky", "smoke", "southern_sky" };
            }
        }

        public void SetCurrentScheme(string scheme)
        {
            _currentScheme = scheme;
        }

        public string GetNextScheme()
        {
            // TLDR: Return next color scheme in cycle
            if (_schemes.Count == 0)
            {
                return _currentScheme; // No schemes available, return current
            }

            int currentIndex = _schemes.IndexOf(_currentScheme);
            if (currentIndex == -1 || currentIndex == _schemes.Count - 1)
            {
                return _schemes[0];
            }
            return _schemes[currentIndex + 1];
        }

        public string GetPreviousScheme()
        {
            // TLDR: Return previous color scheme in cycle
            if (_schemes.Count == 0)
            {
                return _currentScheme; // No schemes available, return current
            }

            int currentIndex = _schemes.IndexOf(_currentScheme);
            if (currentIndex == -1 || currentIndex == 0)
            {
                return _schemes[_schemes.Count - 1];
            }
            return _schemes[currentIndex - 1];
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