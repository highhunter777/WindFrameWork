using System;

namespace WindFrameWork.Core.Container
{
    /// <summary>
    /// 服务键，由服务类型与可选名称组成，是注册与解析的唯一索引。
    /// </summary>
    /// <remarks>
    /// 名称用于在同一契约下并存多个实现（如 Runtime/ 下的平台变体），因此仅按类型索引会使后注册者覆盖先注册者。
    /// </remarks>
    public readonly struct ServiceKey : IEquatable<ServiceKey>
    {
        private readonly string _name;

        /// <summary>
        /// 创建服务键。
        /// </summary>
        /// <param name="serviceType">服务契约类型。</param>
        /// <param name="name">实现名称，为 null 时表示该契约的默认实现。</param>
        public ServiceKey(Type serviceType, string name = null)
        {
            ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
            _name = name;
        }

        /// <summary>
        /// 服务契约类型。
        /// </summary>
        public Type ServiceType { get; }

        /// <summary>
        /// 实现名称，为 null 时表示该契约的默认实现。
        /// </summary>
        public string Name => _name;

        /// <summary>
        /// 是否为指定契约的默认实现。
        /// </summary>
        public bool IsDefault => _name == null;

        /// <summary>
        /// 创建默认实现的服务键。
        /// </summary>
        public static ServiceKey Of<TService>() where TService : class
        {
            return new ServiceKey(typeof(TService));
        }

        /// <summary>
        /// 创建具名实现的服务键。
        /// </summary>
        public static ServiceKey Of<TService>(string name) where TService : class
        {
            return new ServiceKey(typeof(TService), name);
        }

        /// <inheritdoc />
        public bool Equals(ServiceKey other)
        {
            return ServiceType == other.ServiceType && string.Equals(_name, other._name, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is ServiceKey other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (ServiceType.GetHashCode() * 397) ^ (_name != null ? _name.GetHashCode() : 0);
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return _name == null ? ServiceType.Name : ServiceType.Name + ":" + _name;
        }
    }
}
