using NUnit.Framework;
using WindFrameWork.Core.Container;

namespace WindFrameWork.Tests.EditMode.Container
{
    [TestFixture]
    public sealed class ServiceScopeTests
    {
        private ServiceLocator _locator;

        [SetUp]
        public void SetUp()
        {
            _locator = new ServiceLocator();
        }

        [TearDown]
        public void TearDown()
        {
            _locator.Dispose();
        }

        [Test]
        public void ScopedService_ReturnsSameInstanceWithinScope()
        {
            _locator.Register<IProbeService, DisposableProbeService>(ServiceLifetime.Scoped);

            using IServiceScope scope = _locator.CreateScope(ServiceScopeKind.Scene);
            var resolver = (IServiceResolver)scope;

            Assert.That(resolver.Resolve<IProbeService>(), Is.SameAs(resolver.Resolve<IProbeService>()));
        }

        [Test]
        public void ScopedService_ReturnsDifferentInstancesAcrossScopes()
        {
            _locator.Register<IProbeService, DisposableProbeService>(ServiceLifetime.Scoped);

            using IServiceScope first = _locator.CreateScope(ServiceScopeKind.Scene);
            using IServiceScope second = _locator.CreateScope(ServiceScopeKind.Scene);

            object firstInstance = first.Resolver.Resolve<IProbeService>();
            object secondInstance = second.Resolver.Resolve<IProbeService>();

            Assert.That(secondInstance, Is.Not.SameAs(firstInstance));
        }

        [Test]
        public void SingletonService_IsSharedAcrossScopes()
        {
            _locator.Register<IProbeService, ProbeService>(ServiceLifetime.Singleton);

            using IServiceScope first = _locator.CreateScope(ServiceScopeKind.Level);
            using IServiceScope second = _locator.CreateScope(ServiceScopeKind.Level);

            Assert.That(second.Resolver.Resolve<IProbeService>(),
                Is.SameAs(first.Resolver.Resolve<IProbeService>()));
        }

        [Test]
        public void DisposingScope_ReleasesScopedInstances()
        {
            _locator.Register<IProbeService, DisposableProbeService>(ServiceLifetime.Scoped);

            IServiceScope scope = _locator.CreateScope(ServiceScopeKind.Scene);
            var instance = (DisposableProbeService)scope.Resolver.Resolve<IProbeService>();

            scope.Dispose();

            Assert.That(instance.IsDisposed, Is.True);
        }

        [Test]
        public void DisposedScope_RejectsFurtherResolution()
        {
            _locator.Register<IProbeService, DisposableProbeService>(ServiceLifetime.Scoped);

            IServiceScope scope = _locator.CreateScope(ServiceScopeKind.Scene);
            scope.Dispose();

            Assert.Throws<System.ObjectDisposedException>(() => scope.Resolver.Resolve<IProbeService>());
        }

        [Test]
        public void DisposedScope_IsUntrackedByLocator()
        {
            IServiceScope scope = _locator.CreateScope(ServiceScopeKind.Scene);
            Assert.That(_locator.ScopeCount, Is.EqualTo(1));

            scope.Dispose();

            Assert.That(_locator.ScopeCount, Is.Zero);
        }

        [Test]
        public void Scope_CarriesItsKind()
        {
            using IServiceScope scope = _locator.CreateScope(ServiceScopeKind.Level);

            Assert.That(scope.Kind, Is.EqualTo(ServiceScopeKind.Level));
        }

        [Test]
        public void Scope_ResolvesTransientServiceNewlyEachTime()
        {
            _locator.Register<IProbeService, CountingProbeService>(ServiceLifetime.Transient);
            CountingProbeService.CreateCount = 0;

            using IServiceScope scope = _locator.CreateScope(ServiceScopeKind.Scene);
            scope.Resolver.Resolve<IProbeService>();
            scope.Resolver.Resolve<IProbeService>();

            Assert.That(CountingProbeService.CreateCount, Is.EqualTo(2));
        }
    }
}
