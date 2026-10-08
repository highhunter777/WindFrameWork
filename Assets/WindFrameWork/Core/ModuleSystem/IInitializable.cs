namespace WindFrameWork.Core.ModuleSystem
{
    /// <summary>
    /// 可选初始化能力：模块无需实现全部生命周期钩子，只实现自身需要的能力接口。
    /// </summary>
    public interface IInitializable
    {
        /// <summary>
        /// 初始化模块。此处不解析服务：跨模块依赖由组合根构造注入。
        /// </summary>
        void Initialize();
    }
}
