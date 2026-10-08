using System;

namespace WindFrameWork.Core.ObjectPool
{
    /// <summary>
    /// 一条池注册记录：记录元素类型与池实例，并保留类型安全的统计读取入口。
    /// </summary>
    /// <remarks>
    /// 池以接口形式存入 <c>object</c> 字段：接口为引用类型，存取不产生装箱。
    /// 统计读取入口在注册时由泛型实参捕获，因此无需反射即可取回统计。
    /// </remarks>
    internal sealed class PoolRegistration
    {
        private readonly Func<ObjectPoolStatistics> _statisticsAccessor;

        internal PoolRegistration(Type elementType, object pool, Func<ObjectPoolStatistics> statisticsAccessor)
        {
            ElementType = elementType;
            Pool = pool;
            _statisticsAccessor = statisticsAccessor;
        }

        /// <summary>池的元素类型。</summary>
        internal Type ElementType { get; }

        /// <summary>池实例。</summary>
        internal object Pool { get; }

        /// <summary>取池的统计快照。</summary>
        internal ObjectPoolStatistics GetStatistics()
        {
            return _statisticsAccessor();
        }
    }
}
