using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WindFrameWork.Core.Container;

namespace WindFrameWork.Tests.EditMode.Container
{
    [TestFixture]
    public sealed class ServiceLocatorResolveTests
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
        public void Resolve_UnknownService_ReturnsNull()
        {
            Assert.That(_locator.Resolve<IProbeService>(), Is.Null);
        }

        [Test]
        public void GetRequired_UnknownService_ThrowsWithKeyInMessage()
        {
            ServiceResolutionException exception = Assert.Throws<ServiceResolutionException>(
                () => _locator.GetRequired<IProbeService>());

            Assert.That(exception.Key.ServiceType, Is.EqualTo(typeof(IProbeService)));
            Assert.That(exception.Message, Does.Contain(typeof(IProbeService).Name));
        }

        [Test]
        public void TryResolve_UnknownService_ReturnsFalseWithoutThrowing()
        {
            Assert.That(_locator.TryResolve(out IProbeService service), Is.False);
            Assert.That(service, Is.Null);
        }

        [Test]
        public void ServiceResolver_ExposesNoRegistrationMembers()
        {
            // 读写分离是架构约束，用反射锁定，防止后续误将注册方法加回解析侧。
            string[] memberNames = typeof(IServiceResolver)
                .GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Select(member => member.Name)
                .ToArray();

            Assert.That(memberNames, Has.None.StartsWith("Register"));
            Assert.That(memberNames, Has.None.StartsWith("Unregister"));
        }

        [Test]
        public void ServiceRegistry_ExposesNoResolveMembers()
        {
            string[] memberNames = typeof(IServiceRegistry)
                .GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Select(member => member.Name)
                .ToArray();

            Assert.That(memberNames, Has.None.StartsWith("Resolve"));
            Assert.That(memberNames, Has.None.StartsWith("TryResolve"));
            Assert.That(memberNames, Has.None.StartsWith("GetRequired"));
        }

        [Test]
        public void ServiceKey_EqualityDistinguishesName()
        {
            Assert.That(ServiceKey.Of<IProbeService>(), Is.EqualTo(ServiceKey.Of<IProbeService>()));
            Assert.That(ServiceKey.Of<IProbeService>("a"), Is.Not.EqualTo(ServiceKey.Of<IProbeService>("b")));
            Assert.That(ServiceKey.Of<IProbeService>().GetHashCode(),
                Is.EqualTo(ServiceKey.Of<IProbeService>().GetHashCode()));
            Assert.That(ServiceKey.Of<IProbeService>().IsDefault, Is.True);
        }
    }
}
