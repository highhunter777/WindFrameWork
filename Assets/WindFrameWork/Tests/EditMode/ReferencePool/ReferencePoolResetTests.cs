using System.Collections.Generic;
using NUnit.Framework;
using WindFrameWork.Core.ObjectPool;
using WindFrameWork.Core.ReferencePool;

namespace WindFrameWork.Tests.EditMode.ReferencePool
{
    /// <summary>
    /// 生命周期钩子与重置回调：池自身不改业务状态，一切由回调表达。
    /// </summary>
    [TestFixture]
    public sealed class ReferencePoolResetTests
    {
        private List<string> _log;

        private sealed class HookableBag : IPoolable
        {
            private readonly List<string> _log;

            public HookableBag(List<string> log)
            {
                _log = log;
            }

            public void OnRentFromPool()
            {
                _log.Add("hook.rent");
            }

            public void OnReturnToPool()
            {
                _log.Add("hook.return");
            }
        }

        [SetUp]
        public void SetUp()
        {
            _log = new List<string>();
        }

        [Test]
        public void RentReturn_InvokesPoolableHooks()
        {
            using var pool = new ReferencePool<HookableBag>(
                () => new HookableBag(_log),
                ReferencePoolConfig.Default("Hooks"));

            ReferenceHandle<HookableBag> handle = pool.Rent();
            Assert.That(_log, Is.EqualTo(new[] { "hook.rent" }));

            pool.Return(handle);
            Assert.That(_log, Is.EqualTo(new[] { "hook.rent", "hook.return" }));
        }

        [Test]
        public void ResetCallback_IsInvokedAfterHook()
        {
            // 顺序固定：类型自身的钩子先于外部注入的重置回调，
            // 使实现类得以在回调介入前完成自身的一致性收敛。
            using var pool = new ReferencePool<HookableBag>(
                () => new HookableBag(_log),
                ReferencePoolConfig.Default("Order"),
                onReset: _ => _log.Add("reset"));

            ReferenceHandle<HookableBag> handle = pool.Rent();
            pool.Return(handle);

            Assert.That(_log, Is.EqualTo(new[] { "hook.rent", "hook.return", "reset" }));
        }

        [Test]
        public void ResetCallback_ClearsContainerContent()
        {
            // 引用池的典型用法：容器类对象归还时必须清空内容，否则下一任租农户会读到残留数据。
            using var pool = new ReferencePool<List<int>>(
                () => new List<int>(),
                ReferencePoolConfig.Default("ListVRestore"),
                onReset: list => list.Clear());

            ReferenceHandle<List<int>> handle = pool.Rent();
            handle.Object.Add(1);
            handle.Object.Add(2);
            pool.Return(handle);

            ReferenceHandle<List<int>> again = pool.Rent();

            Assert.That(again.Object.Count, Is.Zero);
            pool.Return(again);
        }

        [Test]
        public void ReleaseCallback_IsInvokedWhenObjectLeavesPool()
        {
            var released = new List<HookableBag>();
            var config = new ReferencePoolConfig("Release", maxIdleSize: 0);
            using var pool = new ReferencePool<HookableBag>(
                () => new HookableBag(_log),
                config,
                onRelease: item => released.Add(item));

            ReferenceHandle<HookableBag> handle = pool.Rent();
            HookableBag instance = handle.Object;
            pool.Return(handle);

            Assert.That(released, Is.EqualTo(new[] { instance }));
        }

        [Test]
        public void Clear_InvokesReleaseCallbackForEveryHeldObject()
        {
            int releaseCount = 0;
            var config = new ReferencePoolConfig("ClearRelease", prewarmCount: 3, maxIdleSize: 8);
            using var pool = new ReferencePool<HookableBag>(
                () => new HookableBag(_log),
                config,
                onRelease: _ => releaseCount++);

            pool.Clear();

            Assert.That(releaseCount, Is.EqualTo(3));
        }

        [Test]
        public void Hook_IsResolvedOncePerObject()
        {
            // 钩子在对象创建时解析一次并按槽位缓存：同一对象反复租还不得重复解析。
            using var pool = new ReferencePool<HookableBag>(
                () => new HookableBag(_log),
                ReferencePoolConfig.Default("Cached"));

            ReferenceHandle<HookableBag> first = pool.Rent();
            pool.Return(first);
            ReferenceHandle<HookableBag> second = pool.Rent();

            Assert.That(second.Object, Is.SameAs(first.Object));
            Assert.That(_log, Is.EqualTo(new[] { "hook.rent", "hook.return", "hook.rent" }));

            pool.Return(second);
        }
    }
}
