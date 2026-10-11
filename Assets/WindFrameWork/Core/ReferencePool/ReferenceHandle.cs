namespace WindFrameWork.Core.ReferencePool
{
    /// <summary>
    /// 租约凭证：由 <see cref="IReferencePool{T}.Rent"/> 产出，<see cref="IReferencePool{T}.Return"/> 必须收回同一凭证。
    /// </summary>
    /// <typeparam name="T">池内元素类型。</typeparam>
    /// <remarks>
    /// 归还凭证而非对象，使池得以 O(1) 校验重复归还与陈旧凭证，且无需任何哈希表：
    /// 槽位状态与租出代数即可给出判定，与成功与否、是否与当前池匹配都只在一次数组访问内完成。
    /// <para>
    /// 本类型为只读结构体，存放于栈上，零 GC 分配；严禁存入 <see cref="object"/> 类型的字段或容器，
    /// 否则每次存取都会装箱。
    /// </para>
    /// </remarks>
    public readonly struct ReferenceHandle<T> where T : class
    {
        internal ReferenceHandle(T instance, int slot, int generation, int poolId)
        {
            Object = instance;
            Slot = slot;
            Generation = generation;
            PoolId = poolId;
        }

        /// <summary>租出的对象。</summary>
        public T Object { get; }

        /// <summary>对象所在槽位下标，仅用于诊断泄漏。</summary>
        public int Slot { get; }

        /// <summary>所属池标识，跨池归还校验的依据。</summary>
        public int PoolId { get; }

        /// <summary>租出代数，每次租出自增，用于识别陈旧凭证。</summary>
        internal int Generation { get; }

        /// <summary>凭证是否有效。</summary>
        public bool IsValid => Object != null;

        /// <summary>无效凭证。</summary>
        public static ReferenceHandle<T> Invalid => default;

        /// <inheritdoc />
        public override string ToString()
        {
            return "池 " + PoolId + " 槽位 " + Slot + "#代数 " + Generation;
        }
    }
}
