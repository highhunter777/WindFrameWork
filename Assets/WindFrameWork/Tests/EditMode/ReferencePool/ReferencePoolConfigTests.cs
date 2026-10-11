using System;
using NUnit.Framework;
using WindFrameWork.Core.ReferencePool;

namespace WindFrameWork.Tests.EditMode.ReferencePool
{
    [TestFixture]
    public sealed class ReferencePoolConfigTests
    {
        private sealed class Item
        {
        }

        [Test]
        public void EmptyName_Throws()
        {
            Assert.Throws<ArgumentException>(() => new ReferencePoolConfig(string.Empty));
        }

        [Test]
        public void NegativePrewarmCount_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReferencePoolConfig("Neg", prewarmCount: -1));
        }

        [Test]
        public void NegativeMaxIdleSize_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReferencePoolConfig("Neg", maxIdleSize: -1));
        }

        [Test]
        public void NonPositiveMaxLiveSize_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReferencePoolConfig("Zero", maxLiveSize: 0));
        }

        [Test]
        public void PrewarmCountExceedingMaxIdleSize_Throws()
        {
            // 预热产物会立刻在首次归还时被丢弃，属于配置自相矛盾，应在构造期暴露。
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ReferencePoolConfig("Conflict", prewarmCount: 8, maxIdleSize: 4));
        }

        [Test]
        public void InitialSlotCapacityBelowPrewarmCount_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ReferencePoolConfig("Small", prewarmCount: 16, maxIdleSize: 32, initialSlotCapacity: 2));
        }

        [Test]
        public void DefaultThreadMode_IsSynchronized()
        {
            ReferencePoolConfig config = ReferencePoolConfig.Default("Default");

            Assert.That(config.ThreadMode, Is.EqualTo(ReferencePoolThreadMode.Synchronized));
            Assert.That(config.MaxIdleSize, Is.EqualTo(ReferencePoolConfig.DefaultMaxIdleSize));
            Assert.That(config.MaxLiveSize, Is.EqualTo(ReferencePoolConfig.DefaultMaxLiveSize));
        }

        [Test]
        public void DerivedSlotCapacity_CoversPrewarmAndIdleHeadroom()
        {
            ReferencePoolConfig config = new ReferencePoolConfig("Derived", prewarmCount: 12);

            Assert.That(config.InitialSlotCapacity, Is.GreaterThanOrEqualTo(12 + ReferencePoolConfig.DefaultMaxIdleSize));
        }

        [Test]
        public void ThreadMode_IsExposedThroughPool()
        {
            var config = new ReferencePoolConfig(
                "Single",
                threadMode: ReferencePoolThreadMode.SingleThread);
            using var pool = new ReferencePool<Item>(() => new Item(), config);

            Assert.That(pool.ThreadMode, Is.EqualTo(ReferencePoolThreadMode.SingleThread));
        }
    }
}
