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
                    .Where(d => !Path.GetFileName(d).StartsWith("sprites_orlandeau_")) // Exclude Orlandeau themes
                    .Where(d => !Path.GetFileName(d).StartsWith("sprites_agrias_"))    // Exclude Agrias themes
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
                _schemes = new List<string> { "original", "corpse_brigade", "lucavi", "northern_sky", "southern_sky" };
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
            string nextScheme;
            if (currentIndex == -1 || currentIndex == _schemes.Count - 1)
            {
                nextScheme = _schemes[0];
            }
            else
            {
                nextScheme = _schemes[currentIndex + 1];
            }

            _currentScheme = nextScheme; // Update the current scheme!
            return nextScheme;
        }

        public string GetPreviousScheme()
        {
            // TLDR: Return previous color scheme in cycle
            if (_schemes.Count == 0)
            {
                return _currentScheme; // No schemes available, return current
            }

            int currentIndex = _schemes.IndexOf(_currentScheme);
            string previousScheme;
            if (currentIndex == -1 || currentIndex == 0)
            {
                previousScheme = _schemes[_schemes.Count - 1];
            }
            else
            {
                previousScheme = _schemes[currentIndex - 1];
            }

            _currentScheme = previousScheme; // Update the current scheme!
            return previousScheme;
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