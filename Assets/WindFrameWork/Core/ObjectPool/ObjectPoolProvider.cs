using System;
using System.Collections.Generic;
using UnityEngine;

namespace WindFrameWork.Core.ObjectPool
{
    /// <summary>
    /// 对象池注册表默认实现：按名称管理池，提供类型安全的访问与集中释放。
    /// </summary>
    /// <remarks>
    /// 名称为业务选取的池标识，不从prefab 或类型派生：具体创建什么由工厂决定，
    /// 因此同一套注册表可同时承载不同类型的池。
    /// </remarks>
    public sealed class ObjectPoolProvider : IObjectPoolProvider, IDisposable
    {
        private readonly Dictionary<string, PoolRegistration> _registrations = new Dictionary<string, PoolRegistration>();
        private readonly List<IDisposable> _ownedPools = new List<IDisposable>();
        private bool _disposed;

        /// <summary>
        /// 已注册的池数量。
        /// </summary>
        public int Count => _registrations.Count;

        /// <inheritdoc />
        public IObjectPool<TObject> Get<TObject>(string key) where TObject : UnityEngine.Object
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("池名不能为空。", nameof(key));
            }

            if (!_registrations.TryGetValue(key, out PoolRegistration registration))
            {
                throw new InvalidOperationException(
                    "对象池 " + key + " 未注册。已注册的池：" + string.Join("、", _registrations.Keys) + "。");
            }

            if (registration.ElementType != typeof(TObject))
            {
                throw new InvalidOperationException(
                    "对象池 " + key + " 的元素类型为 " + registration.ElementType.Name +
                    "，与请求的 " + typeof(TObject).Name + " 不一致。");
            }

            return (IObjectPool<TObject>)registration.Pool;
        }

        /// <inheritdoc />
        public bool TryGet<TObject>(string key, out IObjectPool<TObject> pool) where TObject : UnityEngine.Object
        {
            ThrowIfDisposed();

            if (key != null && _registrations.TryGetValue(key, out PoolRegistration registration)
                && registration.ElementType == typeof(TObject))
            {
                pool = (IObjectPool<TObject>)registration.Pool;
                return true;
            }

            pool = null;
            return false;
        }

        /// <inheritdoc />
        public void Register<TObject>(string key, IObjectPool<TObject> pool) where TObject : UnityEngine.Object
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("池名不能为空。", nameof(key));
            }

            if (pool == null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            if (_registrations.TryGetValue(key, out PoolRegistration existing))
            {
                if (existing.ElementType == typeof(TObject))
                {
                    throw new InvalidOperationException("对象池 " + key + " 已注册。");
                }

                throw new InvalidOperationException(
                    "对象池 " + key + " 已以元素类型 " + existing.ElementType.Name +
                    " 注册，无法再注册 " + typeof(TObject).Name + "。");
            }

            _registrations.Add(key, new PoolRegistration(typeof(TObject), pool, pool.GetStatistics));

            if (pool is IDisposable disposable)
            {
                _ownedPools.Add(disposable);
            }
        }

        /// <inheritdoc />
        public bool Release(string key)
        {
            ThrowIfDisposed();

            if (key == null || !_registrations.TryGetValue(key, out PoolRegistration registration))
            {
                return false;
            }

            if (registration.Pool is IDisposable disposable)
            {
                disposable.Dispose();
                _ownedPools.Remove(disposable);
            }

            return _registrations.Remove(key);
        }

        /// <inheritdoc />
        public void ReleaseAll()
        {
            ThrowIfDisposed();

            for (int index = _ownedPools.Count - 1; index >= 0; index--)
            {
                _ownedPools[index].Dispose();
            }

            _ownedPools.Clear();
            _registrations.Clear();
        }

        /// <inheritdoc />
        public ObjectPoolStatistics GetStatistics(string key)
        {
            ThrowIfDisposed();

            if (key == null || !_registrations.TryGetValue(key, out PoolRegistration registration))
            {
                return default;
            }

            return registration.GetStatistics();
        }

        /// <inheritdoc />
        public IReadOnlyList<ObjectPoolInfo> GetPools()
        {
            ThrowIfDisposed();

            var infos = new List<ObjectPoolInfo>(_registrations.Count);
            foreach (KeyValuePair<string, PoolRegistration> pair in _registrations)
            {
                infos.Add(new ObjectPoolInfo(pair.Key, pair.Value.ElementType, pair.Value.GetStatistics()));
            }

            // 字典枚举顺序无契约保证，显式排序以保证测试与日志可复现。
            infos.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
            return infos;
        }

        /// <summary>
        /// 释放全部池。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            ReleaseAll();
            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ObjectPoolProvider), "对象池注册表已释放。");
            }
        }
    }
}
