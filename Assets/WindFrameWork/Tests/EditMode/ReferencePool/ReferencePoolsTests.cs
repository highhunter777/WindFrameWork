using System.Collections.Generic;
using NUnit.Framework;
using WindFrameWork.Core.ReferencePool;

namespace WindFrameWork.Tests.EditMode.ReferencePool
{
    /// <summary>
    /// 共享池入口：按「类型 + 业务键」提供零配置的静态入口。
    /// </summary>
    [TestFixture]
    public sealed class ReferencePoolsTests
    {
        private sealed class Bundle
        {
            public int Value;
        }

        [TearDown]
        public void TearDown()
        {
            ReferencePools.Release<Bundle>("shared");
            ReferencePools.Release<Bundle>("other");
            ReferencePools.Release<Bundle>();
            ReferencePools.Release<List<int>>("list");
        }

        [Test]
        public void SameKey_ReturnsSamePoolInstance()
        {
            ReferencePool<Bundle> first = ReferencePools.GetOrCreate<Bundle>("shared");
            ReferencePool<Bundle> second = ReferencePools.GetOrCreate<Bundle>("shared");

            Assert.That(second, Is.SameAs(first));
        }

        [Test]
        public void DifferentKeys_ReturnDistinctPools()
        {
            ReferencePool<Bundle> first = ReferencePools.GetOrCreate<Bundle>("shared");
            ReferencePool<Bundle> second = ReferencePools.GetOrCreate<Bundle>("other");

            Assert.That(second, Is.Not.SameAs(first));
        }

        [Test]
        public void TryGet_ReturnsFalseWhenAbsent()
        {
            Assert.That(ReferencePools.TryGet<Bundle>("missing", out _), Is.False);
        }

        [Test]
        public void TryGet_ReturnsCreatedPool()
        {
            ReferencePool<Bundle> created = ReferencePools.GetOrCreate<Bundle>("shared");

            Assert.That(ReferencePools.TryGet<Bundle>("shared", out ReferencePool<Bundle> found), Is.True);
            Assert.That(found, Is.SameAs(created));
        }

        [Test]
        public void Release_RemovesAndDisposesPool()
        {
            ReferencePools.GetOrCreate<Bundle>("shared");

            Assert.That(ReferencePools.Release<Bundle>("shared"), Is.True);
            Assert.That(ReferencePools.TryGet<Bundle>("shared", out _), Is.False);
            Assert.That(ReferencePools.Release<Bundle>("shared"), Is.False);
        }

        [Test]
        public void GetOrCreate_AppliesResetCallback()
        {
            ReferencePool<List<int>> pool = ReferencePools.GetOrCreate(
                () => new List<int>(),
                "list",
                onReset: list => list.Clear());

            ReferenceHandle<List<int>> handle = pool.Rent();
            handle.Object.Add(7);
            pool.Return(handle);

            ReferenceHandle<List<int>> again = pool.Rent();
            Assert.That(again.Object.Count, Is.Zero);
            pool.Return(again);
        }

        [Test]
        public void ClearAll_EmptiesEverySharedPool()
        {
            ReferencePool<Bundle> pool = ReferencePools.GetOrCreate<Bundle>("shared");
            ReferenceHandle<Bundle> handle = pool.Rent();
            pool.Return(handle);
            Assert.That(pool.IdleCount, Is.EqualTo(1));

            ReferencePools.ClearAll();

            Assert.That(pool.IdleCount, Is.Zero);
            Assert.That(pool.LiveCount, Is.Zero);
        }
    }
}
