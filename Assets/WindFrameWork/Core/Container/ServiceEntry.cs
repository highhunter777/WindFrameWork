using System;

namespace WindFrameWork.Core.Container
{
    /// <summary>
    /// 一条注册记录的内部存储：描述服务如何创建，并持有单例实例。
    /// </summary>
    internal sealed class ServiceEntry
    {
        private object _singletonInstance;
        private bool _singletonCreated;

        internal ServiceEntry(ServiceKey key, ServiceLifetime lifetime, Type implementationType, object instance)
        {
            Key = key;
            Lifetime = lifetime;
            ImplementationType = implementationType;
            _singletonInstance = instance;
            _singletonCreated = instance != null;
        }

        internal ServiceKey Key { get; }

        internal ServiceLifetime Lifetime { get; }

        /// <summary>
        /// 实现类型；由注册方直接提供实例时为 null。
        /// </summary>
        internal Type ImplementationType { get; }

        /// <summary>
        /// 取单例实例，未创建时按实现类型实例化。
        /// </summary>
        internal object ResolveSingleton()
        {
            if (!_singletonCreated)
            {
                _singletonInstance = CreateInstance();
                _singletonCreated = true;
            }

            return _singletonInstance;
        }

        /// <summary>
        /// 取一个全新实例，不参与单例缓存。
        /// </summary>
        internal object CreateInstance()
        {
            if (ImplementationType == null)
            {
                throw new InvalidOperationException("服务 " + Key + " 未提供实现类型，无法创建新实例。");
            }

            try
            {
                return Activator.CreateInstance(ImplementationType);
            }
            catch (Exception exception)
            {
                throw new ServiceResolutionException(
                    Key,
                    "服务 " + Key + " 的实现类型 " + ImplementationType.FullName + " 无法实例化，实现类型须具备可访问的无参构造函数。",
                    exception);
            }
        }

        /// <summary>
        /// 释放并丢弃单例实例，使下次解析重新创建。
        /// </summary>
        internal void ResetSingleton()
        {
            (_singletonInstance as IDisposable)?.Dispose();
            _singletonInstance = null;
            _singletonCreated = false;
        }
    }
}
