using System;
using System.Threading;
using WindFrameWork.Core.ObjectPool;

namespace WindFrameWork.Core.ReferencePool
{
    /// <summary>
    /// 引用池：纯 C# 引用类型对象的复用池，降低 GC 回收压力。
    /// </summary>
    /// <typeparam name="T">池内元素类型，任意引用类型。</typeparam>
    /// <remarks>
    /// 采用槽位表与侵入式空闲链表，租出与归还不分配内存。
    /// <para>
    /// 与 <c>Core/ObjectPool</c> 的分工：后者池化 <see cref="UnityEngine.Object"/>，需要处理 fake-null 与
    /// 主线程约束；本类型池化托管对象，这两类问题都不存在，因此空闲链表无需在取出时做存活性检查，
    /// 线程模型默认可跨线程（见 <see cref="ReferencePoolThreadMode"/>）。
    /// </para>
    /// <para>
    /// 池不重置对象的业务字段：归还时应重置什么属于业务策略，由 <see cref="IPoolable"/> 或构造时传入的
    /// 重置回调表达。池唯一无条件做的是「不再持有引用」，即把对象交还 GC。
    /// </para>
    /// </remarks>
    public sealed class ReferencePool<T> : ReferencePoolBase, IReferencePool<T>, IDisposable where T : class
    {
        private static int _nextPoolId;

        private readonly ReferencePoolConfig _config;
        private readonly ReferencePoolSlotTable<T> _slots;
        private readonly Func<T> _factory;
        private readonly Action<T> _onReset;
        private readonly Action<T> _onRelease;
        private readonly int _poolId;
        private readonly object _gate;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly int _ownerThreadId;
#endif

        private bool _disposed;

        private long _rentCount;
        private long _hitCount;
        private long _missCount;
        private long _returnCount;
        private long _createCount;
        private long _releasedCount;

        /// <summary>
        /// 创建引用池并按配置预热。
        /// </summary>
        /// <param name="factory">对象创建工厂。</param>
        /// <param name="config">池配置。</param>
        /// <param name="onReset">归还时调用的重置回调，用于清理 <c>List</c>、字典等容器内容。</param>
        /// <param name="onRelease">对象被永久移出池（空闲溢出、清空、释放）时调用的回调。</param>
        public ReferencePool(
            Func<T> factory,
            ReferencePoolConfig config,
            Action<T> onReset = null,
            Action<T> onRelease = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _config = config;
            _onReset = onReset;
            _onRelease = onRelease;
            _poolId = ++_nextPoolId;
            _slots = new ReferencePoolSlotTable<T>(config.InitialSlotCapacity);
            _gate = config.ThreadMode == ReferencePoolThreadMode.Synchronized ? new object() : null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _ownerThreadId = Thread.CurrentThread.ManagedThreadId;
#endif

            ReferencePoolRegistry.Register(this);

            Prewarm(config.PrewarmCount);
        }

        /// <inheritdoc />
        public string Name => _config.Name;

        /// <inheritdoc />
        public int IdleCount
        {
            get
            {
                EnterPool();
                try
                {
                    return _slots.IdleCount;
                }
                finally
                {
                    ExitPool();
                }
            }
        }

        /// <inheritdoc />
        public int LiveCount
        {
            get
            {
                EnterPool();
                try
                {
                    return _slots.LiveCount;
                }
                finally
                {
                    ExitPool();
                }
            }
        }

        /// <inheritdoc />
        public ReferencePoolThreadMode ThreadMode => _config.ThreadMode;

        /// <summary>池标识，用于诊断。</summary>
        public int PoolId => _poolId;

        /// <summary>池的元素类型。</summary>
        public Type ElementType => typeof(T);

        /// <inheritdoc />
        public ReferenceHandle<T> Rent()
        {
            EnterPool();
            try
            {
                ThrowIfDisposed();
                AssertThread();

                _rentCount++;

                int slot = _slots.PopFree();
                if (slot >= 0)
                {
                    _hitCount++;
                    return RentSlot(slot);
                }

                if (_slots.LiveCount >= _config.MaxLiveSize)
                {
                    throw new InvalidOperationException(
                        "引用池 " + _config.Name + " 的并发租出数已达上限 " + _config.MaxLiveSize +
                        "（当前 " + _slots.LiveCount + "）。请确认每次租出都已归还。");
                }

                _missCount++;
                return RentSlot(CreateSlot());
            }
            finally
            {
                ExitPool();
            }
        }

