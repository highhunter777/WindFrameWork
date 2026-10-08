using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WindFrameWork.Core.ObjectPool;

namespace WindFrameWork.Tests.PlayMode.ObjectPool
{
    [TestFixture]
    public sealed class ObjectPoolPlayModeTests
    {
        [Test]
        public IEnumerator RepeatedRentReturn_DoesNotGrowPoolSize()
        {
            var config = new ObjectPoolConfig("PlayModeChurn", prewarmCount: 4, maxIdleSize: 8);
            using var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), config);

            int totalAfterPrewarm = pool.GetStatistics().TotalCount;

            for (int index = 0; index < 200; index++)
            {
                PooledHandle<GameObject> handle = pool.Rent();
                pool.Return(handle);
            }

            yield return null;

            Assert.That(pool.GetStatistics().TotalCount, Is.EqualTo(totalAfterPrewarm));
            Assert.That(pool.GetStatistics().DestroyCount, Is.Zero, "容量充裕时不应发生销毁。");
        }

        [Test]
        public IEnumerator ReturnedObject_IsInactiveInHierarchy()
        {
            using var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), ObjectPoolConfig.Default("PlayModeActive"));

            PooledHandle<GameObject> handle = pool.Rent();
            Assert.That(handle.Object.activeInHierarchy, Is.True);

            pool.Return(handle);
            yield return null;

            Assert.That(handle.Object.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator DestroyedRentedObject_ReturnDoesNotThrow()
        {
            using var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), ObjectPoolConfig.Default("PlayModeDestroyed"));

            PooledHandle<GameObject> handle = pool.Rent();
            Object.Destroy(handle.Object);
            yield return null;

            Assert.DoesNotThrow(() => pool.Return(handle));
            Assert.That(pool.GetStatistics().DiscardedDestroyedCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DestroyedIdleObject_IsSkippedOnNextRent()
        {
            var created = new System.Collections.Generic.List<GameObject>();
            using var pool = new ObjectPool<GameObject>(
                () =>
                {
                    var item = new GameObject("Item");
                    created.Add(item);
                    return item;
                },
                new ObjectPoolConfig("PlayModeIdleDestroyed", prewarmCount: 3));

            foreach (GameObject item in created)
            {
                Object.Destroy(item);
            }

            yield return null;

            PooledHandle<GameObject> handle = pool.Rent();

            Assert.That(handle.Object, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator ClearDuringPlay_DoesNotThrowOnAlreadyDestroyedObjects()
        {
            var created = new System.Collections.Generic.List<GameObject>();
            using var pool = new ObjectPool<GameObject>(
                () =>
                {
                    var item = new GameObject("Item");
                    created.Add(item);
                    return item;
                },
                new ObjectPoolConfig("PlayModeClear", prewarmCount: 2));

            Object.Destroy(created[0]);
            yield return null;

            Assert.DoesNotThrow(() => pool.Clear());
        }
    }
}
