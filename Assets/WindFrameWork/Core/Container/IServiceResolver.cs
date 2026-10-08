namespace WindFrameWork.Core.Container
{
    /// <summary>
    /// 服务解析契约，仅读侧：不暴露任何注册能力。
    /// </summary>
    /// <remarks>
    /// 按规范，解析仅允许出现在组合根与表现层边界类型；领域类与共享层应使用构造注入。
    /// </remarks>
    public interface IServiceResolver
    {
        /// <summary>
        /// 尝试解析服务，未注册时返回 false 而不抛出。
        /// </summary>
        bool TryResolve<TService>(out TService service, string name = null) where TService : class;

        /// <summary>
        /// 解析服务，未注册时返回 null。
        /// </summary>
        TService Resolve<TService>(string name = null) where TService : class;

        /// <summary>
        /// 解析必需服务，未注册时抛出 <see cref="ServiceResolutionException"/>。表现层边界类型的推荐入口。
        /// </summary>
        TService GetRequired<TService>(string name = null) where TService : class;

        /// <summary>
        /// 创建指定种类的作用域。
        /// </summary>
        IServiceScope CreateScope(ServiceScopeKind kind);
    }
}
