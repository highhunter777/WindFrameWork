using System;
using NUnit.Framework;
using UnityEngine;
using WindFrameWork.Core.ObjectPool;

namespace WindFrameWork.Tests.EditMode.ObjectPool
{
    [TestFixture]
    public sealed class ObjectPoolCapacityTests
    {
        [Test]
        public void IdleOverflow_DestroysExcessObject()
        {
            var config = new ObjectPoolConfig("Overflow", maxIdleSize: 2);
            using var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), config);

            PooledHandle<GameObject> first = pool.Rent();
            PooledHandle<GameObject> second = pool.Rent();
            PooledHandle<GameObject> third = pool.Rent();

            GameObject[] rented = { first.Object, second.Object, third.Object };

            pool.Return(first);
            pool.Return(second);
            pool.Return(third);

            Assert.That(pool.IdleCount, Is.EqualTo(2));
            Assert.That(pool.GetStatistics().DestroyCount, Is.EqualTo(1));
            Assert.That(CountDestroyed(rented), Is.EqualTo(1), "超出空闲上限的对象应被销毁。");
        }

        [Test]
        public void LiveLimitExceeded_Throws()
        {
            var config = new ObjectPoolConfig("LiveCap", maxLiveSize: 2);
            using var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), config);

            pool.Rent();
            pool.Rent();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => pool.Rent());

            Assert.That(exception.Message, Does.Contain("LiveCap"));
            Assert.That(exception.Message, Does.Contain("2"));
        }

        [Test]
        public void LiveLimit_AllowsReuseAfterReturn()
        {
            var config = new ObjectPoolConfig("LiveCapReuse", maxLiveSize: 1);
            using var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), config);

            PooledHandle<GameObject> handle = pool.Rent();
            pool.Return(handle);

            Assert.DoesNotThrow(() => pool.Rent());
        }

        [Test]
        public void MaxIdleSize_IsAMemoryCeiling()
        {
            var config = new ObjectPoolConfig("Ceiling", maxIdleSize: 3);
            using var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), config);

            for (int index = 0; index < 10; index++)
            {
                PooledHandle<GameObject> handle = pool.Rent();
                pool.Return(handle);
            }

            Assert.That(pool.IdleCount, Is.LessThanOrEqualTo(3));
        }

        private static int CountDestroyed(GameObject[] items)
        {
            int destroyed = 0;
            foreach (GameObject item in items)
            {
                // 必须使用 Unity 重载的 ==：ReferenceEquals 对已销毁对象仍返回 true。
                if (item == null)
                {
                    destroyed++;
                }
            }

            return destroyed;
        }
    }
}
