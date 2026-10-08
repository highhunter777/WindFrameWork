using NUnit.Framework;
using WindFrameWork.Core.Container;
using WindFrameWork.Core.ModuleSystem;

namespace WindFrameWork.Tests.EditMode.Container
{
    /// <summary>
    /// 最小模块：使用基类默认值，不重写任何成员。
    /// </summary>
    public sealed class MinimalModule : ModuleBase
    {
    }

    /// <summary>
    /// 自注册模块：重写注册并持有能力接口。
    /// </summary>
    public sealed class SelfRegisteringModule : ModuleBase, IInitializable, IShutdownable
    {
        public IServiceRegistry ReceivedRegistry { get; private set; }

        public bool IsInitialized { get; private set; }

        public bool IsShutdown { get; private set; }

        public override string Name => "SelfRegistering";

        public override int Order => 10;

        public override void Register(IServiceRegistry registry)
        {
            ReceivedRegistry = registry;
            registry.Register<IProbeService, ProbeService>(ServiceLifetime.Singleton);
        }

        public void Initialize()
        {
            IsInitialized = true;
        }

        public void Shutdown()
        {
            IsShutdown = true;
        }
    }

    [TestFixture]
    public sealed class ModuleBaseTests
    {
        [Test]
        public void Defaults_DeriveNameFromTypeAndZeroOrder()
        {
            var module = new MinimalModule();

            Assert.That(module.Name, Is.EqualTo(nameof(MinimalModule)));
            Assert.That(module.Order, Is.Zero);
        }

        [Test]
        public void DefaultRegister_IsNoOpThatDoesNotThrow()
        {
            using var locator = new ServiceLocator();

            Assert.DoesNotThrow(() => new MinimalModule().Register(locator));
            Assert.That(locator.RegisteredCount, Is.Zero);
        }

        [Test]
        public void Register_ReceivesTheGivenRegistry()
        {
            using var locator = new ServiceLocator();
            var module = new SelfRegisteringModule();

            module.Register(locator);

            Assert.That(module.ReceivedRegistry, Is.SameAs(locator));
        }

        [Test]
        public void RegisteredServices_AreResolvableFromLocator()
        {
            using var locator = new ServiceLocator();
            new SelfRegisteringModule().Register(locator);

            Assert.That(locator.Resolve<IProbeService>(), Is.InstanceOf<ProbeService>());
        }

        [Test]
        public void CapabilityInterfaces_AreDetectedByTypeCheck()
        {
            var module = new SelfRegisteringModule();

            Assert.That(module, Is.InstanceOf<IInitializable>());
            Assert.That(module, Is.InstanceOf<IShutdownable>());
            Assert.That(new MinimalModule(), Is.Not.InstanceOf<IInitializable>());
        }

        [Test]
        public void LifecycleMethods_AreInvokedThroughCapabilityInterfaces()
        {
            var module = new SelfRegisteringModule();

            ((IInitializable)module).Initialize();
            ((IShutdownable)module).Shutdown();

            Assert.That(module.IsInitialized, Is.True);
            Assert.That(module.IsShutdown, Is.True);
        }

        [Test]
        public void ModuleBase_ImplementsModuleContract()
        {
            Assert.That(new MinimalModule(), Is.InstanceOf<IModule>());
        }
    }
}
