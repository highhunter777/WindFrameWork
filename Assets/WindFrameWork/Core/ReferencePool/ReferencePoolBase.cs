namespace WindFrameWork.Core.ReferencePool
{
    /// <summary>
    /// 引用池的非泛型基类：向全局注册表暴露与元素类型无关的生命周期回调。
    /// </summary>
    /// <remarks>
    /// 注册表需要容纳任意元素类型的池，泛型无法作为集合元素类型，故以本基类作为桥接。
    /// </remarks>
    public abstract class ReferencePoolBase
    {
        /// <summary>
        /// 解除池持有的全部对象引用。
        /// </summary>
        internal abstract void ReleaseEverything();
    }
}
