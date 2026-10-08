using UnityEngine;

namespace WindFrameWork.Core.ObjectPool
{
    /// <summary>
    /// 生命周期适配器：抽象池对不同 <see cref="Object"/> 子类型的类型特定操作。
    /// </summary>
    /// <remarks>
    /// <c>SetActive</c> 与 <c>transform</c> 只存在于 <see cref="GameObject"/>，泛型池无法直接调用；
    /// 在池内部做类型判断会同时违反单一职责与开闭原则，故以适配器隔离。
    /// </remarks>
    public interface IPoolLifecycleAdapter<TObject> where TObject : Object
    {
        /// <summary>
        /// 对象创建完成后调用，适配器可在此完成挂载层级与隐藏标记等一次性设置。
        /// </summary>
        void OnCreated(TObject item, Transform poolRoot, ObjectPoolConfig config);

        /// <summary>
        /// 租出时调用。
        /// </summary>
        void OnRent(TObject item);

        /// <summary>
        /// 归还时调用。
        /// </summary>
        void OnReturned(TObject item);

        /// <summary>
        /// 销毁前调用，适配器可在此解除层级关系。
        /// </summary>
        void OnBeforeDestroy(TObject item);
    }
}
