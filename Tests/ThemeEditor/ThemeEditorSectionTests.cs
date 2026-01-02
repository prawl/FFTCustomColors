using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.ThemeEditor;
using Xunit;

namespace FFTColorCustomizer.Tests.ThemeEditor
{
    public class ThemeEditorSectionTests
    {
        [Fact]
        [STAThread]
        public void ConfigurationForm_HasThemeEditorSection_WithCollapsibleHeader()
        {
            // Arrange
            var config = new Config();

            // Act
            using var form = new ConfigurationForm(config);

            // Assert - Find the Theme Editor header label
            var mainPanel = form.Controls.OfType<Panel>()
                .SelectMany(p => p.Controls.OfType<TableLayoutPanel>())
                .FirstOrDefault();

            Assert.NotNull(mainPanel);

            var themeEditorHeader = mainPanel.Controls.OfType<Label>()
                .FirstOrDefault(l => l.Text.Contains("Theme Editor"));

            Assert.NotNull(themeEditorHeader);
            Assert.Contains("Theme Editor", themeEditorHeader.Text);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_HasTemplateDropdown_WithAvailableJobs()
        {
            // Arrange & Act
            using var panel = new ThemeEditorPanel();

            // Assert - Should have a template ComboBox
            var templateDropdown = panel.Controls.OfType<ComboBox>()
                .FirstOrDefault(c => c.Name == "TemplateDropdown");

            Assert.NotNull(templateDropdown);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_TemplateDropdown_PopulatedFromMappingsDirectory()
        {
            // Arrange - create temp directory with test mapping files
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ThemeEditorTest_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(tempDir);
            try
            {
                // Need valid JSON since Squire_Male will be auto-selected and parsed
                System.IO.File.WriteAllText(System.IO.Path.Combine(tempDir, "Squire_Male.json"), @"{""job"":""Squire_Male"",""sprite"":""battle_mina_m_spr.bin"",""sections"":[]}");
                System.IO.File.WriteAllText(System.IO.Path.Combine(tempDir, "Knight_Female.json"), @"{""job"":""Knight_Female"",""sprite"":""battle_knight_w_spr.bin"",""sections"":[]}");

                // Act
                using var panel = new ThemeEditorPanel(tempDir);

                // Assert
                var templateDropdown = panel.Controls.OfType<ComboBox>()
                    .FirstOrDefault(c => c.Name == "TemplateDropdown");

                Assert.NotNull(templateDropdown);
                Assert.Equal(2, templateDropdown.Items.Count);
                Assert.Contains("Knight Female", templateDropdown.Items.Cast<string>());
                Assert.Contains("Squire Male", templateDropdown.Items.Cast<string>());
            }
            finally
            {
                System.IO.Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_HasSpritePreviewPictureBox()
        {
            // Arrange & Act
            using var panel = new ThemeEditorPanel();

            // Assert - Should have a PictureBox for sprite preview
            var previewBox = panel.Controls.OfType<PictureBox>()
                .FirstOrDefault(c => c.Name == "SpritePreview");

            Assert.NotNull(previewBox);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_HasThemeNameTextBox()
        {
            // Arrange & Act
            using var panel = new ThemeEditorPanel();

            // Assert - Should have a TextBox for theme name input
            var themeNameBox = panel.Controls.OfType<TextBox>()
                .FirstOrDefault(c => c.Name == "ThemeNameInput");

            Assert.NotNull(themeNameBox);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_HasSaveAndResetButtons()
        {
            // Arrange & Act
            using var panel = new ThemeEditorPanel();

            // Assert - Should have Save and Reset All buttons
            var saveButton = panel.Controls.OfType<Button>()
                .FirstOrDefault(c => c.Name == "SaveButton");
            var resetButton = panel.Controls.OfType<Button>()
                .FirstOrDefault(c => c.Name == "ResetButton");

            Assert.NotNull(saveButton);
            Assert.NotNull(resetButton);
            Assert.Equal("Reset All", resetButton.Text);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_HasSectionColorPickersPanel()
        {
            // Arrange & Act
            using var panel = new ThemeEditorPanel();

            // Assert - Should have a panel to contain section color pickers
            var colorPickersPanel = panel.Controls.OfType<Panel>()
                .FirstOrDefault(c => c.Name == "SectionColorPickersPanel");

            Assert.NotNull(colorPickersPanel);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_WhenTemplateSelected_LoadsSectionMapping()
        {
            // Arrange - create temp directory with a valid mapping file
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ThemeEditorTest_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(tempDir);
            try
            {
                var mappingJson = @"{
                    ""job"": ""Knight_Male"",
                    ""sprite"": ""battle_knight_m_spr.bin"",
                    ""sections"": [
                        {
                            ""name"": ""Cape"",
                            ""displayName"": ""Cape"",
                            ""indices"": [3, 4, 5],
                            ""roles"": [""shadow"", ""base"", ""highlight""]
                        },
                        {
                            ""name"": ""Boots"",
                            ""displayName"": ""Boots"",
                            ""indices"": [6, 7],
                            ""roles"": [""base"", ""highlight""]
                        }
                    ]
                }";
                System.IO.File.WriteAllText(System.IO.Path.Combine(tempDir, "Knight_Male.json"), mappingJson);

                using var panel = new ThemeEditorPanel(tempDir);
                var templateDropdown = panel.Controls.OfType<ComboBox>()
                    .First(c => c.Name == "TemplateDropdown");

                // Act - select the template
                templateDropdown.SelectedItem = "Knight Male";

                // Assert - CurrentMapping should be loaded
                Assert.NotNull(panel.CurrentMapping);
                Assert.Equal("Knight_Male", panel.CurrentMapping.Job);
                Assert.Equal("battle_knight_m_spr.bin", panel.CurrentMapping.Sprite);
                Assert.Equal(2, panel.CurrentMapping.Sections.Length);
            }
            finally
            {
                System.IO.Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_HasRotationArrowButtons()
        {
            // Arrange & Act
            using var panel = new ThemeEditorPanel();

            // Assert - Should have left and right rotation buttons
            var leftArrow = panel.Controls.OfType<Button>()
                .FirstOrDefault(c => c.Name == "RotateLeftButton");
            var rightArrow = panel.Controls.OfType<Button>()
                .FirstOrDefault(c => c.Name == "RotateRightButton");

            Assert.NotNull(leftArrow);
            Assert.NotNull(rightArrow);
            Assert.Equal("◄", leftArrow.Text);
            Assert.Equal("►", rightArrow.Text);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_RotateRight_CyclesClockwise()
        {
            // Arrange
            using var panel = new ThemeEditorPanel();
            var rightArrow = panel.Controls.OfType<Button>()
                .First(c => c.Name == "RotateRightButton");

            // Act - click right arrow (starting from SW = 5)
            Assert.Equal(5, panel.CurrentSpriteDirection); // SW
            rightArrow.PerformClick();

            // Assert - should go clockwise to S = 4
            Assert.Equal(4, panel.CurrentSpriteDirection);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_RotateLeft_CyclesCounterClockwise()
        {
            // Arrange
            using var panel = new ThemeEditorPanel();
            var leftArrow = panel.Controls.OfType<Button>()
                .First(c => c.Name == "RotateLeftButton");

            // Starting at SW(5)
            Assert.Equal(5, panel.CurrentSpriteDirection); // SW

            // Act - click left arrow (counter-clockwise)
            leftArrow.PerformClick();

            // Assert - should go counter-clockwise to W(6)
            Assert.Equal(6, panel.CurrentSpriteDirection);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_RotateRight_CyclesThroughAll8Directions()
        {
            // Arrange
            using var panel = new ThemeEditorPanel();
            var rightArrow = panel.Controls.OfType<Button>()
                .First(c => c.Name == "RotateRightButton");

            // Direction cycle (clockwise): SW(5) → S(4) → SE(3) → E(2) → NE(1) → N(0) → NW(7) → W(6) → SW(5)
            Assert.Equal(5, panel.CurrentSpriteDirection); // SW

            rightArrow.PerformClick();
            Assert.Equal(4, panel.CurrentSpriteDirection); // S

            rightArrow.PerformClick();
            Assert.Equal(3, panel.CurrentSpriteDirection); // SE

            rightArrow.PerformClick();
            Assert.Equal(2, panel.CurrentSpriteDirection); // E

            rightArrow.PerformClick();
            Assert.Equal(1, panel.CurrentSpriteDirection); // NE

            rightArrow.PerformClick();
            Assert.Equal(0, panel.CurrentSpriteDirection); // N

            rightArrow.PerformClick();
            Assert.Equal(7, panel.CurrentSpriteDirection); // NW

            rightArrow.PerformClick();
            Assert.Equal(6, panel.CurrentSpriteDirection); // W

            rightArrow.PerformClick();
            Assert.Equal(5, panel.CurrentSpriteDirection); // Back to SW
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_WhenTemplateSelected_LoadsSpriteIntoPaletteModifier()
        {
            // Arrange - create temp directories with mapping and sprite files
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ThemeEditorTest_" + Guid.NewGuid().ToString("N"));
            var mappingsDir = System.IO.Path.Combine(tempDir, "mappings");
            var spritesDir = System.IO.Path.Combine(tempDir, "sprites");
            System.IO.Directory.CreateDirectory(mappingsDir);
            System.IO.Directory.CreateDirectory(spritesDir);

            try
            {
                // Create mapping file
                var mappingJson = @"{
                    ""job"": ""Knight_Male"",
                    ""sprite"": ""battle_knight_m_spr.bin"",
                    ""sections"": []
                }";
                System.IO.File.WriteAllText(System.IO.Path.Combine(mappingsDir, "Knight_Male.json"), mappingJson);

                // Create a minimal sprite file (needs at least 512 bytes for palette)
                var spriteData = new byte[1024];
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(spritesDir, "battle_knight_m_spr.bin"), spriteData);

                using var panel = new ThemeEditorPanel(mappingsDir, spritesDir);
                var templateDropdown = panel.Controls.OfType<ComboBox>()
                    .First(c => c.Name == "TemplateDropdown");

                // Act - select the template
                templateDropdown.SelectedItem = "Knight Male";

                // Assert - PaletteModifier should be loaded
                Assert.NotNull(panel.PaletteModifier);
                Assert.True(panel.PaletteModifier.IsLoaded);
            }
            finally
            {
                System.IO.Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_HasHueSaturationLightnessSliders()
        {
            // Arrange & Act
            using var picker = new HslColorPicker();

            // Assert - Should have three track bars for H, S, L
            var hueSlider = picker.Controls.OfType<TrackBar>()
                .FirstOrDefault(c => c.Name == "HueSlider");
            var saturationSlider = picker.Controls.OfType<TrackBar>()
                .FirstOrDefault(c => c.Name == "SaturationSlider");
            var lightnessSlider = picker.Controls.OfType<TrackBar>()
                .FirstOrDefault(c => c.Name == "LightnessSlider");

            Assert.NotNull(hueSlider);
            Assert.NotNull(saturationSlider);
            Assert.NotNull(lightnessSlider);

            // Hue: 0-360, Saturation: 0-100, Lightness: 0-100
            Assert.Equal(0, hueSlider.Minimum);
            Assert.Equal(360, hueSlider.Maximum);
            Assert.Equal(0, saturationSlider.Minimum);
            Assert.Equal(100, saturationSlider.Maximum);
            Assert.Equal(0, lightnessSlider.Minimum);
            Assert.Equal(100, lightnessSlider.Maximum);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_ExposesHslProperties()
        {
            // Arrange
            using var picker = new HslColorPicker();

            // Act - set slider values
            var hueSlider = picker.Controls.OfType<TrackBar>().First(c => c.Name == "HueSlider");
            var saturationSlider = picker.Controls.OfType<TrackBar>().First(c => c.Name == "SaturationSlider");
            var lightnessSlider = picker.Controls.OfType<TrackBar>().First(c => c.Name == "LightnessSlider");

            hueSlider.Value = 180;
            saturationSlider.Value = 75;
            lightnessSlider.Value = 50;

            // Assert - properties reflect slider values
            Assert.Equal(180, picker.Hue);
            Assert.Equal(75, picker.Saturation);
            Assert.Equal(50, picker.Lightness);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_CanSetHslPropertiesProgrammatically()
        {
            // Arrange
            using var picker = new HslColorPicker();

            // Act - set properties directly
            picker.Hue = 240;
            picker.Saturation = 80;
            picker.Lightness = 60;

            // Assert - sliders should reflect the new values
            var hueSlider = picker.Controls.OfType<TrackBar>().First(c => c.Name == "HueSlider");
            var saturationSlider = picker.Controls.OfType<TrackBar>().First(c => c.Name == "SaturationSlider");
            var lightnessSlider = picker.Controls.OfType<TrackBar>().First(c => c.Name == "LightnessSlider");

            Assert.Equal(240, hueSlider.Value);
            Assert.Equal(80, saturationSlider.Value);
            Assert.Equal(60, lightnessSlider.Value);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_CurrentColor_ReturnsRgbFromHslValues()
        {
            // Arrange
            using var picker = new HslColorPicker();

            // Act - set to pure red (H=0, S=100, L=50)
            picker.Hue = 0;
            picker.Saturation = 100;
            picker.Lightness = 50;

            // Assert - should be pure red
            var color = picker.CurrentColor;
            Assert.Equal(255, color.R);
            Assert.Equal(0, color.G);
            Assert.Equal(0, color.B);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_SetColor_UpdatesHslSlidersFromRgb()
        {
            // Arrange
            using var picker = new HslColorPicker();

            // Act - set color to pure green
            picker.SetColor(Color.FromArgb(0, 255, 0));

            // Assert - should be H=120, S=100, L=50 for pure green
            Assert.Equal(120, picker.Hue);
            Assert.Equal(100, picker.Saturation);
            Assert.Equal(50, picker.Lightness);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_RaisesColorChangedEvent_WhenSliderChanges()
        {
            // Arrange
            using var picker = new HslColorPicker();
            var eventRaised = false;
            picker.ColorChanged += (sender, e) => eventRaised = true;

            // Act - change hue slider
            picker.Hue = 180;

            // Assert
            Assert.True(eventRaised);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_RaisesColorChangedEvent_WhenSaturationChanges()
        {
            // Arrange
            using var picker = new HslColorPicker();
            var eventRaised = false;
            picker.ColorChanged += (sender, e) => eventRaised = true;

            // Act - change saturation slider
            picker.Saturation = 75;

            // Assert
            Assert.True(eventRaised);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_RaisesColorChangedEvent_WhenLightnessChanges()
        {
            // Arrange
            using var picker = new HslColorPicker();
            var eventRaised = false;
            picker.ColorChanged += (sender, e) => eventRaised = true;

            // Act - change lightness slider
            picker.Lightness = 25;

            // Assert
            Assert.True(eventRaised);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_HasSectionNameProperty()
        {
            // Arrange & Act
            using var picker = new HslColorPicker();
            picker.SectionName = "Cape";

            // Assert
            Assert.Equal("Cape", picker.SectionName);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_RaisesColorChangedEvent_WhenSliderDragged()
        {
            // Arrange
            using var picker = new HslColorPicker();
            var eventRaised = false;
            picker.ColorChanged += (sender, e) => eventRaised = true;

            // Act - simulate user dragging the slider by directly changing TrackBar value
            var hueSlider = picker.Controls.OfType<TrackBar>().First(c => c.Name == "HueSlider");
            hueSlider.Value = 180;

            // Assert
            Assert.True(eventRaised);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_SetColorSilent_DoesNotRaiseColorChangedEvent()
        {
            // Arrange
            using var picker = new HslColorPicker();
            var eventRaised = false;
            picker.ColorChanged += (sender, e) => eventRaised = true;

            // Act - set color silently (for initialization)
            picker.SetColorSilent(Color.FromArgb(255, 0, 0)); // Red

            // Assert - event should NOT be raised
            Assert.False(eventRaised);
            // But the color should be set correctly
            Assert.Equal(0, picker.Hue);
            Assert.Equal(100, picker.Saturation);
            Assert.Equal(50, picker.Lightness);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_SlidersAreVerticallyStacked()
        {
            // Arrange & Act
            using var picker = new HslColorPicker();

            // Assert - sliders should have different Top positions (not overlapping)
            var hueSlider = picker.Controls.OfType<TrackBar>().First(c => c.Name == "HueSlider");
            var satSlider = picker.Controls.OfType<TrackBar>().First(c => c.Name == "SaturationSlider");
            var lightSlider = picker.Controls.OfType<TrackBar>().First(c => c.Name == "LightnessSlider");

            // Each slider should be at a different vertical position
            Assert.True(satSlider.Top > hueSlider.Top, "Saturation slider should be below Hue slider");
            Assert.True(lightSlider.Top > satSlider.Top, "Lightness slider should be below Saturation slider");
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_HasLabelsForEachSlider()
        {
            // Arrange & Act
            using var picker = new HslColorPicker();

            // Assert - should have labels for Hue, Saturation, and Lightness (plus section header = 4 total)
            var labels = picker.Controls.OfType<Label>().ToList();
            Assert.Equal(4, labels.Count);
            Assert.Contains(labels, l => l.Text == "Hue");
            Assert.Contains(labels, l => l.Text == "Saturation");
            Assert.Contains(labels, l => l.Text == "Lightness");
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_DisplaysSectionNameAsHeader()
        {
            // Arrange & Act
            using var picker = new HslColorPicker();
            picker.SectionName = "Cape";

            // Assert - should have a section header label displaying the section name
            var sectionHeader = picker.Controls.OfType<Label>().FirstOrDefault(l => l.Name == "SectionHeaderLabel");
            Assert.NotNull(sectionHeader);
            Assert.Equal("Cape", sectionHeader.Text);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_WhenTemplateSelected_InitializesColorPickersFromPalette()
        {
            // Arrange - create temp directories with mapping and sprite files
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ThemeEditorTest_" + Guid.NewGuid().ToString("N"));
            var mappingsDir = System.IO.Path.Combine(tempDir, "mappings");
            var spritesDir = System.IO.Path.Combine(tempDir, "sprites");
            System.IO.Directory.CreateDirectory(mappingsDir);
            System.IO.Directory.CreateDirectory(spritesDir);

            try
            {
                // Create mapping file with a section that uses index 3 as base
                var mappingJson = @"{
                    ""job"": ""Knight_Male"",
                    ""sprite"": ""battle_knight_m_spr.bin"",
                    ""sections"": [
                        {
                            ""name"": ""Cape"",
                            ""displayName"": ""Cape"",
                            ""indices"": [3, 4, 5],
                            ""roles"": [""shadow"", ""base"", ""highlight""]
                        }
                    ]
                }";
                System.IO.File.WriteAllText(System.IO.Path.Combine(mappingsDir, "Knight_Male.json"), mappingJson);

                // Create sprite file with a known color at palette index 4 (base role for Cape)
                // Palette is 16 colors x 2 bytes = 32 bytes for palette 0
                // Index 4 = bytes 8-9
                var spriteData = new byte[2048];
                // Set index 4 to pure red (BGR555: R=31, G=0, B=0 = 0x001F)
                spriteData[8] = 0x1F; // Low byte
                spriteData[9] = 0x00; // High byte
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(spritesDir, "battle_knight_m_spr.bin"), spriteData);

                using var panel = new ThemeEditorPanel(mappingsDir, spritesDir);
                var templateDropdown = panel.Controls.OfType<ComboBox>().First(c => c.Name == "TemplateDropdown");

                // Act - select the template
                templateDropdown.SelectedItem = "Knight Male";

                // Assert - the Cape color picker should be initialized with the color from palette
                var sectionPanel = panel.Controls.OfType<Panel>().First(c => c.Name == "SectionColorPickersPanel");
                var capePicker = sectionPanel.Controls.OfType<HslColorPicker>().First(c => c.SectionName == "Cape");

                // The color should not be black (L=0) - it should have been initialized from palette
                Assert.True(capePicker.Lightness > 0, "Color picker should be initialized from palette, not left at default black");
            }
            finally
            {
                System.IO.Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_WhenTemplateSelected_UpdatesSpritePreview()
        {
            // Arrange - create temp directories with mapping and sprite files
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ThemeEditorTest_" + Guid.NewGuid().ToString("N"));
            var mappingsDir = System.IO.Path.Combine(tempDir, "mappings");
            var spritesDir = System.IO.Path.Combine(tempDir, "sprites");
            System.IO.Directory.CreateDirectory(mappingsDir);
            System.IO.Directory.CreateDirectory(spritesDir);

            try
            {
                // Create mapping file
                var mappingJson = @"{
                    ""job"": ""Knight_Male"",
                    ""sprite"": ""battle_knight_m_spr.bin"",
                    ""sections"": []
                }";
                System.IO.File.WriteAllText(System.IO.Path.Combine(mappingsDir, "Knight_Male.json"), mappingJson);

                // Create a minimal sprite file (needs at least 512 bytes for palette + some sprite data)
                // Minimum for BinSpriteExtractor to work: 512 palette + sprite pixel data
                var spriteData = new byte[2048];
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(spritesDir, "battle_knight_m_spr.bin"), spriteData);

                using var panel = new ThemeEditorPanel(mappingsDir, spritesDir);
                var templateDropdown = panel.Controls.OfType<ComboBox>()
                    .First(c => c.Name == "TemplateDropdown");
                var spritePreview = panel.Controls.OfType<PictureBox>()
                    .First(c => c.Name == "SpritePreview");

                // Assert - preview should be null before selection
                Assert.Null(spritePreview.Image);

                // Act - select the template
                templateDropdown.SelectedItem = "Knight Male";

                // Assert - preview should now have an image
                Assert.NotNull(spritePreview.Image);
            }
            finally
            {
                System.IO.Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_RotateButtons_UpdateSpritePreview()
        {
            // Arrange - create temp directories with mapping and sprite files
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ThemeEditorTest_" + Guid.NewGuid().ToString("N"));
            var mappingsDir = System.IO.Path.Combine(tempDir, "mappings");
            var spritesDir = System.IO.Path.Combine(tempDir, "sprites");
            System.IO.Directory.CreateDirectory(mappingsDir);
            System.IO.Directory.CreateDirectory(spritesDir);

            try
            {
                // Create mapping file
                var mappingJson = @"{
                    ""job"": ""Knight_Male"",
                    ""sprite"": ""battle_knight_m_spr.bin"",
                    ""sections"": []
                }";
                System.IO.File.WriteAllText(System.IO.Path.Combine(mappingsDir, "Knight_Male.json"), mappingJson);

                // Create a minimal sprite file
                var spriteData = new byte[2048];
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(spritesDir, "battle_knight_m_spr.bin"), spriteData);

                using var panel = new ThemeEditorPanel(mappingsDir, spritesDir);
                var templateDropdown = panel.Controls.OfType<ComboBox>().First(c => c.Name == "TemplateDropdown");
                var spritePreview = panel.Controls.OfType<PictureBox>().First(c => c.Name == "SpritePreview");
                var rotateRightButton = panel.Controls.OfType<Button>().First(c => c.Name == "RotateRightButton");

                // Select template to load sprite
                templateDropdown.SelectedItem = "Knight Male";
                var initialImage = spritePreview.Image;
                Assert.NotNull(initialImage);

                // Act - rotate right
                rotateRightButton.PerformClick();

                // Assert - image instance should be different (new bitmap for new direction)
                Assert.NotNull(spritePreview.Image);
                Assert.NotSame(initialImage, spritePreview.Image);
            }
            finally
            {
                System.IO.Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_DefaultDirection_IsSouthwest()
        {
            // Arrange & Act
            using var panel = new ThemeEditorPanel();

            // Assert - default direction should be Southwest (index 5 in the sprite array)
            Assert.Equal(5, panel.CurrentSpriteDirection);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_ColorPickerChange_UpdatesPreview()
        {
            // Arrange - create temp directories with mapping and sprite files
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ThemeEditorTest_" + Guid.NewGuid().ToString("N"));
            var mappingsDir = System.IO.Path.Combine(tempDir, "mappings");
            var spritesDir = System.IO.Path.Combine(tempDir, "sprites");
            System.IO.Directory.CreateDirectory(mappingsDir);
            System.IO.Directory.CreateDirectory(spritesDir);

            try
            {
                // Create mapping file with a section
                var mappingJson = @"{
                    ""job"": ""Knight_Male"",
                    ""sprite"": ""battle_knight_m_spr.bin"",
                    ""sections"": [
                        {
                            ""name"": ""Cape"",
                            ""displayName"": ""Cape"",
                            ""indices"": [3, 4, 5],
                            ""roles"": [""shadow"", ""base"", ""highlight""]
                        }
                    ]
                }";
                System.IO.File.WriteAllText(System.IO.Path.Combine(mappingsDir, "Knight_Male.json"), mappingJson);

                // Create sprite file (needs at least 512 bytes for palette + sprite data)
                var spriteData = new byte[2048];
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(spritesDir, "battle_knight_m_spr.bin"), spriteData);

                using var panel = new ThemeEditorPanel(mappingsDir, spritesDir);
                var templateDropdown = panel.Controls.OfType<ComboBox>().First(c => c.Name == "TemplateDropdown");
                var spritePreview = panel.Controls.OfType<PictureBox>().First(c => c.Name == "SpritePreview");

                // Select template to load sprite and generate color pickers
                templateDropdown.SelectedItem = "Knight Male";
                Assert.NotNull(panel.PaletteModifier);
                Assert.True(panel.PaletteModifier.IsLoaded);

                // Get the color picker for Cape section
                var sectionPanel = panel.Controls.OfType<Panel>().First(c => c.Name == "SectionColorPickersPanel");
                var capePicker = sectionPanel.Controls.OfType<HslColorPicker>().First(c => c.SectionName == "Cape");

                var initialImage = spritePreview.Image;
                Assert.NotNull(initialImage);

                // Act - change the color picker value
                capePicker.Hue = 200; // Change hue to trigger ColorChanged event

                // Assert - preview should have been updated (new bitmap instance)
                Assert.NotNull(spritePreview.Image);
                // The image should be different because palette was modified
                Assert.NotSame(initialImage, spritePreview.Image);
            }
            finally
            {
                System.IO.Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        [STAThread]
        public void ConfigurationForm_ThemeEditorPanel_HasSpritesDirectory()
        {
            // Arrange
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FFTColorCustomizerTest_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(tempDir);

            // Create mock directory structure
            var mappingsDir = System.IO.Path.Combine(tempDir, "Data", "SectionMappings");
            var spritesDir = System.IO.Path.Combine(tempDir, "FFTIVC", "data", "enhanced", "fftpack", "unit", "sprites_original");
            System.IO.Directory.CreateDirectory(mappingsDir);
            System.IO.Directory.CreateDirectory(spritesDir);

            // Create a mapping file and sprite file
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(mappingsDir, "Squire_Male.json"),
                "{\"job\":\"Squire_Male\",\"sprite\":\"battle_mina_m_spr.bin\",\"sections\":[]}"
            );
            System.IO.File.WriteAllBytes(
                System.IO.Path.Combine(spritesDir, "battle_mina_m_spr.bin"),
                new byte[2048]
            );

            try
            {
                var config = new Config();

                // Act
                using var form = new ConfigurationForm(config, null, tempDir);
                var themeEditorPanel = FindControlRecursive<ThemeEditorPanel>(form);

                Assert.NotNull(themeEditorPanel);

                // Select the template
                var dropdown = themeEditorPanel.Controls.OfType<ComboBox>()
                    .First(c => c.Name == "TemplateDropdown");
                dropdown.SelectedItem = "Squire Male";

                // Assert - PaletteModifier should be loaded
                Assert.NotNull(themeEditorPanel.PaletteModifier);
                Assert.True(themeEditorPanel.PaletteModifier.IsLoaded);

                // Assert - sprite preview should have an image
                var spritePreview = themeEditorPanel.Controls.OfType<PictureBox>()
                    .First(c => c.Name == "SpritePreview");
                Assert.NotNull(spritePreview.Image);
            }
            finally
            {
                System.IO.Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_WhenTemplateSelected_GeneratesColorPickersForEachSection()
        {
            // Arrange - create temp directories with mapping and sprite files
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ThemeEditorTest_" + Guid.NewGuid().ToString("N"));
            var mappingsDir = System.IO.Path.Combine(tempDir, "mappings");
            var spritesDir = System.IO.Path.Combine(tempDir, "sprites");
            System.IO.Directory.CreateDirectory(mappingsDir);
            System.IO.Directory.CreateDirectory(spritesDir);

            try
            {
                // Create mapping file with 3 sections
                var mappingJson = @"{
                    ""job"": ""Knight_Male"",
                    ""sprite"": ""battle_knight_m_spr.bin"",
                    ""sections"": [
                        { ""name"": ""Cape"", ""displayName"": ""Cape"", ""indices"": [3, 4, 5], ""roles"": [""shadow"", ""base"", ""highlight""] },
                        { ""name"": ""Boots"", ""displayName"": ""Boots"", ""indices"": [6, 7], ""roles"": [""base"", ""highlight""] },
                        { ""name"": ""Armor"", ""displayName"": ""Armor"", ""indices"": [8, 9, 10], ""roles"": [""shadow"", ""base"", ""highlight""] }
                    ]
                }";
                System.IO.File.WriteAllText(System.IO.Path.Combine(mappingsDir, "Knight_Male.json"), mappingJson);

                // Create minimal sprite file
                var spriteData = new byte[1024];
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(spritesDir, "battle_knight_m_spr.bin"), spriteData);

                using var panel = new ThemeEditorPanel(mappingsDir, spritesDir);
                var templateDropdown = panel.Controls.OfType<ComboBox>().First(c => c.Name == "TemplateDropdown");

                // Act - select the template
                templateDropdown.SelectedItem = "Knight Male";

                // Assert - should have 3 HslColorPickers in the section panel
                var colorPickersPanel = panel.Controls.OfType<Panel>().First(c => c.Name == "SectionColorPickersPanel");
                var colorPickers = colorPickersPanel.Controls.OfType<HslColorPicker>().ToList();

                Assert.Equal(3, colorPickers.Count);
                Assert.Contains(colorPickers, p => p.SectionName == "Cape");
                Assert.Contains(colorPickers, p => p.SectionName == "Boots");
                Assert.Contains(colorPickers, p => p.SectionName == "Armor");
            }
            finally
            {
                System.IO.Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_LinkedSections_ShowsOnlyPrimaryPicker()
        {
            // Arrange - create temp directories with mapping file containing linked sections
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ThemeEditorTest_" + Guid.NewGuid().ToString("N"));
            var mappingsDir = System.IO.Path.Combine(tempDir, "mappings");
            var spritesDir = System.IO.Path.Combine(tempDir, "sprites");
            System.IO.Directory.CreateDirectory(mappingsDir);
            System.IO.Directory.CreateDirectory(spritesDir);

            try
            {
                // Create mapping with Hood linking to ArmBands (like Squire)
                var mappingJson = @"{
                    ""job"": ""Squire_Male"",
                    ""sprite"": ""battle_mina_m_spr.bin"",
                    ""sections"": [
                        {
                            ""name"": ""Hood"",
                            ""displayName"": ""Hood and Arm Bands"",
                            ""indices"": [8, 9, 10],
                            ""roles"": [""shadow"", ""base"", ""highlight""],
                            ""linkedTo"": ""ArmBands""
                        },
                        {
                            ""name"": ""ArmBands"",
                            ""displayName"": ""Arm Bands"",
                            ""indices"": [4],
                            ""roles"": [""base""],
                            ""linkedTo"": ""Hood""
                        }
                    ]
                }";
                System.IO.File.WriteAllText(System.IO.Path.Combine(mappingsDir, "Squire_Male.json"), mappingJson);

                // Create minimal sprite file
                var spriteData = new byte[2048];
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(spritesDir, "battle_mina_m_spr.bin"), spriteData);

                using var panel = new ThemeEditorPanel(mappingsDir, spritesDir);
                var templateDropdown = panel.Controls.OfType<ComboBox>().First(c => c.Name == "TemplateDropdown");

                // Act - select the template
                templateDropdown.SelectedItem = "Squire Male";

                // Assert - should have only 1 picker (Hood) since ArmBands is linked
                var colorPickersPanel = panel.Controls.OfType<Panel>().First(c => c.Name == "SectionColorPickersPanel");
                var colorPickers = colorPickersPanel.Controls.OfType<HslColorPicker>().ToList();

                Assert.Single(colorPickers);
                Assert.Equal("Hood and Arm Bands", colorPickers[0].SectionName);
            }
            finally
            {
                System.IO.Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        [STAThread]
        public void ConfigurationForm_ThemeEditorSection_ContainsThemeEditorPanel()
        {
            // Arrange
            var config = new Config();

            // Act
            using var form = new ConfigurationForm(config);

            // Assert - Find the ThemeEditorPanel in the form's control hierarchy
            var themeEditorPanel = FindControlRecursive<ThemeEditorPanel>(form);
            Assert.NotNull(themeEditorPanel);
        }

        [Fact]
        public void ThemeEditorPanel_HasProperLayout_WithMinimumHeight()
        {
            // Arrange & Act
            var panel = new ThemeEditorPanel();

            // Assert - Panel should have a reasonable minimum height for all components
            Assert.True(panel.MinimumSize.Height >= 400, "ThemeEditorPanel should have minimum height of 400px");
        }

        [Fact]
        public void ThemeEditorPanel_TemplateDropdown_HasVisibleSize()
        {
            // Arrange & Act
            var panel = new ThemeEditorPanel();
            var dropdown = panel.Controls.Find("TemplateDropdown", false).FirstOrDefault() as ComboBox;

            // Assert - Dropdown should have reasonable visible dimensions (compact layout)
            Assert.NotNull(dropdown);
            Assert.True(dropdown.Width >= 100, "Template dropdown should have width of at least 100px");
        }

        [Fact]
        public void ThemeEditorPanel_SpritePreview_HasVisibleSize()
        {
            // Arrange & Act
            var panel = new ThemeEditorPanel();
            var preview = panel.Controls.Find("SpritePreview", false).FirstOrDefault() as PictureBox;

            // Assert - Sprite preview should be sized for 6x scaled sprite (32x40 -> 192x240)
            // This makes the preview take up approximately 1/3 of a typical panel width
            Assert.NotNull(preview);
            Assert.True(preview.Width >= 192, "Sprite preview should have width of at least 192px (6x scale)");
            Assert.True(preview.Height >= 240, "Sprite preview should have height of at least 240px (6x scale)");
        }

        [Fact]
        public void ThemeEditorPanel_ThemeNameInput_HasVisibleWidth()
        {
            // Arrange & Act
            var panel = new ThemeEditorPanel();
            var input = panel.Controls.Find("ThemeNameInput", false).FirstOrDefault() as TextBox;

            // Assert - Theme name input should have reasonable width (compact layout)
            Assert.NotNull(input);
            Assert.True(input.Width >= 100, "Theme name input should have width of at least 100px");
        }

        [Fact]
        public void ThemeEditorPanel_ControlsArePositioned()
        {
            // Arrange & Act
            var panel = new ThemeEditorPanel();
            var dropdown = panel.Controls.Find("TemplateDropdown", false).FirstOrDefault();
            var preview = panel.Controls.Find("SpritePreview", false).FirstOrDefault();
            var colorPickersPanel = panel.Controls.Find("SectionColorPickersPanel", false).FirstOrDefault();

            // Assert - Controls should have explicit positions set
            Assert.NotNull(dropdown);
            Assert.NotNull(preview);
            Assert.NotNull(colorPickersPanel);

            // Dropdown should be at the top
            Assert.True(dropdown.Top >= 0 && dropdown.Top <= 20, "Template dropdown should be near the top");

            // Preview should be below dropdown on the left
            Assert.True(preview.Top > dropdown.Bottom, "Sprite preview should be below dropdown");

            // Color pickers panel should be to the right of the preview
            Assert.True(colorPickersPanel.Left > preview.Left, "Color pickers panel should be to the right of sprite preview");
        }

        [Fact]
        public void ConfigurationForm_ThemeEditorPanel_HasMappingsDirectory()
        {
            // Arrange
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FFTColorCustomizerTest_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(tempDir);

            // Create a mock SectionMappings directory with a mapping file
            var mappingsDir = System.IO.Path.Combine(tempDir, "Data", "SectionMappings");
            System.IO.Directory.CreateDirectory(mappingsDir);
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(mappingsDir, "Squire_Male.json"),
                "{\"job\":\"Squire_Male\",\"sprite\":\"battle_mina_m_spr.bin\",\"sections\":[]}"
            );

            try
            {
                var config = new Config();

                // Act
                using var form = new ConfigurationForm(config, null, tempDir);
                var themeEditorPanel = FindControlRecursive<ThemeEditorPanel>(form);

                // Assert
                Assert.NotNull(themeEditorPanel);
                var dropdown = themeEditorPanel.Controls.Find("TemplateDropdown", false).FirstOrDefault() as ComboBox;
                Assert.NotNull(dropdown);
                Assert.True(dropdown.Items.Count > 0, "Template dropdown should have items when mappings directory exists");
            }
            finally
            {
                System.IO.Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_TemplateDropdown_DisplaysHumanReadableNames()
        {
            // Arrange - create temp directory with test mapping files
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ThemeEditorTest_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(tempDir);
            try
            {
                // Need valid JSON since Squire_Male will be auto-selected and parsed
                var validMappingJson = @"{""job"":""Squire_Male"",""sprite"":""battle_mina_m_spr.bin"",""sections"":[]}";
                System.IO.File.WriteAllText(System.IO.Path.Combine(tempDir, "Squire_Male.json"), validMappingJson);
                System.IO.File.WriteAllText(System.IO.Path.Combine(tempDir, "Knight_Female.json"), @"{""job"":""Knight_Female"",""sprite"":""battle_knight_w_spr.bin"",""sections"":[]}");

                // Act
                using var panel = new ThemeEditorPanel(tempDir);

                // Assert - dropdown should show human-readable names, not snake_case
                var templateDropdown = panel.Controls.OfType<ComboBox>()
                    .FirstOrDefault(c => c.Name == "TemplateDropdown");

                Assert.NotNull(templateDropdown);
                Assert.Equal(2, templateDropdown.Items.Count);
                Assert.Contains("Knight Female", templateDropdown.Items.Cast<string>());
                Assert.Contains("Squire Male", templateDropdown.Items.Cast<string>());
                // Should NOT contain snake_case versions
                Assert.DoesNotContain("Knight_Female", templateDropdown.Items.Cast<string>());
                Assert.DoesNotContain("Squire_Male", templateDropdown.Items.Cast<string>());
            }
            finally
            {
                System.IO.Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_HasTemplateLabelBeforeDropdown()
        {
            // Arrange & Act
            using var panel = new ThemeEditorPanel();

            // Assert - Should have "Template:" label
            var templateLabel = panel.Controls.OfType<Label>()
                .FirstOrDefault(l => l.Text == "Template:");

            Assert.NotNull(templateLabel);

            // Label should be to the left of the dropdown
            var dropdown = panel.Controls.OfType<ComboBox>()
                .First(c => c.Name == "TemplateDropdown");
            Assert.True(templateLabel.Left < dropdown.Left, "Template label should be to the left of dropdown");
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_HasThemeNameLabelBeforeInput()
        {
            // Arrange & Act
            using var panel = new ThemeEditorPanel();

            // Assert - Should have "Theme Name:" label
            var themeNameLabel = panel.Controls.OfType<Label>()
                .FirstOrDefault(l => l.Text == "Theme Name:");

            Assert.NotNull(themeNameLabel);

            // Label should be to the left of the input
            var input = panel.Controls.OfType<TextBox>()
                .First(c => c.Name == "ThemeNameInput");
            Assert.True(themeNameLabel.Left < input.Left, "Theme Name label should be to the left of input");
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_HasWarningLabel()
        {
            // Arrange & Act
            using var panel = new ThemeEditorPanel();

            // Assert - Should have warning label about themes being immutable
            var warningLabel = panel.Controls.OfType<Label>()
                .FirstOrDefault(l => l.Text.Contains("Once saved"));

            Assert.NotNull(warningLabel);
            Assert.Contains("cannot be edited", warningLabel.Text);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_RotationButtonsAreCenteredBelowPreview()
        {
            // Arrange & Act
            using var panel = new ThemeEditorPanel();

            var preview = panel.Controls.OfType<PictureBox>()
                .First(c => c.Name == "SpritePreview");
            var leftButton = panel.Controls.OfType<Button>()
                .First(c => c.Name == "RotateLeftButton");
            var rightButton = panel.Controls.OfType<Button>()
                .First(c => c.Name == "RotateRightButton");

            // Assert - Buttons should be below the preview
            Assert.True(leftButton.Top > preview.Bottom, "Rotate buttons should be below sprite preview");

            // Buttons should be roughly centered under the preview
            var previewCenter = preview.Left + (preview.Width / 2);
            var buttonsCenter = leftButton.Left + ((rightButton.Right - leftButton.Left) / 2);
            var tolerance = 20;
            Assert.True(Math.Abs(previewCenter - buttonsCenter) <= tolerance,
                $"Rotate buttons should be centered under preview. Preview center: {previewCenter}, Buttons center: {buttonsCenter}");
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_SpritePreviewHasNoBorder()
        {
            // Arrange & Act
            using var panel = new ThemeEditorPanel();

            var preview = panel.Controls.OfType<PictureBox>()
                .First(c => c.Name == "SpritePreview");

            // Assert - Preview should have no border for cleaner look
            Assert.Equal(BorderStyle.None, preview.BorderStyle);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_ColorPickersPanelHasBorder()
        {
            // Arrange & Act
            using var panel = new ThemeEditorPanel();

            var colorPickersPanel = panel.Controls.OfType<Panel>()
                .First(c => c.Name == "SectionColorPickersPanel");

            // Assert - Color pickers panel should have a border
            Assert.Equal(BorderStyle.FixedSingle, colorPickersPanel.BorderStyle);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_ColorPickersPanel_ExpandsToFillRemainingWidth()
        {
            // Arrange - Create panel with a set width
            using var panel = new ThemeEditorPanel();
            panel.Width = 600;

            // Act - Force layout
            panel.PerformLayout();

            // Assert
            var preview = panel.Controls.OfType<PictureBox>()
                .First(c => c.Name == "SpritePreview");
            var colorPickersPanel = panel.Controls.OfType<Panel>()
                .First(c => c.Name == "SectionColorPickersPanel");

            // Color pickers panel should extend close to the right edge of the panel
            // Allow some padding (20px) on the right
            var expectedMinRight = panel.Width - 20;
            Assert.True(colorPickersPanel.Right >= expectedMinRight,
                $"Color pickers panel should extend to the right edge. Panel width: {panel.Width}, ColorPickersPanel.Right: {colorPickersPanel.Right}, Expected min: {expectedMinRight}");
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_ExpandsToFillParentWidth()
        {
            // Arrange - Create a parent panel with known width
            using var parent = new Panel { Width = 400 };
            using var picker = new HslColorPicker();
            parent.Controls.Add(picker);

            // Act - Set picker to dock fill or use anchoring
            picker.Dock = DockStyle.Top;
            parent.PerformLayout();

            // Assert - Picker should fill the parent width
            Assert.Equal(parent.ClientSize.Width, picker.Width);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_SlidersExpandToFillWidth()
        {
            // Arrange - Create picker with a wide width
            using var picker = new HslColorPicker();
            picker.Width = 400;

            // Act - Force layout
            picker.PerformLayout();

            // Assert - Sliders should expand to fill available width (minus label space and padding)
            var hueSlider = picker.Controls.OfType<TrackBar>().First(c => c.Name == "HueSlider");
            var satSlider = picker.Controls.OfType<TrackBar>().First(c => c.Name == "SaturationSlider");
            var lightSlider = picker.Controls.OfType<TrackBar>().First(c => c.Name == "LightnessSlider");

            // Sliders should extend close to the right edge (within 20px padding)
            var expectedMinRight = picker.Width - 20;
            Assert.True(hueSlider.Right >= expectedMinRight,
                $"Hue slider should extend to right edge. Picker width: {picker.Width}, Slider.Right: {hueSlider.Right}");
            Assert.True(satSlider.Right >= expectedMinRight,
                $"Saturation slider should extend to right edge. Picker width: {picker.Width}, Slider.Right: {satSlider.Right}");
            Assert.True(lightSlider.Right >= expectedMinRight,
                $"Lightness slider should extend to right edge. Picker width: {picker.Width}, Slider.Right: {lightSlider.Right}");
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_ColorPickers_AreDocked()
        {
            // Arrange - create temp directories with mapping and sprite files
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ThemeEditorTest_" + Guid.NewGuid().ToString("N"));
            var mappingsDir = System.IO.Path.Combine(tempDir, "mappings");
            var spritesDir = System.IO.Path.Combine(tempDir, "sprites");
            System.IO.Directory.CreateDirectory(mappingsDir);
            System.IO.Directory.CreateDirectory(spritesDir);

            try
            {
                // Create mapping file with sections
                var mappingJson = @"{
                    ""job"": ""Knight_Male"",
                    ""sprite"": ""battle_knight_m_spr.bin"",
                    ""sections"": [
                        { ""name"": ""Cape"", ""displayName"": ""Cape"", ""indices"": [3, 4, 5], ""roles"": [""shadow"", ""base"", ""highlight""] },
                        { ""name"": ""Boots"", ""displayName"": ""Boots"", ""indices"": [6, 7], ""roles"": [""base"", ""highlight""] }
                    ]
                }";
                System.IO.File.WriteAllText(System.IO.Path.Combine(mappingsDir, "Knight_Male.json"), mappingJson);

                // Create minimal sprite file
                var spriteData = new byte[2048];
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(spritesDir, "battle_knight_m_spr.bin"), spriteData);

                using var panel = new ThemeEditorPanel(mappingsDir, spritesDir);
                panel.Width = 600;
                panel.PerformLayout();

                var templateDropdown = panel.Controls.OfType<ComboBox>().First(c => c.Name == "TemplateDropdown");

                // Act - select the template
                templateDropdown.SelectedItem = "Knight Male";

                // Assert - color pickers should be docked top to fill width
                var colorPickersPanel = panel.Controls.OfType<Panel>().First(c => c.Name == "SectionColorPickersPanel");
                var colorPickers = colorPickersPanel.Controls.OfType<HslColorPicker>().ToList();

                Assert.Equal(2, colorPickers.Count);
                foreach (var picker in colorPickers)
                {
                    Assert.Equal(DockStyle.Top, picker.Dock);
                }
            }
            finally
            {
                System.IO.Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_ColorPickersPanel_HasSufficientHeight()
        {
            // Arrange - create temp directories with mapping and sprite files
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ThemeEditorTest_" + Guid.NewGuid().ToString("N"));
            var mappingsDir = System.IO.Path.Combine(tempDir, "mappings");
            var spritesDir = System.IO.Path.Combine(tempDir, "sprites");
            System.IO.Directory.CreateDirectory(mappingsDir);
            System.IO.Directory.CreateDirectory(spritesDir);

            try
            {
                // Create mapping file with 3 sections
                var mappingJson = @"{
                    ""job"": ""Knight_Male"",
                    ""sprite"": ""battle_knight_m_spr.bin"",
                    ""sections"": [
                        { ""name"": ""Cape"", ""displayName"": ""Cape"", ""indices"": [3, 4, 5], ""roles"": [""shadow"", ""base"", ""highlight""] },
                        { ""name"": ""Boots"", ""displayName"": ""Boots"", ""indices"": [6, 7], ""roles"": [""base"", ""highlight""] },
                        { ""name"": ""Armor"", ""displayName"": ""Armor"", ""indices"": [8, 9, 10], ""roles"": [""shadow"", ""base"", ""highlight""] }
                    ]
                }";
                System.IO.File.WriteAllText(System.IO.Path.Combine(mappingsDir, "Knight_Male.json"), mappingJson);

                // Create minimal sprite file
                var spriteData = new byte[2048];
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(spritesDir, "battle_knight_m_spr.bin"), spriteData);

                using var panel = new ThemeEditorPanel(mappingsDir, spritesDir);
                panel.Width = 600;
                panel.Height = 500;
                panel.PerformLayout();

                var templateDropdown = panel.Controls.OfType<ComboBox>().First(c => c.Name == "TemplateDropdown");

                // Act - select the template
                templateDropdown.SelectedItem = "Knight Male";

                // Assert - color pickers panel should be tall enough to show all pickers
                var colorPickersPanel = panel.Controls.OfType<Panel>().First(c => c.Name == "SectionColorPickersPanel");
                var colorPickers = colorPickersPanel.Controls.OfType<HslColorPicker>().ToList();

                Assert.Equal(3, colorPickers.Count);

                // Panel should have height to accommodate bottom controls (theme name, buttons, warning)
                // and leave room for color pickers. Each picker is about 160px tall.
                // With 3 pickers and scrolling enabled, the panel height should be reasonable
                Assert.True(colorPickersPanel.Height >= 200,
                    $"Color pickers panel should have reasonable height. Actual: {colorPickersPanel.Height}");
            }
            finally
            {
                System.IO.Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_ColorPickersPanelTop_MatchesSpritePreviewTop()
        {
            // Arrange & Act
            using var panel = new ThemeEditorPanel();

            var preview = panel.Controls.OfType<PictureBox>()
                .First(c => c.Name == "SpritePreview");
            var colorPickersPanel = panel.Controls.OfType<Panel>()
                .First(c => c.Name == "SectionColorPickersPanel");

            // Assert - Color pickers panel should start at the same vertical position as sprite preview
            // so they align visually on the same row (height may differ due to scrolling needs)
            Assert.Equal(preview.Top, colorPickersPanel.Top);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_SpritePreview_UsesZoomSizeMode()
        {
            // Arrange & Act
            using var panel = new ThemeEditorPanel();

            var preview = panel.Controls.OfType<PictureBox>()
                .First(c => c.Name == "SpritePreview");

            // Assert - Preview should use Zoom mode to scale the sprite to fill the box
            Assert.Equal(PictureBoxSizeMode.Zoom, preview.SizeMode);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_TemplateDropdown_IsDropDownListStyle()
        {
            // Arrange & Act
            using var panel = new ThemeEditorPanel();

            // Assert - Dropdown should be DropDownList style (not editable)
            var templateDropdown = panel.Controls.OfType<ComboBox>()
                .First(c => c.Name == "TemplateDropdown");

            Assert.Equal(ComboBoxStyle.DropDownList, templateDropdown.DropDownStyle);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_TemplateDropdown_DefaultsToSquireMale()
        {
            // Arrange - create temp directory with Squire_Male and other mapping files
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ThemeEditorTest_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(tempDir);
            try
            {
                // Need valid JSON since Squire_Male will be auto-selected and parsed
                System.IO.File.WriteAllText(System.IO.Path.Combine(tempDir, "Squire_Male.json"), @"{""job"":""Squire_Male"",""sprite"":""battle_mina_m_spr.bin"",""sections"":[]}");
                System.IO.File.WriteAllText(System.IO.Path.Combine(tempDir, "Knight_Female.json"), @"{""job"":""Knight_Female"",""sprite"":""battle_knight_w_spr.bin"",""sections"":[]}");
                System.IO.File.WriteAllText(System.IO.Path.Combine(tempDir, "Archer_Male.json"), @"{""job"":""Archer_Male"",""sprite"":""battle_archer_m_spr.bin"",""sections"":[]}");

                // Act
                using var panel = new ThemeEditorPanel(tempDir);

                // Assert - Squire Male should be selected by default
                var templateDropdown = panel.Controls.OfType<ComboBox>()
                    .First(c => c.Name == "TemplateDropdown");

                Assert.Equal("Squire Male", templateDropdown.SelectedItem);
            }
            finally
            {
                System.IO.Directory.Delete(tempDir, true);
            }
        }

        private T? FindControlRecursive<T>(Control parent) where T : Control
        {
            foreach (Control control in parent.Controls)
            {
                if (control is T found)
                    return found;

                var result = FindControlRecursive<T>(control);
                if (result != null)
                    return result;
            }
            return null;
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_SectionLabel_HasLargerFont()
        {
            // Arrange & Act
            using var picker = new HslColorPicker { SectionName = "Hood" };

            // Assert - Section label should have a larger font (at least 10pt)
            var sectionLabel = picker.Controls.OfType<Label>()
                .First(c => c.Name == "SectionHeaderLabel");

            Assert.True(sectionLabel.Font.Size >= 10,
                $"Section label font should be at least 10pt, was {sectionLabel.Font.Size}pt");
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_SectionLabel_HasRedForeColor()
        {
            // Arrange & Act
            using var picker = new HslColorPicker { SectionName = "Hood" };

            // Assert - Section label should have red text color
            var sectionLabel = picker.Controls.OfType<Label>()
                .First(c => c.Name == "SectionHeaderLabel");

            Assert.Equal(Color.DarkRed, sectionLabel.ForeColor);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_HasResetButton()
        {
            // Arrange & Act
            using var picker = new HslColorPicker { SectionName = "Hood" };

            // Assert - Should have a reset button
            var resetButton = picker.Controls.OfType<Button>()
                .FirstOrDefault(c => c.Name == "ResetButton");

            Assert.NotNull(resetButton);
            Assert.Equal("Reset", resetButton.Text);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_ResetButton_RestoresOriginalColor()
        {
            // Arrange
            using var picker = new HslColorPicker { SectionName = "Hood" };
            var originalColor = Color.FromArgb(128, 64, 32);
            picker.SetColorSilent(originalColor);
            picker.StoreOriginalColor(); // Store the original color

            // Change the color
            picker.Hue = 200;
            picker.Saturation = 80;
            picker.Lightness = 60;

            // Act - Click reset
            var resetButton = picker.Controls.OfType<Button>()
                .First(c => c.Name == "ResetButton");
            resetButton.PerformClick();

            // Assert - Color should be restored to original
            var restoredColor = picker.CurrentColor;
            // Allow tolerance of 10 due to HSL conversion rounding (RGB->HSL->RGB loses precision)
            Assert.True(Math.Abs(restoredColor.R - originalColor.R) <= 10,
                $"Red channel should be close to original. Expected ~{originalColor.R}, got {restoredColor.R}");
            Assert.True(Math.Abs(restoredColor.G - originalColor.G) <= 10,
                $"Green channel should be close to original. Expected ~{originalColor.G}, got {restoredColor.G}");
            Assert.True(Math.Abs(restoredColor.B - originalColor.B) <= 10,
                $"Blue channel should be close to original. Expected ~{originalColor.B}, got {restoredColor.B}");
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_StoreOriginalColor_StoresSliderValues_NotConvertedColor()
        {
            // Arrange - This test verifies the bug where StoreOriginalColor was reading
            // from CurrentColor (which converts HSL->RGB) instead of storing slider values.
            // The double conversion (RGB->HSL->RGB) causes precision loss.
            using var picker = new HslColorPicker { SectionName = "Hood" };
            var originalColor = Color.FromArgb(128, 64, 32);

            // Act - Store original immediately after setting (simulates real usage)
            picker.SetColorSilent(originalColor);
            picker.StoreOriginalColor();

            // Now reset without changing anything - should get EXACT same sliders
            var hueBeforeReset = picker.Hue;
            var satBeforeReset = picker.Saturation;
            var lightBeforeReset = picker.Lightness;

            var resetButton = picker.Controls.OfType<Button>()
                .First(c => c.Name == "ResetButton");
            resetButton.PerformClick();

            // Assert - Sliders should be EXACTLY the same (no drift from double conversion)
            Assert.Equal(hueBeforeReset, picker.Hue);
            Assert.Equal(satBeforeReset, picker.Saturation);
            Assert.Equal(lightBeforeReset, picker.Lightness);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_SetColorSilent_AutomaticallyStoresOriginalColor()
        {
            // Arrange - When SetColorSilent is called, it should automatically store
            // the original values so reset works correctly without explicit StoreOriginalColor call
            using var picker = new HslColorPicker { SectionName = "Hood" };
            var originalColor = Color.FromArgb(128, 64, 32);

            // Act - Set color (this simulates loading from palette)
            picker.SetColorSilent(originalColor);

            // Capture current values
            var hueAfterSet = picker.Hue;
            var satAfterSet = picker.Saturation;
            var lightAfterSet = picker.Lightness;

            // Click reset without having called StoreOriginalColor explicitly
            var resetButton = picker.Controls.OfType<Button>()
                .First(c => c.Name == "ResetButton");
            resetButton.PerformClick();

            // Assert - Should restore to the values set by SetColorSilent
            Assert.Equal(hueAfterSet, picker.Hue);
            Assert.Equal(satAfterSet, picker.Saturation);
            Assert.Equal(lightAfterSet, picker.Lightness);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_ResetButton_DoesNotFireColorChanged_WhenValuesUnchanged()
        {
            // Arrange - The bug: Reset fires ColorChanged even when values haven't changed,
            // which triggers ApplySectionColor and modifies the palette unnecessarily.
            using var picker = new HslColorPicker { SectionName = "Hood" };
            var originalColor = Color.FromArgb(128, 64, 32);
            picker.SetColorSilent(originalColor);

            var eventCount = 0;
            picker.ColorChanged += (s, e) => eventCount++;

            // Act - Click reset when values are already at original (no user changes made)
            var resetButton = picker.Controls.OfType<Button>()
                .First(c => c.Name == "ResetButton");
            resetButton.PerformClick();

            // Assert - ColorChanged should NOT fire because values didn't change
            Assert.Equal(0, eventCount);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_HasBottomMargin_ForSectionSeparation()
        {
            // Arrange & Act
            using var picker = new HslColorPicker { SectionName = "Hood" };

            // Assert - Picker should have bottom margin/padding for visual separation
            Assert.True(picker.Margin.Bottom >= 10,
                $"HslColorPicker should have at least 10px bottom margin for section separation, was {picker.Margin.Bottom}px");
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_SectionHeader_HasTopPadding()
        {
            // Arrange & Act
            using var picker = new HslColorPicker { SectionName = "Hood" };

            // Assert - Section header should have padding from the top to separate from previous section
            var sectionLabel = picker.Controls.OfType<Label>()
                .First(c => c.Name == "SectionHeaderLabel");

            Assert.True(sectionLabel.Top >= 5,
                $"Section header should have at least 5px top padding, was {sectionLabel.Top}px");
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_Sliders_HaveNoTickMarks()
        {
            // Arrange & Act
            using var picker = new HslColorPicker { SectionName = "Hood" };

            // Assert - Sliders should not display tick marks (cleaner appearance)
            var sliders = picker.Controls.OfType<TrackBar>().ToList();
            Assert.Equal(3, sliders.Count);

            foreach (var slider in sliders)
            {
                Assert.Equal(TickStyle.None, slider.TickStyle);
            }
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_Sliders_IgnoreMouseWheel()
        {
            // Arrange
            using var picker = new HslColorPicker { SectionName = "Hood" };
            picker.Hue = 180;
            picker.Saturation = 50;
            picker.Lightness = 50;

            var hueSlider = picker.Controls.OfType<NoScrollTrackBar>().First(c => c.Name == "HueSlider");

            // Act - Simulate mouse wheel event
            var wheelArgs = new MouseEventArgs(MouseButtons.None, 0, 0, 0, 120); // 120 = one notch up

            // Use reflection to invoke OnMouseWheel since it's protected
            var onMouseWheelMethod = typeof(Control).GetMethod("OnMouseWheel",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            onMouseWheelMethod?.Invoke(hueSlider, new object[] { wheelArgs });

            // Assert - Value should NOT have changed (mouse wheel is disabled)
            Assert.Equal(180, picker.Hue);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_SectionColorPickersPanel_HasAdequateHeight()
        {
            // Arrange & Act
            using var panel = new ThemeEditorPanel();

            // Assert - Color pickers panel should be tall enough for multiple sections
            var colorPickersPanel = panel.Controls.OfType<Panel>()
                .First(c => c.Name == "SectionColorPickersPanel");

            Assert.True(colorPickersPanel.Height >= 300,
                $"SectionColorPickersPanel should be at least 300px tall, was {colorPickersPanel.Height}px");
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_ThemeNameAndButtons_AreInTopRow()
        {
            // Arrange & Act
            using var panel = new ThemeEditorPanel();

            var templateDropdown = panel.Controls.OfType<ComboBox>()
                .First(c => c.Name == "TemplateDropdown");
            var themeNameInput = panel.Controls.OfType<TextBox>()
                .First(c => c.Name == "ThemeNameInput");
            var saveButton = panel.Controls.OfType<Button>()
                .First(c => c.Name == "SaveButton");

            // Assert - Theme name and buttons should be in the same row as template dropdown
            // (within 30px vertical tolerance for alignment)
            Assert.True(Math.Abs(themeNameInput.Top - templateDropdown.Top) <= 30,
                $"Theme name input should be in same row as template. Input.Top: {themeNameInput.Top}, Dropdown.Top: {templateDropdown.Top}");
            Assert.True(Math.Abs(saveButton.Top - templateDropdown.Top) <= 30,
                $"Save button should be in same row as template. Button.Top: {saveButton.Top}, Dropdown.Top: {templateDropdown.Top}");
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_WarningLabel_IsBelowButtons()
        {
            // Arrange & Act
            using var panel = new ThemeEditorPanel();

            var saveButton = panel.Controls.OfType<Button>()
                .First(c => c.Name == "SaveButton");
            var warningLabel = panel.Controls.OfType<Label>()
                .First(l => l.Text.Contains("Once saved"));

            // Assert - Warning label should be below the buttons row
            Assert.True(warningLabel.Top > saveButton.Bottom,
                $"Warning label should be below buttons. Warning.Top: {warningLabel.Top}, Button.Bottom: {saveButton.Bottom}");
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_HasSpritePreviewLabel()
        {
            // Arrange & Act
            using var panel = new ThemeEditorPanel();

            // Assert - Should have "Sprite Preview" label
            var spritePreviewLabel = panel.Controls.OfType<Label>()
                .FirstOrDefault(l => l.Text == "Sprite Preview");

            Assert.NotNull(spritePreviewLabel);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_HasColorCustomizerLabel()
        {
            // Arrange & Act
            using var panel = new ThemeEditorPanel();

            // Assert - Should have "Color Customizer" label
            var colorCustomizerLabel = panel.Controls.OfType<Label>()
                .FirstOrDefault(l => l.Text == "Color Customizer");

            Assert.NotNull(colorCustomizerLabel);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_NoImportFromClipboardButton()
        {
            // Arrange & Act
            using var panel = new ThemeEditorPanel();

            // Assert - Import from Clipboard button should not exist (removed for now)
            var importButton = panel.Controls.OfType<Button>()
                .FirstOrDefault(b => b.Name == "ImportClipboardButton");

            Assert.Null(importButton);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_ColorPickersPanel_ExtendsToBottom()
        {
            // Arrange & Act
            using var panel = new ThemeEditorPanel();
            panel.Height = 500;
            // Force layout update by calling the resize logic
            panel.Width = panel.Width; // Trigger layout

            var colorPickersPanel = panel.Controls.OfType<Panel>()
                .First(c => c.Name == "SectionColorPickersPanel");

            // Assert - Color pickers panel should extend close to the bottom (within 20px padding)
            // row3Top = 65, padding = 10, so expected height = 500 - 65 - 10 = 425
            var expectedMinBottom = panel.Height - 20;
            Assert.True(colorPickersPanel.Bottom >= expectedMinBottom,
                $"Color pickers panel should extend to bottom. Panel.Height: {panel.Height}, ColorPickers.Bottom: {colorPickersPanel.Bottom}");
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_SectionColorPickers_DisplayInJsonOrder()
        {
            // Arrange - The sections in the JSON should display in that exact order
            // (first section in JSON = first/top picker in UI)
            var tempDir = Path.Combine(Path.GetTempPath(), "ThemeEditorTest_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);

            try
            {
                // Create a test mapping with specific order
                var mappingJson = @"{
                    ""job"": ""Test_Job"",
                    ""sprite"": ""test.bin"",
                    ""sections"": [
                        { ""name"": ""First"", ""displayName"": ""First Section"", ""indices"": [1], ""roles"": [""base""] },
                        { ""name"": ""Second"", ""displayName"": ""Second Section"", ""indices"": [2], ""roles"": [""base""] },
                        { ""name"": ""Third"", ""displayName"": ""Third Section"", ""indices"": [3], ""roles"": [""base""] }
                    ]
                }";
                File.WriteAllText(Path.Combine(tempDir, "Test_Job.json"), mappingJson);

                using var panel = new ThemeEditorPanel(tempDir);

                // Select the test job
                var dropdown = panel.Controls.OfType<ComboBox>().First(c => c.Name == "TemplateDropdown");
                dropdown.SelectedIndex = dropdown.Items.IndexOf("Test Job");

                // Get the color pickers panel
                var colorPickersPanel = panel.Controls.OfType<Panel>().First(c => c.Name == "SectionColorPickersPanel");
                var pickers = colorPickersPanel.Controls.OfType<HslColorPicker>().ToList();

                // Assert - Should have 3 pickers in the correct visual order (top to bottom)
                Assert.Equal(3, pickers.Count);

                // Sort by Top position to get visual order
                var orderedPickers = pickers.OrderBy(p => p.Top).ToList();
                Assert.Equal("First Section", orderedPickers[0].SectionName);
                Assert.Equal("Second Section", orderedPickers[1].SectionName);
                Assert.Equal("Third Section", orderedPickers[2].SectionName);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_HasColorPreviewSwatch()
        {
            // Arrange & Act
            using var picker = new HslColorPicker();

            // Assert - Should have a Panel for color preview swatch
            var swatch = picker.Controls.OfType<Panel>()
                .FirstOrDefault(c => c.Name == "ColorPreviewSwatch");

            Assert.NotNull(swatch);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_ColorPreviewSwatch_IsPositionedBelowSliders()
        {
            // Arrange & Act
            using var picker = new HslColorPicker();

            var lightnessSlider = picker.Controls.OfType<TrackBar>()
                .First(c => c.Name == "LightnessSlider");
            var swatch = picker.Controls.OfType<Panel>()
                .First(c => c.Name == "ColorPreviewSwatch");

            // Assert - Swatch should be below the lightness slider
            Assert.True(swatch.Top > lightnessSlider.Bottom,
                $"Swatch should be below lightness slider. Swatch.Top: {swatch.Top}, Slider.Bottom: {lightnessSlider.Bottom}");

            // Assert - Swatch should be aligned with slider start (labelWidth = 70)
            Assert.Equal(70, swatch.Left);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_ColorPreviewSwatch_HasVisibleSize()
        {
            // Arrange & Act
            using var picker = new HslColorPicker();

            var swatch = picker.Controls.OfType<Panel>()
                .First(c => c.Name == "ColorPreviewSwatch");

            // Assert - Swatch should be a small square (30x30)
            Assert.Equal(30, swatch.Width);
            Assert.Equal(30, swatch.Height);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_ColorPreviewSwatch_DisplaysCurrentColor()
        {
            // Arrange
            using var picker = new HslColorPicker();

            // Act - Set to pure red
            picker.Hue = 0;
            picker.Saturation = 100;
            picker.Lightness = 50;

            var swatch = picker.Controls.OfType<Panel>()
                .First(c => c.Name == "ColorPreviewSwatch");

            // Assert - Swatch background should be pure red
            Assert.Equal(255, swatch.BackColor.R);
            Assert.Equal(0, swatch.BackColor.G);
            Assert.Equal(0, swatch.BackColor.B);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_ColorPreviewSwatch_HasBorder()
        {
            // Arrange & Act
            using var picker = new HslColorPicker();

            var swatch = picker.Controls.OfType<Panel>()
                .First(c => c.Name == "ColorPreviewSwatch");

            // Assert - Swatch should have a border for visibility
            Assert.Equal(BorderStyle.FixedSingle, swatch.BorderStyle);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_Height_AccommodatesSwatchWithoutClipping()
        {
            // Arrange & Act
            using var picker = new HslColorPicker();

            var swatch = picker.Controls.OfType<Panel>()
                .First(c => c.Name == "ColorPreviewSwatch");

            // Assert - Panel height should be greater than swatch bottom to prevent clipping
            Assert.True(picker.Height >= swatch.Bottom,
                $"Picker height ({picker.Height}) must be >= swatch bottom ({swatch.Bottom}) to prevent clipping");
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_HasHexInputTextBox()
        {
            // Arrange & Act
            using var picker = new HslColorPicker();

            // Assert - Should have a TextBox for hex color input
            var hexInput = picker.Controls.OfType<TextBox>()
                .FirstOrDefault(c => c.Name == "HexInput");

            Assert.NotNull(hexInput);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_HexInput_DisplaysCurrentColorAsHex()
        {
            // Arrange
            using var picker = new HslColorPicker();

            // Act - Set to pure red
            picker.Hue = 0;
            picker.Saturation = 100;
            picker.Lightness = 50;

            var hexInput = picker.Controls.OfType<TextBox>()
                .First(c => c.Name == "HexInput");

            // Assert - Should display #FF0000 for pure red
            Assert.Equal("#FF0000", hexInput.Text);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_HexInput_TypingValidHex_UpdatesSliders()
        {
            // Arrange
            using var picker = new HslColorPicker();
            var hexInput = picker.Controls.OfType<TextBox>()
                .First(c => c.Name == "HexInput");

            // Act - Type a hex color (pure green)
            hexInput.Text = "#00FF00";
            // Simulate leaving the field (triggers validation)
            hexInput.Focus();

            // Assert - Sliders should reflect pure green (H=120, S=100, L=50)
            Assert.Equal(120, picker.Hue);
            Assert.Equal(100, picker.Saturation);
            Assert.Equal(50, picker.Lightness);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_HexInput_InvalidHex_DoesNotChangeSliders()
        {
            // Arrange
            using var picker = new HslColorPicker();
            picker.Hue = 180;
            picker.Saturation = 50;
            picker.Lightness = 50;

            var hexInput = picker.Controls.OfType<TextBox>()
                .First(c => c.Name == "HexInput");

            // Act - Type invalid hex
            hexInput.Text = "invalid";

            // Assert - Sliders should remain unchanged
            Assert.Equal(180, picker.Hue);
            Assert.Equal(50, picker.Saturation);
            Assert.Equal(50, picker.Lightness);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_HasCopyButton()
        {
            // Arrange & Act
            using var picker = new HslColorPicker();

            // Assert - Should have a Copy button
            var copyButton = picker.Controls.OfType<Button>()
                .FirstOrDefault(c => c.Name == "CopyButton");

            Assert.NotNull(copyButton);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_HasPasteButton()
        {
            // Arrange & Act
            using var picker = new HslColorPicker();

            // Assert - Should have a Paste button
            var pasteButton = picker.Controls.OfType<Button>()
                .FirstOrDefault(c => c.Name == "PasteButton");

            Assert.NotNull(pasteButton);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_GetHexColor_ReturnsCurrentColorAsHex()
        {
            // Arrange
            using var picker = new HslColorPicker();
            picker.SetColorSilent(Color.FromArgb(255, 0, 0)); // Red

            // Act
            var hexColor = picker.GetHexColor();

            // Assert
            Assert.Equal("#FF0000", hexColor);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_HasCopyToClipboardMethod()
        {
            // Arrange
            using var picker = new HslColorPicker();

            // Act - Verify the method exists via reflection
            var method = typeof(HslColorPicker).GetMethod("CopyToClipboard");

            // Assert - Method should exist and be public
            Assert.NotNull(method);
            Assert.True(method.IsPublic);
            Assert.Equal(typeof(void), method.ReturnType);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_HasPasteFromClipboardMethod()
        {
            // Arrange
            using var picker = new HslColorPicker();

            // Act - Verify the method exists via reflection
            var method = typeof(HslColorPicker).GetMethod("PasteFromClipboard");

            // Assert - Method should exist and be public
            Assert.NotNull(method);
            Assert.True(method.IsPublic);
            Assert.Equal(typeof(void), method.ReturnType);
        }

        [Fact]
        [STAThread]
        public void HslColorPicker_PasteButton_HasClickHandler()
        {
            // Arrange
            using var picker = new HslColorPicker();

            var pasteButton = picker.Controls.OfType<Button>()
                .First(c => c.Name == "PasteButton");

            // Act - Verify the OnPasteClick handler exists via reflection
            var method = typeof(HslColorPicker).GetMethod("OnPasteClick",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Assert - Handler method should exist
            Assert.NotNull(method);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_HasThemeSavedEvent()
        {
            // Arrange & Act
            using var panel = new ThemeEditorPanel();

            // Assert - Panel should have ThemeSaved event
            var eventInfo = typeof(ThemeEditorPanel).GetEvent("ThemeSaved");
            Assert.NotNull(eventInfo);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_SaveButton_Click_RaisesThemeSavedEvent()
        {
            // Arrange
            using var panel = new ThemeEditorPanel();
            var eventRaised = false;
            panel.ThemeSaved += (sender, e) => eventRaised = true;

            // Set theme name (required for save to work)
            var themeNameInput = panel.Controls.OfType<TextBox>()
                .First(c => c.Name == "ThemeNameInput");
            themeNameInput.Text = "Test Theme";

            var saveButton = panel.Controls.OfType<Button>()
                .First(c => c.Name == "SaveButton");

            // Act
            saveButton.PerformClick();

            // Assert
            Assert.True(eventRaised);
        }

        [Fact]
        [STAThread]
        public void ThemeSavedEventArgs_ContainsJobNameThemeNameAndPaletteData()
        {
            // Arrange & Act
            var args = new ThemeSavedEventArgs("Knight_Male", "Ocean Blue", new byte[512]);

            // Assert
            Assert.Equal("Knight_Male", args.JobName);
            Assert.Equal("Ocean Blue", args.ThemeName);
            Assert.Equal(512, args.PaletteData.Length);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_ThemeSavedEvent_UsesThemeSavedEventArgs()
        {
            // Arrange
            var mappingsDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Data", "SectionMappings");
            var spritesDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Data", "Sprites");
            using var panel = new ThemeEditorPanel(mappingsDir, spritesDir);

            // Set theme name
            var themeNameInput = panel.Controls.OfType<TextBox>()
                .First(c => c.Name == "ThemeNameInput");
            themeNameInput.Text = "My Test Theme";

            ThemeSavedEventArgs? receivedArgs = null;
            panel.ThemeSaved += (sender, e) => receivedArgs = e as ThemeSavedEventArgs;

            var saveButton = panel.Controls.OfType<Button>()
                .First(c => c.Name == "SaveButton");

            // Act
            saveButton.PerformClick();

            // Assert
            Assert.NotNull(receivedArgs);
            Assert.Equal("My Test Theme", receivedArgs.ThemeName);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_SaveButton_DoesNotRaiseEvent_WhenThemeNameIsEmpty()
        {
            // Arrange
            using var panel = new ThemeEditorPanel();
            var eventRaised = false;
            panel.ThemeSaved += (sender, e) => eventRaised = true;

            var themeNameInput = panel.Controls.OfType<TextBox>()
                .First(c => c.Name == "ThemeNameInput");
            themeNameInput.Text = ""; // Empty theme name

            var saveButton = panel.Controls.OfType<Button>()
                .First(c => c.Name == "SaveButton");

            // Act
            saveButton.PerformClick();

            // Assert
            Assert.False(eventRaised);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_ThemeSavedEvent_IncludesJobName()
        {
            // Arrange
            var mappingsDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Data", "SectionMappings");
            var spritesDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Data", "Sprites");
            using var panel = new ThemeEditorPanel(mappingsDir, spritesDir);

            // Select a template
            var templateDropdown = panel.Controls.OfType<ComboBox>()
                .First(c => c.Name == "TemplateDropdown");
            var squireMaleIndex = -1;
            for (int i = 0; i < templateDropdown.Items.Count; i++)
            {
                if (templateDropdown.Items[i]?.ToString() == "Squire Male")
                {
                    squireMaleIndex = i;
                    break;
                }
            }
            if (squireMaleIndex >= 0)
                templateDropdown.SelectedIndex = squireMaleIndex;

            // Set theme name
            var themeNameInput = panel.Controls.OfType<TextBox>()
                .First(c => c.Name == "ThemeNameInput");
            themeNameInput.Text = "My Test Theme";

            ThemeSavedEventArgs? receivedArgs = null;
            panel.ThemeSaved += (sender, e) => receivedArgs = e as ThemeSavedEventArgs;

            var saveButton = panel.Controls.OfType<Button>()
                .First(c => c.Name == "SaveButton");

            // Act
            saveButton.PerformClick();

            // Assert
            Assert.NotNull(receivedArgs);
            Assert.Equal("Squire_Male", receivedArgs.JobName);
            Assert.Equal("My Test Theme", receivedArgs.ThemeName);
            Assert.Equal(512, receivedArgs.PaletteData.Length);
        }

        [Fact]
        [STAThread]
        public void ThemeEditorPanel_SectionResetButton_RestoresOriginalPaletteColors()
        {
            // Arrange - Create panel with a real sprite file to test palette restoration
            // The bug: clicking a section's Reset button only restores the base color,
            // but the shadow/highlight/accent colors get regenerated instead of restored
            var tempDir = Path.Combine(Path.GetTempPath(), "ThemeEditorResetTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // Create test mapping with name that sorts first alphabetically
                var mappingJson = @"{
                    ""job"": ""Squire_Male"",
                    ""sprite"": ""test_sprite.bin"",
                    ""sections"": [
                        {
                            ""name"": ""TestSection"",
                            ""displayName"": ""Test Section"",
                            ""indices"": [4, 5, 3],
                            ""roles"": [""base"", ""highlight"", ""shadow""]
                        }
                    ]
                }";
                File.WriteAllText(Path.Combine(tempDir, "Squire_Male.json"), mappingJson);

                // Create a test sprite file with known palette values
                var spriteData = new byte[512];
                // Set index 3 (shadow) to a specific color: BGR555 value
                spriteData[6] = 0x00; spriteData[7] = 0x40; // Some blue-ish color
                // Set index 4 (base) to another color
                spriteData[8] = 0x00; spriteData[9] = 0x7C; // Red-ish color
                // Set index 5 (highlight) to another color
                spriteData[10] = 0xE0; spriteData[11] = 0x03; // Green-ish color
                File.WriteAllBytes(Path.Combine(tempDir, "test_sprite.bin"), spriteData);

                using var panel = new ThemeEditorPanel(tempDir, tempDir);

                // Verify PaletteModifier was loaded
                Assert.NotNull(panel.PaletteModifier);
                Assert.True(panel.PaletteModifier.IsLoaded, "PaletteModifier should be loaded");

                // Get the original palette values before any changes
                var originalIndex3 = (ushort)(panel.PaletteModifier.GetModifiedPalette()[6] |
                                              (panel.PaletteModifier.GetModifiedPalette()[7] << 8));
                var originalIndex4 = (ushort)(panel.PaletteModifier.GetModifiedPalette()[8] |
                                              (panel.PaletteModifier.GetModifiedPalette()[9] << 8));
                var originalIndex5 = (ushort)(panel.PaletteModifier.GetModifiedPalette()[10] |
                                              (panel.PaletteModifier.GetModifiedPalette()[11] << 8));

                // Find the color picker and change its color
                var colorPicker = panel.Controls.OfType<Panel>()
                    .First(c => c.Name == "SectionColorPickersPanel")
                    .Controls.OfType<HslColorPicker>()
                    .First();

                // Change the color (this will apply auto-generated shades)
                colorPicker.Hue = 180; // Cyan hue
                colorPicker.Saturation = 100;
                colorPicker.Lightness = 50;

                // Verify palette was modified
                var modifiedIndex4 = (ushort)(panel.PaletteModifier.GetModifiedPalette()[8] |
                                               (panel.PaletteModifier.GetModifiedPalette()[9] << 8));
                Assert.NotEqual(originalIndex4, modifiedIndex4); // Should be different after change

                // Act - Click the section's individual Reset button
                var resetButton = colorPicker.Controls.OfType<Button>()
                    .First(c => c.Name == "ResetButton");
                resetButton.PerformClick();

                // Assert - Palette should be restored to original values (not regenerated shades)
                var resetIndex3 = (ushort)(panel.PaletteModifier.GetModifiedPalette()[6] |
                                           (panel.PaletteModifier.GetModifiedPalette()[7] << 8));
                var resetIndex4 = (ushort)(panel.PaletteModifier.GetModifiedPalette()[8] |
                                           (panel.PaletteModifier.GetModifiedPalette()[9] << 8));
                var resetIndex5 = (ushort)(panel.PaletteModifier.GetModifiedPalette()[10] |
                                           (panel.PaletteModifier.GetModifiedPalette()[11] << 8));

                Assert.Equal(originalIndex3, resetIndex3);
                Assert.Equal(originalIndex4, resetIndex4);
                Assert.Equal(originalIndex5, resetIndex5);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
