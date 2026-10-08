using System;
using NUnit.Framework;
using UnityEngine;
using WindFrameWork.Core.ObjectPool;

namespace WindFrameWork.Tests.EditMode.ObjectPool
{
    [TestFixture]
    public sealed class ObjectPoolConfigTests
    {
        [Test]
        public void EmptyName_Throws()
        {
            Assert.Throws<ArgumentException>(() => new ObjectPoolConfig(string.Empty));
        }

        [Test]
        public void NegativePrewarmCount_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObjectPoolConfig("t", prewarmCount: -1));
        }

        [Test]
        public void NonPositiveMaxLiveSize_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObjectPoolConfig("t", maxLiveSize: 0));
        }

        [Test]
        public void PrewarmExceedingMaxIdle_Throws()
        {
            // 预热产物会立刻超出空闲上限而被销毁，属配置错误，应当在构造时暴露而非静默丢弃。
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObjectPoolConfig("t", prewarmCount: 8, maxIdleSize: 4));
        }

        [Test]
        public void InitialSlotCapacityBelowPrewarm_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ObjectPoolConfig("t", prewarmCount: 4, initialSlotCapacity: 2));
        }

        [Test]
        public void Default_UsesDocumentedConstants()
        {
            ObjectPoolConfig config = ObjectPoolConfig.Default("t");

            Assert.That(config.PrewarmCount, Is.EqualTo(ObjectPoolConfig.DefaultPrewarmCount));
            Assert.That(config.MaxIdleSize, Is.EqualTo(ObjectPoolConfig.DefaultMaxIdleSize));
            Assert.That(config.MaxLiveSize, Is.EqualTo(ObjectPoolConfig.DefaultMaxLiveSize));
        }

        [Test]
        public void DerivedSlotCapacity_IsPositiveEvenWithoutPrewarm()
        {
            ObjectPoolConfig config = ObjectPoolConfig.Default("t");

            Assert.That(config.InitialSlotCapacity, Is.GreaterThan(0));
        }

        [Test]
        public void DerivedSlotCapacity_CoversPrewarmCount()
        {
            ObjectPoolConfig config = new ObjectPoolConfig("t", prewarmCount: 6);

            Assert.That(config.InitialSlotCapacity, Is.GreaterThanOrEqualTo(6));
        }
    }
}
