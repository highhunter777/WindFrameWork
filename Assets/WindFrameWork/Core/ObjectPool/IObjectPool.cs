using UnityEngine;

namespace WindFrameWork.Core.ObjectPool
{
    /// <summary>
    /// 对象池契约：按租约凭证租出与归还。
    /// </summary>
    /// <remarks>
    /// 仅主线程可用：池内的 Unity 对象操作要求主线程，池不加锁以免数据结构与 Unity 调用之间出现虚假的安全感。
    /// </remarks>
    public interface IObjectPool<TObject> where TObject : Object
    {
        /// <summary>池名。</summary>
        string Name { get; }

        /// <summary>池的当前空闲对象数。</summary>
        int IdleCount { get; }

        /// <summary>池的当前租出对象数。</summary>
        int LiveCount { get; }

        /// <summary>
        /// 租出一个对象。
        /// </summary>
        /// <exception cref="System.InvalidOperationException">并发租出数已达上限时抛出，通常意味着调用方漏归还。</exception>
        PooledHandle<TObject> Rent();

        /// <summary>
        /// 归还租约凭证。
        /// </summary>
        /// <remarks>
        /// 重复归还、跨池归还与陈旧凭证一律抛出：这类错误会静默破坏空闲链表并引发更难排查的故障，故无条件快速失败。
        /// 归还的对象若已被外部销毁，则安全丢弃而不抛出——池不是对象生命周期的所有者。
        /// </remarks>
        void Return(PooledHandle<TObject> handle);

        /// <summary>
        /// 销毁全部空闲对象并重置统计计数，累计计数保留。
        /// </summary>
        void Clear();

        /// <summary>
        /// 取统计快照。诊断用途，不应放在每帧路径上。
        /// </summary>
        ObjectPoolStatistics GetStatistics();
    }
}
