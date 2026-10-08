namespace WindFrameWork.Core.Container
{
    /// <summary>
    /// 服务生命周期，决定实例的创建时机与存活范围。
    /// </summary>
    public enum ServiceLifetime
    {
        /// <summary>
        /// 全局唯一实例，首次解析时创建，由定位器持有至其自身被释放。
        /// </summary>
        Singleton,

        /// <summary>
        /// 作用域内唯一，作用域创建后首次解析时创建，作用域释放时随之释放。
        /// </summary>
        Scoped,

        /// <summary>
        /// 每次解析都创建新实例，不被容器持有。
        /// </summary>
        Transient
    }
}
