using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FFTColorCustomizer.Utilities;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.ThemeEditor;

namespace FFTColorCustomizer.Configuration.UI
{
    /// <summary>
    /// Builds character rows for the configuration form
    /// </summary>
    public class CharacterRowBuilder
    {
        private readonly TableLayoutPanel _mainPanel;
        private readonly PreviewImageManager _previewManager;
        private readonly Func<bool> _isInitializing;
        private readonly List<Control> _genericCharacterControls;
        private readonly BinSpriteExtractor _binExtractor;
        private readonly List<Control> _storyCharacterControls;
        private readonly JobClassDefinitionService _jobClassService;
        private readonly List<PreviewCarousel> _allCarousels = new List<PreviewCarousel>();
        private readonly UserThemeService _userThemeService;

        public IReadOnlyList<PreviewCarousel> AllCarousels => _allCarousels;

        public CharacterRowBuilder(
            TableLayoutPanel mainPanel,
            PreviewImageManager previewManager,
            Func<bool> isInitializing,
            List<Control> genericCharacterControls,
            List<Control> storyCharacterControls)
        {
            _mainPanel = mainPanel;
            _previewManager = previewManager;
            _isInitializing = isInitializing;
            _genericCharacterControls = genericCharacterControls;
            _storyCharacterControls = storyCharacterControls;
            _jobClassService = JobClassServiceSingleton.Instance;
            _userThemeService = UserThemeServiceSingleton.Instance;
            _binExtractor = new BinSpriteExtractor();
        }