        /// <inheritdoc />
        public void Return(ReferenceHandle<T> handle)
        {
            EnterPool();
            try
            {
                ThrowIfDisposed();
                AssertThread();

                if (handle.PoolId != _poolId)
                {
                    throw new InvalidOperationException(
                        "归还的租约凭证属于引用池 #" + handle.PoolId + "，与当前引用池 " + _config.Name +
                        "（#" + _poolId + "）不符。");
                }

                int slot = handle.Slot;
                if (!_slots.IsLeaseValid(slot, handle.Generation))
                {
                    throw new InvalidOperationException(
                        "引用池 " + _config.Name + " 收到无效的归还凭证（槽位 " + slot +
                        "）：可能已被归还，或凭证已陈旧。");
                }

                // 代数在此推进，使同一凭证无法再次归还，即使槽位已被重新租出。
                _slots.NextGeneration(slot);

                T item = _slots.GetItem(slot);
                _returnCount++;

                _slots.GetHook(slot)?.OnReturnToPool();
                _onReset?.Invoke(item);

                if (_slots.IdleCount >= _config.MaxIdleSize)
                {
                    // 空闲已满：解除引用而不是保留。对象是托管的，不必销毁，交给 GC 即可。
                    ReleaseSlot(slot, item);
                    return;
                }

                _slots.PushFree(slot);
            }
            finally
            {
                ExitPool();
            }
        }

        /// <inheritdoc />
        public void Clear()
        {
            EnterPool();
            try
            {
                ThrowIfDisposed();

                ReleaseAllReferences();

                _rentCount = 0;
                _hitCount = 0;
                _missCount = 0;
                _returnCount = 0;
                _createCount = 0;
                _releasedCount = 0;
            }
            finally
            {
                ExitPool();
            }
        }

        /// <inheritdoc />
        public ReferencePoolStatistics GetStatistics()
        {
            EnterPool();
            try
            {
                return new ReferencePoolStatistics(
                    _rentCount,
                    _hitCount,
                    _missCount,
                    _returnCount,
                    _createCount,
                    _releasedCount,
                    _slots.IdleCount,
                    _slots.LiveCount,
                    _slots.PeakLiveCount);
            }
            finally
            {
                ExitPool();
            }
        }

        /// <summary>
        /// 释放池：解除全部对象引用并从全局注册表注销。租出中的对象一并失效，不再被接受归还。
        /// </summary>
        public void Dispose()
        {
            EnterPool();
            try
            {
                if (_disposed)
                {
                    return;
                }

                ReferencePoolRegistry.Unregister(this);
                ReleaseAllReferences();
                _disposed = true;
            }
            finally
            {
                ExitPool();
            }
        }

        /// <inheritdoc />
        internal override void ReleaseEverything()
        {
            ReleaseAllReferences();
        }

        private ReferenceHandle<T> RentSlot(int slot)
        {
            T item = _slots.GetItem(slot);
            int generation = _slots.NextGeneration(slot);

            _slots.MarkRented(slot);

            _slots.GetHook(slot)?.OnRentFromPool();

            return new ReferenceHandle<T>(item, slot, generation, _poolId);
        }

        private int CreateSlot()
        {
            T item = _factory();

            if (item == null)
            {
                throw new InvalidOperationException(
                    "引用池 " + _config.Name + " 的工厂返回了空对象，无法创建实例。");
            }

            _createCount++;

            // 钩子在对象创建时解析一次并按槽位缓存：租还需要 drive O(1) 而不能重复做类型检查。
            return _slots.Attach(item, item as IPoolable);
        }

        /// <summary>
        /// 预热：创建并归还指定数量的对象，使其在首次租出前即处于空闲状态。
        /// </summary>
        private void Prewarm(int count)
        {
            for (int index = 0; index < count; index++)
            {
                int slot = CreateSlot();
                T item = _slots.GetItem(slot);

                _slots.GetHook(slot)?.OnReturnToPool();
                _onReset?.Invoke(item);

                // 预热对象自始即空闲，不经过租出路径，以免污染租出计数与并发峰值。
                _slots.MarkIdle(slot);
            }
        }

        /// <summary>
        /// 空闲容量已满：解除引用，使 <see cref="ReferencePoolConfig.MaxIdleSize"/> 成为可审计的内存硬边界。
        /// </summary>
        private void ReleaseSlot(int slot, T item)
        {
            _slots.MarkDead(slot);
            _onRelease?.Invoke(item);
            _releasedCount++;
        }

        private void ReleaseAllReferences()
        {
            for (int slot = 0; slot < _slots.SlotCount; slot++)
            {
                T item = _slots.GetItem(slot);
                if (item == null)
                {
                    continue;
                }

                _slots.GetHook(slot)?.OnReturnToPool();
                _onRelease?.Invoke(item);
                _releasedCount++;
            }

            _slots.ReleaseAll();
        }

        private void EnterPool()
        {
            if (_gate != null)
            {
                Monitor.Enter(_gate);
            }
        }

        private void ExitPool()
        {
            if (_gate != null)
            {
                Monitor.Exit(_gate);
            }
        }

        private void AssertThread()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_config.ThreadMode != ReferencePoolThreadMode.SingleThread)
            {
                return;
            }

            int current = Thread.CurrentThread.ManagedThreadId;
            if (current != _ownerThreadId)
            {
                throw new InvalidOperationException(
                    "引用池 " + _config.Name + " 以单线程模式创建，却在线程 " + current +
                    " 上被访问（创建线程 " + _ownerThreadId + "）。");
            }
#endif
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ReferencePool<T>), "引用池 " + _config.Name + " 已释放。");
            }
        }
    }
}
