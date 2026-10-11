using NUnit.Framework;
using UnityEngine;
using WindFrameWork.Core.ObjectPool;

namespace WindFrameWork.Tests.EditMode.ObjectPool
{
    /// <summary>
    /// 层级归置与隐藏标记：池内对象应集中于单一池根之下，且不污染场景存档。
    /// </summary>
    [TestFixture]
    public sealed class ObjectPoolHierarchyTests
    {
        private GameObject _sharedRoot;

        [SetUp]
        public void SetUp()
        {
            _sharedRoot = new GameObject("SharedPoolRoot");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sharedRoot);
        }

        [Test]
        public void PooledObjects_AreParentedUnderPoolRoot()
        {
            var config = new ObjectPoolConfig("Nested", reparentToPoolRoot: true);
            using var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), config, _sharedRoot.transform);

            PooledHandle<GameObject> handle = pool.Rent();

            Assert.That(handle.Object.transform.parent, Is.Not.Null);
            Assert.That(handle.Object.transform.parent.parent, Is.EqualTo(_sharedRoot.transform));
        }

        [Test]
        public void PoolRoot_CarriesDontSave()
        {
            var config = new ObjectPoolConfig("DontSaveRoot", reparentToPoolRoot: true);
            using var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), config, _sharedRoot.transform);

            PooledHandle<GameObject> handle = pool.Rent();
            Transform poolRoot = handle.Object.transform.parent;

            Assert.That(poolRoot.gameObject.hideFlags & HideFlags.DontSave, Is.EqualTo(HideFlags.DontSave));
        }

        [Test]
        public void PooledObjects_CarryDontSave()
        {
            var config = new ObjectPoolConfig("DontSaveItems", reparentToPoolRoot: true);
            using var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), config, _sharedRoot.transform);

            PooledHandle<GameObject> handle = pool.Rent();

            Assert.That(handle.Object.hideFlags & HideFlags.DontSave, Is.EqualTo(HideFlags.DontSave));
        }

        [Test]
        public void HidePoolRootInHierarchy_ControlsHiding()
        {
            var hidden = new ObjectPoolConfig("HiddenRoot", reparentToPoolRoot: true, hidePoolRootInHierarchy: true);
            using var hiddenPool = new ObjectPool<GameObject>(() => new GameObject("Item"), hidden, _sharedRoot.transform);
            PooledHandle<GameObject> hiddenHandle = hiddenPool.Rent();

            var shown = new ObjectPoolConfig("ShownRoot", reparentToPoolRoot: true, hidePoolRootInHierarchy: false);
            using var shownPool = new ObjectPool<GameObject>(() => new GameObject("Item"), shown, _sharedRoot.transform);
            PooledHandle<GameObject> shownHandle = shownPool.Rent();

            Assert.That(hiddenHandle.Object.transform.parent.gameObject.hideFlags & HideFlags.HideInHierarchy,
                Is.EqualTo(HideFlags.HideInHierarchy));
            // 枚举零值须与 HideFlags.None 比较：Is.Zero 会因类型不兼容而失败。
            Assert.That(shownHandle.Object.transform.parent.gameObject.hideFlags & HideFlags.HideInHierarchy,
                Is.EqualTo(HideFlags.None));
        }

        [Test]
        public void ReparentDisabled_LeavesParentUntouched()
        {
            var config = new ObjectPoolConfig("NoReparent", reparentToPoolRoot: false);
            using var pool = new ObjectPool<GameObject>(() => new GameObject("Item"), config, _sharedRoot.transform);

            PooledHandle<GameObject> handle = pool.Rent();

            Assert.That(handle.Object.transform.parent, Is.Null);
        }

        [Test]
        public void Reparenting_DoesNotWorldPositionShift()
        {
            var config = new ObjectPoolConfig("KeepLocal", reparentToPoolRoot: true);
            using var pool = new ObjectPool<GameObject>(() =>
            {
                var item = new GameObject("Item");
                item.transform.localPosition = new Vector3(5f, 0f, 0f);
                return item;
            }, config, _sharedRoot.transform);

            PooledHandle<GameObject> handle = pool.Rent();

            Assert.That(handle.Object.transform.localPosition, Is.EqualTo(new Vector3(5f, 0f, 0f)));
        }
    }
}