        private string FindActualUnitPath(string modPath)
        {
            // First try the direct path
            var directPath = Path.Combine(modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            if (Directory.Exists(directPath))
            {
                return directPath;
            }

            // If not found, look for versioned directories
            var parentDir = Path.GetDirectoryName(modPath);
            if (!string.IsNullOrEmpty(parentDir))
            {
                try
                {
                    var versionedDirs = Directory.GetDirectories(parentDir, "FFTColorCustomizer_v*")
                        .OrderByDescending(dir =>
                        {
                            var dirName = Path.GetFileName(dir);
                            var versionStr = dirName.Substring(dirName.LastIndexOf('v') + 1);
                            if (int.TryParse(versionStr, out int version))
                                return version;
                            return 0;
                        })
                        .ToArray();

                    foreach (var versionedDir in versionedDirs)
                    {
                        var versionedPath = Path.Combine(versionedDir, "FFTIVC", "data", "enhanced", "fftpack", "unit");
                        if (Directory.Exists(versionedPath))
                        {
                            ModLogger.Log($"Found FFTIVC for previews in versioned directory: {versionedPath}");
                            return versionedPath;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.LogWarning($"Error searching for versioned directories: {ex.Message}");
                }
            }

            // Return the expected path even if it doesn't exist
            return directPath;
        }

        public void AddGenericCharacterRow(int row, string jobName, string currentTheme,
            Action<string> setter, Func<bool> isFullyLoaded, List<Control> controlsList = null)
        {
            // Use provided controls list or default to generic character controls
            var targetControlsList = controlsList ?? _genericCharacterControls;

            ModLogger.Log($"AddJobRow: {jobName} = {currentTheme}");

            // Create label
            var label = ConfigUIComponentFactory.CreateCharacterLabel(jobName);
            _mainPanel.Controls.Add(label, 0, row);
            targetControlsList.Add(label);

            // Create theme combo box with formatted display
            var comboBox = new ThemeComboBox();
            ConfigUIComponentFactory.ApplyThemeComboBoxStyling(comboBox);
            // Get available themes for this specific job (shared + job-specific)
            var builtInThemes = GetAvailableGenericThemesForJob(jobName);

            // Get user themes for this job - convert "Knight (Male)" to "Knight_Male"
            var jobProperty = ConvertJobNameToPropertyFormat(jobName);
            var userThemes = _userThemeService.GetUserThemes(jobProperty);

            comboBox.SetThemesWithUserThemes(builtInThemes, userThemes);
            comboBox.SelectedThemeValue = currentTheme;

            // Create preview carousel with lazy loading
            var carousel = ConfigUIComponentFactory.CreatePreviewPictureBox();

            // Store data for lazy loading
            carousel.Tag = new { JobName = jobName, Theme = currentTheme, Setter = setter };

            // Set up lazy loading callback
            carousel.LoadImagesCallback = (c) => {
                var tag = c.Tag as dynamic;
                if (tag != null)
                {
                    ModLogger.Log($"[LAZY] Loading images for {tag.JobName}");
                    UpdateGenericPreviewImages(c as PreviewCarousel, tag.JobName.ToString(), tag.Theme.ToString());
                }
            };

            // Track carousel for lazy loading
            _allCarousels.Add(carousel);

            // Add controls to panel
            _mainPanel.Controls.Add(comboBox, 1, row);
            _mainPanel.Controls.Add(carousel, 2, row);

            // Track controls
            targetControlsList.Add(comboBox);
            targetControlsList.Add(carousel);

            // Store reference for verification
            comboBox.Tag = new { JobName = jobName, ExpectedValue = currentTheme, Setter = setter };

            // Setup event handler
            comboBox.SelectedThemeChanged += (s, newTheme) =>
            {
                if (_isInitializing != null && _isInitializing()) return;

                // Check if form is fully loaded using the provided function
                if (isFullyLoaded != null && isFullyLoaded() && !string.IsNullOrEmpty(newTheme))
                {
                    ModLogger.Log($"Selection changed: {jobName} = {newTheme}");
                    setter(newTheme);

                    // Update tag for lazy loading
                    carousel.Tag = new { JobName = jobName, Theme = newTheme, Setter = setter };

                    // Only update images if already loaded
                    if (carousel.HasImagesLoaded)
                    {
                        UpdateGenericPreviewImages(carousel, jobName, newTheme);
                    }
                }
            };
        }

        public void AddStoryCharacterRow(int row, StoryCharacterRegistry.StoryCharacterConfig characterConfig)
        {
            // Create label with display name
            var displayName = GetCharacterDisplayName(characterConfig.Name);
            var label = ConfigUIComponentFactory.CreateCharacterLabel(displayName);

            // For Ramza characters, add warning text to the label
            if (characterConfig.Name.StartsWith("RamzaChapter"))
            {
                // Create a panel to hold both the name and warning
                var labelPanel = new Panel
                {
                    AutoSize = true,
                    BackColor = Color.Transparent,
                    Padding = new Padding(0)
                };

                // Adjust the main label
                label.AutoSize = true;
                label.Location = new Point(0, 0);

                // Create warning label
                var warningLabel = new Label
                {
                    Text = "Requires a full game restart",
                    ForeColor = Color.FromArgb(255, 100, 100), // Red color
                    BackColor = Color.Transparent,
                    Font = new Font("Segoe UI", 7, FontStyle.Italic),
                    AutoSize = true,
                    Location = new Point(0, label.Height - 2)
                };

                // Add both to the panel
                labelPanel.Controls.Add(label);
                labelPanel.Controls.Add(warningLabel);
                labelPanel.Height = label.Height + warningLabel.Height - 2;

                _mainPanel.Controls.Add(labelPanel, 0, row);
                _storyCharacterControls.Add(labelPanel);
            }
            else
            {
                _mainPanel.Controls.Add(label, 0, row);
                _storyCharacterControls.Add(label);
            }

            // Create theme combo box with formatted display
            var comboBox = new ThemeComboBox();
            ConfigUIComponentFactory.ApplyThemeComboBoxStyling(comboBox);
            var carousel = ConfigUIComponentFactory.CreatePreviewPictureBox();

            // Set up for lazy loading
            _allCarousels.Add(carousel);

            // Get available themes for this story character
            var availableThemes = characterConfig.AvailableThemes?.Length > 0
                ? characterConfig.AvailableThemes.ToList()
                : new List<string> { "original" }; // fallback if no themes available

            // Get user themes for this story character
            var userThemes = _userThemeService.GetUserThemes(characterConfig.Name);

            ModLogger.LogDebug($"Story character {characterConfig.Name} has {availableThemes.Count} built-in themes: {string.Join(", ", availableThemes)}");
            ModLogger.LogDebug($"Story character {characterConfig.Name} has {userThemes.Count} user themes: {string.Join(", ", userThemes)}");
            comboBox.SetThemesWithUserThemes(availableThemes, userThemes);

            // Set Tag with JobName for RefreshDropdownsForJob to find this comboBox
            // Also include ExpectedValue for VerifyAllSelections compatibility
            var initialValue = characterConfig.GetValue();
            comboBox.Tag = new { JobName = characterConfig.Name, ExpectedValue = initialValue?.ToString() ?? "original" };

            // Setup event handler
            comboBox.SelectedThemeChanged += (s, newTheme) =>
            {
                if (_isInitializing != null && _isInitializing()) return;

                // Also check if form is loaded for story characters
                // We need to get the form's _isFullyLoaded state
                // For now, we'll check if the control's handle is created
                if (comboBox.IsHandleCreated && !string.IsNullOrEmpty(newTheme))
                {
                    ModLogger.Log($"[THEME CHANGE] Story character {characterConfig.Name} changing to theme: {newTheme}");
                    characterConfig.SetValue(newTheme);

                    // Update tag for lazy loading
                    carousel.Tag = new {
                        CharacterName = characterConfig.PreviewName,
                        Theme = newTheme,
                        Config = characterConfig
                    };

                    // Only update images if already loaded
                    if (carousel.HasImagesLoaded)
                    {
                        UpdateStoryCharacterPreview(carousel, characterConfig.PreviewName, newTheme);
                    }
                }
            };

            // Set initial preview data for lazy loading
            var currentValue = characterConfig.GetValue();
            carousel.Tag = new {
                CharacterName = characterConfig.PreviewName,
                Theme = currentValue.ToString(),
                Config = characterConfig
            };

            // Set up lazy loading callback
            carousel.LoadImagesCallback = (c) => {
                var tag = c.Tag as dynamic;
                if (tag != null)
                {
                    ModLogger.Log($"[LAZY] Loading story character images for {tag.CharacterName}");
                    UpdateStoryCharacterPreview(c as PreviewCarousel, tag.CharacterName.ToString(), tag.Theme.ToString());
                }
            };

            // Add to panel
            _mainPanel.Controls.Add(comboBox, 1, row);
            _mainPanel.Controls.Add(carousel, 2, row);

            // Force Handle creation
            var handle = comboBox.Handle;

            // Set the initial selection using the internal value
            comboBox.SelectedThemeValue = currentValue.ToString();

            // Track controls
            _storyCharacterControls.Add(comboBox);
            _storyCharacterControls.Add(carousel);
        }

        private List<string> GetAvailableGenericThemes()
        {
            // Get shared themes from the JobClassDefinitionService
            // This could later be loaded from a JSON file for easier configuration
            var themes = _jobClassService.GetAvailableThemes();
            ModLogger.Log($"GetAvailableGenericThemes returned {themes.Count} themes: {string.Join(", ", themes)}");
            return themes;
        }

        private List<string> GetAvailableGenericThemesForJob(string jobName)
        {
            // Get themes for specific job (shared + job-specific)
            // Need to convert display name to internal name (e.g., "Knight (Male)" -> "Knight_Male")
            var internalName = jobName.Replace(" (Male)", "_Male")
                                     .Replace(" (Female)", "_Female")
                                     .Replace(" ", "");

            var themes = _jobClassService.GetAvailableThemesForJob(internalName);
            ModLogger.Log($"GetAvailableGenericThemesForJob({jobName}) returned {themes.Count} themes: {string.Join(", ", themes)}");
            return themes;
        }

        private void UpdateGenericPreviewImages(PreviewCarousel carousel, string jobName, string theme)
        {
            // If we don't have a valid mod path, don't load embedded resources
            // This ensures tests can verify that without a proper mod installation, no images load
            if (!_previewManager.HasValidModPath())
            {
                carousel.SetImages(new Image[0]);
                return;
            }

            // First try to load from .bin file if it exists (for generic jobs too)
            var binImages = TryLoadGenericFromBinFile(jobName, theme);
            if (binImages != null && binImages.Length > 0)
            {
                carousel.SetImages(binImages);
                ModLogger.LogSuccess($"Loaded {binImages.Length} sprites from .bin file for {jobName} - {theme}");
                return;
            }

            string fileName = jobName.ToLower()
                .Replace(" (male)", "_male")
                .Replace(" (female)", "_female")
                .Replace(" ", "_")
                .Replace("(", "")
                .Replace(")", "");

            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var images = new List<Image>();

            // Try to load all 8 directions from embedded resources
            // FFT sprite order: s, sw, w, nw, n, ne, e, se (starting facing camera, going clockwise)
            string[] directions = { "_s", "_sw", "_w", "_nw", "_n", "_ne", "_e", "_se" };

            foreach (var direction in directions)
            {
                // Resource name format: FFTColorCustomizer.Resources.Previews.{job_folder}.{job}_{theme}_{direction}.png
                string resourceName = $"FFTColorCustomizer.Resources.Previews.{fileName}.{fileName}_{theme.ToLower()}{direction}.png";

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        try
                        {
                            var image = Image.FromStream(stream);
                            images.Add(image);
                            ModLogger.LogDebug($"Loaded direction sprite: {direction} for {fileName}_{theme}");
                        }
                        catch (Exception ex)
                        {
                            ModLogger.LogDebug($"Failed to load direction {direction}: {ex.Message}");
                        }
                    }
                }
            }

            // If we loaded any directional sprites, use them
            if (images.Count > 0)
            {
                carousel.SetImages(images.ToArray());
                ModLogger.LogSuccess($"Loaded {images.Count} directional views for {jobName} - {theme}");
                return;
            }

            // Fall back to original single-image loading
            foreach (var direction in new[] { "" })
            {
                string resourceName = $"FFTColorCustomizer.Resources.Previews.{fileName}_{theme.ToLower()}{direction}.png";
                ModLogger.LogDebug($"Looking for directional resource: {resourceName}");

                // Also log the actual resource name we're looking for
                System.Diagnostics.Debug.WriteLine($"[CAROUSEL] Trying to load: {resourceName}");

                try
                {
                    using (var stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                        {
                            var image = Image.FromStream(stream);
                            images.Add(image);
                            ModLogger.LogDebug($"Loaded directional view: {direction}");
                            System.Diagnostics.Debug.WriteLine($"[CAROUSEL] SUCCESS: Loaded {resourceName}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[CAROUSEL] FAILED: Stream null for {resourceName}");

                            if (direction == "") // Only check alternate names for main image
                            {
                                var allResources = assembly.GetManifestResourceNames();
                                var matchingResource = allResources.FirstOrDefault(r =>
                                    r.EndsWith($"{fileName}_{theme.ToString().ToLower()}.png", StringComparison.OrdinalIgnoreCase));

                                if (!string.IsNullOrEmpty(matchingResource))
                                {
                                    using (var altStream = assembly.GetManifestResourceStream(matchingResource))
                                    {
                                        if (altStream != null)
                                        {
                                            images.Add(Image.FromStream(altStream));
                                            ModLogger.LogDebug($"Loaded main view with alt resource name");
                                            System.Diagnostics.Debug.WriteLine($"[CAROUSEL] SUCCESS via alt: {matchingResource}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.LogDebug($"Could not load direction {direction}: {ex.Message}");
                }
            }

            // Set the images in the carousel
            if (images.Count > 0)
            {
                carousel.SetImages(images.ToArray());
                ModLogger.LogSuccess($"Loaded {images.Count} directional views for {jobName} - {theme}");
            }
            else
            {
                carousel.SetImages(new Image[0]);
                ModLogger.Log($"No preview images found for {jobName} - {theme}");
            }
        }

        private void UpdateStoryCharacterPreview(PreviewCarousel carousel, string characterName, string theme)
        {
            ModLogger.Log($"[UpdateStoryCharacterPreview] Called with characterName='{characterName}', theme='{theme}'");

            // If we don't have a valid mod path, don't load embedded resources
            // This ensures tests can verify that without a proper mod installation, no images load
            if (!_previewManager.HasValidModPath())
            {
                carousel.SetImages(new Image[0]);
                return;
            }

            // First try to load from sprite sheets for Ramza characters
            if (characterName.StartsWith("RamzaChapter"))
            {
                // Get the mod path from PreviewImageManager
                var modPathField = _previewManager.GetType().GetField("_modPath",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (modPathField != null)
                {
                    var modPath = modPathField.GetValue(_previewManager) as string;
                    if (!string.IsNullOrEmpty(modPath))
                    {
                        var spriteSheetLoader = new SpriteSheetPreviewLoader(modPath);
                        var spriteImages = spriteSheetLoader.LoadPreviewsWithExtractor(characterName, theme);

                        if (spriteImages != null && spriteImages.Count > 0)
                        {
                            // Convert Bitmap list to Image array for carousel
                            var spriteImageArray = spriteImages.Cast<Image>().ToArray();
                            carousel.SetImages(spriteImageArray);
                            carousel.Invalidate();
                            carousel.Refresh();
                            ModLogger.LogSuccess($"Loaded {spriteImageArray.Length} sprites from sprite sheet for {characterName} - {theme}");
                            return;
                        }
                        else
                        {
                            // For Ramza, if no sprite sheet found for this theme, show empty preview
                            ModLogger.LogDebug($"No sprite sheet found for {characterName} - {theme}, showing empty preview");
                            carousel.SetImages(new Image[0]);
                            return;
                        }
                    }
                }
            }

            // For non-Ramza characters, fall back to loading from .bin file if it exists
            // For characters that use tex files (like Ramza), apply theme transformation
            var binImages = TryLoadFromBinFileWithTheme(characterName, theme);
            if (binImages != null && binImages.Length > 0)
            {
                carousel.SetImages(binImages);
                carousel.Invalidate();
                carousel.Refresh();
                ModLogger.LogSuccess($"Loaded {binImages.Length} sprites from .bin file for {characterName} - {theme}");
                return;
            }
            else
            {
                ModLogger.LogDebug($"No .bin file found or failed to load for {characterName} - {theme}, falling back to embedded resources");
            }

            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var images = new List<Image>();

            // Try to load all 8 directions from embedded resources
            // FFT sprite order: s, sw, w, nw, n, ne, e, se (starting facing camera, going clockwise)
            string[] directions = { "_s", "_sw", "_w", "_nw", "_n", "_ne", "_e", "_se" };

            foreach (var direction in directions)
            {
                // Resource name format: FFTColorCustomizer.Resources.Previews.{character_folder}.{character}_{theme}_{direction}.png
                string resourceName = $"FFTColorCustomizer.Resources.Previews.{characterName.ToLower()}.{characterName.ToLower()}_{theme.ToLower()}{direction}.png";

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        try
                        {
                            var image = Image.FromStream(stream);
                            images.Add(image);
                            ModLogger.LogDebug($"Loaded direction sprite: {direction} for {characterName}_{theme}");
                        }
                        catch (Exception ex)
                        {
                            ModLogger.LogDebug($"Failed to load direction {direction}: {ex.Message}");
                        }
                    }
                }
            }

            // If we loaded any directional sprites, use them
            if (images.Count > 0)
            {
                carousel.SetImages(images.ToArray());
                ModLogger.LogSuccess($"Loaded {images.Count} directional views for story character {characterName} - {theme}");
                return;
            }

            // Fall back to single image loading using PreviewImageManager
            if (_previewManager != null)
            {
                using (var tempPictureBox = new PictureBox())
                {
                    _previewManager.UpdateStoryCharacterPreview(tempPictureBox, characterName, theme);
                    if (tempPictureBox.Image != null)
                    {
                        carousel.SetImages(new Image[] { tempPictureBox.Image });
                        ModLogger.LogDebug($"Loaded single view for story character {characterName} - {theme}");
                    }
                    else
                    {
                        carousel.SetImages(new Image[0]);
                        ModLogger.LogDebug($"No preview images found for story character {characterName} - {theme}");
                    }
                }
            }
        }

        private Image[] TryLoadFromBinFileWithTheme(string characterName, string theme)
        {
            // Load the base sprites
            var baseImages = TryLoadFromBinFile(characterName, theme);

            if (baseImages == null || baseImages.Length == 0)
                return null;

            // Check if this character uses tex files (needs theme transformation)
            var texFileManager = new TexFileManager();
            if (texFileManager.UsesTexFiles(characterName))
            {
                // Apply theme transformation to the sprites
                var transformer = new RamzaColorTransformer();
                var transformedImages = new Image[baseImages.Length];

                for (int i = 0; i < baseImages.Length; i++)
                {
                    if (baseImages[i] is Bitmap bitmap)
                    {
                        transformedImages[i] = transformer.TransformBitmap(bitmap, theme, characterName);
                    }
                    else
                    {
                        transformedImages[i] = baseImages[i];
                    }
                }

                return transformedImages;
            }

            // No transformation needed
            return baseImages;
        }

        private Image[] TryLoadFromBinFile(string characterName, string theme)
        {
            try
            {
                // Get the mod path from PreviewImageManager's private field using reflection
                var modPathField = _previewManager.GetType().GetField("_modPath",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (modPathField == null)
                    return null;

                string modPath = modPathField.GetValue(_previewManager) as string;
                if (string.IsNullOrEmpty(modPath))
                    return null;

                // Construct path to .bin file
                // FFT sprite files use internal names, not display names
                string internalName = GetInternalSpriteName(characterName);
                string spriteFileName = $"battle_{internalName}_spr.bin";

                ModLogger.LogDebug($"Character name mapping: '{characterName}' -> '{internalName}' -> '{spriteFileName}'");

                // Find the actual FFTIVC path (handles versioned directories)
                string unitPath = FindActualUnitPath(modPath);

                // Check if this is a user theme - if so, load from sprites_original with external palette
                if (_userThemeService.IsUserTheme(characterName, theme))
                {
                    return TryLoadStoryCharacterUserThemeFromBinFile(modPath, unitPath, spriteFileName, characterName, theme);
                }

                // Story character sprites can be in themed folders (e.g., sprites_cloud_sephiroth_black)
                // or in the main unit folder
                string binPath = "";

                // First try a character-specific theme folder
                // Convert theme names: "Sephiroth Black" -> "sephiroth_black"
                string themeFolderName = theme.ToLower()
                    .Replace(" ", "_")  // Replace spaces with underscores
                    .Replace("__", "_"); // Clean up any double underscores

                string characterThemeFolder = $"sprites_{characterName.ToLower()}_{themeFolderName}";
                binPath = Path.Combine(unitPath, characterThemeFolder, spriteFileName);
                ModLogger.Log($"[THEME LOADING] Character: '{characterName}', Theme: '{theme}', Folder: '{characterThemeFolder}', File: '{spriteFileName}'");
                ModLogger.Log($"[THEME LOADING] Full path: {binPath}");
                ModLogger.Log($"[THEME LOADING] File exists: {File.Exists(binPath)}");

                // If not found, try the generic theme folder (for characters without special themes)
                if (!File.Exists(binPath))
                {
                    string genericThemeFolder = $"sprites_{themeFolderName}";
                    binPath = Path.Combine(unitPath, genericThemeFolder, spriteFileName);
                    ModLogger.LogDebug($"Not found, trying generic theme folder: {binPath}");
                }

                // Finally, try the main unit folder (for original sprites)
                if (!File.Exists(binPath))
                {
                    binPath = Path.Combine(unitPath, spriteFileName);
                    ModLogger.LogDebug($"Not found, trying main unit folder: {binPath}");
                }

                if (!File.Exists(binPath))
                {
                    ModLogger.LogDebug($"Bin file not found: {binPath}");
                    return null;
                }

                // Read the bin file
                byte[] binData = File.ReadAllBytes(binPath);
                ModLogger.LogDebug($"Loaded bin file: {binPath}, Size: {binData.Length} bytes");

                // Extract only 4 corner directions for faster loading
                // Themed sprites should use palette 0 (they have their own color data)
                // Only use different palettes for generic job sprites with palette swaps
                int paletteIndex = 0; // Story character themed sprites have their colors baked in
                var sprites = _binExtractor.ExtractCornerDirections(binData, 0, paletteIndex);
                ModLogger.LogDebug($"Extracted {sprites.Length} corner sprites from bin file using palette {paletteIndex} for theme '{theme}'");

                // Return 4 corner sprites: NE, SE, SW, NW (for smooth rotation)
                var carouselImages = new Image[]
                {
                    sprites[2], // SW (default - matches PNG previews)
                    sprites[3], // NW
                    sprites[0], // NE
                    sprites[1]  // SE
                };

                ModLogger.LogDebug($"Successfully loaded 4 corner sprites from .bin: {binPath}");

                return carouselImages;
            }
            catch (Exception ex)
            {
                ModLogger.LogDebug($"Failed to load from .bin file: {ex.Message}");
                return null;
            }
        }

        private Image[] TryLoadStoryCharacterUserThemeFromBinFile(string modPath, string unitPath, string spriteFileName, string characterName, string themeName)
        {
            ModLogger.Log($"[TryLoadStoryCharacterUserThemeFromBinFile] Loading user theme '{themeName}' for {characterName}");

            // Get the user theme palette path
            var palettePath = _userThemeService.GetUserThemePalettePath(characterName, themeName);
            if (string.IsNullOrEmpty(palettePath) || !File.Exists(palettePath))
            {
                ModLogger.LogWarning($"User theme palette not found: {palettePath}");
                return null;
            }

            // For story characters, original sprites may be in character-specific folders
            // e.g., sprites_rapha_original/ or sprites_meliadoul_original/
            // Fall back to sprites_original/ if character-specific folder doesn't have the file
            var characterOriginalDir = Path.Combine(unitPath, $"sprites_{characterName.ToLower()}_original");
            var characterOriginalFile = Path.Combine(characterOriginalDir, spriteFileName);
            var genericOriginalDir = Path.Combine(unitPath, "sprites_original");
            var genericOriginalFile = Path.Combine(genericOriginalDir, spriteFileName);

            string originalFile;
            if (File.Exists(characterOriginalFile))
            {
                originalFile = characterOriginalFile;
                ModLogger.Log($"[TryLoadStoryCharacterUserThemeFromBinFile] Using character-specific original: {originalFile}");
            }
            else if (File.Exists(genericOriginalFile))
            {
                originalFile = genericOriginalFile;
                ModLogger.Log($"[TryLoadStoryCharacterUserThemeFromBinFile] Using generic original: {originalFile}");
            }
            else
            {
                ModLogger.LogWarning($"Original sprite not found for story character user theme preview:");
                ModLogger.LogWarning($"  Character-specific: {characterOriginalFile}");
                ModLogger.LogWarning($"  Generic: {genericOriginalFile}");
                return null;
            }

            // Read original sprite and user palette
            var originalSprite = File.ReadAllBytes(originalFile);
            var userPalette = File.ReadAllBytes(palettePath);

            // Validate palette size
            if (userPalette.Length != 512)
            {
                ModLogger.LogWarning($"Invalid user palette size: {userPalette.Length} (expected 512)");
                return null;
            }

            // Extract sprites using the external user palette
            var cornerSprites = _binExtractor.ExtractCornerDirectionsWithExternalPalette(originalSprite, 0, userPalette);

            // Return 4 corner sprites for carousel
            var carouselSprites = new Image[]
            {
                cornerSprites[2], // SW (default preview angle)
                cornerSprites[3], // NW
                cornerSprites[0], // NE
                cornerSprites[1]  // SE
            };

            ModLogger.Log($"[STORY USER THEME] Character: '{characterName}', Theme: '{themeName}', Using external palette");
            return carouselSprites;
        }

        private bool IsWotLJob(string jobName)
        {
            return jobName.StartsWith("Dark Knight") || jobName.StartsWith("Onion Knight");
        }

        private string FindActualUnitPspPath(string modPath)
        {
            // First try the direct path for unit_psp
            var directPath = Path.Combine(modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit_psp");
            if (Directory.Exists(directPath))
            {
                return directPath;
            }

            // If not found, look for versioned directories
            var parentDir = Path.GetDirectoryName(modPath);
            if (!string.IsNullOrEmpty(parentDir))
            {
                try
                {
                    var versionedDirs = Directory.GetDirectories(parentDir, "FFTColorCustomizer_v*")
                        .OrderByDescending(dir =>
                        {
                            var dirName = Path.GetFileName(dir);
                            var versionStr = dirName.Substring(dirName.LastIndexOf('v') + 1);
                            if (int.TryParse(versionStr, out int version))
                                return version;
                            return 0;
                        })
                        .ToArray();

                    foreach (var versionedDir in versionedDirs)
                    {
                        var versionedPath = Path.Combine(versionedDir, "FFTIVC", "data", "enhanced", "fftpack", "unit_psp");
                        if (Directory.Exists(versionedPath))
                        {
                            ModLogger.Log($"Found FFTIVC unit_psp for WotL previews in versioned directory: {versionedPath}");
                            return versionedPath;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.LogWarning($"Error searching for versioned directories: {ex.Message}");
                }
            }

            // Return the expected path even if it doesn't exist
            return directPath;
        }

        private Image[] TryLoadGenericFromBinFile(string jobName, string theme)
        {
            try
            {
                ModLogger.Log($"[TryLoadGenericFromBinFile] Starting for {jobName} - {theme}");
                // Get the mod path from PreviewImageManager
                var modPathField = _previewManager.GetType().GetField("_modPath",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (modPathField == null)
                    return null;

                string modPath = modPathField.GetValue(_previewManager) as string;
                if (string.IsNullOrEmpty(modPath))
                    return null;

                // Convert job name to sprite file name format
                // e.g., "Knight (Male)" -> "battle_knight_m_spr.bin"
                string spriteFileName = ConvertJobNameToSpriteFile(jobName);

                // Look for themed sprite files
                // Find the actual FFTIVC path (handles versioned directories)
                // For WotL jobs, use unit_psp instead of unit
                string unitPath = IsWotLJob(jobName) ? FindActualUnitPspPath(modPath) : FindActualUnitPath(modPath);

                // Check if this is a user theme
                var jobProperty = ConvertJobNameToPropertyFormat(jobName);
                if (_userThemeService.IsUserTheme(jobProperty, theme))
                {
                    return TryLoadUserThemeFromBinFile(modPath, unitPath, spriteFileName, jobProperty, theme);
                }

                // First check if this is a job-specific theme (e.g., sprites_knight_h78/battle_knight_m_spr.bin)
                string jobType = jobName.Replace(" (Male)", "").Replace(" (Female)", "").Replace(" ", "").ToLower();
                string jobSpecificFolderName = $"sprites_{jobType}_{theme.ToLower().Replace(" ", "_")}";
                string binPath = Path.Combine(unitPath, jobSpecificFolderName, spriteFileName);

                ModLogger.Log($"[DEBUG] Job: '{jobName}' → JobType: '{jobType}' → Folder: '{jobSpecificFolderName}'");
                ModLogger.LogDebug($"Looking for job-specific themed sprite at: {binPath}");

                // If job-specific doesn't exist, try generic theme folder (e.g., sprites_crimson_red/battle_knight_m_spr.bin)
                if (!File.Exists(binPath))
                {
                    string themeFolderName = $"sprites_{theme.ToLower().Replace(" ", "_")}";
                    binPath = Path.Combine(unitPath, themeFolderName, spriteFileName);
                    ModLogger.LogDebug($"Job-specific not found, trying generic themed sprite at: {binPath}");
                }

                // If themed version doesn't exist, fall back to main unit folder
                if (!File.Exists(binPath))
                {
                    binPath = Path.Combine(unitPath, spriteFileName);
                    ModLogger.LogDebug($"Themed sprite not found, trying main unit folder: {binPath}");
                }

                if (!File.Exists(binPath))
                {
                    ModLogger.LogDebug($"Generic bin file not found: {binPath}");
                    return null;
                }

                // Read the bin file
                byte[] binData = File.ReadAllBytes(binPath);
                ModLogger.LogDebug($"Loaded generic bin file: {binPath}, Size: {binData.Length} bytes");

                // Extract all 8 directional sprites for carousel
                // For themed sprites, use palette 0 (they have the theme colors baked in)
                int paletteIndex = 0;

                var cornerSprites = _binExtractor.ExtractCornerDirections(binData, 0, paletteIndex);

                // Return 4 corner sprites for faster loading
                var carouselSprites = new Image[]
                {
                    cornerSprites[2], // SW (default preview angle)
                    cornerSprites[3], // NW
                    cornerSprites[0], // NE
                    cornerSprites[1]  // SE
                };

                ModLogger.Log($"[GENERIC THEME] Job: '{jobName}', Theme: '{theme}', Using themed sprite file");
                ModLogger.LogDebug($"Extracted 4 corner sprites from themed bin file");

                // Return 4 corner sprites for carousel
                ModLogger.Log($"[TryLoadGenericFromBinFile] Success - returning 4 corner sprites");
                return carouselSprites;
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[TryLoadGenericFromBinFile] FAILED for {jobName} - {theme}: {ex.Message}");
                ModLogger.LogError($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        private Image[] TryLoadUserThemeFromBinFile(string modPath, string unitPath, string spriteFileName, string jobProperty, string themeName)
        {
            ModLogger.Log($"[TryLoadUserThemeFromBinFile] Loading user theme '{themeName}' for {jobProperty}");

            // Get the user theme palette path
            var palettePath = _userThemeService.GetUserThemePalettePath(jobProperty, themeName);
            if (string.IsNullOrEmpty(palettePath) || !File.Exists(palettePath))
            {
                ModLogger.LogWarning($"User theme palette not found: {palettePath}");
                return null;
            }

            // Get the original sprite from sprites_original
            var originalDir = Path.Combine(unitPath, "sprites_original");
            var originalFile = Path.Combine(originalDir, spriteFileName);
            if (!File.Exists(originalFile))
            {
                ModLogger.LogWarning($"Original sprite not found for user theme preview: {originalFile}");
                return null;
            }

            // Read original sprite and user palette
            var originalSprite = File.ReadAllBytes(originalFile);
            var userPalette = File.ReadAllBytes(palettePath);

            // Validate palette size
            if (userPalette.Length != 512)
            {
                ModLogger.LogWarning($"Invalid user palette size: {userPalette.Length} (expected 512)");
                return null;
            }

            // Extract sprites using the external user palette
            var cornerSprites = _binExtractor.ExtractCornerDirectionsWithExternalPalette(originalSprite, 0, userPalette);

            // Return 4 corner sprites for carousel
            var carouselSprites = new Image[]
            {
                cornerSprites[2], // SW (default preview angle)
                cornerSprites[3], // NW
                cornerSprites[0], // NE
                cornerSprites[1]  // SE
            };

            ModLogger.Log($"[USER THEME] Job: '{jobProperty}', Theme: '{themeName}', Using external palette");
            return carouselSprites;
        }

        private string GetCharacterDisplayName(string characterName)
        {
            switch (characterName)
            {
                case "RamzaChapter1":
                    return "Ramza (Chapter 1)";
                case "RamzaChapter23":
                    return "Ramza (Chapter 2 & 3)";
                case "RamzaChapter4":
                    return "Ramza (Chapter 4)";
                default:
                    return characterName;
            }
        }

        private string GetInternalSpriteName(string characterName)
        {
            // Map display names to internal FFT sprite file names
            switch (characterName.ToLower())
            {
                case "ramza":
                    return "ramuza";
                case "ramzachapter1":
                    return "ramuza";
                case "ramzachapter23":
                    return "ramuza2";
                case "ramzachapter4":
                    return "ramuza3";
                case "agrias":
                    return "aguri";
                case "cloud":
                    return "cloud";
                case "orlandeau":
                    return "oru";
                case "rapha":
                    return "h79";
                case "marach":
                    return "mara";
                case "mustadio":
                    return "musu";
                case "meliadoul":
                    return "h85";
                case "beowulf":
                    return "beio";
                case "reis":
                    return "reze";
                case "alma":
                    return "aruma";
                case "delita":
                    return "dily";
                default:
                    // Fallback to the character name itself
                    return characterName.ToLower();
            }
        }

        /// <summary>
        /// Converts a job display name like "Knight (Male)" to property format like "Knight_Male"
        /// </summary>
        public static string ConvertJobNameToPropertyFormat(string jobName)
        {
            return jobName
                .Replace(" (Male)", "_Male")
                .Replace(" (Female)", "_Female")
                .Replace(" ", "");
        }

        private string ConvertJobNameToSpriteFile(string jobName)
        {
            // Map job display names to sprite file names based on JobClasses.json
            // Format: "Job Name (Gender)" -> "battle_sprite_spr.bin"

            switch (jobName)
            {
                // Knights
                case "Knight (Male)": return "battle_knight_m_spr.bin";
                case "Knight (Female)": return "battle_knight_w_spr.bin";

                // Basic Jobs
                case "Archer (Male)": return "battle_yumi_m_spr.bin";
                case "Archer (Female)": return "battle_yumi_w_spr.bin";
                case "Chemist (Male)": return "battle_item_m_spr.bin";
                case "Chemist (Female)": return "battle_item_w_spr.bin";
                case "Monk (Male)": return "battle_monk_m_spr.bin";
                case "Monk (Female)": return "battle_monk_w_spr.bin";
                case "Squire (Male)": return "battle_mina_m_spr.bin";
                case "Squire (Female)": return "battle_mina_w_spr.bin";
                case "Thief (Male)": return "battle_thief_m_spr.bin";
                case "Thief (Female)": return "battle_thief_w_spr.bin";

                // Mage Jobs
                case "White Mage (Male)": return "battle_siro_m_spr.bin";
                case "White Mage (Female)": return "battle_siro_w_spr.bin";
                case "Black Mage (Male)": return "battle_kuro_m_spr.bin";
                case "Black Mage (Female)": return "battle_kuro_w_spr.bin";
                case "Time Mage (Male)": return "battle_toki_m_spr.bin";
                case "Time Mage (Female)": return "battle_toki_w_spr.bin";
                case "Summoner (Male)": return "battle_syou_m_spr.bin";
                case "Summoner (Female)": return "battle_syou_w_spr.bin";
                case "Mystic (Male)": return "battle_onmyo_m_spr.bin";
                case "Mystic (Female)": return "battle_onmyo_w_spr.bin";

                // Advanced Jobs
                case "Ninja (Male)": return "battle_ninja_m_spr.bin";
                case "Ninja (Female)": return "battle_ninja_w_spr.bin";
                case "Samurai (Male)": return "battle_samu_m_spr.bin";
                case "Samurai (Female)": return "battle_samu_w_spr.bin";
                case "Dragoon (Male)": return "battle_ryu_m_spr.bin";
                case "Dragoon (Female)": return "battle_ryu_w_spr.bin";
                case "Geomancer (Male)": return "battle_fusui_m_spr.bin";
                case "Geomancer (Female)": return "battle_fusui_w_spr.bin";
                case "Mediator (Male)": return "battle_waju_m_spr.bin";
                case "Mediator (Female)": return "battle_waju_w_spr.bin";
                case "Calculator (Male)": return "battle_san_m_spr.bin";
                case "Calculator (Female)": return "battle_san_w_spr.bin";
                case "Mime (Male)": return "battle_mono_m_spr.bin";
                case "Mime (Female)": return "battle_mono_w_spr.bin";

                // Gender-specific Jobs
                case "Bard":
                case "Bard (Male)": return "battle_gin_m_spr.bin";
                case "Dancer":
                case "Dancer (Female)": return "battle_odori_w_spr.bin";

                // WotL Jobs (unit_psp)
                case "Dark Knight (Male)": return "spr_dst_bchr_ankoku_m_spr.bin";
                case "Dark Knight (Female)": return "spr_dst_bchr_ankoku_w_spr.bin";
                case "Onion Knight (Male)": return "spr_dst_bchr_tama_m_spr.bin";
                case "Onion Knight (Female)": return "spr_dst_bchr_tama_w_spr.bin";

                // Fallback to old logic if not found
                default:
                    string baseName = jobName.ToLower()
                        .Replace(" (male)", "_m")
                        .Replace(" (female)", "_w")
                        .Replace(" ", "_")
                        .Replace("(", "")
                        .Replace(")", "");
                    string fileName = $"battle_{baseName}_spr.bin";
                    ModLogger.LogDebug($"ConvertJobNameToSpriteFile: '{jobName}' -> '{fileName}' (fallback)");
                    return fileName;
            }
        }

        private int GetPaletteIndexForTheme(string theme)
        {
            // FFT sprites have 16 palettes (0-15)
            // Map each theme to its corresponding palette index
            // These mappings are based on the actual palette data in the .bin files

            switch (theme?.ToLower().Replace(" ", "_"))
            {
                // Most FFT sprites only have 5 valid palettes (0-4)
                // Palettes 5-15 are usually empty or all black

                // Original colors
                case "original":
                    return 0;

                // Valid alternate palettes (tested to have actual color data)
                case "corpse_brigade":
                    return 1;
                case "lucavi":
                    return 2;
                case "northern_sky":
                    return 3;
                case "southern_sky":
                    return 4;

                // Map remaining themes to cycle through the valid palettes
                case "crimson_red":
                    return 1;  // Reuse palette 1
                case "royal_purple":
                    return 2;  // Reuse palette 2
                case "amethyst":
                    return 3;  // Reuse palette 3
                case "emerald_green":
                    return 4;  // Reuse palette 4
                case "sapphire_blue":
                    return 1;
                case "topaz_yellow":
                    return 2;
                case "obsidian_black":
                    return 3;
                case "pearl_white":
                    return 4;
                case "ivalician":
                    return 1;
                case "deep_dungeon":
                    return 2;
                case "mystic":
                    return 3;
                case "phoenix_flame":
                    return 4;
                case "frost_knight":
                    return 1;
                case "shadow_assassin":
                    return 2;
                case "holy_guard":
                    return 3;
                case "dragon_tamer":
                    return 4;

                // Additional theme variants that might exist
                case "ash_dark":
                    return 9;
                case "void_black":
                    return 10;
                case "royal_crimson":
                    return 11;
                case "forest_green":
                    return 12;
                case "ocean_blue":
                    return 13;
                case "holy_white":
                    return 14;
                case "dark_knight":
                    return 15;

                // Default to original if theme not found
                default:
                    ModLogger.LogDebug($"Theme '{theme}' not mapped to palette, using default (0)");
                    return 0;
            }
        }
    }
}
