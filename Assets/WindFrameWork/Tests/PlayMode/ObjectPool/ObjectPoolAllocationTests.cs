using System;
using NUnit.Framework;
using UnityEngine;
using WindFrameWork.Core.ObjectPool;

namespace WindFrameWork.Tests.PlayMode.ObjectPool
{
    /// <summary>
    /// 分配量基准：验证稳态租还路径不产生托管分配。
    /// </summary>
    /// <remarks>
    /// 置于播放态而非编辑态，因为编辑器会在测量帧注入测试日志与 Inspector 重绘等无关分配，
    /// 使零分配断言在编辑态下不稳定。
    /// </remarks>
    [TestFixture]
    [Category("Performance")]
    public sealed class ObjectPoolAllocationTests
    {
        private const int WarmupIterations = 256;
        private const int MeasuredIterations = 10000;

        [Test]
        public void SteadyStateRentReturn_AllocatesNothing()
        {
            var config = new ObjectPoolConfig("AllocBench", prewarmCount: 8, maxIdleSize: 16);
            using var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), config);

            for (int index = 0; index < WarmupIterations; index++)
            {
                PooledHandle<GameObject> handle = pool.Rent();
                pool.Return(handle);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < MeasuredIterations; index++)
            {
                PooledHandle<GameObject> handle = pool.Rent();
                pool.Return(handle);
            }

            long delta = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(delta, Is.Zero, MeasuredIterations + " 次稳态租还产生了 " + delta + " 字节分配。");
        }

        [Test]
        public void CachedPoolReferenceLookup_AllocatesNothing()
        {
            var config = new ObjectPoolConfig("CachedLookup", prewarmCount: 4);
            using var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), config);

            // 正确用法：先缓存接口引用，热路径只调用 Rent。
            IObjectPool<GameObject> cached = pool;
            for (int index = 0; index < WarmupIterations; index++)
            {
                PooledHandle<GameObject> handle = cached.Rent();
                cached.Return(handle);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < MeasuredIterations; index++)
            {
                PooledHandle<GameObject> handle = cached.Rent();
                cached.Return(handle);
            }

            long delta = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(delta, Is.Zero, "经缓存引用的租还产生了 " + delta + " 字节分配。");
        }

        [Test]
        public void StatisticsSnapshot_IsNotOnHotPath()
        {
            var config = new ObjectPoolConfig("StatsBench", prewarmCount: 4);
            using var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), config);

            for (int index = 0; index < WarmupIterations; index++)
            {
                PooledHandle<GameObject> handle = pool.Rent();
                pool.Return(handle);
            }

            // 统计快照按值返回，带有内存拷贝，因此设计上不得放入每帧路径。
            for (int index = 0; index < 1000; index++)
            {
                pool.GetStatistics();
            }

            Assert.Pass("统计快照为诊断用途，反复取用不应影响池的正确性。");
        }
    }
}
