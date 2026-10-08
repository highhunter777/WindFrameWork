using System;
using UnityEngine;

namespace WindFrameWork.Core.ObjectPool
{
    /// <summary>
    /// <see cref="UnityEngine.Object"/> 级对象池：降低频繁实例化与销毁的开销。
    /// </summary>
    /// <typeparam name="TObject">池内元素类型，可为 GameObject、Component 或 ScriptableObject。</typeparam>
    /// <remarks>
    /// 采用槽位表与侵入式空闲链表，租出与归还不分配内存。仅主线程可用。
    /// <para>
    /// 池不修改对象的位置与旋转：重置变换属于业务策略，应在 <see cref="IPoolable"/> 中实现。
    /// </para>
    /// </remarks>
    public sealed class ObjectPool<TObject> : ObjectPoolBase, IObjectPool<TObject>, IDisposable where TObject : UnityEngine.Object
    {
        private static int _nextPoolId;

        private readonly ObjectPoolConfig _config;
        private readonly ObjectPoolFreeList _slots;
        private readonly IPoolLifecycleAdapter<TObject> _adapter;
        private readonly Func<TObject> _factory;
        private readonly int _poolId;

        private Transform _poolRoot;
        private bool _disposed;

        private long _rentCount;
        private long _hitCount;
        private long _missCount;
        private long _returnCount;
        private long _createCount;
        private long _destroyCount;
        private long _discardedDestroyedCount;

        /// <summary>
        /// 创建对象池并按配置预热。
        /// </summary>
        /// <param name="factory">对象创建工厂，负责决定实例化 prefab 还是调用 CreateInstance。</param>
        /// <param name="config">池配置。</param>
        /// <param name="poolRootParent">池根的父节点，为 null 时池内对象不参与层级归置。</param>
        /// <param name="adapter">生命周期适配器，为 null 时使用内置适配器。</param>
        public ObjectPool(
            Func<TObject> factory,
            ObjectPoolConfig config,
            Transform poolRootParent = null,
            IPoolLifecycleAdapter<TObject> adapter = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _config = config;
            _adapter = adapter ?? UnityObjectLifecycleAdapter<TObject>.Instance;
            _poolId = ++_nextPoolId;
            _slots = new ObjectPoolFreeList(config.InitialSlotCapacity);

            PoolThreadGuard.Assert(_poolId);

            if (config.ReparentToPoolRoot)
            {
                _poolRoot = ObjectPoolRoot.GetOrCreate(poolRootParent, config.Name, config.HidePoolRootInHierarchy);
            }

            ObjectPoolRegistry.Register(this);

            Prewarm(config.PrewarmCount);
        }

        /// <inheritdoc />
        public string Name => _config.Name;

        /// <inheritdoc />
        public int IdleCount => _slots.IdleCount;

        /// <inheritdoc />
        public int LiveCount => _slots.LiveCount;

        /// <summary>池标识，用于诊断。</summary>
        public int PoolId => _poolId;

        /// <summary>池的元素类型。</summary>
        public Type ElementType => typeof(TObject);

        /// <inheritdoc />
        public PooledHandle<TObject> Rent()
        {
            ThrowIfDisposed();
            PoolThreadGuard.Assert(_poolId);

            _rentCount++;

            int slot = TakeFreeSlot();
            if (slot >= 0)
            {
                _hitCount++;
                return RentSlot(slot);
            }

            if (_slots.LiveCount >= _config.MaxLiveSize)
            {
                throw new InvalidOperationException(
                    "对象池 " + _config.Name + " 的并发租出数已达上限 " + _config.MaxLiveSize +
                    "（当前 " + _slots.LiveCount + "）。请确认每次租出都已归还。");
            }

            _missCount++;
            return RentSlot(CreateSlot());
        }

