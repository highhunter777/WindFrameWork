using System;
using NUnit.Framework;
using UnityEngine;
using WindFrameWork.Core.ObjectPool;

namespace WindFrameWork.Tests.EditMode.ObjectPool
{
    [TestFixture]
    public sealed class ObjectPoolProviderTests
    {
        private ObjectPoolProvider _provider;

        [SetUp]
        public void SetUp()
        {
            _provider = new ObjectPoolProvider();
        }

        [TearDown]
        public void TearDown()
        {
            _provider.Dispose();
        }

        [Test]
        public void Get_ReturnsSamePoolForSameKeyAndType()
        {
            IObjectPool<GameObject> pool = CreatePool("Bullet");
            _provider.Register("Bullet", pool);

            Assert.That(_provider.Get<GameObject>("Bullet"), Is.SameAs(pool));
        }

        [Test]
        public void Get_WithMismatchedType_ThrowsDiagnostic()
        {
            _provider.Register("Fx", CreatePool("Fx"));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => _provider.Get<Transform>("Fx"));

            Assert.That(exception.Message, Does.Contain("Fx"));
            Assert.That(exception.Message, Does.Contain("GameObject"));
            Assert.That(exception.Message, Does.Contain("Transform"));
        }

        [Test]
        public void Get_WithUnknownKey_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _provider.Get<GameObject>("Missing"));
        }

        [Test]
        public void TryGet_ReturnsFalseForUnknownKey()
        {
            Assert.That(_provider.TryGet("Missing", out IObjectPool<GameObject> pool), Is.False);
            Assert.That(pool, Is.Null);
        }

        [Test]
        public void TryGet_ReturnsFalseForMismatchedType()
        {
            _provider.Register("Bullet", CreatePool("Bullet"));

            Assert.That(_provider.TryGet("Bullet", out IObjectPool<Transform> typed), Is.False);
            Assert.That(typed, Is.Null);
        }

        [Test]
        public void DuplicateRegistration_Throws()
        {
            _provider.Register("Bullet", CreatePool("Bullet"));

            Assert.Throws<InvalidOperationException>(() => _provider.Register("Bullet", CreatePool("Bullet")));
        }

        [Test]
        public void DifferentTypesUnderSameKey_Throws()
        {
            _provider.Register("Mixed", CreatePool("Mixed"));

            Assert.Throws<InvalidOperationException>(
                () => _provider.Register("Mixed", new ObjectPool<Transform>(CreatePrimitive, ObjectPoolConfig.Default("MixedOther"))));
        }

        [Test]
        public void Release_RemovesPool()
        {
            _provider.Register("Bullet", CreatePool("Bullet"));

            Assert.That(_provider.Release("Bullet"), Is.True);
            Assert.That(_provider.TryGet<GameObject>("Bullet", out _), Is.False);
            Assert.That(_provider.Release("Bullet"), Is.False);
        }

        [Test]
        public void ReleaseAll_ClearsEverything()
        {
            _provider.Register("A", CreatePool("A"));
            _provider.Register("B", CreatePool("B"));

            _provider.ReleaseAll();

            Assert.That(_provider.Count, Is.Zero);
        }

        [Test]
        public void GetPools_IsSortedByName()
        {
            _provider.Register("Zeta", CreatePool("Zeta"));
            _provider.Register("Alpha", CreatePool("Alpha"));
            _provider.Register("Mid", CreatePool("Mid"));

            System.Collections.Generic.IReadOnlyList<ObjectPoolInfo> pools = _provider.GetPools();

            Assert.That(pools.Count, Is.EqualTo(3));
            Assert.That(pools[0].Name, Is.EqualTo("Alpha"));
            Assert.That(pools[1].Name, Is.EqualTo("Mid"));
            Assert.That(pools[2].Name, Is.EqualTo("Zeta"));
        }

        [Test]
        public void GetStatistics_ReturnsPoolStatistics()
        {
            IObjectPool<GameObject> pool = CreatePool("Counted");
            _provider.Register("Counted", pool);
            pool.Rent();

            Assert.That(_provider.GetStatistics("Counted").RentCount, Is.EqualTo(1));
        }

        [Test]
        public void GetStatistics_ForUnknownKey_ReturnsDefault()
        {
            Assert.That(_provider.GetStatistics("Missing").RentCount, Is.Zero);
        }

        [Test]
        public void GetPools_ExposesElementType()
        {
            _provider.Register("Bullet", CreatePool("Bullet"));

            Assert.That(_provider.GetPools()[0].ElementType, Is.EqualTo(typeof(GameObject)));
        }

        private static IObjectPool<GameObject> CreatePool(string name)
        {
            return new ObjectPool<GameObject>(() => new GameObject(name), ObjectPoolConfig.Default(name));
        }

        private static Transform CreatePrimitive()
        {
            var host = new GameObject("PrimitiveHost");
            return host.transform;
        }
    }
}
