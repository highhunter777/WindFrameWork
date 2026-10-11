using System;
using System.Collections.Generic;
using NUnit.Framework;
using WindFrameWork.Core.ReferencePool;

namespace WindFrameWork.Tests.PlayMode.ReferencePool
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
    public sealed class ReferencePoolAllocationTests
    {
        private const int WarmupIterations = 256;
        private const int MeasuredIterations = 10000;

        private sealed class Node
        {
            public int Value;
        }

        [Test]
        public void SteadyStateRentReturn_AllocatesNothing()
        {
            var config = new ReferencePoolConfig("AllocBench", prewarmCount: 8, maxIdleSize: 16);
            using var pool = new ReferencePool<Node>(() => new Node(), config);

            WarmUp(pool);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < MeasuredIterations; index++)
            {
                ReferenceHandle<Node> handle = pool.Rent();
                pool.Return(handle);
            }

            long delta = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(delta, Is.Zero, MeasuredIterations + " 次稳态租还产生了 " + delta + " 字节分配。");
        }

        [Test]
        public void ContainerWithResetCallback_AllocatesNothing()
        {
            // 对容器类元素而言，归还即清空；该回调同样不得带来任何分配。
            var config = new ReferencePoolConfig("ListBench", prewarmCount: 4);
            using var pool = new ReferencePool<List<int>>(
                () => new List<int>(),
                config,
                onReset: list => list.Clear());

            for (int index = 0; index < WarmupIterations; index++)
            {
                ReferenceHandle<List<int>> handle = pool.Rent();
                pool.Return(handle);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < MeasuredIterations; index++)
            {
                ReferenceHandle<List<int>> handle = pool.Rent();
                pool.Return(handle);
            }

            long delta = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(delta, Is.Zero, "带重置回调的稳态租还产生了 " + delta + " 字节分配。");
        }

        [Test]
        public void StatisticsSnapshot_IsNotOnHotPath()
        {
            var config = new ReferencePoolConfig("StatsBench", prewarmCount: 4);
            using var pool = new ReferencePool<Node>(() => new Node(), config);

            WarmUp(pool);

            // 统计快照按值返回，带有内存拷贝，因此设计上不得放入每帧路径。
            for (int index = 0; index < 1000; index++)
            {
                pool.GetStatistics();
            }

            Assert.Pass("统计快照为诊断用途，反复取用不应影响池的正确性。");
        }

        private static void WarmUp(ReferencePool<Node> pool)
        {
            for (int index = 0; index < WarmupIterations; index++)
            {
                ReferenceHandle<Node> handle = pool.Rent();
                pool.Return(handle);
            }
        }
    }
}
