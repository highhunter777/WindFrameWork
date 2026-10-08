using NUnit.Framework;
using UnityEngine;
using WindFrameWork.Core.ObjectPool;

namespace WindFrameWork.Tests.EditMode.ObjectPool
{
    [TestFixture]
    public sealed class ObjectPoolStatisticsTests
    {
        [Test]
        public void ScriptedSequence_ProducesExactCounters()
        {
            var config = new ObjectPoolConfig("Scripted", prewarmCount: 2, maxIdleSize: 2);
            using var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), config);

            PooledHandle<GameObject> first = pool.Rent();
            PooledHandle<GameObject> second = pool.Rent();
            PooledHandle<GameObject> third = pool.Rent();
            pool.Return(first);
            pool.Return(second);
            pool.Return(third);

            ObjectPoolStatistics statistics = pool.GetStatistics();

            Assert.That(statistics.RentCount, Is.EqualTo(3));
            Assert.That(statistics.HitCount, Is.EqualTo(2), "两个预热对象应被命中。");
            Assert.That(statistics.MissCount, Is.EqualTo(1));
            Assert.That(statistics.ReturnCount, Is.EqualTo(3));
            Assert.That(statistics.CreateCount, Is.EqualTo(3), "预热 2 个加新建 1 个。");
            Assert.That(statistics.DestroyCount, Is.EqualTo(1), "超出空闲上限的一个应被销毁。");
            Assert.That(statistics.PeakLiveCount, Is.EqualTo(3));
            Assert.That(statistics.IdleCount, Is.EqualTo(2));
            Assert.That(statistics.LiveCount, Is.Zero);
            Assert.That(statistics.TotalCount, Is.EqualTo(2));
            Assert.That(statistics.HitRate, Is.EqualTo(2f / 3f).Within(0.0001f));
        }

        [Test]
        public void HitRate_IsZeroWithoutRent()
        {
            using var pool = new ObjectPool<GameObject>(
                () => new GameObject("Item"),
                new ObjectPoolConfig("NoRent", prewarmCount: 4));

            Assert.That(pool.GetStatistics().HitRate, Is.Zero);
        }

        [Test]
        public void HitRate_IsOneWhenEveryRentHits()
        {
            using var pool = new ObjectPool<GameObject>(
                () => new GameObject("Item"),
                new ObjectPoolConfig("AllHit", prewarmCount: 2));

            pool.Rent();
            pool.Rent();

            Assert.That(pool.GetStatistics().HitRate, Is.EqualTo(1f));
        }

        [Test]
        public void PeakLiveCount_IsZeroAfterPrewarmOnly()
        {
            using var pool = new ObjectPool<GameObject>(
                () => new GameObject("Item"),
                new ObjectPoolConfig("PrewarmPeak", prewarmCount: 5));

            Assert.That(pool.GetStatistics().PeakLiveCount, Is.Zero, "预热不应被记为租出。");
        }

        [Test]
        public void CumulativeCounters_SurviveGauges()
        {
            using var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), ObjectPoolConfig.Default("Cumulative"));
            PooledHandle<GameObject> handle = pool.Rent();
            pool.Return(handle);

            Assert.That(pool.GetStatistics().RentCount, Is.EqualTo(1));
            Assert.That(pool.GetStatistics().ReturnCount, Is.EqualTo(1));
            Assert.That(pool.IdleCount, Is.EqualTo(1));
            Assert.That(pool.LiveCount, Is.Zero);
        }

        [Test]
        public void StatisticsSnapshot_IsIndependentOfPoolState()
        {
            using var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), ObjectPoolConfig.Default("Snapshot"));
            pool.Rent();

            ObjectPoolStatistics snapshot = pool.GetStatistics();

            pool.Rent();
            Assert.That(snapshot.RentCount, Is.EqualTo(1), "统计快照为值类型，不随池状态变化。");
        }
    }
}
