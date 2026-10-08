using System;
using System.Collections.Generic;

namespace WindFrameWork.Core.Container
{
    /// <summary>
    /// 默认服务定位器：读写合一的容器实现。
    /// </summary>
    /// <remarks>
    /// 本类型是普通类而非静态单例：静态全局状态不可测试，且作用域需要持有各自的实例状态，二者都要求以实例形式存在。
    /// DI 容器等变体实现 <see cref="IServiceRegistry"/> 与 <see cref="IServiceResolver"/> 后整体替换本类型即可。
    /// </remarks>
    public sealed class ServiceLocator : IServiceRegistry, IServiceResolver, IDisposable
    {
        private readonly Dictionary<ServiceKey, ServiceEntry> _entries = new Dictionary<ServiceKey, ServiceEntry>();
        private readonly List<ServiceScope> _scopes = new List<ServiceScope>();
        private bool _disposed;

        /// <summary>
        /// 已注册的服务数量。
        /// </summary>
        public int RegisteredCount => _entries.Count;

        /// <summary>
        /// 当前存活的作用域数量。
        /// </summary>
        public int ScopeCount => _scopes.Count;

        /// <inheritdoc />
        public void Register<TService>(ServiceLifetime lifetime, TService instance) where TService : class
        {
            RegisterInternal(ServiceKey.Of<TService>(), lifetime, null, instance);
        }

        /// <inheritdoc />
        public void Register<TService>(string name, ServiceLifetime lifetime, TService instance) where TService : class
        {
            RegisterInternal(ServiceKey.Of<TService>(name), lifetime, null, instance);
        }

        /// <inheritdoc />
        public void Register<TService, TImplementation>(ServiceLifetime lifetime)
            where TService : class
            where TImplementation : class, TService
        {
            RegisterInternal(ServiceKey.Of<TService>(), lifetime, typeof(TImplementation), null);
        }

        /// <inheritdoc />
        public void Register<TService, TImplementation>(string name, ServiceLifetime lifetime)
            where TService : class
            where TImplementation : class, TService
        {
            RegisterInternal(ServiceKey.Of<TService>(name), lifetime, typeof(TImplementation), null);
        }

        /// <inheritdoc />
        public bool Unregister<TService>(string name = null) where TService : class
        {
            ThrowIfDisposed();

            var key = ServiceKey.Of<TService>(name);
            if (!_entries.TryGetValue(key, out ServiceEntry entry))
            {
                return false;
            }

            entry.ResetSingleton();
            return _entries.Remove(key);
        }

        /// <inheritdoc />
        public bool TryResolve<TService>(out TService service, string name = null) where TService : class
        {
            ThrowIfDisposed();
            return TryResolveCore<TService>(name, out service);
        }

        /// <inheritdoc />
        public TService Resolve<TService>(string name = null) where TService : class
        {
            return TryResolve(out TService service, name) ? service : null;
        }

        /// <inheritdoc />
        public TService GetRequired<TService>(string name = null) where TService : class
        {
            if (TryResolve(out TService service, name))
            {
                return service;
            }

            var key = ServiceKey.Of<TService>(name);
            throw new ServiceResolutionException(key, "服务 " + key + " 未注册。");
        }

        /// <inheritdoc />
        public IServiceScope CreateScope(ServiceScopeKind kind)
        {
            ThrowIfDisposed();

            var scope = new ServiceScope(this, kind);
            _scopes.Add(scope);
            return scope;
        }

        /// <summary>
        /// 释放定位器：先释放全部作用域，再释放单例实例并清空注册表。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            for (int index = _scopes.Count - 1; index >= 0; index--)
            {
                _scopes[index].Dispose();
            }

            _scopes.Clear();

            foreach (ServiceEntry entry in _entries.Values)
            {
                entry.ResetSingleton();
            }

            _entries.Clear();
            _disposed = true;
        }

        /// <summary>
        /// 移除已释放的作用域，避免定位器长期持有失效引用。
        /// </summary>
        internal void ForgetScope(ServiceScope scope)
        {
            _scopes.Remove(scope);
        }

        /// <summary>
        /// 在根定位器上解析，供作用域回退使用：单例与瞬态服务不由作用域持有。
        /// </summary>
        internal bool TryResolveCore<TService>(string name, out TService service) where TService : class
        {
            service = null;

            if (!TryGetEntry(ServiceKey.Of<TService>(name), out ServiceEntry entry))
            {
                return false;
            }

            switch (entry.Lifetime)
            {
                case ServiceLifetime.Transient:
                    service = (TService)entry.CreateInstance();
                    return true;

                default:
                    // 单例，以及在根定位器上解析的 Scoped（等价于解析全局作用域），均走单例缓存。
                    service = (TService)entry.ResolveSingleton();
                    return true;
            }
        }

        /// <summary>
        /// 查询注册记录，供作用域按生命周期自行分派。
        /// </summary>
        internal bool TryGetEntry(ServiceKey key, out ServiceEntry entry)
        {
            return _entries.TryGetValue(key, out entry);
        }

        private void RegisterInternal(ServiceKey key, ServiceLifetime lifetime, Type implementationType, object instance)
        {
            ThrowIfDisposed();

            if (implementationType == null && instance == null)
            {
                throw new InvalidOperationException("注册服务 " + key + " 需要提供实现类型或实例。");
            }

            if (instance != null && lifetime == ServiceLifetime.Transient)
            {
                throw new InvalidOperationException(
                    "注册服务 " + key + " 时实例与 Transient 生命周期矛盾：瞬态服务每次解析都需创建新实例。");
            }

            if (_entries.TryGetValue(key, out ServiceEntry existing))
            {
                throw new InvalidOperationException("服务 " + key + " 已注册，重复注册将导致先前实现被静默覆盖。");
            }

            _entries.Add(key, new ServiceEntry(key, lifetime, implementationType, instance));
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ServiceLocator), "服务定位器已释放。");
            }
        }
    }
}
