using System.Collections.Generic;
using UnityEngine;

namespace WindFrameWork.Core.ReferencePool
{
    /// <summary>
    /// 引用池全局注册表：在引擎生命周期边界统一解除全部池的对象引用。
    /// </summary>
    /// <remarks>
    /// 池在构造时自注册，因此无需由业务代码集中管理其生命周期，即可避免静态容器把上一会话的对象带进新会话。
    /// <para>
    /// 与 <see cref="ObjectPool.ObjectPoolRegistry"/> 的差异仅来自元素性质：托管对象的引用在任何时机都可以解除，
    /// 不像 Unity 对象那样受「非播放态不得销毁」约束，因此这里不需要 <c>Application.isPlaying</c> 守卫，
    /// 也不需要在编辑器程序集重载前另做一次销毁。
    /// </para>
    /// <para>
    /// 注册表的写入需要互斥：同步模式的池可能在任意线程上创建，注册因而被多个线程同时触发。
    /// </para>
    /// </remarks>
    public static class ReferencePoolRegistry
    {
        private static readonly List<ReferencePoolBase> Pools = new List<ReferencePoolBase>();
        private static readonly object Gate = new object();

        /// <summary>当前已注册的池数量。</summary>
        public static int Count
        {
            get
            {
                lock (Gate)
                {
                    return Pools.Count;
                }
            }
        }

        /// <summary>
        /// 注册一个池。
        /// </summary>
        public static void Register(ReferencePoolBase pool)
        {
            if (pool == null)
            {
                return;
            }

            lock (Gate)
            {
                if (!Pools.Contains(pool))
                {
                    Pools.Add(pool);
                }
            }
        }

        /// <summary>
        /// 注销一个池。
        /// </summary>
        public static void Unregister(ReferencePoolBase pool)
        {
            if (pool == null)
            {
                return;
            }

            lock (Gate)
            {
                Pools.Remove(pool);
            }
        }

        /// <summary>
        /// 解除全部池的对象引用并清空注册表。
        /// </summary>
        public static void ReleaseAll()
        {
            lock (Gate)
            {
                for (int index = 0; index < Pools.Count; index++)
                {
                    Pools[index].ReleaseEverything();
                }

                Pools.Clear();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnSubsystemRegistration()
        {
            // 静态容器可能跨播放会话残留，此处释放以确保不会把失效对象引用带入新会话。
            ReleaseAll();
        }
    }
}
