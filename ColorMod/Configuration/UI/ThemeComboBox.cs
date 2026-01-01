using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace FFTColorCustomizer.Configuration.UI
{
    /// <summary>
    /// A ComboBox wrapper that displays formatted theme names while maintaining internal theme values
    /// </summary>
    public class ThemeComboBox : ComboBox
    {
        private List<ThemeItem> _themeItems = new List<ThemeItem>();
        private List<string> _builtInThemes = new List<string>();
        private bool _isUpdating = false;

        /// <summary>
        /// Internal representation of a theme with display name and value
        /// </summary>
        private class ThemeItem
        {
            public string Value { get; set; }
            public string DisplayName { get; set; }
            public bool IsSeparator { get; set; }

            public ThemeItem(string value)
            {
                Value = value;
                DisplayName = ThemeNameFormatter.FormatThemeName(value);
                IsSeparator = false;
            }

            public ThemeItem(string value, string displayName, bool isSeparator)
            {
                Value = value;
                DisplayName = displayName;
                IsSeparator = isSeparator;
            }

            public override string ToString()
            {
                return DisplayName;
            }

            public override bool Equals(object obj)
            {
                if (obj is ThemeItem other)
                    return Value.Equals(other.Value, StringComparison.OrdinalIgnoreCase);
                if (obj is string str)
                    return Value.Equals(str, StringComparison.OrdinalIgnoreCase);
                return false;
            }

            public override int GetHashCode()
            {
                return Value?.GetHashCode() ?? 0;
            }
        }

        /// <summary>
        /// Sets the available themes for the ComboBox
        /// </summary>
        public void SetThemes(IEnumerable<string> themes)
        {
            _isUpdating = true;
            try
            {
                _themeItems = themes.Select(t => new ThemeItem(t)).ToList();
                Items.Clear();
                Items.AddRange(_themeItems.ToArray());

                // Debug logging
                Utilities.ModLogger.LogDebug($"[ThemeComboBox] SetThemes called with {_themeItems.Count} themes");
                if (_themeItems.Count <= 5)
                {
                    Utilities.ModLogger.LogDebug($"[ThemeComboBox] Themes: {string.Join(", ", _themeItems.Select(t => t.Value))}");
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        /// <summary>
        /// Sets the available themes including user-created themes with a separator
        /// </summary>
        public void SetThemesWithUserThemes(IEnumerable<string> builtInThemes, IEnumerable<string> userThemes)
        {
            _isUpdating = true;
            try
            {
                _builtInThemes = builtInThemes.ToList();
                _themeItems = _builtInThemes.Select(t => new ThemeItem(t)).ToList();

                var userThemeList = userThemes.ToList();
                if (userThemeList.Count > 0)
                {
                    // Add separator
                    _themeItems.Add(new ThemeItem("__separator__", "── My Themes ──", isSeparator: true));

                    // Add user themes
                    _themeItems.AddRange(userThemeList.Select(t => new ThemeItem(t)));
                }

                Items.Clear();
                Items.AddRange(_themeItems.ToArray());
            }
            finally
            {
                _isUpdating = false;
            }
        }

        /// <summary>
        /// Refreshes the user themes list while preserving the current selection
        /// </summary>
        public void RefreshUserThemes(IEnumerable<string> userThemes)
        {
            var currentSelection = SelectedThemeValue;

            _isUpdating = true;
            try
            {
                // Rebuild from built-in themes
                _themeItems = _builtInThemes.Select(t => new ThemeItem(t)).ToList();

                var userThemeList = userThemes.ToList();
                if (userThemeList.Count > 0)
                {
                    // Add separator
                    _themeItems.Add(new ThemeItem("__separator__", "── My Themes ──", isSeparator: true));

                    // Add user themes
                    _themeItems.AddRange(userThemeList.Select(t => new ThemeItem(t)));
                }

                Items.Clear();
                Items.AddRange(_themeItems.ToArray());

                // Restore selection
                if (!string.IsNullOrEmpty(currentSelection))
                {
                    SelectedThemeValue = currentSelection;
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        /// <summary>
        /// Gets whether the currently selected item is a separator
        /// </summary>
        public bool IsSelectedItemSeparator
        {
            get
            {
                var selectedItem = SelectedItem as ThemeItem;
                return selectedItem?.IsSeparator ?? false;
            }
        }

        /// <summary>
        /// Gets or sets the selected theme value (internal format)
        /// </summary>
        public string SelectedThemeValue
        {
            get
            {
                var selectedItem = SelectedItem as ThemeItem;
                return selectedItem?.Value;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    SelectedIndex = -1;
                    return;
                }

                // Find the item with matching value
                for (int i = 0; i < Items.Count; i++)
                {
                    var item = Items[i] as ThemeItem;
                    if (item != null && item.Value.Equals(value, StringComparison.OrdinalIgnoreCase))
                    {
                        SelectedIndex = i;
                        return;
                    }
                }

                // If not found, add it (for backward compatibility)
                var newItem = new ThemeItem(value);
                Items.Add(newItem);
                SelectedItem = newItem;
            }
        }

        /// <summary>
        /// Event raised when the selected theme value changes
        /// </summary>
        public event EventHandler<string> SelectedThemeChanged;

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);

            if (!_isUpdating)
            {
                SelectedThemeChanged?.Invoke(this, SelectedThemeValue);
            }
        }
    }
}