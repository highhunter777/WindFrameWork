using System;
using UnityEngine;

namespace WindFrameWork.Core.ObjectPool
{
    /// <summary>
    /// 对象池配置：集中承载全部可调项，取代散落在逻辑中的魔法数字。
    /// </summary>
    public readonly struct ObjectPoolConfig
    {
        /// <summary>默认预热数量：不预先创建。</summary>
        public const int DefaultPrewarmCount = 0;

        /// <summary>默认空闲保留上限。</summary>
        public const int DefaultMaxIdleSize = 32;

        /// <summary>默认并发租出上限。</summary>
        public const int DefaultMaxLiveSize = 256;

        /// <summary>槽位数组初始容量的推导标记，由预热数量推导。</summary>
        public const int DeriveInitialSlots = -1;

        /// <summary>无预热时槽位数组的基础容量。</summary>
        private const int BaseSlotCapacity = 8;

        private const HideFlags DefaultPooledHideFlags = HideFlags.DontSave;

        /// <summary>
        /// 创建对象池配置。
        /// </summary>
        /// <param name="name">池名，用于诊断与层级节点命名。</param>
        /// <param name="prewarmCount">构造时预先创建并归还的数量。</param>
        /// <param name="maxIdleSize">池内保留的空闲对象上限，超出部分归还时销毁。</param>
        /// <param name="maxLiveSize">同时租出的对象上限，超出时租出失败。</param>
        /// <param name="initialSlotCapacity">槽位数组初始容量，传 <see cref="DeriveInitialSlots"/> 时由预热数量推导。</param>
        /// <param name="activateOnRent">租出时是否激活对象。</param>
        /// <param name="deactivateOnReturn">归还时是否停用对象。</param>
        /// <param name="reparentToPoolRoot">是否将对象挂到池根节点下。</param>
        /// <param name="hidePoolRootInHierarchy">是否在层级窗口隐藏池根节点。</param>
        /// <param name="collectHooksFromChildren">是否连同子节点一起收集生命周期钩子。</param>
        /// <param name="pooledHideFlags">池内对象的隐藏标记。</param>
        public ObjectPoolConfig(
            string name,
            int prewarmCount = DefaultPrewarmCount,
            int maxIdleSize = DefaultMaxIdleSize,
            int maxLiveSize = DefaultMaxLiveSize,
            int initialSlotCapacity = DeriveInitialSlots,
            bool activateOnRent = true,
            bool deactivateOnReturn = true,
            bool reparentToPoolRoot = true,
            bool hidePoolRootInHierarchy = true,
            bool collectHooksFromChildren = false,
            HideFlags pooledHideFlags = DefaultPooledHideFlags)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("池名不能为空。", nameof(name));
            }

            if (prewarmCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(prewarmCount), prewarmCount, "预热数量不能为负。");
            }

            if (maxIdleSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxIdleSize), maxIdleSize, "空闲保留上限不能为负。");
            }

            if (maxLiveSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxLiveSize), maxLiveSize, "并发租出上限必须为正。");
            }

            if (prewarmCount > maxIdleSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(prewarmCount),
                    prewarmCount,
                    "预热数量不能超过空闲保留上限 " + maxIdleSize + "，否则预热产物会立刻被销毁。");
            }

            if (initialSlotCapacity != DeriveInitialSlots && initialSlotCapacity < prewarmCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialSlotCapacity),
                    initialSlotCapacity,
                    "槽位数组初始容量不能小于预热数量 " + prewarmCount + "。");
            }

            Name = name;
            PrewarmCount = prewarmCount;
            MaxIdleSize = maxIdleSize;
            MaxLiveSize = maxLiveSize;
            InitialSlotCapacity = initialSlotCapacity == DeriveInitialSlots
                ? DeriveSlotCapacity(prewarmCount)
                : initialSlotCapacity;
            ActivateOnRent = activateOnRent;
            DeactivateOnReturn = deactivateOnReturn;
            ReparentToPoolRoot = reparentToPoolRoot;
            HidePoolRootInHierarchy = hidePoolRootInHierarchy;
            CollectHooksFromChildren = collectHooksFromChildren;
            PooledHideFlags = pooledHideFlags;
        }

        /// <summary>池名。</summary>
        public string Name { get; }

        /// <summary>构造时预先创建并归还的数量。</summary>
        public int PrewarmCount { get; }

        /// <summary>池内保留的空闲对象上限，即池的内存占用上限。</summary>
        public int MaxIdleSize { get; }

        /// <summary>同时租出的对象上限，即并发上限。</summary>
        public int MaxLiveSize { get; }

        /// <summary>槽位数组初始容量。</summary>
        public int InitialSlotCapacity { get; }

        /// <summary>租出时是否激活对象。</summary>
        public bool ActivateOnRent { get; }

        /// <summary>归还时是否停用对象。</summary>
        public bool DeactivateOnReturn { get; }

        /// <summary>是否将对象挂到池根节点下。</summary>
        public bool ReparentToPoolRoot { get; }

        /// <summary>是否在层级窗口隐藏池根节点。</summary>
        public bool HidePoolRootInHierarchy { get; }

        /// <summary>是否连同子节点一起收集生命周期钩子。</summary>
        public bool CollectHooksFromChildren { get; }

        /// <summary>池内对象的隐藏标记。</summary>
        public HideFlags PooledHideFlags { get; }

        /// <summary>
        /// 使用默认常量创建配置。
        /// </summary>
        public static ObjectPoolConfig Default(string name)
        {
            return new ObjectPoolConfig(name);
        }

        /// <summary>
        /// 由预热数量推导槽位数组初始容量：预热产物加上少量余量，避免首轮租出触发扩容。
        /// </summary>
        private static int DeriveSlotCapacity(int prewarmCount)
        {
            return prewarmCount > 0 ? prewarmCount + DefaultMaxIdleSize : BaseSlotCapacity;
        }
    }
}
