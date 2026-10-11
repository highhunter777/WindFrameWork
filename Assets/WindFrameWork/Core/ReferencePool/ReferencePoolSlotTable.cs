using WindFrameWork.Core.ObjectPool;

namespace WindFrameWork.Core.ReferencePool
{
    /// <summary>
    /// 槽位状态。
    /// </summary>
    internal enum ReferenceSlotState : byte
    {
        /// <summary>空闲且持有可用对象。</summary>
        Free = 0,

        /// <summary>已租出。</summary>
        InUse = 1,

        /// <summary>对象已被移出池，不再持有引用。</summary>
        Dead = 2
    }

    /// <summary>
    /// 槽位表 + 侵入式空闲链表：进出均为 O(1)，稳态零分配，且不依赖任何等值比较。
    /// </summary>
    /// <typeparam name="T">池中元素类型。</typeparam>
    /// <remarks>
    /// 结构与 <c>Core/ObjectPool</c> 的槽位表同构，差异只有两处，均由「元素是纯托管对象」这一点推出：
    /// 一是空闲链表中的槽位无需在取出时做 fake-null 检查，托管对象不会在池外被销毁；
    /// 二是生命周期钩子按单引用缓存而非数组——托管元素没有 <c>GameObject</c> 那样的组件层级，
    /// 每个槽位至多一个钩子，用数组支付一次多余分配毫无收益。
    /// </remarks>
    internal sealed class ReferencePoolSlotTable<T> where T : class
    {
        private const int EmptyList = -1;

        private T[] _items;
        private IPoolable[] _hookBySlot;
        private int[] _nextFreeBySlot;
        private int[] _generationBySlot;
        private ReferenceSlotState[] _stateBySlot;

        private int _freeHead = EmptyList;
        private int _slotCount;
        private int _idleCount;
        private int _liveCount;
        private int _peakLiveCount;

        internal ReferencePoolSlotTable(int initialSlotCapacity)
        {
            if (initialSlotCapacity < 1)
            {
                initialSlotCapacity = 1;
            }

            _items = new T[initialSlotCapacity];
            _hookBySlot = new IPoolable[initialSlotCapacity];
            _nextFreeBySlot = new int[initialSlotCapacity];
            _generationBySlot = new int[initialSlotCapacity];
            _stateBySlot = new ReferenceSlotState[initialSlotCapacity];
        }

        /// <summary>槽位总数。</summary>
        internal int SlotCount => _slotCount;

        /// <summary>空闲对象数。</summary>
        internal int IdleCount => _idleCount;

        /// <summary>租出中的对象数。</summary>
        internal int LiveCount => _liveCount;

        /// <summary>并发租出峰值。</summary>
        internal int PeakLiveCount => _peakLiveCount;

        /// <summary>空闲链表是否为空。</summary>
        internal bool HasIdle => _freeHead != EmptyList;

        /// <summary>
        /// 追加一个已创建对象，使其成为新槽位。租出与空闲计数交由调用方按后续流转更新，
        /// 以便预热流程不经由租出路径、不污染并发峰值统计。
        /// </summary>
        internal int Attach(T item, IPoolable hook)
        {
            int slot = AllocateSlot();
            _items[slot] = item;
            _hookBySlot[slot] = hook;
            _generationBySlot[slot] = 0;
            _stateBySlot[slot] = ReferenceSlotState.Dead;
            return slot;
        }

        /// <summary>
        /// 标记槽位为租出中并更新并发统计。
        /// </summary>
        internal void MarkRented(int slot)
        {
            _stateBySlot[slot] = ReferenceSlotState.InUse;

            _liveCount++;
            if (_liveCount > _peakLiveCount)
            {
                _peakLiveCount = _liveCount;
            }
        }

        /// <summary>
        /// 将新追加的槽位直接置为空闲并入链，不经过租出路径，因此不影响租出计数与并发峰值。
        /// </summary>
        internal void MarkIdle(int slot)
        {
            _nextFreeBySlot[slot] = _freeHead;
            _freeHead = slot;
            _stateBySlot[slot] = ReferenceSlotState.Free;
            _idleCount++;
        }

        /// <summary>
        /// 取下一个空闲槽位并从空闲链表摘除。
        /// </summary>
        /// <remarks>
        /// 此处只调整空闲计数。租出计数与并发峰值统一由 <see cref="MarkRented"/> 负责，避免重复计数。
        /// </remarks>
        internal int PopFree()
        {
            int slot = _freeHead;
            if (slot == EmptyList)
            {
                return EmptyList;
            }

            _freeHead = _nextFreeBySlot[slot];
            _nextFreeBySlot[slot] = EmptyList;
            _idleCount--;
            return slot;
        }

        /// <summary>
        /// 将槽位放回空闲链表。
        /// </summary>
        internal void PushFree(int slot)
        {
            _nextFreeBySlot[slot] = _freeHead;
            _freeHead = slot;
            _stateBySlot[slot] = ReferenceSlotState.Free;
            _idleCount++;
            _liveCount--;
        }

        /// <summary>
        /// 将已租出的槽位标记为失效：清除引用并扣减租出计数，使其不再参与租出。
        /// </summary>
        internal void MarkDead(int slot)
        {
            _items[slot] = null;
            _hookBySlot[slot] = null;
            _stateBySlot[slot] = ReferenceSlotState.Dead;
            _liveCount--;
        }

        /// <summary>
        /// 校验归还凭证：槽位归属、代数与状态须与租出时一致。
        /// </summary>
        internal bool IsLeaseValid(int slot, int generation)
        {
            if (slot < 0 || slot >= _slotCount)
            {
                return false;
            }

            return _stateBySlot[slot] == ReferenceSlotState.InUse && _generationBySlot[slot] == generation;
        }

        /// <summary>
        /// 推进租出代数并返回推进后的值，从而使旧凭证失效。
        /// </summary>
        internal int NextGeneration(int slot)
        {
            return ++_generationBySlot[slot];
        }

        internal T GetItem(int slot)
        {
            return _items[slot];
        }

        internal IPoolable GetHook(int slot)
        {
            return _hookBySlot[slot];
        }

        /// <summary>
        /// 清空全部引用与空闲链表，保留槽位数组容量以便复用。
        /// </summary>
        internal void ReleaseAll()
        {
            for (int slot = 0; slot < _slotCount; slot++)
            {
                _items[slot] = null;
                _hookBySlot[slot] = null;
                _nextFreeBySlot[slot] = EmptyList;
                _stateBySlot[slot] = ReferenceSlotState.Dead;
            }

            _freeHead = EmptyList;
            _slotCount = 0;
            _idleCount = 0;
            _liveCount = 0;
            _peakLiveCount = 0;
        }

        private int AllocateSlot()
        {
            if (_slotCount == _items.Length)
            {
                Grow();
            }

            return _slotCount++;
        }

        private void Grow()
        {
            int newCapacity = _items.Length * 2;
            System.Array.Resize(ref _items, newCapacity);
            System.Array.Resize(ref _hookBySlot, newCapacity);
            System.Array.Resize(ref _nextFreeBySlot, newCapacity);
            System.Array.Resize(ref _generationBySlot, newCapacity);
            System.Array.Resize(ref _stateBySlot, newCapacity);
        }
    }
}
