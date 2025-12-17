using System;

namespace FFTColorCustomizer.Interfaces
{
    /// <summary>
    /// Defines a dependency injection container for service registration and resolution
    /// </summary>
    public interface IServiceContainer : IDisposable
    {
        /// <summary>
        /// Registers a service instance
        /// </summary>
        void Register<T>(T instance) where T : class;

        /// <summary>
        /// Registers a service factory
        /// </summary>
        void Register<T>(Func<T> factory) where T : class;

        /// <summary>
        /// Registers a singleton service instance
        /// </summary>
        void RegisterSingleton<T>(T instance) where T : class;

        /// <summary>
        /// Registers a singleton service factory
        /// </summary>
        void RegisterSingleton<T>(Func<T> factory) where T : class;

        /// <summary>
        /// Resolves a service
        /// </summary>
        T Resolve<T>() where T : class;

        /// <summary>
        /// Tries to resolve a service
        /// </summary>
        bool TryResolve<T>(out T service) where T : class;

        /// <summary>
        /// Creates a new scope for scoped services
        /// </summary>
        IServiceContainer CreateScope();
    }
}
