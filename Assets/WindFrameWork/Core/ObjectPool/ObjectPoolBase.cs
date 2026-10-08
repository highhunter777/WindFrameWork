namespace WindFrameWork.Core.ObjectPool
{
    /// <summary>
    /// 对象池的非泛型基类：向全局注册表暴露与元素类型无关的生命周期回调。
    /// </summary>
    /// <remarks>
    /// 注册表需要容纳任意元素类型的池，泛型无法作为集合元素类型，故以本基类作为桥接。
    /// </remarks>
    public abstract class ObjectPoolBase
    {
        /// <summary>
        /// 清空对象引用，不销毁任何 Unity 对象。供非播放态的引擎生命周期阶段调用。
        /// </summary>
        internal abstract void Recycle();

        /// <summary>
        /// 销毁池内全部对象。供编辑器程序集重载前等确定可销毁的时机调用。
        /// </summary>
        internal abstract void DestroyEverything();
    }
}
