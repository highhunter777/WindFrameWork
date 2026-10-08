namespace WindFrameWork.Core.ModuleSystem
{
    /// <summary>
    /// 可选关闭能力：模块无需实现全部生命周期钩子，只实现自身需要的能力接口。
    /// </summary>
    public interface IShutdownable
    {
        /// <summary>
        /// 关闭模块并释放其自身持有的资源。
        /// </summary>
        void Shutdown();
    }
}
