using WindFrameWork.Core.Container;

namespace WindFrameWork.Core.ModuleSystem
{
    /// <summary>
    /// 模块基类，提供可直接使用的默认实现，最小模块无需重写任何成员。
    /// </summary>
    public abstract class ModuleBase : IModule
    {
        /// <inheritdoc />
        public virtual string Name => GetType().Name;

        /// <inheritdoc />
        public virtual int Order => 0;

        /// <inheritdoc />
        public virtual void Register(IServiceRegistry registry)
        {
        }
    }
}
