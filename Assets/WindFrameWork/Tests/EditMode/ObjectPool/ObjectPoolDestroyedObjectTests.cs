using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WindFrameWork.Core.ObjectPool;

namespace WindFrameWork.Tests.EditMode.ObjectPool
{
    /// <summary>
    /// 已销毁对象（Unity 的 fake null）处理：池不是对象生命周期的所有者，被销毁是预期事件。
    /// </summary>
    [TestFixture]
    public sealed class ObjectPoolDestroyedObjectTests
    {
        [Test]
        public void ReturnDestroyedObject_DoesNotThrowAndDiscardsIt()
        {
            using var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), ObjectPoolConfig.Default("DiscardOnReturn"));
            PooledHandle<GameObject> handle = pool.Rent();
            Object.DestroyImmediate(handle.Object);

            Assert.DoesNotThrow(() => pool.Return(handle));

            ObjectPoolStatistics statistics = pool.GetStatistics();
            Assert.That(statistics.DiscardedDestroyedCount, Is.EqualTo(1));
            Assert.That(pool.IdleCount, Is.Zero, "已销毁对象不得留在空闲池中。");
            Assert.That(pool.LiveCount, Is.Zero);
        }

        [Test]
        public void ReturnDestroyedObject_NextRentYieldsLiveObject()
        {
            using var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), ObjectPoolConfig.Default("DiscardThenRent"));
            PooledHandle<GameObject> handle = pool.Rent();
            Object.DestroyImmediate(handle.Object);
            pool.Return(handle);

            PooledHandle<GameObject> next = pool.Rent();

            Assert.That(next.Object, Is.Not.Null);
        }

        [Test]
        public void DestroyedIdleObject_IsSkippedOnRent()
        {
            var created = new List<GameObject>();
            using var pool = new ObjectPool<GameObject>(
                () =>
                {
                    var item = new GameObject("Item");
                    created.Add(item);
                    return item;
                },
                new ObjectPoolConfig("SkipIdle", prewarmCount: 3));

            Assert.That(pool.IdleCount, Is.EqualTo(3));

            // 销毁全部空闲对象：池无从得知，只能在租出时逐个发现并跳过。
            foreach (GameObject item in created)
            {
                Object.DestroyImmediate(item);
            }

            PooledHandle<GameObject> handle = pool.Rent();

            Assert.That(handle.Object, Is.Not.Null, "失效的空闲对象不得交付给业务代码。");
            Assert.That(pool.GetStatistics().DiscardedDestroyedCount, Is.EqualTo(3));
        }

        [Test]
        public void AllIdleObjectsDestroyed_RentStillSucceeds()
        {
            using var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), new ObjectPoolConfig("AllDead", prewarmCount: 2));

            // 借出全部空闲对象并在其租出期间销毁，再归还，使空闲池不再有可用对象。
            PooledHandle<GameObject> first = pool.Rent();
            PooledHandle<GameObject> second = pool.Rent();
            Object.DestroyImmediate(first.Object);
            Object.DestroyImmediate(second.Object);
            pool.Return(first);
            pool.Return(second);

            PooledHandle<GameObject> handle = pool.Rent();

            Assert.That(handle.Object, Is.Not.Null);
            Assert.That(pool.GetStatistics().DiscardedDestroyedCount, Is.EqualTo(2));
        }

        [Test]
        public void DestroyedIdleObjects_AreCountedAsDiscarded()
        {
            int created = 0;
            using var pool = new ObjectPool<GameObject>(
                () =>
                {
                    created++;
                    return new GameObject("Item");
                },
                new ObjectPoolConfig("CountDead", prewarmCount: 1));

            Assert.That(created, Is.EqualTo(1));
            Assert.That(pool.IdleCount, Is.EqualTo(1));
        }

        [Test]
        public void Statistics_ExposeDiscardedCountForDiagnostics()
        {
            using var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), ObjectPoolConfig.Default("Diag"));
            PooledHandle<GameObject> handle = pool.Rent();
            Object.DestroyImmediate(handle.Object);
            pool.Return(handle);

            Assert.That(pool.GetStatistics().DiscardedDestroyedCount, Is.EqualTo(1));
        }
    }
}
