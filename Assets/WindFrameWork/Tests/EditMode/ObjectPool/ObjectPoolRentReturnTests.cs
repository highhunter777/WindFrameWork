using System;
using NUnit.Framework;
using UnityEngine;
using WindFrameWork.Core.ObjectPool;

namespace WindFrameWork.Tests.EditMode.ObjectPool
{
    [TestFixture]
    public sealed class ObjectPoolRentReturnTests
    {
        private ObjectPool<GameObject> _pool;

        [SetUp]
        public void SetUp()
        {
            _pool = new ObjectPool<GameObject>(() => new GameObject("Pooled"), ObjectPoolConfig.Default("RentReturn"));
        }

        [TearDown]
        public void TearDown()
        {
            _pool.Dispose();
        }

        [Test]
        public void RentThenReturn_RentsSameInstance()
        {
            PooledHandle<GameObject> first = _pool.Rent();
            GameObject instance = first.Object;
            _pool.Return(first);

            PooledHandle<GameObject> second = _pool.Rent();

            Assert.That(second.Object, Is.SameAs(instance));
        }

        [Test]
        public void RentWithoutIdle_CreatesDistinctInstances()
        {
            PooledHandle<GameObject> first = _pool.Rent();
            PooledHandle<GameObject> second = _pool.Rent();

            Assert.That(second.Object, Is.Not.SameAs(first.Object));
            Assert.That(_pool.LiveCount, Is.EqualTo(2));
            Assert.That(_pool.IdleCount, Is.Zero);
        }

        [Test]
        public void Return_MovesObjectToIdle()
        {
            PooledHandle<GameObject> handle = _pool.Rent();

            _pool.Return(handle);

            Assert.That(_pool.IdleCount, Is.EqualTo(1));
            Assert.That(_pool.LiveCount, Is.Zero);
        }

        [Test]
        public void ReturnedObject_IsDeactivated()
        {
            PooledHandle<GameObject> handle = _pool.Rent();
            Assert.That(handle.Object.activeSelf, Is.True);

            _pool.Return(handle);

            Assert.That(handle.Object.activeSelf, Is.False);
        }

        [Test]
        public void MixedRentReturn_KeepsCountersConsistent()
        {
            PooledHandle<GameObject> first = _pool.Rent();
            PooledHandle<GameObject> second = _pool.Rent();
            _pool.Return(first);

            Assert.That(_pool.LiveCount, Is.EqualTo(1));
            Assert.That(_pool.IdleCount, Is.EqualTo(1));

            _pool.Return(second);

            Assert.That(_pool.LiveCount, Is.Zero);
            Assert.That(_pool.IdleCount, Is.EqualTo(2));
        }

        [Test]
        public void Pool_DoesNotResetTransform()
        {
            // 重置变换属于业务策略，由IPoolable 负责；池不得擅自修改。
            PooledHandle<GameObject> handle = _pool.Rent();
            handle.Object.transform.localPosition = new Vector3(1f, 2f, 3f);
            handle.Object.transform.localRotation = Quaternion.Euler(10f, 20f, 30f);
            Vector3 position = handle.Object.transform.localPosition;
            Quaternion rotation = handle.Object.transform.localRotation;

            _pool.Return(handle);
            PooledHandle<GameObject> again = _pool.Rent();

            Assert.That(again.Object.transform.localPosition, Is.EqualTo(position));
            Assert.That(again.Object.transform.localRotation, Is.EqualTo(rotation));
        }

        [Test]
        public void Clear_DestroysIdleObjectsAndResetsGauges()
        {
            _pool.Rent();
            _pool.Rent();

            _pool.Clear();

            ObjectPoolStatistics statistics = _pool.GetStatistics();
            Assert.That(_pool.IdleCount, Is.Zero);
            Assert.That(_pool.LiveCount, Is.Zero);
            Assert.That(statistics.RentCount, Is.Zero);
            Assert.That(statistics.CreateCount, Is.Zero);
        }

        [Test]
        public void FactoryReturningNull_Throws()
        {
            using var pool = new ObjectPool<GameObject>(() => null, ObjectPoolConfig.Default("NullFactory"));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => pool.Rent());
            Assert.That(exception.Message, Does.Contain("NullFactory"));
        }

        [Test]
        public void DisposedPool_RejectsFurtherUse()
        {
            var pool = new ObjectPool<GameObject>(() => new GameObject("X"), ObjectPoolConfig.Default("Disposed"));
            pool.Dispose();

            Assert.Throws<ObjectDisposedException>(() => pool.Rent());
        }
    }
}
