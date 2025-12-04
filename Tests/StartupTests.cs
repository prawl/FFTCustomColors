using Xunit;
using FFTColorMod;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;
using Reloaded.Hooks.ReloadedII.Interfaces;

namespace FFTColorMod.Tests;

public class StartupTests
{
    [Fact]
    public void Startup_ShouldExist()
    {
        // This test verifies that we have a Startup class
        // which is what Reloaded-II looks for
        var type = typeof(Startup);

        // Assert
        Assert.NotNull(type);
        Assert.Equal("Startup", type.Name);
    }

    [Fact]
    public void Startup_ShouldImplementIMod()
    {
        // Arrange
        var startup = new Startup();

        // Assert - Startup must implement IMod for Reloaded-II
        Assert.IsAssignableFrom<IMod>(startup);
    }

    [Fact]
    public void Startup_ShouldHaveStartExMethod()
    {
        // This is the critical method that Reloaded-II calls
        var method = typeof(Startup).GetMethod("StartEx");

        Assert.NotNull(method);

        // Verify it has the correct parameters
        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("loaderApi", parameters[0].Name);
        Assert.Equal(typeof(IModLoaderV1), parameters[0].ParameterType);
        Assert.Equal("modConfig", parameters[1].Name);
        Assert.Equal(typeof(IModConfigV1), parameters[1].ParameterType);
    }

    [Fact]
    public void StartEx_ShouldCreateModInstance()
    {
        // Arrange
        var startup = new Startup();

        // Act - Call StartEx (with null params for now - minimal test)
        startup.StartEx(null!, null!);

        // Assert - Verify that Mod property was set
        var modField = typeof(Startup).GetField("_mod",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(modField);
        var modValue = modField.GetValue(startup);
        Assert.NotNull(modValue);
    }

    [Fact]
    public void ModContext_ShouldExist()
    {
        // ModContext should be a class that holds services
        var type = typeof(ModContext);

        Assert.NotNull(type);
        Assert.Equal("ModContext", type.Name);
    }

    [Fact]
    public void Mod_ShouldAcceptModContext()
    {
        // Arrange
        var context = new ModContext();

        // Act - Mod should accept ModContext in constructor
        var mod = new Mod(context);

        // Assert
        Assert.NotNull(mod);
    }
}