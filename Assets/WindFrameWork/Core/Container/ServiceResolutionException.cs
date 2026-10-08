using System;

namespace WindFrameWork.Core.Container
{
    /// <summary>
    /// 解析服务失败时抛出，标识请求的键在容器中不存在或无法实例化。
    /// </summary>
    /// <remarks>
    /// 注册期误用（重复键、生命周期冲突）使用 <see cref="InvalidOperationException"/>：注册只发生在组合根与模块注册阶段，
    /// 该处的错误是立即可见的程序错误；解析期失败则可能在场景流转中浮现，需要可捕获的自描述类型。
    /// </remarks>
    public sealed class ServiceResolutionException : Exception
    {
        /// <summary>
        /// 创建解析异常。
        /// </summary>
        public ServiceResolutionException(ServiceKey key, string message)
            : base(message)
        {
            Key = key;
        }

        /// <summary>
        /// 创建解析异常。
        /// </summary>
        public ServiceResolutionException(ServiceKey key, string message, Exception innerException)
            : base(message, innerException)
        {
            Key = key;
        }

        /// <summary>
        /// 解析失败的服务键。
        /// </summary>
        public ServiceKey Key { get; }
    }
}
