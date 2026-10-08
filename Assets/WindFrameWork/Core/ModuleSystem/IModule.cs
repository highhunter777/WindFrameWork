using WindFrameWork.Core.Container;

namespace WindFrameWork.Core.ModuleSystem
{
    /// <summary>
    /// 功能模块契约：模块在注册阶段向容器写入自身提供的服务。
    /// </summary>
    /// <remarks>
    /// 模块只持有 <see cref="IServiceRegistry"/>（写侧），因此无法在注册阶段解析服务。
    /// </remarks>
    public interface IModule
    {
        /// <summary>
        /// 模块名，用于日志与诊断。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 启动顺序，由组合根按装配清单配置，模块自身不决定全局顺序。
        /// </summary>
        int Order { get; }

        /// <summary>
        /// 注册模块提供的服务，仅在组合根或模块注册阶段调用。
        /// </summary>
        void Register(IServiceRegistry registry);
    }
}
