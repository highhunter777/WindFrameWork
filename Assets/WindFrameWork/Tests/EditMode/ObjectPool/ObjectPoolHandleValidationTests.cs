using System;
using NUnit.Framework;
using UnityEngine;
using WindFrameWork.Core.ObjectPool;

namespace WindFrameWork.Tests.EditMode.ObjectPool
{
    /// <summary>
    /// 租约凭证校验：重复归还、陈旧凭证与跨池凭证必须被无条件拒绝。
    /// </summary>
    /// <remarks>
    /// 这些校验无需任何配置开关即可生效：错误的归还会静默破坏空闲链表，引发远更难排查的故障。
    /// </remarks>
    [TestFixture]
    public sealed class ObjectPoolHandleValidationTests
    {
        private ObjectPool<GameObject> _pool;

        [SetUp]
        public void SetUp()
        {
            _pool = new ObjectPool<GameObject>(() => new GameObject("Item"), ObjectPoolConfig.Default("Handles"));
        }

        [TearDown]
        public void TearDown()
        {
            _pool.Dispose();
        }

        [Test]
        public void DoubleReturn_Throws()
        {
            PooledHandle<GameObject> handle = _pool.Rent();
            _pool.Return(handle);

            Assert.Throws<InvalidOperationException>(() => _pool.Return(handle));
        }

        [Test]
        public void StaleHandle_ThrowsAfterSlotIsRentedAgain()
        {
            PooledHandle<GameObject> first = _pool.Rent();
            _pool.Return(first);

            PooledHandle<GameObject> second = _pool.Rent();

            Assert.Throws<InvalidOperationException>(() => _pool.Return(first));
            _pool.Return(second);
        }

        [Test]
        public void HandleFromAnotherPool_Throws()
        {
            using var other = new ObjectPool<GameObject>(() => new GameObject("Other"), ObjectPoolConfig.Default("OtherPool"));
            PooledHandle<GameObject> handle = _pool.Rent();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => other.Return(handle));

            Assert.That(exception.Message, Does.Contain("OtherPool"), "诊断须指明拒绝归还的池。");
        }

        [Test]
        public void DefaultHandle_IsRejected()
        {
            Assert.Throws<InvalidOperationException>(() => _pool.Return(default));
        }

        [Test]
        public void NormalRentReturn_IsNotAffected()
        {
            for (int index = 0; index < 5; index++)
            {
                PooledHandle<GameObject> handle = _pool.Rent();
                Assert.DoesNotThrow(() => _pool.Return(handle));
            }
        }

        [Test]
        public void Handle_ExposesSlotAndPoolIdentity()
        {
            PooledHandle<GameObject> handle = _pool.Rent();

            Assert.That(handle.IsValid, Is.True);
            Assert.That(handle.Slot, Is.GreaterThanOrEqualTo(0));
            Assert.That(handle.PoolId, Is.EqualTo(_pool.PoolId));
            Assert.That(PooledHandle<GameObject>.Invalid.IsValid, Is.False);
        }

        [Test]
        public void HandleGeneration_DistinguishesSuccessiveRentsOfSameSlot()
        {
            PooledHandle<GameObject> first = _pool.Rent();
            _pool.Return(first);
            PooledHandle<GameObject> second = _pool.Rent();

            // 复用同一槽位，且旧凭证已失效——由上面的陈旧凭证用例覆盖，此处确认归还合法。
            Assert.That(second.Slot, Is.EqualTo(first.Slot), "复用同一槽位才能验证代数校验。");
            Assert.DoesNotThrow(() => _pool.Return(second));

            // 旧凭证必须仍然无效：代数已推进。
            Assert.Throws<InvalidOperationException>(() => _pool.Return(first));
        }
    }
}
