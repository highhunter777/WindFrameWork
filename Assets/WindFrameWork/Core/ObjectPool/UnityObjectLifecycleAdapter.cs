using UnityEngine;

namespace WindFrameWork.Core.ObjectPool
{
    /// <summary>
    /// 内置生命周期适配器：统一处理 <see cref="GameObject"/>、<see cref="Component"/> 与 <see cref="ScriptableObject"/>。
    /// </summary>
    /// <remarks>
    /// 无状态，以静态单例缓存，为每个池不产生额外分配。
    /// </remarks>
    public sealed class UnityObjectLifecycleAdapter<TObject> : IPoolLifecycleAdapter<TObject> where TObject : Object
    {
        private static readonly UnityObjectLifecycleAdapter<TObject> Shared = new UnityObjectLifecycleAdapter<TObject>();

        private UnityObjectLifecycleAdapter()
        {
        }

        /// <summary>共享实例。</summary>
        public static UnityObjectLifecycleAdapter<TObject> Instance => Shared;

        /// <inheritdoc />
        public void OnCreated(TObject item, Transform poolRoot, ObjectPoolConfig config)
        {
            if (item == null || !config.ReparentToPoolRoot || poolRoot == null)
            {
                return;
            }

            GameObject target = ResolveGameObject(item);
            if (target == null)
            {
                return;
            }

            target.hideFlags = config.PooledHideFlags;
            target.transform.SetParent(poolRoot, false);
        }

        /// <inheritdoc />
        public void OnRent(TObject item)
        {
            SetActiveState(item, true);
        }

        /// <inheritdoc />
        public void OnReturned(TObject item)
        {
            SetActiveState(item, false);
        }

        /// <inheritdoc />
        public void OnBeforeDestroy(TObject item)
        {
            if (item == null)
            {
                return;
            }

            GameObject target = ResolveGameObject(item);
            if (target != null)
            {
                target.transform.SetParent(null, false);
            }
        }

        /// <summary>
        /// 取得承载激活态与层级的 GameObject；ScriptableObject 无承载对象，返回 null。
        /// </summary>
        private static GameObject ResolveGameObject(TObject item)
        {
            switch (item)
            {
                case GameObject gameObject:
                    return gameObject;

                case Component component:
                    return component.gameObject;

                default:
                    // ScriptableObject 等类型没有激活态与层级概念。
                    return null;
            }
        }

        private static void SetActiveState(TObject item, bool active)
        {
            GameObject target = ResolveGameObject(item);
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }
}
