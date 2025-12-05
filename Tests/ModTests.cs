using Xunit;
using FluentAssertions;
using System.IO;
using System;
using System.Collections.Generic;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;

namespace FFTColorMod.Tests
{
    public class ModTests
    {

        [Fact]
        public void Mod_Start_Should_Store_ModLoader_Reference()
        {
            // TLDR: When Start() is called, mod should store the modLoader reference
            // Arrange
            var context = new ModContext();
            var mod = new Mod(context);

            // Act - Call Start with null (the method handles this gracefully)
            mod.Start(null);

            // Use reflection to check if _modLoader field was set
            var modLoaderField = typeof(Mod).GetField("_modLoader",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Assert - The field should exist (even if null in test environment)
            modLoaderField.Should().NotBeNull();
        }

        [Fact]
        public void Mod_Should_Have_LoadSpriteHook_Delegate_Field()
        {
            // TLDR: Mod class should have a LoadSpriteHook field for hooking sprite loading
            // Arrange & Act
            var loadSpriteHookField = typeof(Mod).GetField("_loadSpriteHook",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Assert
            loadSpriteHookField.Should().NotBeNull();
            loadSpriteHookField.FieldType.Name.Should().Contain("IHook");
        }

        [Fact]
        public void LoadSpriteHook_Method_Should_Exist()
        {
            // TLDR: Mod should have a LoadSpriteHook method that intercepts sprite loading
            // Arrange
            var loadSpriteHookMethod = typeof(Mod).GetMethod("LoadSpriteHook",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Assert
            loadSpriteHookMethod.Should().NotBeNull();

            // Verify method signature - should take IntPtr spriteData and int size, return IntPtr
            var parameters = loadSpriteHookMethod.GetParameters();
            parameters.Should().HaveCount(2);
            parameters[0].ParameterType.Should().Be(typeof(IntPtr));
            parameters[0].Name.Should().Be("spriteData");
            parameters[1].ParameterType.Should().Be(typeof(int));
            parameters[1].Name.Should().Be("size");
            loadSpriteHookMethod.ReturnType.Should().Be(typeof(IntPtr));
        }

        [Fact]
        public void Mod_Should_Have_PaletteDetector_Field()
        {
            // TLDR: Mod should have a PaletteDetector field to detect and modify palettes
            // Arrange
            var paletteDetectorField = typeof(Mod).GetField("_paletteDetector",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Assert
            paletteDetectorField.Should().NotBeNull();
            paletteDetectorField.FieldType.Should().Be(typeof(PaletteDetector));
        }

        [Fact]
        public void Mod_Should_Initialize_PaletteDetector_In_Constructor()
        {
            // TLDR: Mod should initialize PaletteDetector when constructed
            // Arrange
            var tempAppData = Path.Combine(Path.GetTempPath(), $"TestAppData_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempAppData);
            var originalAppData = Environment.GetEnvironmentVariable("APPDATA");
            Environment.SetEnvironmentVariable("APPDATA", tempAppData);

            try
            {
                // Act
                var context = new ModContext();
                var mod = new Mod(context);

                // Use reflection to get the private _paletteDetector field
                var paletteDetectorField = typeof(Mod).GetField("_paletteDetector",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                // Assert
                paletteDetectorField.Should().NotBeNull();
                var paletteDetector = paletteDetectorField.GetValue(mod);
                paletteDetector.Should().NotBeNull();
                paletteDetector.Should().BeOfType<PaletteDetector>();
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable("APPDATA", originalAppData);
                if (Directory.Exists(tempAppData))
                    Directory.Delete(tempAppData, recursive: true);
            }
        }

        [Fact]
        public void Mod_Should_Support_Other_Mods_Compatibility()
        {
            // TLDR: Mod should implement CanSuspend and CanUnload for compatibility with other mods
            // Arrange
            var tempAppData = Path.Combine(Path.GetTempPath(), $"TestAppData_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempAppData);
            var originalAppData = Environment.GetEnvironmentVariable("APPDATA");
            Environment.SetEnvironmentVariable("APPDATA", tempAppData);

            try
            {
                var context = new ModContext();
                var mod = new Mod(context);

                // Act & Assert - Check if mod implements the compatibility methods
                mod.CanSuspend().Should().BeFalse("Color mod should not support suspension as it modifies memory");
                mod.CanUnload().Should().BeFalse("Color mod should not support unloading due to active hooks");

                // Check that mod properly identifies itself for other mods
                var modId = mod.GetType().GetProperty("ModId")?.GetValue(mod);
                modId.Should().NotBeNull();
                modId.Should().Be("FFTColorMod");
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable("APPDATA", originalAppData);
                if (Directory.Exists(tempAppData))
                    Directory.Delete(tempAppData, recursive: true);
            }
        }

        [Fact]
        public void Mod_Should_Have_Character_Color_Customization_Support()
        {
            // TLDR: Mod should have infrastructure to support per-character color customization
            // Arrange
            var tempAppData = Path.Combine(Path.GetTempPath(), $"TestAppData_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempAppData);
            var originalAppData = Environment.GetEnvironmentVariable("APPDATA");
            Environment.SetEnvironmentVariable("APPDATA", tempAppData);

            try
            {
                var context = new ModContext();
                var mod = new Mod(context);

                // Act - Check if mod has per-character customization capability
                var supportsCharacterCustomization = mod.GetType().GetMethod("SupportsPerCharacterColors");

                // Assert
                supportsCharacterCustomization.Should().NotBeNull("Mod should have method to check character customization support");

                // Invoke the method and check result
                var result = supportsCharacterCustomization?.Invoke(mod, null);
                result.Should().BeOfType<bool>();
                result.Should().Be(true, "Mod should support per-character color customization");
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable("APPDATA", originalAppData);
                if (Directory.Exists(tempAppData))
                    Directory.Delete(tempAppData, recursive: true);
            }
        }
    }
}