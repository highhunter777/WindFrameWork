using System;
using System.Threading;
using NUnit.Framework;
using WindFrameWork.Core.ReferencePool;

namespace WindFrameWork.Tests.EditMode.ReferencePool
{
    /// <summary>
    /// 线程模型验证：同步模式在并发下保持计数不变量，单线程模式对越界访问快速失败。
    /// </summary>
    [TestFixture]
    public sealed class ReferencePoolThreadSafetyTests
    {
        private const int ThreadCount = 4;
        private const int IterationsPerThread = 2000;

        private sealed class Node
        {
        }

        [Test]
        public void ConcurrentRentReturn_KeepsCountersConsistent()
        {
            var config = new ReferencePoolConfig("Concurrent", maxLiveSize: 4096, maxIdleSize: 128);
            using var pool = new ReferencePool<Node>(() => new Node(), config);

            Exception failure = null;
            var threads = new Thread[ThreadCount];

            for (int index = 0; index < ThreadCount; index++)
            {
                threads[index] = new Thread(() =>
                {
                    try
                    {
                        for (int iteration = 0; iteration < IterationsPerThread; iteration++)
                        {
                            ReferenceHandle<Node> handle = pool.Rent();
                            pool.Return(handle);
                        }
                    }
                    catch (Exception exception)
                    {
                        Volatile.Write(ref failure, exception);
                    }
                });

                threads[index].Start();
            }

            for (int index = 0; index < ThreadCount; index++)
            {
                Assert.That(threads[index].Join(TimeSpan.FromSeconds(15)), Is.True, "线程未在限时内结束。");
            }

            Assert.That(failure, Is.Null, "并发租还过程中抛出异常：" + failure);

            ReferencePoolStatistics statistics = pool.GetStatistics();
            Assert.That(statistics.RentCount, Is.EqualTo(ThreadCount * (long)IterationsPerThread));
            Assert.That(statistics.ReturnCount, Is.EqualTo(statistics.RentCount));
            Assert.That(statistics.LiveCount, Is.Zero);
            Assert.That(statistics.IdleCount, Is.LessThanOrEqualTo(config.MaxIdleSize));
        }

        [Test]
        public void SingleThreadMode_RejectsForeignThread()
        {
            var config = new ReferencePoolConfig(
                "SingleThread",
                threadMode: ReferencePoolThreadMode.SingleThread);
            using var pool = new ReferencePool<Node>(() => new Node(), config);

            Exception captured = null;
            var worker = new Thread(() =>
            {
                try
                {
                    pool.Rent();
                }
                catch (Exception exception)
                {
                    captured = exception;
                }
            });

            worker.Start();
            Assert.That(worker.Join(TimeSpan.FromSeconds(15)), Is.True, "线程未在限时内结束。");

            Assert.That(captured, Is.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public void SynchronizedMode_AcceptsForeignThread()
        {
            var config = new ReferencePoolConfig(
                "Synchronized",
                threadMode: ReferencePoolThreadMode.Synchronized);
            using var pool = new ReferencePool<Node>(() => new Node(), config);

            Exception captured = null;
            var worker = new Thread(() =>
            {
                try
                {
                    ReferenceHandle<Node> handle = pool.Rent();
                    pool.Return(handle);
                }
                catch (Exception exception)
                {
                    captured = exception;
                }
            });

            worker.Start();
            Assert.That(worker.Join(TimeSpan.FromSeconds(15)), Is.True, "线程未在限时内结束。");

            Assert.That(captured, Is.Null);
            Assert.That(pool.IdleCount, Is.EqualTo(1));
        }
    }
}
