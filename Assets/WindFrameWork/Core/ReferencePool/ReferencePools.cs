using System;
using System.Collections.Generic;
using UnityEngine;

namespace WindFrameWork.Core.ReferencePool
{
    /// <summary>
    /// 按类型的共享引用池入口：为「取一个 <c>List&lt;T&gt;</c> 用完还回」这类散点需求提供零配置的静态入口。
    /// </summary>
    /// <remarks>
    /// 本类只负责「按类型 + 可选业务键」持有共享池实例，不代表推荐的唯一用法：
    /// 有明确归属与生命周期的池（例如某个模块的命令缓冲）应自行持有一个 <see cref="ReferencePool{T}"/> 实例。
    /// <para>
    /// 查找不是零成本：键是字符串组合键，每次调用都要重算哈希。因此**不应**把它放在每帧路径上——
    /// 正确用法与容器规则同构：在初始化阶段取出池引用缓存起来，热路径上只调用 <see cref="ReferencePool{T}.Rent"/>。
    /// </para>
    /// </remarks>
    public static class ReferencePools
    {
        private const string DefaultKey = "";

        private static readonly object Gate = new object();
        private static readonly Dictionary<Type, Dictionary<string, object>> Shared =
            new Dictionary<Type, Dictionary<string, object>>();

        /// <summary>当前共享池数量。</summary>
        public static int Count
        {
            get
            {
                lock (Gate)
                {
                    int count = 0;
                    foreach (Dictionary<string, object> byKey in Shared.Values)
                    {
                        count += byKey.Count;
                    }

                    return count;
                }
            }
        }

        /// <summary>
        /// 取或创建共享池，工厂为默认构造。
        /// </summary>
        /// <param name="key">业务键，同一类型下可按业务细分；为 null 时使用默认键。</param>
        /// <param name="config">池配置，为 null 时以类型名派生默认配置。</param>
        /// <param name="onReset">归还时调用的重置回调。</param>
        /// <param name="onRelease">对象被永久移出池时调用的回调。</param>
        public static ReferencePool<T> GetOrCreate<T>(
            string key = null,
            ReferencePoolConfig? config = null,
            Action<T> onReset = null,
            Action<T> onRelease = null) where T : class, new()
        {
            return GetOrCreateCore<T>(() => new T(), key, config, onReset, onRelease);
        }

        /// <summary>
        /// 取或创建共享池，显式指定工厂。
        /// </summary>
        /// <param name="factory">对象创建工厂。</param>
        /// <param name="key">业务键，同一类型下可按业务细分；为 null 时使用默认键。</param>
        /// <param name="config">池配置，为 null 时以类型名派生默认配置。</param>
        /// <param name="onReset">归还时调用的重置回调。</param>
        /// <param name="onRelease">对象被永久移出池时调用的回调。</param>
        public static ReferencePool<T> GetOrCreate<T>(
            Func<T> factory,
            string key = null,
            ReferencePoolConfig? config = null,
            Action<T> onReset = null,
            Action<T> onRelease = null) where T : class
        {
            return GetOrCreateCore(factory, key, config, onReset, onRelease);
        }

        /// <summary>
        /// 尝试取出已存在的共享池，不存在时返回 false 而不创建。
        /// </summary>
        public static bool TryGet<T>(string key, out ReferencePool<T> pool) where T : class
        {
            lock (Gate)
            {
                if (Shared.TryGetValue(typeof(T), out Dictionary<string, object> byKey)
                    && byKey.TryGetValue(NormalizeKey(key), out object existing))
                {
                    pool = (ReferencePool<T>)existing;
                    return true;
                }
            }

            pool = null;
            return false;
        }

        /// <summary>
        /// 释放并从入口移除指定的共享池：解除其全部对象引用并注销。
        /// </summary>
        /// <returns>指定键下存在池并被释放时为 true。</returns>
        public static bool Release<T>(string key = null) where T : class
        {
            lock (Gate)
            {
                if (!Shared.TryGetValue(typeof(T), out Dictionary<string, object> byKey))
                {
                    return false;
                }

                string normalized = NormalizeKey(key);
                if (!byKey.TryGetValue(normalized, out object existing))
                {
                    return false;
                }

                ((ReferencePool<T>)existing).Dispose();
                byKey.Remove(normalized);

                if (byKey.Count == 0)
                {
                    Shared.Remove(typeof(T));
                }

                return true;
            }
        }

        /// <summary>
        /// 清空全部共享池内容，保留池本身以便继续使用。
        /// </summary>
        public static void ClearAll()
        {
            lock (Gate)
            {
                foreach (Dictionary<string, object> byKey in Shared.Values)
                {
                    foreach (object pool in byKey.Values)
                    {
                        ((ReferencePoolBase)pool).ReleaseEverything();
                    }
                }
            }
        }

        private static ReferencePool<T> GetOrCreateCore<T>(
            Func<T> factory,
            string key,
            ReferencePoolConfig? config,
            Action<T> onReset,
            Action<T> onRelease) where T : class
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            string normalized = NormalizeKey(key);

            lock (Gate)
            {
                if (!Shared.TryGetValue(typeof(T), out Dictionary<string, object> byKey))
                {
                    byKey = new Dictionary<string, object>(2);
                    Shared.Add(typeof(T), byKey);
                }

                if (byKey.TryGetValue(normalized, out object existing))
                {
                    return (ReferencePool<T>)existing;
                }

                ReferencePoolConfig effective = config ?? ReferencePoolConfig.Default(BuildName<T>(normalized));
                var pool = new ReferencePool<T>(factory, effective, onReset, onRelease);
                byKey.Add(normalized, pool);
                return pool;
            }
        }

        private static string NormalizeKey(string key)
        {
            return string.IsNullOrEmpty(key) ? DefaultKey : key;
        }

        private static string BuildName<T>(string normalizedKey)
        {
            return normalizedKey == DefaultKey ? typeof(T).Name : typeof(T).Name + "#" + normalizedKey;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnSubsystemRegistration()
        {
            lock (Gate)
            {
                foreach (Dictionary<string, object> byKey in Shared.Values)
                {
                    foreach (object pool in byKey.Values)
                    {
                        ((ReferencePoolBase)pool).ReleaseEverything();
                    }
                }

                Shared.Clear();
            }
        }
    }
}
