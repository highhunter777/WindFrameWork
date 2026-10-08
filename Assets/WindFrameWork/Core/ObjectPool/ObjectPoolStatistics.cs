namespace WindFrameWork.Core.ObjectPool
{
    /// <summary>
    /// 对象池统计快照。
    /// </summary>
    /// <remarks>
    /// 诊断用途，不应放在每帧路径上：结构体按值返回会带来内存拷贝。
    /// </remarks>
    public readonly struct ObjectPoolStatistics
    {
        internal ObjectPoolStatistics(
            long rentCount,
            long hitCount,
            long missCount,
            long returnCount,
            long createCount,
            long destroyCount,
            long discardedDestroyedCount,
            int idleCount,
            int liveCount,
            int peakLiveCount)
        {
            RentCount = rentCount;
            HitCount = hitCount;
            MissCount = missCount;
            ReturnCount = returnCount;
            CreateCount = createCount;
            DestroyCount = destroyCount;
            DiscardedDestroyedCount = discardedDestroyedCount;
            IdleCount = idleCount;
            LiveCount = liveCount;
            PeakLiveCount = peakLiveCount;
        }

        /// <summary>租出调用总次数。</summary>
        public long RentCount { get; }

        /// <summary>命中空闲对象的次数。</summary>
        public long HitCount { get; }

        /// <summary>因空闲池为空而新建的次数。</summary>
        public long MissCount { get; }

        /// <summary>归还次数。</summary>
        public long ReturnCount { get; }

        /// <summary>累计创建的对象数，含预热与溢出补建。</summary>
        public long CreateCount { get; }

        /// <summary>累计销毁的对象数，来自空闲溢出或清空。</summary>
        public long DestroyCount { get; }

        /// <summary>
        /// 发现对象已被外部销毁而丢弃的次数。
        /// </summary>
        /// <remarks>
        /// 生产环境出现非零值意味着池外代码在直接销毁池内对象，属需追查的缺陷。
        /// </remarks>
        public long DiscardedDestroyedCount { get; }

        /// <summary>当前空闲对象数。</summary>
        public int IdleCount { get; }

        /// <summary>当前租出中的对象数。</summary>
        public int LiveCount { get; }

        /// <summary>历史并发租出峰值。</summary>
        public int PeakLiveCount { get; }

        /// <summary>池内对象总数，含空闲与租出。</summary>
        public int TotalCount => IdleCount + LiveCount;

        /// <summary>命中率，未发生租出时为 0。</summary>
        public float HitRate => RentCount > 0 ? (float)HitCount / RentCount : 0f;
    }
}
