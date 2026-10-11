using NUnit.Framework;
using WindFrameWork.Core.ReferencePool;

namespace WindFrameWork.Tests.EditMode.ReferencePool
{
    [TestFixture]
    public sealed class ReferencePoolPrewarmTests
    {
        private sealed class Node
        {
        }

        [Test]
        public void Prewarm_CreatesIdleObjectsUpfront()
        {
            var config = new ReferencePoolConfig("Prewarm", prewarmCount: 4, maxIdleSize: 16);
            using var pool = new ReferencePool<Node>(() => new Node(), config);

            Assert.That(pool.IdleCount, Is.EqualTo(4));
            Assert.That(pool.LiveCount, Is.Zero);
        }

        [Test]
        public void PrewarmThenRent_HitsWithoutCreatingMore()
        {
            var config = new ReferencePoolConfig("Hit", prewarmCount: 4, maxIdleSize: 16);
            using var pool = new ReferencePool<Node>(() => new Node(), config);

            ReferenceHandle<Node> handle = pool.Rent();
            ReferencePoolStatistics statistics = pool.GetStatistics();

            Assert.That(statistics.HitCount, Is.EqualTo(1));
            Assert.That(statistics.MissCount, Is.Zero);
            Assert.That(statistics.CreateCount, Is.EqualTo(4));

            pool.Return(handle);
        }

        [Test]
        public void Prewarm_DoesNotInflateConcurrencyPeak()
        {
            // 预热对象自始即空闲，不经过租出路径，因此不得污染并发峰值。
            var config = new ReferencePoolConfig("Peak", prewarmCount: 8, maxIdleSize: 16);
            using var pool = new ReferencePool<Node>(() => new Node(), config);

            Assert.That(pool.GetStatistics().PeakLiveCount, Is.Zero);
        }

        [Test]
        public void Prewarm_InvokesResetCallbackPerItem()
        {
            var config = new ReferencePoolConfig("ResetOnPrewarm", prewarmCount: 3, maxIdleSize: 8);
            int resetCount = 0;
            using var pool = new ReferencePool<Node>(() => new Node(), config, onReset: _ => resetCount++);

            Assert.That(resetCount, Is.EqualTo(3));
        }
    }
}
