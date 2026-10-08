using System.Collections.Generic;
using UnityEngine;

namespace WindFrameWork.Core.ObjectPool
{
    /// <summary>
    /// 对象池全局注册表：在引擎生命周期边界统一清理全部池。
    /// </summary>
    /// <remarks>
    /// 池在构造时自注册，因此无需由业务代码集中管理其生命周期，即可避免编辑器跨会话残留。
    /// </remarks>
    public static class ObjectPoolRegistry
    {
        private static readonly List<ObjectPoolBase> Pools = new List<ObjectPoolBase>();

        /// <summary>
        /// 当前已注册的池数量。
        /// </summary>
        public static int Count => Pools.Count;

        /// <summary>
        /// 注册一个池。
        /// </summary>
        public static void Register(ObjectPoolBase pool)
        {
            if (pool != null && !Pools.Contains(pool))
            {
                Pools.Add(pool);
            }
        }

        /// <summary>
        /// 注销一个池。
        /// </summary>
        public static void Unregister(ObjectPoolBase pool)
        {
            if (pool != null)
            {
                Pools.Remove(pool);
            }
        }

        /// <summary>
        /// 清空全部池的对象引用，不销毁任何 Unity 对象。
        /// </summary>
        /// <remarks>
        /// 供 <c>SubsystemRegistration</c> 阶段调用：域重载与「关闭 Reload Domain 进入播放态」两种情况都会触发该阶段，
        /// 而此阶段并非播放态，调用销毁是非法的。
        /// </remarks>
        public static void RecycleAll()
        {
            for (int index = 0; index < Pools.Count; index++)
            {
                Pools[index].Recycle();
            }

            Pools.Clear();
        }

        /// <summary>
        /// 销毁全部池内对象并清空注册表，供编辑器程序集重载前调用。
        /// </summary>
        public static void DestroyAll()
        {
            for (int index = Pools.Count - 1; index >= 0; index--)
            {
                Pools[index].DestroyEverything();
            }

            Pools.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnSubsystemRegistration()
        {
            // 静态容器可能跨播放会话残留，此处重置以确保不会把失效状态带入新会话。
            for (int index = 0; index < Pools.Count; index++)
            {
                Pools[index].Recycle();
            }

            Pools.Clear();
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void OnEditorLoad()
        {
            UnityEditor.EditorApplication.quitting += OnEditorQuitting;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        private static void OnEditorQuitting()
        {
            if (Application.isPlaying)
            {
                DestroyAll();
            }
        }

        private static void OnBeforeAssemblyReload()
        {
            if (Application.isPlaying)
            {
                DestroyAll();
            }
        }
#endif
    }
}
