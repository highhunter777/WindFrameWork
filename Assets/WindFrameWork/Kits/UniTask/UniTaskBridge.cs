using System;
using Cysharp.Threading.Tasks;

namespace WindFrameWork.Kits.Tasks
{
    /// <summary>
    /// 运行时快照：把 UniTask 的运行事实收敛为框架可查询的形式。
    /// </summary>
    /// <remarks>
    /// 诊断用途，不应放在每帧路径上。
    /// </remarks>
    public readonly struct UniTaskBridgeSnapshot
    {
        internal UniTaskBridgeSnapshot(int mainThreadId, bool isCurrentThreadMain, int activeTaskCount, bool trackingSupported)
        {
            MainThreadId = mainThreadId;
            IsCurrentThreadMain = isCurrentThreadMain;
            ActiveTaskCount = activeTaskCount;
            TrackingSupported = trackingSupported;
        }

        /// <summary>UniTask 认定的主线程标识。</summary>
        public int MainThreadId { get; }

        /// <summary>调用方当前是否位于主线程。</summary>
        public bool IsCurrentThreadMain { get; }

        /// <summary>
        /// 当前被跟踪的未完成任务数。
        /// </summary>
        /// <remarks>
        /// 跟踪由 UniTask 的 <c>TaskTracker</c> 提供，仅在编辑器内且开启跟踪选项后有数据；
        /// 其余环境恒为 0，属预期而非「没有泄漏」。
        /// </remarks>
        public int ActiveTaskCount { get; }

        /// <summary>当前构建是否支持任务跟踪。</summary>
        public bool TrackingSupported { get; }
    }

    /// <summary>
    /// UniTask 桥接：把第三方异步运行时的状态纳入框架的可观测范围。
    /// </summary>
    /// <remarks>
    /// 本类刻意不做生命周期干预。UniTask 已通过
    /// <c>[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]</c> 自行注入 PlayerLoop，
    /// 框架再插一手只会带来「谁负责初始化」的第二种答案；域重载与播放态切换由 UniTask 自行处理。
    /// <para>
    /// 命名空间取 <c>WindFrameWork.Kits.Tasks</c> 而非 <c>…Kits.UniTask</c>：后者会让裸记号
    /// <c>UniTask</c> 在本程序集内解析到命名空间而非 <c>Cysharp.Threading.Tasks.UniTask</c> 类型，
    /// 这是与 <c>Core/ObjectPool</c> 同源的「命名空间与类型同名」陷阱。
    /// </para>
    /// </remarks>
    public static class UniTaskBridge
    {
        /// <summary>UniTask 认定的主线程标识。</summary>
        public static int MainThreadId => PlayerLoopHelper.MainThreadId;

        /// <summary>当前是否位于主线程。</summary>
        /// <remarks>
        /// UniTask 的 awaiter 依赖 PlayerLoop 推进：非主线程上启动的操作必须自行切回主线程完成续体，
        /// 否则续体永不触发。此处提供判定而不代为切换——切换策略属于业务。
        /// </remarks>
        public static bool IsCurrentThreadMain => PlayerLoopHelper.IsMainThread;

        /// <summary>
        /// 取运行快照。
        /// </summary>
        public static UniTaskBridgeSnapshot CaptureSnapshot()
        {
            int activeTaskCount = 0;

            TaskTracker.ForEachActiveTask((_, __, ___, ____, _____) => activeTaskCount++);

            return new UniTaskBridgeSnapshot(
                PlayerLoopHelper.MainThreadId,
                PlayerLoopHelper.IsMainThread,
                activeTaskCount,
                trackingSupported: true);
        }
    }
}