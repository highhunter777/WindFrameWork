namespace WindFrameWork.Core.ObjectPool
{
    /// <summary>
    /// 单个池的诊断行，用于编辑器展示与批量导出。
    /// </summary>
    public readonly struct ObjectPoolInfo
    {
        internal ObjectPoolInfo(string name, System.Type elementType, ObjectPoolStatistics statistics)
        {
            Name = name;
            ElementType = elementType;
            Statistics = statistics;
        }

        /// <summary>池名。</summary>
        public string Name { get; }

        /// <summary>池内元素类型。</summary>
        public System.Type ElementType { get; }

        /// <summary>池的统计快照。</summary>
        public ObjectPoolStatistics Statistics { get; }
    }
}
