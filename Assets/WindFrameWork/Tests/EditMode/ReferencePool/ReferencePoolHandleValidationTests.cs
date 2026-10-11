using System;
using NUnit.Framework;
using WindFrameWork.Core.ReferencePool;

namespace WindFrameWork.Tests.EditMode.ReferencePool
{
    /// <summary>
    /// 租约凭证校验：重复归还、跨池归还与陈旧凭证必须无条件快速失败。
    /// </summary>
    [TestFixture]
    public sealed class ReferencePoolHandleValidationTests
    {
        private ReferencePool<Slot> _first;
        private ReferencePool<Slot> _second;

        private sealed class Slot
        {
        }

        [SetUp]
        public void SetUp()
        {
            _first = new ReferencePool<Slot>(() => new Slot(), ReferencePoolConfig.Default("First"));
            _second = new ReferencePool<Slot>(() => new Slot(), ReferencePoolConfig.Default("Second"));
        }

        [TearDown]
        public void TearDown()
        {
            _first.Dispose();
            _second.Dispose();
        }

        [Test]
        public void Handle_CarriesPoolIdentity()
        {
            ReferenceHandle<Slot> handle = _first.Rent();

            Assert.That(handle.PoolId, Is.EqualTo(_first.PoolId));
            Assert.That(handle.IsValid, Is.True);
        }

        [Test]
        public void ReturnTwice_Throws()
        {
            ReferenceHandle<Slot> handle = _first.Rent();
            _first.Return(handle);

            Assert.Throws<InvalidOperationException>(() => _first.Return(handle));
        }

        [Test]
        public void ReturnToForeignPool_Throws()
        {
            ReferenceHandle<Slot> handle = _first.Rent();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _second.Return(handle));
            Assert.That(exception.Message, Does.Contain("不符"));
        }

        [Test]
        public void StaleHandleAfterClear_Throws()
        {
            ReferenceHandle<Slot> handle = _first.Rent();
            _first.Clear();

            Assert.Throws<InvalidOperationException>(() => _first.Return(handle));
        }

        [Test]
        public void StaleSlotAfterReRent_Throws()
        {
            // 槽位被重新租出后，旧凭证即便代数相符也不得再次归还：
            // 归还路径依靠代数推进而非对象身份识别，这是 O(1) 且无需哈希表的判定依据。
            ReferenceHandle<Slot> first = _first.Rent();
            _first.Return(first);
            ReferenceHandle<Slot> second = _first.Rent();

            Assert.That(second.Slot, Is.EqualTo(first.Slot));
            Assert.Throws<InvalidOperationException>(() => _first.Return(first));
        }

        [Test]
        public void DefaultHandle_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _first.Return(ReferenceHandle<Slot>.Invalid));
        }

        [Test]
        public void ReturnToDisposedPool_Throws()
        {
            ReferenceHandle<Slot> handle = _first.Rent();
            _first.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _first.Return(handle));
        }
    }
}
