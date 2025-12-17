using System;
using FFTColorCustomizer.Core;
using FFTColorCustomizer.Interfaces;
using Xunit;

namespace FFTColorCustomizer.Tests.Core
{
    public class ServiceContainerTests
    {
        [Fact]
        public void RegisterService_ShouldStoreService()
        {
            // Arrange
            var container = new ServiceContainer();
            var logger = new ConsoleLogger();

            // Act
            container.Register<ILogger>(logger);
            var resolved = container.Resolve<ILogger>();

            // Assert
            Assert.Same(logger, resolved);
        }

        [Fact]
        public void RegisterService_WithFactory_ShouldCreateInstance()
        {
            // Arrange
            var container = new ServiceContainer();

            // Act
            container.Register<ILogger>(() => new ConsoleLogger("Test"));
            var resolved1 = container.Resolve<ILogger>();
            var resolved2 = container.Resolve<ILogger>();

            // Assert
            Assert.NotNull(resolved1);
            Assert.NotNull(resolved2);
            Assert.NotSame(resolved1, resolved2); // Factory creates new instance each time
        }

        [Fact]
        public void RegisterSingleton_ShouldReturnSameInstance()
        {
            // Arrange
            var container = new ServiceContainer();
            var logger = new ConsoleLogger();

            // Act
            container.RegisterSingleton<ILogger>(logger);
            var resolved1 = container.Resolve<ILogger>();
            var resolved2 = container.Resolve<ILogger>();

            // Assert
            Assert.Same(logger, resolved1);
            Assert.Same(logger, resolved2);
        }

        [Fact]
        public void RegisterSingleton_WithFactory_ShouldCreateOnceAndReuse()
        {
            // Arrange
            var container = new ServiceContainer();
            var createCount = 0;

            // Act
            container.RegisterSingleton<ILogger>(() =>
            {
                createCount++;
                return new ConsoleLogger($"Test{createCount}");
            });

            var resolved1 = container.Resolve<ILogger>();
            var resolved2 = container.Resolve<ILogger>();
            var resolved3 = container.Resolve<ILogger>();

            // Assert
            Assert.Same(resolved1, resolved2);
            Assert.Same(resolved2, resolved3);
            Assert.Equal(1, createCount); // Factory called only once
        }

        [Fact]
        public void Resolve_UnregisteredService_ShouldThrowException()
        {
            // Arrange
            var container = new ServiceContainer();

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                container.Resolve<ILogger>());
            Assert.Contains("ILogger", exception.Message);
        }

        [Fact]
        public void RegisterService_CanOverwriteExisting()
        {
            // Arrange
            var container = new ServiceContainer();
            var logger1 = new ConsoleLogger("First");
            var logger2 = new ConsoleLogger("Second");

            // Act
            container.Register<ILogger>(logger1);
            container.Register<ILogger>(logger2);
            var resolved = container.Resolve<ILogger>();

            // Assert
            Assert.Same(logger2, resolved);
        }

        [Fact]
        public void TryResolve_RegisteredService_ShouldReturnTrue()
        {
            // Arrange
            var container = new ServiceContainer();
            container.Register<ILogger>(new ConsoleLogger());

            // Act
            var success = container.TryResolve<ILogger>(out var logger);

            // Assert
            Assert.True(success);
            Assert.NotNull(logger);
        }

        [Fact]
        public void TryResolve_UnregisteredService_ShouldReturnFalse()
        {
            // Arrange
            var container = new ServiceContainer();

            // Act
            var success = container.TryResolve<ILogger>(out var logger);

            // Assert
            Assert.False(success);
            Assert.Null(logger);
        }

        [Fact]
        public void Container_CanRegisterMultipleServices()
        {
            // Arrange
            var container = new ServiceContainer();
            var logger = new ConsoleLogger();
            var pathResolver = new PathResolver("/root", "/source", "/user");

            // Act
            container.RegisterSingleton<ILogger>(logger);
            container.RegisterSingleton<IPathResolver>(pathResolver);

            var resolvedLogger = container.Resolve<ILogger>();
            var resolvedPath = container.Resolve<IPathResolver>();

            // Assert
            Assert.Same(logger, resolvedLogger);
            Assert.Same(pathResolver, resolvedPath);
        }

        [Fact]
        public void Container_CanResolveConcreteTypes()
        {
            // Arrange
            var container = new ServiceContainer();
            var logger = new ConsoleLogger();

            // Act
            container.Register<ConsoleLogger>(logger);
            var resolved = container.Resolve<ConsoleLogger>();

            // Assert
            Assert.Same(logger, resolved);
        }

        [Fact]
        public void CreateScope_ShouldShareSingletons()
        {
            // Arrange
            var container = new ServiceContainer();
            var logger = new ConsoleLogger();
            container.RegisterSingleton<ILogger>(logger);

            // Act
            using (var scope = container.CreateScope())
            {
                var scopedLogger = scope.Resolve<ILogger>();

                // Assert
                Assert.Same(logger, scopedLogger); // Singletons are shared across scopes
            }
        }

        [Fact]
        public void CreateScope_ShouldHaveOwnTransientInstances()
        {
            // Arrange
            var container = new ServiceContainer();
            container.Register<ILogger>(() => new ConsoleLogger());

            // Act
            var mainLogger = container.Resolve<ILogger>();

            using (var scope = container.CreateScope())
            {
                var scopedLogger = scope.Resolve<ILogger>();

                // Assert
                Assert.NotSame(mainLogger, scopedLogger); // Transient instances are different
            }
        }

        [Fact]
        public void Container_ShouldBeThreadSafe()
        {
            // Arrange
            var container = new ServiceContainer();
            var iterations = 100;
            var results = new ILogger[iterations];

            container.RegisterSingleton<ILogger>(() => new ConsoleLogger());

            // Act - resolve from multiple threads
            System.Threading.Tasks.Parallel.For(0, iterations, i =>
            {
                results[i] = container.Resolve<ILogger>();
            });

            // Assert - all should be the same singleton instance
            var first = results[0];
            for (int i = 1; i < iterations; i++)
            {
                Assert.Same(first, results[i]);
            }
        }
    }
}
