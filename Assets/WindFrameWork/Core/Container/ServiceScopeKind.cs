namespace WindFrameWork.Core.Container
{
    /// <summary>
    /// 作用域种类，标识一组作用域实例的归属边界。
    /// </summary>
    public enum ServiceScopeKind
    {
        /// <summary>
        /// 全局作用域，随定位器存活。
        /// </summary>
        Global,

        /// <summary>
        /// 场景作用域，通常在场景进入时创建、离开时释放。
        /// </summary>
        Scene,

        /// <summary>
        /// 关卡作用域，通常在关卡加载时创建、结束时释放。
        /// </summary>
        Level
    }
}
