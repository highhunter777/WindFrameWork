using NUnit.Framework;
using WindFrameWork.Core.Container;

namespace WindFrameWork.Tests.EditMode.Container
{
    /// <summary>
    /// 测试用服务契约。
    /// </summary>
    public interface IProbeService
    {
        int Value { get; }
    }

    /// <summary>
    /// 测试用默认实现。
    /// </summary>
    public sealed class ProbeService : IProbeService
    {
        public int Value => 1;
    }

    /// <summary>
    /// 测试用平台变体实现，用于验证同一契约下多实现并存。
    /// </summary>
    public sealed class AlternateProbeService : IProbeService
    {
        public int Value => 2;
    }

    /// <summary>
    /// 测试用瞬态实现，由计数器验证是否每次新建。
    /// </summary>
    public sealed class CountingProbeService : IProbeService
    {
        public static int CreateCount;

        public int Value => CreateCount;

        public CountingProbeService()
        {
            CreateCount++;
        }
    }

    /// <summary>
    /// 测试用可释放实现，验证作用域释放时资源被回收。
    /// </summary>
    public sealed class DisposableProbeService : IProbeService, System.IDisposable
    {
        public bool IsDisposed { get; private set; }

        public int Value => 3;

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    [TestFixture]
    public sealed class ServiceLocatorRegistrationTests
    {
        private ServiceLocator _locator;

        [SetUp]
        public void SetUp()
        {
            _locator = new ServiceLocator();
            CountingProbeService.CreateCount = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _locator.Dispose();
        }

        [Test]
        public void Singleton_ReturnsSameInstance()
        {
            _locator.Register<IProbeService, ProbeService>(ServiceLifetime.Singleton);

            IProbeService first = _locator.Resolve<IProbeService>();
            IProbeService second = _locator.Resolve<IProbeService>();

            Assert.That(second, Is.SameAs(first));
        }

        [Test]
        public void Transient_ReturnsNewInstanceEachResolve()
        {
            _locator.Register<IProbeService, CountingProbeService>(ServiceLifetime.Transient);

            _locator.Resolve<IProbeService>();
            _locator.Resolve<IProbeService>();

            Assert.That(CountingProbeService.CreateCount, Is.EqualTo(2));
        }

        [Test]
        public void DuplicateSingletonRegistration_Throws()
        {
            _locator.Register<IProbeService, ProbeService>(ServiceLifetime.Singleton);

            Assert.Throws<System.InvalidOperationException>(
                () => _locator.Register<IProbeService, AlternateProbeService>(ServiceLifetime.Singleton));
        }

        [Test]
        public void RegisterInstance_WithTransient_Throws()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => _locator.Register(ServiceLifetime.Transient, new ProbeService()));
        }

        [Test]
        public void Unregister_RemovesService()
        {
            _locator.Register<IProbeService, ProbeService>(ServiceLifetime.Singleton);

            Assert.That(_locator.Unregister<IProbeService>(), Is.True);
            Assert.That(_locator.Resolve<IProbeService>(), Is.Null);
            Assert.That(_locator.Unregister<IProbeService>(), Is.False);
        }

        [Test]
        public void NamedRegistrations_CoexistUnderOneContract()
        {
            _locator.Register<IProbeService, ProbeService>(ServiceLifetime.Singleton);
            _locator.Register<IProbeService, AlternateProbeService>("alternate", ServiceLifetime.Singleton);

            Assert.That(_locator.Resolve<IProbeService>(), Is.InstanceOf<ProbeService>());
            Assert.That(_locator.Resolve<IProbeService>("alternate"), Is.InstanceOf<AlternateProbeService>());
        }

        [Test]
        public void RegisteredCount_ReflectsRegistrations()
        {
            _locator.Register<IProbeService, ProbeService>(ServiceLifetime.Singleton);
            _locator.Register<IProbeService, AlternateProbeService>("alternate", ServiceLifetime.Singleton);

            Assert.That(_locator.RegisteredCount, Is.EqualTo(2));
        }
    }
}
