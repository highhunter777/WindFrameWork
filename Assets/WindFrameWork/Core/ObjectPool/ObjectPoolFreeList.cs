namespace WindFrameWork.Core.ObjectPool
{
    /// <summary>
    /// 槽位状态。
    /// </summary>
    internal enum SlotState : byte
    {
        /// <summary>空闲且持有可用对象。</summary>
        Free = 0,

        /// <summary>已租出。</summary>
        InUse = 1,

        /// <summary>对象已被外部销毁，不再持有引用。</summary>
        Dead = 2
    }

    /// <summary>
    /// 槽位表 + 侵入式空闲链表：进出均为 O(1)，稳态零分配，且不依赖任何等值比较。
    /// </summary>
    /// <remarks>
    /// 空闲链表以槽位下标串联，存放于并行的 <c>int[]</c> 中，因此无需为每个空闲对象分配链表节点。
    /// 链式集合（<c>LinkedList&lt;T&gt;</c>）会在每次归还时分配节点，是归还路径上的 GC 陷阱，故不采用；
    /// 栈结构则无法表达槽位状态与代数，无法支撑句柄校验。
    /// </remarks>
    internal sealed class ObjectPoolFreeList
    {
        private const int EmptyList = -1;

        private UnityEngine.Object[] _objects;
        private IPoolable[][] _hooksBySlot;
        private int[] _nextFreeBySlot;
        private int[] _generationBySlot;
        private SlotState[] _stateBySlot;

        private int _freeHead = EmptyList;
        private int _slotCount;
        private int _idleCount;
        private int _liveCount;
        private int _deadCount;
        private int _peakLiveCount;

        internal ObjectPoolFreeList(int initialSlotCapacity)
        {
            if (initialSlotCapacity < 1)
            {
                initialSlotCapacity = 1;
            }

            _objects = new UnityEngine.Object[initialSlotCapacity];
            _hooksBySlot = new IPoolable[initialSlotCapacity][];
            _nextFreeBySlot = new int[initialSlotCapacity];
            _generationBySlot = new int[initialSlotCapacity];
            _stateBySlot = new SlotState[initialSlotCapacity];
        }

        /// <summary>槽位总数。</summary>
        internal int SlotCount => _slotCount;

        /// <summary>空闲对象数。</summary>
        internal int IdleCount => _idleCount;

        /// <summary>租出中的对象数。</summary>
        internal int LiveCount => _liveCount;

        /// <summary>已销毁槽位数。</summary>
        internal int DeadCount => _deadCount;

        /// <summary>并发租出峰值。</summary>
        internal int PeakLiveCount => _peakLiveCount;

        /// <summary>空闲链表是否为空。</summary>
        internal bool HasIdle => _freeHead != EmptyList;

        /// <summary>
        /// 追加一个已创建对象，使其成为新槽位。租出与空闲计数交由调用方按后续流转更新，
        /// 以便预热流程不经由租出路径、不污染并发峰值统计。
        /// </summary>
        internal int Attach(UnityEngine.Object item, IPoolable[] hooks)
        {
            int slot = AllocateSlot();
            _objects[slot] = item;
            _hooksBySlot[slot] = hooks;
            _generationBySlot[slot] = 0;
            _stateBySlot[slot] = SlotState.Dead;
            return slot;
        }

        /// <summary>
        /// 标记槽位为租出中并更新并发统计。
        /// </summary>
        internal void MarkRented(int slot)
        {
            _stateBySlot[slot] = SlotState.InUse;

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
            _stateBySlot[slot] = SlotState.Free;
            _idleCount++;
        }

        /// <summary>
        /// 取下一个可用空闲槽位并从空闲链表摘除；调用方需自行校验对象是否已被销毁。
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
            _stateBySlot[slot] = SlotState.Free;
            _idleCount++;
            _liveCount--;
        }

        /// <summary>
        /// 将已租出的槽位标记为失效：清除引用并扣减租出计数，使其不再参与租出。
        /// </summary>
        /// <remarks>槽位须已计入租出，即已调用 <see cref="MarkRented"/>。</remarks>
        internal void MarkDead(int slot)
        {
            ClearSlot(slot);
            _liveCount--;
        }

        /// <summary>
        /// 将空闲链表中的槽位标记为失效：清除引用并移出空闲链表。
        /// </summary>
        /// <remarks>
        /// 该槽位此前处于空闲、未计入租出，因此不调整租出计数。
        /// </remarks>
        internal void MarkIdleSlotDead(int slot)
        {
            ClearSlot(slot);
        }

        private void ClearSlot(int slot)
        {
            _objects[slot] = null;
            _hooksBySlot[slot] = null;
            _stateBySlot[slot] = SlotState.Dead;
            _deadCount++;
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

            return _stateBySlot[slot] == SlotState.InUse && _generationBySlot[slot] == generation;
        }

        /// <summary>
        /// 租出代数，用于下次租出自增，从而使旧凭证失效。
        /// </summary>
        internal int NextGeneration(int slot)
        {
            return ++_generationBySlot[slot];
        }

        internal UnityEngine.Object GetObject(int slot)
        {
            return _objects[slot];
        }

        internal IPoolable[] GetHooks(int slot)
        {
            return _hooksBySlot[slot];
        }

        /// <summary>
        /// 清空全部计数与空闲链表，保留槽位数组容量以便复用。
        /// </summary>
        internal void RecycleAll()
        {
            for (int slot = 0; slot < _slotCount; slot++)
            {
                _objects[slot] = null;
                _hooksBySlot[slot] = null;
                _nextFreeBySlot[slot] = EmptyList;
                _stateBySlot[slot] = SlotState.Dead;
            }

            _freeHead = EmptyList;
            _slotCount = 0;
            _idleCount = 0;
            _liveCount = 0;
            _deadCount = 0;
            _peakLiveCount = 0;
        }

        private int AllocateSlot()
        {
            if (_slotCount == _objects.Length)
            {
                Grow();
            }

            return _slotCount++;
        }

        private void Grow()
        {
            int newCapacity = _objects.Length * 2;
            System.Array.Resize(ref _objects, newCapacity);
            System.Array.Resize(ref _hooksBySlot, newCapacity);
            System.Array.Resize(ref _nextFreeBySlot, newCapacity);
            System.Array.Resize(ref _generationBySlot, newCapacity);
            System.Array.Resize(ref _stateBySlot, newCapacity);
        }
    }
}
