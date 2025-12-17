using System;
using Xunit;
using FFTColorCustomizer.Services;

namespace Tests.Services
{
    public class CharacterServiceSingletonTests
    {
        [Fact]
        public void Instance_Should_Return_Same_Instance()
        {
            // Act
            var instance1 = CharacterServiceSingleton.Instance;
            var instance2 = CharacterServiceSingleton.Instance;

            // Assert
            Assert.NotNull(instance1);
            Assert.NotNull(instance2);
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void Instance_Should_Initialize_With_Characters()
        {
            // Act
            var instance = CharacterServiceSingleton.Instance;

            // Assert
            Assert.NotNull(instance);
            var characters = instance.GetAllCharacters();
            Assert.NotNull(characters);

            // Should have auto-discovered characters or loaded from JSON
            // The count may vary depending on the environment
        }

        [Fact]
        public void Instance_Should_Be_Thread_Safe()
        {
            // Arrange
            CharacterDefinitionService? instance1 = null;
            CharacterDefinitionService? instance2 = null;

            // Act - Create instances from different threads
            var thread1 = new System.Threading.Thread(() =>
            {
                instance1 = CharacterServiceSingleton.Instance;
            });

            var thread2 = new System.Threading.Thread(() =>
            {
                instance2 = CharacterServiceSingleton.Instance;
            });

            thread1.Start();
            thread2.Start();

            thread1.Join();
            thread2.Join();

            // Assert
            Assert.NotNull(instance1);
            Assert.NotNull(instance2);
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void Reset_Should_Create_New_Instance()
        {
            // Arrange
            var originalInstance = CharacterServiceSingleton.Instance;
            originalInstance.AddCharacter(new CharacterDefinition
            {
                Name = "TestCharacter",
                SpriteNames = new[] { "test" },
                DefaultTheme = "original",
                AvailableThemes = new[] { "original" }
            });

            // Act
            CharacterServiceSingleton.Reset();
            var newInstance = CharacterServiceSingleton.Instance;

            // Assert
            Assert.NotSame(originalInstance, newInstance);

            // The test character should not be in the new instance
            var testChar = newInstance.GetCharacterByName("TestCharacter");
            Assert.Null(testChar);
        }
    }
}
