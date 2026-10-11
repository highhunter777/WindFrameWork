using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace WindFrameWork.Kits.UniTaskSamples
{
    /// <summary>
    /// 模拟热更侧程序集：只依赖 UniTask，不依赖框架程序集。
    /// </summary>
    /// <remarks>
    /// 该程序集扮演将来热更程序集的角色，用于验证「AOT 侧消费热更侧产出的 <c>UniTask&lt;T&gt;</c>」这一形态：
    /// 热更侧编译出的 <c>async</c> 方法，其状态机类型是该侧独有的泛型实例，AOT 侧并不存在对应代码。
    /// 编辑器（Mono）下两者都能即时编译，因此这里的用例验证的是**跨程序集契约**；
    /// 真正的 AOT 泛型缺口只有在 IL2CPP + HybridCLR 构建后才暴露，验证步骤见
    /// <c>Docs/Design/UniTaskBridge.md</c>。
    /// </remarks>
    public static class HotUpdateLikeService
    {
        /// <summary>
        /// 同步完成的异步方法：不产生跨帧挂起，可在编辑态测试中直接断言。
        /// </summary>
        public static async UniTask<int> ComputeImmediateAsync(int input)
        {
            await UniTask.CompletedTask;
            return input * 2;
        }

        /// <summary>
        /// 跨帧挂起的异步方法：需要 PlayerLoop 推进，仅在播放态验证。
        /// </summary>
        public static async UniTask<int> ComputeDeferredAsync(int input)
        {
            await UniTask.Yield();
            return input * 2;
        }

        /// <summary>
        /// 立即失败的异步方法，用于验证异常能否穿过程序集边界原样抵达调用方。
        /// </summary>
        public static UniTask<int> FailAsync(string reason)
        {
            return UniTask.FromException<int>(new InvalidOperationException(reason));
        }

        /// <summary>
        /// 立即取消的异步方法，用于验证取消语义。
        /// </summary>
        public static UniTask<int> CancelAsync(CancellationToken cancellationToken)
        {
            return UniTask.FromCanceled<int>(cancellationToken);
        }

        /// <summary>
        /// 携带自定义负载的异步方法，用于验证泛型 <c>UniTask&lt;T&gt;</c> 在跨程序集时保持同一类型身份。
        /// </summary>
        public static UniTask<Payload> CreatePayloadAsync(Payload payload)
        {
            return UniTask.FromResult(payload);
        }
    }

    /// <summary>跨程序集往返所用的负载类型。</summary>
    public sealed class Payload
    {
        public Payload(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public string Describe()
        {
            return "payload:" + Value;
        }
    }
}