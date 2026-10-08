using System;
using NUnit.Framework;
using UnityEngine;
using WindFrameWork.Core.ObjectPool;

namespace WindFrameWork.Tests.EditMode.ObjectPool
{
    [TestFixture]
    public sealed class ObjectPoolPrewarmTests
    {
        [Test]
        public void Prewarm_FillsIdleWithoutRenting()
        {
            var config = new ObjectPoolConfig("Prewarm", prewarmCount: 5);
            using var pool = new ObjectPool<GameObject>(() => new GameObject("Prewarmed"), config);

            ObjectPoolStatistics statistics = pool.GetStatistics();

            Assert.That(pool.IdleCount, Is.EqualTo(5));
            Assert.That(pool.LiveCount, Is.Zero);
            Assert.That(statistics.CreateCount, Is.EqualTo(5));
            Assert.That(statistics.RentCount, Is.Zero);
            Assert.That(statistics.PeakLiveCount, Is.Zero);
        }

        [Test]
        public void Prewarm_InvokesFactoryExactlyPrewarmCountTimes()
        {
            int created = 0;
            var config = new ObjectPoolConfig("PrewarmCount", prewarmCount: 4);

            using var pool = new ObjectPool<GameObject>(
                () =>
                {
                    created++;
                    return new GameObject("Item");
                },
                config);

            Assert.That(created, Is.EqualTo(4));
        }

        [Test]
        public void Prewarm_LeavesObjectsInactive()
        {
            var config = new ObjectPoolConfig("PrewarmInactive", prewarmCount: 3);
            using var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), config);

            int inactive = 0;
            for (int index = 0; index < 3; index++)
            {
                PooledHandle<GameObject> handle = pool.Rent();
                if (!handle.Object.activeSelf)
                {
                    inactive++;
                }

                pool.Return(handle);
            }

            Assert.That(inactive, Is.Zero, "预热对象在归还时应已停用，租出时才激活。");
        }

        [Test]
        public void Prewarm_LeavesFirstRentAsHit()
        {
            var config = new ObjectPoolConfig("PrewarmHit", prewarmCount: 2);
            using var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), config);

            pool.Rent();

            Assert.That(pool.GetStatistics().HitCount, Is.EqualTo(1));
            Assert.That(pool.GetStatistics().MissCount, Is.Zero);
        }

        [Test]
        public void ZeroPrewarm_CreatesNothing()
        {
            int created = 0;
            var config = new ObjectPoolConfig("NoPrewarm", prewarmCount: 0);

            using var pool = new ObjectPool<GameObject>(
                () =>
                {
                    created++;
                    return new GameObject("Item");
                },
                config);

            Assert.That(created, Is.Zero);
            Assert.That(pool.IdleCount, Is.Zero);
        }

        [Test]
        public void Prewarm_RespectsMaxIdleSize()
        {
            // 预热数量不得超过空闲上限，该约束在配置构造时即校验。
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new ObjectPoolConfig("TooWarm", prewarmCount: 10, maxIdleSize: 5));
        }
    }
}
