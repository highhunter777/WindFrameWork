using System;
using System.Collections.Generic;

namespace WindFrameWork.Core.Container
{
    /// <summary>
    /// 作用域契约：持有作用域内唯一的 <see cref="ServiceLifetime.Scoped"/> 实例，并在自身释放时释放它们。
    /// </summary>
    public interface IServiceScope : IDisposable
    {
        /// <summary>
        /// 作用域种类。
        /// </summary>
        ServiceScopeKind Kind { get; }

        /// <summary>
        /// 作用域内解析器，作用域销毁后其持有的作用域实例不可再解析。
        /// </summary>
        IServiceResolver Resolver { get; }

        /// <summary>
        /// 是否已释放。
        /// </summary>
        bool IsDisposed { get; }
    }

    /// <summary>
    /// 作用域默认实现：作用域内唯一实例的存储与释放。
    /// </summary>
    /// <remarks>
    /// 单例与瞬态服务不由作用域持有，解析时向上回退到 <see cref="ServiceLocator"/>。
    /// </remarks>
    public sealed class ServiceScope : IServiceScope, IServiceResolver
    {
        private readonly Dictionary<ServiceKey, object> _scopedInstances = new Dictionary<ServiceKey, object>();
        private readonly ServiceLocator _root;
        private bool _disposed;

        internal ServiceScope(ServiceLocator root, ServiceScopeKind kind)
        {
            _root = root;
            Kind = kind;
        }

        /// <inheritdoc />
        public ServiceScopeKind Kind { get; }

        /// <inheritdoc />
        public IServiceResolver Resolver => this;

        /// <inheritdoc />
        public bool IsDisposed => _disposed;

        /// <inheritdoc />
        public bool TryResolve<TService>(out TService service, string name = null) where TService : class
        {
            ThrowIfDisposed();

            var key = new ServiceKey(typeof(TService), name);

            if (_scopedInstances.TryGetValue(key, out object existing))
            {
                service = (TService)existing;
                return true;
            }

            if (!_root.TryGetEntry(key, out ServiceEntry entry))
            {
                service = null;
                return false;
            }

            if (entry.Lifetime == ServiceLifetime.Scoped)
            {
                service = (TService)entry.CreateInstance();
                _scopedInstances[key] = service;
                return true;
            }

            // 单例与瞬态服务不由作用域持有，向上回退到根定位器。
            return _root.TryResolveCore<TService>(name, out service);
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

            var key = new ServiceKey(typeof(TService), name);
            throw new ServiceResolutionException(key, "服务 " + key + " 未注册。");
        }

        /// <inheritdoc />
        public IServiceScope CreateScope(ServiceScopeKind kind)
        {
            ThrowIfDisposed();
            return _root.CreateScope(kind);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (object instance in _scopedInstances.Values)
            {
                (instance as IDisposable)?.Dispose();
            }

            _scopedInstances.Clear();
            _root.ForgetScope(this);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ServiceScope), "作用域 " + Kind + " 已释放，无法继续解析。");
            }
        }
    }
}
