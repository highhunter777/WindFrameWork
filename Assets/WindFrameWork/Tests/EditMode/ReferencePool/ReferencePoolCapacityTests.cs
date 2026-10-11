using System;
using NUnit.Framework;
using WindFrameWork.Core.ReferencePool;

namespace WindFrameWork.Tests.EditMode.ReferencePool
{
    [TestFixture]
    public sealed class ReferencePoolCapacityTests
    {
        private sealed class Item
        {
        }

        [Test]
        public void RentBeyondMaxLiveSize_Throws()
        {
            var config = new ReferencePoolConfig("Capacity", maxLiveSize: 2);
            using var pool = new ReferencePool<Item>(() => new Item(), config);

            pool.Rent();
            pool.Rent();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => pool.Rent());
            Assert.That(exception.Message, Does.Contain("Capacity"));
        }

        [Test]
        public void PeakLiveCount_TracksConcurrencyPeak()
        {
            var config = new ReferencePoolConfig("Peak", maxLiveSize: 8);
            using var pool = new ReferencePool<Item>(() => new Item(), config);

            ReferenceHandle<Item> first = pool.Rent();
            ReferenceHandle<Item> second = pool.Rent();
            pool.Return(first);
            ReferenceHandle<Item> third = pool.Rent();

            Assert.That(pool.LiveCount, Is.EqualTo(2));
            Assert.That(pool.GetStatistics().PeakLiveCount, Is.EqualTo(2));

            pool.Return(second);
            pool.Return(third);
        }

        [Test]
        public void ReturnBeyondMaxIdleSize_ReleasesObject()
        {
            var config = new ReferencePoolConfig("IdleOverflow", maxIdleSize: 1);
            using var pool = new ReferencePool<Item>(() => new Item(), config);

            ReferenceHandle<Item> first = pool.Rent();
            ReferenceHandle<Item> second = pool.Rent();
            pool.Return(first);
            pool.Return(second);

            // 空闲上限是内存占用的硬边界：溢出对象被解除引用而非保留。
            Assert.That(pool.IdleCount, Is.EqualTo(1));
            Assert.That(pool.GetStatistics().ReleasedCount, Is.EqualTo(1));
        }

        [Test]
        public void ZeroMaxIdleSize_ReleasesEveryReturn()
        {
            var config = new ReferencePoolConfig("NoIdle", maxIdleSize: 0);
            using var pool = new ReferencePool<Item>(() => new Item(), config);

            for (int index = 0; index < 4; index++)
            {
                pool.Return(pool.Rent());
            }

            Assert.That(pool.IdleCount, Is.Zero);
            Assert.That(pool.GetStatistics().ReleasedCount, Is.EqualTo(4));
            Assert.That(pool.GetStatistics().CreateCount, Is.EqualTo(4));
        }

        [Test]
        public void Overflow_InvokesReleaseCallback()
        {
            var config = new ReferencePoolConfig("ReleaseCallback", maxIdleSize: 1);
            int releaseCount = 0;
            var pool = new ReferencePool<Item>(() => new Item(), config, onRelease: _ => releaseCount++);

            ReferenceHandle<Item> first = pool.Rent();
            ReferenceHandle<Item> second = pool.Rent();
            pool.Return(first);
            pool.Return(second);

            Assert.That(releaseCount, Is.EqualTo(1));

            pool.Dispose();
        }
    }
}
