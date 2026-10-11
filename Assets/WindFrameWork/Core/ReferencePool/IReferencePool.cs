namespace WindFrameWork.Core.ReferencePool
{
    /// <summary>
    /// 引用池契约：按租约凭证租出与归还纯 C# 对象。
    /// </summary>
    /// <typeparam name="T">池内元素类型，任意引用类型。</typeparam>
    /// <remarks>
    /// 与 <c>WindFrameWork.Core.ObjectPool</c> 的差别不在于数据结构，而在于被池化对象的性质：
    /// 托管对象不存在 fake-null，也不要求主线程访问，因此本契约不定义任何 Unity 状态开关。
    /// <para>
    /// 线程模型由 <see cref="ReferencePoolConfig.ThreadMode"/> 决定，默认 <see cref="ReferencePoolThreadMode.Synchronized"/>，
    /// 即任意线程均可安全调用。
    /// </para>
    /// </remarks>
    public interface IReferencePool<T> where T : class
    {
        /// <summary>池名。</summary>
        string Name { get; }

        /// <summary>池的当前空闲对象数。</summary>
        int IdleCount { get; }

        /// <summary>池的当前租出对象数。</summary>
        int LiveCount { get; }

        /// <summary>池的线程模型。</summary>
        ReferencePoolThreadMode ThreadMode { get; }

        /// <summary>
        /// 租出一个对象。
        /// </summary>
        /// <returns>租约凭证，归还时必须交回同一凭证。</returns>
        /// <exception cref="System.InvalidOperationException">并发租出数已达上限，或工厂返回了空对象。</exception>
        ReferenceHandle<T> Rent();

        /// <summary>
        /// 归还租约凭证。
        /// </summary>
        /// <remarks>
        /// 重复归还、跨池归还与陈旧凭证一律抛出：这类错误会静默破坏空闲链表并引发更难排查的故障，
        /// 故无条件快速失败，而非引入运行时的集合校验开关。
        /// </remarks>
        void Return(ReferenceHandle<T> handle);

        /// <summary>
        /// 释放池持有的全部对象引用并重置统计计数。
        /// </summary>
        /// <remarks>
        /// 含租出中的对象：清空后全部在外的凭证即告失效，再归还会被拒绝。
        /// </remarks>
        void Clear();

        /// <summary>
        /// 取统计快照。诊断用途，不应放在每帧路径上。
        /// </summary>
        ReferencePoolStatistics GetStatistics();
    }
}
