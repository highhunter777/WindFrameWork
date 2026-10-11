namespace WindFrameWork.Core.ReferencePool
{
    /// <summary>
    /// 引用池统计快照。
    /// </summary>
    /// <remarks>
    /// 诊断用途，不应放在每帧路径上：结构体按值返回会带来内存拷贝。
    /// </remarks>
    public readonly struct ReferencePoolStatistics
    {
        internal ReferencePoolStatistics(
            long rentCount,
            long hitCount,
            long missCount,
            long returnCount,
            long createCount,
            long releasedCount,
            int idleCount,
            int liveCount,
            int peakLiveCount)
        {
            RentCount = rentCount;
            HitCount = hitCount;
            MissCount = missCount;
            ReturnCount = returnCount;
            CreateCount = createCount;
            ReleasedCount = releasedCount;
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

        /// <summary>
        /// 累计解除引用并交还 GC 的对象数，来自空闲溢出、清空与释放。
        /// </summary>
        /// <remarks>
        /// 该值与 <see cref="CreateCount"/> 之差即为当前仍被引擎外持有的对象数：
        /// 持续增长说明调用方漏归还。
        /// </remarks>
        public long ReleasedCount { get; }

        /// <summary>当前空闲对象数。</summary>
        public int IdleCount { get; }

        /// <summary>当前租出中的对象数。</summary>
        public int LiveCount { get; }

        /// <summary>历史并发租出峰值。</summary>
        public int PeakLiveCount { get; }

        /// <summary>池持有的对象总数，含空闲与租出。</summary>
        public int TotalCount => IdleCount + LiveCount;

        /// <summary>命中率，未发生租出时为 0。</summary>
        public float HitRate => RentCount > 0 ? (float)HitCount / RentCount : 0f;

        /// <summary>
        /// 未归还的对象数，即 <see cref="CreateCount"/> 减去累计释放数后仍在池内的部分。
        /// </summary>
        public int OutstandingCount => TotalCount;
    }
}
