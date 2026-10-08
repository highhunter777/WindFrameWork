using System.Threading;

namespace WindFrameWork.Core.ObjectPool
{
    /// <summary>
    /// 主线程断言：池在构造时记录线程标识，开发构建下校验租出与归还发生于同一线程。
    /// </summary>
    /// <remarks>
    /// 池不加锁。加锁只能让数据结构线程安全，而 <c>Instantiate</c>、<c>SetActive</c> 等 Unity 调用仍要求主线程，
    /// 加锁反而制造虚假的安全感。断言在发布构建中编译期消除，不产生任何开销。
    /// </remarks>
    internal static class PoolThreadGuard
    {
        static PoolThreadGuard()
        {
            OwnerThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>池所属线程标识。</summary>
        internal static int OwnerThreadId { get; }

        /// <summary>
        /// 断言当前处于池所属线程。
        /// </summary>
        internal static void Assert(int poolId)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int current = Thread.CurrentThread.ManagedThreadId;
            if (current != OwnerThreadId)
            {
                throw new System.InvalidOperationException(
                    "对象池 #" + poolId + " 在非所属线程 " + current + " 上被访问（所属线程 " + OwnerThreadId +
                    "）。Unity 对象操作必须在主线程进行。");
            }
#endif
        }
    }
}