        /// <inheritdoc />
        public void Return(PooledHandle<TObject> handle)
        {
            ThrowIfDisposed();
            PoolThreadGuard.Assert(_poolId);

            if (handle.PoolId != _poolId)
            {
                throw new InvalidOperationException(
                    "归还的租约凭证属于对象池 #" + handle.PoolId + "，与当前对象池 " + _config.Name + "（#" + _poolId + "）不符。");
            }

            int slot = handle.Slot;
            if (!_slots.IsLeaseValid(slot, handle.Generation))
            {
                throw new InvalidOperationException(
                    "对象池 " + _config.Name + " 收到无效的归还凭证（槽位 " + slot + "）：可能已被归还，或凭证已陈旧。");
            }

            // 代数在此推进，使同一凭证无法再次归还，即使槽位已被重新租出。
            _slots.NextGeneration(slot);

            var item = (TObject)_slots.GetObject(slot);

            // 必须使用 Unity 重载的 == ：ReferenceEquals 对已销毁对象返回 true，是错误的。
            if (item == null)
            {
                _slots.MarkDead(slot);
                _discardedDestroyedCount++;
                _returnCount++;
                Debug.LogWarning("[对象池] " + _config.Name + "：归还的对象已被外部销毁，已丢弃（槽位 " + slot + "）。");
                return;
            }

            _returnCount++;
            InvokeHooks(_slots.GetHooks(slot), false);

            if (_config.DeactivateOnReturn)
            {
                _adapter.OnReturned(item);
            }

            if (_slots.IdleCount >= _config.MaxIdleSize)
            {
                HandleOverflow(slot, item);
                return;
            }

            _slots.PushFree(slot);
        }

        /// <inheritdoc />
        public void Clear()
        {
            ThrowIfDisposed();
            PoolThreadGuard.Assert(_poolId);

            DestroyAll();

            _rentCount = 0;
            _hitCount = 0;
            _missCount = 0;
            _returnCount = 0;
            _createCount = 0;
            _destroyCount = 0;
            _discardedDestroyedCount = 0;

            _slots.RecycleAll();
        }

        /// <inheritdoc />
        public ObjectPoolStatistics GetStatistics()
        {
            return new ObjectPoolStatistics(
                _rentCount,
                _hitCount,
                _missCount,
                _returnCount,
                _createCount,
                _destroyCount,
                _discardedDestroyedCount,
                _slots.IdleCount,
                _slots.LiveCount,
                _slots.PeakLiveCount);
        }

        /// <summary>
        /// 释放池：销毁空闲对象并从全局注册表注销。租出中的对象不做处理，由调用方负责。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            ObjectPoolRegistry.Unregister(this);
            DestroyAll();
            _slots.RecycleAll();
            _disposed = true;
        }

        /// <summary>
        /// 重置全部对象引用，不销毁任何 Unity 对象。
        /// </summary>
        /// <remarks>
        /// 用于域重载与进入播放态的 <c>SubsystemRegistration</c> 阶段：该阶段并非播放态，调用销毁是非法的。
        /// </remarks>
        internal override void Recycle()
        {
            _slots.RecycleAll();
        }

        /// <summary>
        /// 清空池内对象（含租出中的），用于编辑器程序集重载前。
        /// </summary>
        internal override void DestroyEverything()
        {
            DestroyAll();
        }

        /// <summary>
        /// 取下一个可用空闲槽位，跳过已被外部销毁的对象。
        /// </summary>
        private int TakeFreeSlot()
        {
            while (_slots.HasIdle)
            {
                int slot = _slots.PopFree();

                var item = (TObject)_slots.GetObject(slot);

                if (item != null)
                {
                    return slot;
                }

                // 该空闲对象已被外部销毁：标记失效，使其不再参与租出。
                _slots.MarkIdleSlotDead(slot);
                _discardedDestroyedCount++;
            }

            return -1;
        }

        private PooledHandle<TObject> RentSlot(int slot)
        {
            var item = (TObject)_slots.GetObject(slot);
            int generation = _slots.NextGeneration(slot);

            _slots.MarkRented(slot);

            if (_config.ActivateOnRent)
            {
                _adapter.OnRent(item);
            }

            InvokeHooks(_slots.GetHooks(slot), true);

            return new PooledHandle<TObject>(item, slot, generation, _poolId);
        }

