using NUnit.Framework;
using WindFrameWork.Core.ReferencePool;

namespace WindFrameWork.Tests.EditMode.ReferencePool
{
    [TestFixture]
    public sealed class ReferencePoolRentReturnTests
    {
        private ReferencePool<Bag> _pool;

        private sealed class Bag
        {
            public int Value;
        }

        [SetUp]
        public void SetUp()
        {
            _pool = new ReferencePool<Bag>(() => new Bag(), ReferencePoolConfig.Default("RentReturn"));
        }

        [TearDown]
        public void TearDown()
        {
            _pool.Dispose();
        }

        [Test]
        public void RentThenReturn_RentsSameInstance()
        {
            ReferenceHandle<Bag> first = _pool.Rent();
            Bag instance = first.Object;
            _pool.Return(first);

            ReferenceHandle<Bag> second = _pool.Rent();

            Assert.That(second.Object, Is.SameAs(instance));
        }

        [Test]
        public void RentWithoutIdle_CreatesDistinctInstances()
        {
            ReferenceHandle<Bag> first = _pool.Rent();
            ReferenceHandle<Bag> second = _pool.Rent();

            Assert.That(second.Object, Is.Not.SameAs(first.Object));
            Assert.That(_pool.LiveCount, Is.EqualTo(2));
            Assert.That(_pool.IdleCount, Is.Zero);
        }

        [Test]
        public void Return_MovesObjectToIdle()
        {
            ReferenceHandle<Bag> handle = _pool.Rent();

            _pool.Return(handle);

            Assert.That(_pool.IdleCount, Is.EqualTo(1));
            Assert.That(_pool.LiveCount, Is.Zero);
        }

        [Test]
        public void MixedRentReturn_KeepsCountersConsistent()
        {
            ReferenceHandle<Bag> first = _pool.Rent();
            ReferenceHandle<Bag> second = _pool.Rent();
            _pool.Return(first);

            Assert.That(_pool.LiveCount, Is.EqualTo(1));
            Assert.That(_pool.IdleCount, Is.EqualTo(1));

            _pool.Return(second);

            Assert.That(_pool.LiveCount, Is.Zero);
            Assert.That(_pool.IdleCount, Is.EqualTo(2));
        }

        [Test]
        public void Pool_DoesNotResetBusinessFields()
        {
            // 重置业务状态属于调用方策略（IPoolable 或重置回调）；池自身不做任何猜度。
            ReferenceHandle<Bag> handle = _pool.Rent();
            handle.Object.Value = 42;
            _pool.Return(handle);

            ReferenceHandle<Bag> again = _pool.Rent();

            Assert.That(again.Object.Value, Is.EqualTo(42));
        }

        [Test]
        public void Clear_ReleasesObjectsAndResetsGauges()
        {
            _pool.Rent();
            _pool.Rent();

            _pool.Clear();

            ReferencePoolStatistics statistics = _pool.GetStatistics();
            Assert.That(_pool.IdleCount, Is.Zero);
            Assert.That(_pool.LiveCount, Is.Zero);
            Assert.That(statistics.RentCount, Is.Zero);
            Assert.That(statistics.CreateCount, Is.Zero);
        }

        [Test]
        public void Clear_InvalidatesOutstandingHandles()
        {
            ReferenceHandle<Bag> handle = _pool.Rent();

            _pool.Clear();

            Assert.Throws<System.InvalidOperationException>(() => _pool.Return(handle));
        }

        [Test]
        public void FactoryReturningNull_Throws()
        {
            using var pool = new ReferencePool<Bag>(() => null, ReferencePoolConfig.Default("NullFactory"));

            System.InvalidOperationException exception =
                Assert.Throws<System.InvalidOperationException>(() => pool.Rent());
            Assert.That(exception.Message, Does.Contain("NullFactory"));
        }

        [Test]
        public void DisposedPool_RejectsFurtherUse()
        {
            var pool = new ReferencePool<Bag>(() => new Bag(), ReferencePoolConfig.Default("Disposed"));
            pool.Dispose();

            Assert.Throws<System.ObjectDisposedException>(() => pool.Rent());
        }
    }
}
