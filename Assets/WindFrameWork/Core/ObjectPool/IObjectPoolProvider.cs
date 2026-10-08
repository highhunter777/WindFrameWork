using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WindFrameWork.Core.ObjectPool
{
    /// <summary>
    /// 对象池注册表契约：按名称提供类型安全的池访问。
    /// </summary>
    public interface IObjectPoolProvider
    {
        /// <summary>
        /// 按名称取池，同名同类型返回同一实例。
        /// </summary>
        /// <exception cref="InvalidOperationException">名称未注册，或已注册的元素类型与请求类型不一致时抛出。</exception>
        IObjectPool<TObject> Get<TObject>(string key) where TObject : UnityEngine.Object;

        /// <summary>
        /// 按名称尝试取池。
        /// </summary>
        bool TryGet<TObject>(string key, out IObjectPool<TObject> pool) where TObject : UnityEngine.Object;

        /// <summary>
        /// 按名称注册池。
        /// </summary>
        /// <exception cref="InvalidOperationException">同名已注册过其他元素类型时抛出。</exception>
        void Register<TObject>(string key, IObjectPool<TObject> pool) where TObject : UnityEngine.Object;

        /// <summary>
        /// 释放并移除指定名称的池。
        /// </summary>
        /// <returns>存在并已释放时返回 true。</returns>
        bool Release(string key);

        /// <summary>释放全部池。</summary>
        void ReleaseAll();

        /// <summary>
        /// 取指定名称的池统计。
        /// </summary>
        ObjectPoolStatistics GetStatistics(string key);

        /// <summary>
        /// 取全部池的诊断信息，按池名排序以保证结果可复现。
        /// </summary>
        IReadOnlyList<ObjectPoolInfo> GetPools();
    }
}
