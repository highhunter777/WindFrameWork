namespace WindFrameWork.Core.ReferencePool
{
    /// <summary>
    /// 引用池配置：集中承载全部可调项，取代散落在逻辑中的魔法数字。
    /// </summary>
    public readonly struct ReferencePoolConfig
    {
        /// <summary>默认预热数量：不预先创建。</summary>
        public const int DefaultPrewarmCount = 0;

        /// <summary>默认空闲保留上限。</summary>
        public const int DefaultMaxIdleSize = 64;

        /// <summary>默认并发租出上限。</summary>
        public const int DefaultMaxLiveSize = 1024;

        /// <summary>槽位数组初始容量的推导标记，由预热数量推导。</summary>
        public const int DeriveInitialSlots = -1;

        /// <summary>无预热时槽位数组的基础容量。</summary>
        private const int BaseSlotCapacity = 8;

        /// <summary>
        /// 创建引用池配置。
        /// </summary>
        /// <param name="name">池名，用于诊断。</param>
        /// <param name="prewarmCount">构造时预先创建并归还的数量。</param>
        /// <param name="maxIdleSize">池内保留的空闲对象上限，超出部分在归还时解除引用并交还 GC。</param>
        /// <param name="maxLiveSize">同时租出的对象上限，超出时租出失败。</param>
        /// <param name="initialSlotCapacity">槽位数组初始容量，传 <see cref="DeriveInitialSlots"/> 时由预热数量推导。</param>
        /// <param name="threadMode">线程模型，默认为 <see cref="ReferencePoolThreadMode.Synchronized"/>。</param>
        public ReferencePoolConfig(
            string name,
            int prewarmCount = DefaultPrewarmCount,
            int maxIdleSize = DefaultMaxIdleSize,
            int maxLiveSize = DefaultMaxLiveSize,
            int initialSlotCapacity = DeriveInitialSlots,
            ReferencePoolThreadMode threadMode = ReferencePoolThreadMode.Synchronized)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new System.ArgumentException("池名不能为空。", nameof(name));
            }

            if (prewarmCount < 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(prewarmCount), prewarmCount, "预热数量不能为负。");
            }

            if (maxIdleSize < 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(maxIdleSize), maxIdleSize, "空闲保留上限不能为负。");
            }

            if (maxLiveSize <= 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(maxLiveSize), maxLiveSize, "并发租出上限必须为正。");
            }

            if (prewarmCount > maxIdleSize)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(prewarmCount),
                    prewarmCount,
                    "预热数量不能超过空闲保留上限 " + maxIdleSize + "，否则预热产物会立刻在首次归还时被丢弃。");
            }

            if (initialSlotCapacity != DeriveInitialSlots && initialSlotCapacity < prewarmCount)
            {
                throw new System.ArgumentOutOfRangeException(
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
            ThreadMode = threadMode;
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

        /// <summary>池的线程模型。</summary>
        public ReferencePoolThreadMode ThreadMode { get; }

        /// <summary>
        /// 使用默认常量创建配置。
        /// </summary>
        public static ReferencePoolConfig Default(string name)
        {
            return new ReferencePoolConfig(name);
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
