namespace WindFrameWork.Core.ObjectPool
{
    /// <summary>
    /// 池化对象生命周期契约：归还时清理业务状态，租出时重建自身状态。
    /// </summary>
    /// <remarks>
    /// 池不修改对象的位置与旋转：重置变换属于业务策略，应在此实现。
    /// 钩子在对象创建时解析一次并按槽位缓存，因此实现内不应假设钩子每次都会被重新发现。
    /// </remarks>
    public interface IPoolable
    {
        /// <summary>
        /// 被租出时调用，用于重置位置、计时器、事件订阅等业务状态。
        /// </summary>
        void OnRentFromPool();

        /// <summary>
        /// 被归还时调用，用于停止协程、取消订阅、还原初始值等。
        /// </summary>
        void OnReturnToPool();
    }
}