        private int CreateSlot()
        {
            TObject item = _factory();

            if (item == null)
            {
                throw new InvalidOperationException(
                    "对象池 " + _config.Name + " 的工厂返回了空对象，无法创建实例。");
            }

            IPoolable[] hooks = CollectHooks(item);
            _createCount++;

            _adapter.OnCreated(item, _poolRoot, _config);

            if (_config.DeactivateOnReturn)
            {
                _adapter.OnReturned(item);
            }

            int slot = _slots.Attach(item, hooks);
            return slot;
        }

        /// <summary>
        /// 预热：创建并归还指定数量的对象，使其在首次租出前即处于空闲状态。
        /// </summary>
        private void Prewarm(int count)
        {
            for (int index = 0; index < count; index++)
            {
                int slot = CreateSlot();
                var item = (TObject)_slots.GetObject(slot);

                InvokeHooks(_slots.GetHooks(slot), false);

                if (_config.DeactivateOnReturn)
                {
                    _adapter.OnReturned(item);
                }

                // 预热对象自始即空闲，不经过租出路径，以免污染租出计数与并发峰值。
                _slots.MarkIdle(slot);
            }
        }

        /// <summary>
        /// 空闲容量已满：销毁溢出对象而非保留。
        /// </summary>
        /// <remarks>
        /// 该策略使空闲上限成为可审计的内存硬边界，且溢出路径只是一次计数比较；
        /// LRU 需在每次归还时写入时间戳，在最热的路径上持续付出缓存行开销。
        /// 策略替换只需改动本方法，不影响任何公开签名。
        /// </remarks>
        private void HandleOverflow(int slot, TObject item)
        {
            InvokeHooks(_slots.GetHooks(slot), false);
            _slots.MarkDead(slot);

            _adapter.OnBeforeDestroy(item);
            DestroyItem(item);
            _destroyCount++;
        }

        private void DestroyAll()
        {
            for (int slot = 0; slot < _slots.SlotCount; slot++)
            {
                var item = (TObject)_slots.GetObject(slot);
                if (item == null)
                {
                    continue;
                }

                InvokeHooks(_slots.GetHooks(slot), false);
                _adapter.OnBeforeDestroy(item);
                DestroyItem(item);
                _destroyCount++;
            }
        }

        /// <summary>
        /// 按播放态选择销毁 API：编辑态调用 Destroy 会报错，播放态调用 DestroyImmediate 会被禁止。
        /// </summary>
        private static void DestroyItem(TObject item)
        {
            if (item == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(item);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(item);
            }
        }

        /// <summary>
        /// 收集生命周期钩子，仅在对象创建时执行一次并按槽位缓存。
        /// </summary>
        private IPoolable[] CollectHooks(TObject item)
        {
            switch (item)
            {
                case GameObject gameObject:
                    return _config.CollectHooksFromChildren
                        ? gameObject.GetComponentsInChildren<IPoolable>(true)
                        : gameObject.GetComponents<IPoolable>();

                case Component component:
                {
                    var hooks = new System.Collections.Generic.List<IPoolable>(2);
                    if (component is IPoolable self)
                    {
                        hooks.Add(self);
                    }

                    if (_config.CollectHooksFromChildren)
                    {
                        hooks.AddRange(component.GetComponentsInChildren<IPoolable>(true));
                    }
                    else
                    {
                        hooks.AddRange(component.GetComponents<IPoolable>());
                    }

                    return hooks.ToArray();
                }

                default:
                    return item is IPoolable single ? new IPoolable[] { single } : Array.Empty<IPoolable>();
            }
        }

        /// <summary>
        /// 触发钩子。租用时按收集顺序执行，归还时按相反顺序执行。
        /// </summary>
        private static void InvokeHooks(IPoolable[] hooks, bool rented)
        {
            if (hooks == null || hooks.Length == 0)
            {
                return;
            }

            if (rented)
            {
                for (int index = 0; index < hooks.Length; index++)
                {
                    hooks[index]?.OnRentFromPool();
                }

                return;
            }

            for (int index = hooks.Length - 1; index >= 0; index--)
            {
                hooks[index]?.OnReturnToPool();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ObjectPool<TObject>), "对象池 " + _config.Name + " 已释放。");
            }
        }
    }
}
