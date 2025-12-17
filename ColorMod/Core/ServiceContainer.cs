using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using FFTColorCustomizer.Interfaces;

namespace FFTColorCustomizer.Core
{
    /// <summary>
    /// Simple dependency injection container implementation
    /// </summary>
    public class ServiceContainer : IServiceContainer
    {
        private readonly ConcurrentDictionary<Type, ServiceDescriptor> _services;
        private readonly ConcurrentDictionary<Type, object> _singletonInstances;
        private readonly ServiceContainer? _parent;
        private bool _disposed;

        public ServiceContainer(ServiceContainer? parent = null)
        {
            _services = new ConcurrentDictionary<Type, ServiceDescriptor>();
            _singletonInstances = new ConcurrentDictionary<Type, object>();
            _parent = parent;
        }

        public void Register<T>(T instance) where T : class
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var descriptor = new ServiceDescriptor(
                typeof(T),
                ServiceLifetime.Transient,
                null,
                instance);

            _services[typeof(T)] = descriptor;
        }

        public void Register<T>(Func<T> factory) where T : class
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            var descriptor = new ServiceDescriptor(
                typeof(T),
                ServiceLifetime.Transient,
                () => factory(),
                null);

            _services[typeof(T)] = descriptor;
        }

        public void RegisterSingleton<T>(T instance) where T : class
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var descriptor = new ServiceDescriptor(
                typeof(T),
                ServiceLifetime.Singleton,
                null,
                instance);

            _services[typeof(T)] = descriptor;
            _singletonInstances[typeof(T)] = instance;
        }

        public void RegisterSingleton<T>(Func<T> factory) where T : class
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            var descriptor = new ServiceDescriptor(
                typeof(T),
                ServiceLifetime.Singleton,
                () => factory(),
                null);

            _services[typeof(T)] = descriptor;
        }

        public T Resolve<T>() where T : class
        {
            var type = typeof(T);

            if (TryResolve<T>(out var service))
            {
                return service;
            }

            throw new InvalidOperationException(
                $"Service of type {type.Name} is not registered.");
        }

        public bool TryResolve<T>(out T? service) where T : class
        {
            service = null;
            var type = typeof(T);

            // Check if we have the service
            if (!_services.TryGetValue(type, out var descriptor))
            {
                // Check parent container if this is a scoped container
                if (_parent != null)
                {
                    return _parent.TryResolve(out service);
                }
                return false;
            }

            // Handle singletons
            if (descriptor.Lifetime == ServiceLifetime.Singleton)
            {
                // Thread-safe singleton creation using GetOrAdd
                var instance = _singletonInstances.GetOrAdd(type, _ =>
                {
                    if (descriptor.Factory != null)
                    {
                        return descriptor.Factory();
                    }
                    else if (descriptor.Instance != null)
                    {
                        return descriptor.Instance;
                    }
                    throw new InvalidOperationException($"No factory or instance for singleton {type.Name}");
                });

                service = (T)instance;
                return true;
            }

            // Handle transient
            if (descriptor.Factory != null)
            {
                service = (T)descriptor.Factory();
                return true;
            }
            else if (descriptor.Instance != null)
            {
                service = (T)descriptor.Instance;
                return true;
            }

            return false;
        }

        public IServiceContainer CreateScope()
        {
            // Create a child container that shares singleton instances
            var scope = new ServiceContainer(this);

            // Copy service descriptors
            foreach (var kvp in _services)
            {
                scope._services[kvp.Key] = kvp.Value;
            }

            // Share singleton instances
            foreach (var kvp in _singletonInstances)
            {
                scope._singletonInstances[kvp.Key] = kvp.Value;
            }

            return scope;
        }

        public void Dispose()
        {
            if (_disposed) return;

            // Dispose any disposable singleton instances
            foreach (var instance in _singletonInstances.Values)
            {
                if (instance is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            _singletonInstances.Clear();
            _services.Clear();
            _disposed = true;
        }

        private class ServiceDescriptor
        {
            public Type ServiceType { get; }
            public ServiceLifetime Lifetime { get; }
            public Func<object>? Factory { get; }
            public object? Instance { get; }

            public ServiceDescriptor(Type serviceType, ServiceLifetime lifetime,
                Func<object>? factory, object? instance)
            {
                ServiceType = serviceType;
                Lifetime = lifetime;
                Factory = factory;
                Instance = instance;
            }
        }

        private enum ServiceLifetime
        {
            Transient,
            Singleton,
            Scoped
        }
    }
}
