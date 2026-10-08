using UnityEngine;

namespace WindFrameWork.Core.ObjectPool
{
    /// <summary>
    /// 池根节点：为每个池在层级中提供统一的归置父节点，并承载编辑器隐藏策略。
    /// </summary>
    /// <remarks>
    /// 池根带 <see cref="HideFlags.DontSave"/>，因而不会被写入场景存档；
    /// 代价是它不随场景卸载存活，跨场景池需由调用方挂到常驻对象下。
    /// </remarks>
    internal static class ObjectPoolRoot
    {
        private const string RootName = "[ObjectPoolRoot]";

        /// <summary>
        /// 取得（或按需创建）指定池的根节点。
        /// </summary>
        internal static Transform GetOrCreate(Transform sharedRoot, string poolName, bool hideInHierarchy)
        {
            if (sharedRoot == null)
            {
                return null;
            }

            Transform child = sharedRoot.Find(poolName);
            if (child != null)
            {
                return child;
            }

            var node = new GameObject(poolName);
            var nodeTransform = node.transform;
            nodeTransform.SetParent(sharedRoot, false);
            node.hideFlags = HideFlags.DontSave;

            if (hideInHierarchy)
            {
                node.hideFlags |= HideFlags.HideInHierarchy;
            }

            return nodeTransform;
        }

        /// <summary>
        /// 创建共享根节点。
        /// </summary>
        internal static Transform CreateSharedRoot(Transform parent)
        {
            var root = new GameObject(RootName);
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            root.hideFlags = HideFlags.DontSave;
            return root.transform;
        }
    }
}
