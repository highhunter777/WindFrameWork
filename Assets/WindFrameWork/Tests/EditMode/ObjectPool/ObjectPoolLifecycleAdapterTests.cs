using NUnit.Framework;
using UnityEngine;
using WindFrameWork.Core.ObjectPool;

namespace WindFrameWork.Tests.EditMode.ObjectPool
{
    /// <summary>
    /// 生命周期钩子组件：验证钩子在创建时解析一次并按槽位缓存，租还时被正确触发。
    /// </summary>
    public sealed class PoolHookCounter : MonoBehaviour, IPoolable
    {
        public static int RentCount;
        public static int ReturnCount;

        public void OnRentFromPool()
        {
            RentCount++;
        }

        public void OnReturnToPool()
        {
            ReturnCount++;
        }
    }

    [TestFixture]
    public sealed class ObjectPoolLifecycleAdapterTests
    {
        [SetUp]
        public void SetUp()
        {
            PoolHookCounter.RentCount = 0;
            PoolHookCounter.ReturnCount = 0;
        }

        [Test]
        public void GameObjectPool_InvokesHooksOnRentAndReturn()
        {
            using var pool = new ObjectPool<GameObject>(CreateHookedGameObject, ObjectPoolConfig.Default("Hooked"));

            PooledHandle<GameObject> handle = pool.Rent();
            Assert.That(PoolHookCounter.RentCount, Is.EqualTo(1));

            pool.Return(handle);
            Assert.That(PoolHookCounter.ReturnCount, Is.EqualTo(1));
        }

        [Test]
        public void ComponentPool_ActivatesUnderlyingGameObject()
        {
            using var pool = new ObjectPool<Transform>(CreateHookedTransform, ObjectPoolConfig.Default("ComponentPool"));

            PooledHandle<Transform> handle = pool.Rent();
            Assert.That(handle.Object.gameObject.activeSelf, Is.True);

            pool.Return(handle);
            Assert.That(handle.Object.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void ScriptableObjectPool_RentsAndReturnsWithoutActivation()
        {
            var config = new ObjectPoolConfig("SoPool", reparentToPoolRoot: false);
            using var pool = new ObjectPool<ScriptableObject>(
                () => ScriptableObject.CreateInstance<PoolableAsset>(),
                config);

            PooledHandle<ScriptableObject> handle = pool.Rent();
            Assert.That(handle.Object, Is.Not.Null);

            Assert.DoesNotThrow(() => pool.Return(handle));
            Assert.That(pool.IdleCount, Is.EqualTo(1));
        }

        [Test]
        public void PoolDoesNotInvokeHooksOnUnrelatedObjects()
        {
            using var pool = new ObjectPool<GameObject>(() => new GameObject("Plain"), ObjectPoolConfig.Default("NoHooks"));

            PooledHandle<GameObject> handle = pool.Rent();
            pool.Return(handle);

            Assert.That(PoolHookCounter.RentCount, Is.Zero);
        }

        [Test]
        public void CustomAdapter_CanOverrideActivation()
        {
            var config = new ObjectPoolConfig("CustomAdapter", activateOnRent: true, deactivateOnReturn: true);
            var adapter = new CountingAdapter();

            using var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), config, null, adapter);

            PooledHandle<GameObject> handle = pool.Rent();
            pool.Return(handle);

            Assert.That(adapter.CreatedCount, Is.EqualTo(1));
            Assert.That(adapter.RentCount, Is.EqualTo(1));
            Assert.That(adapter.ReturnedCount, Is.EqualTo(1));
        }

        private static GameObject CreateHookedGameObject()
        {
            var item = new GameObject("Hooked");
            item.AddComponent<PoolHookCounter>();
            return item;
        }

        private static Transform CreateHookedTransform()
        {
            var host = new GameObject("HookedHost");
            host.AddComponent<PoolHookCounter>();
            return host.transform;
        }

        /// <summary>
        /// 自定义适配器：验证适配器是有效的扩展点，而不仅是内置实现。
        /// </summary>
        private sealed class CountingAdapter : IPoolLifecycleAdapter<GameObject>
        {
            public int CreatedCount { get; private set; }

            public int RentCount { get; private set; }

            public int ReturnedCount { get; private set; }

            public void OnCreated(GameObject item, Transform poolRoot, ObjectPoolConfig config)
            {
                CreatedCount++;
            }

            public void OnRent(GameObject item)
            {
                RentCount++;
            }

            public void OnReturned(GameObject item)
            {
                ReturnedCount++;
            }

            public void OnBeforeDestroy(GameObject item)
            {
            }
        }
    }

    /// <summary>
    /// 可池化的 ScriptableObject 资产。
    /// </summary>
    public sealed class PoolableAsset : ScriptableObject, IPoolable
    {
        public void OnRentFromPool()
        {
        }

        public void OnReturnToPool()
        {
        }
    }
}
