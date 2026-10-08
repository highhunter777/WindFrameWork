namespace WindFrameWork.Core.Container
{
    /// <summary>
    /// 服务注册契约，仅写侧：暴露给组合根与模块注册阶段，不含任何解析能力。
    /// </summary>
    /// <remarks>
    /// 接口不继承 <see cref="IServiceResolver"/>，是读写分离在类型系统层面的强制：持有注册契约的代码无法解析服务。
    /// </remarks>
    public interface IServiceRegistry
    {
        /// <summary>
        /// 注册单例实例。
        /// </summary>
        void Register<TService>(ServiceLifetime lifetime, TService instance) where TService : class;

        /// <summary>
        /// 注册具名实例。
        /// </summary>
        void Register<TService>(string name, ServiceLifetime lifetime, TService instance) where TService : class;

        /// <summary>
        /// 按实现类型注册，首次解析时实例化。
        /// </summary>
        void Register<TService, TImplementation>(ServiceLifetime lifetime)
            where TService : class
            where TImplementation : class, TService;

        /// <summary>
        /// 按实现类型注册具名服务，首次解析时实例化。
        /// </summary>
        void Register<TService, TImplementation>(string name, ServiceLifetime lifetime)
            where TService : class
            where TImplementation : class, TService;

        /// <summary>
        /// 注销服务。
        /// </summary>
        /// <returns>存在并已注销时返回 true。</returns>
        bool Unregister<TService>(string name = null) where TService : class;
    }
}
