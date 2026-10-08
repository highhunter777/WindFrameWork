using NUnit.Framework;
using UnityEngine;
using WindFrameWork.Core.ObjectPool;

namespace WindFrameWork.Tests.EditMode.ObjectPool
{
    /// <summary>
    /// 全局注册表：池在构造时自注册，以便引擎生命周期边界统一清理。
    /// </summary>
    [TestFixture]
    public sealed class ObjectPoolRegistryTests
    {
        [Test]
        public void ConstructedPool_IsRegistered()
        {
            int before = ObjectPoolRegistry.Count;

            using var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), ObjectPoolConfig.Default("Registered"));

            Assert.That(ObjectPoolRegistry.Count, Is.EqualTo(before + 1));
        }

        [Test]
        public void DisposedPool_IsUnregistered()
        {
            var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), ObjectPoolConfig.Default("Unregistered"));
            int afterCreate = ObjectPoolRegistry.Count;

            pool.Dispose();

            Assert.That(ObjectPoolRegistry.Count, Is.EqualTo(afterCreate - 1));
        }

        [Test]
        public void Register_IsIdempotent()
        {
            var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), ObjectPoolConfig.Default("Idempotent"));
            int afterCreate = ObjectPoolRegistry.Count;

            ObjectPoolRegistry.Register((ObjectPoolBase)pool);
            ObjectPoolRegistry.Register((ObjectPoolBase)pool);

            Assert.That(ObjectPoolRegistry.Count, Is.EqualTo(afterCreate));

            pool.Dispose();
        }

        [Test]
        public void Register_IgnoresNull()
        {
            int before = ObjectPoolRegistry.Count;

            ObjectPoolRegistry.Register(null);
            ObjectPoolRegistry.Unregister(null);

            Assert.That(ObjectPoolRegistry.Count, Is.EqualTo(before));
        }
    }
}
