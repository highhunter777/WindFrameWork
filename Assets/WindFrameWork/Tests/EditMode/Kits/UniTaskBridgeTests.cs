using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using WindFrameWork.Kits.Tasks;
using WindFrameWork.Kits.UniTaskSamples;

namespace WindFrameWork.Tests.EditMode.Kits
{
    /// <summary>
    /// 桥接层与跨程序集契约：验证 UniTask 在本工程可用，且「模拟热更侧」产出的 <c>UniTask&lt;T&gt;</c>
    /// 能被框架侧正常消费。此处只覆盖同步完成的路径——编辑态的 NUnit 用例不推进帧，
    /// 跨帧挂起只能放 PlayMode（见 <c>UniTaskDeferredTests</c>）。
    /// <para>
    /// 取值一律经 <c>GetAwaiter().GetResult()</c>：<c>UniTask&lt;T&gt;</c> 本身不暴露
    /// <c>Result</c> / <c>Exception</c> 属性，只有 awaiter 才提供取值入口。
    /// 另，本工程的 Test Framework 版本不支持 <c>async Task</c> 测试方法。
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class UniTaskBridgeTests
    {
        [Test]
        public void Bridge_ReportsMainThreadIdentity()
        {
            Assert.That(UniTaskBridge.MainThreadId, Is.GreaterThan(0));
            Assert.That(UniTaskBridge.IsCurrentThreadMain, Is.True, "编辑态测试运行在主线程上。");
        }

        [Test]
        public void Bridge_Snapshot_IsQueryable()
        {
            UniTaskBridgeSnapshot snapshot = UniTaskBridge.CaptureSnapshot();

            Assert.That(snapshot.ActiveTaskCount, Is.GreaterThanOrEqualTo(0));
            Assert.That(snapshot.TrackingSupported, Is.True);
            Assert.That(snapshot.IsCurrentThreadMain, Is.True);
        }

        [Test]
        public void Samples_ImmediateTask_RoundTripsAcrossAssemblies()
        {
            UniTask<int> task = HotUpdateLikeService.ComputeImmediateAsync(21);

            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(task.GetAwaiter().GetResult(), Is.EqualTo(42));
        }

        [Test]
        public void Samples_FaultedTask_PropagatesExceptionUnchanged()
        {
            // 跨程序集不得吞异常或改写其类型：热更侧抛出的诊断信息必须原样抵达 AOT 侧。
            UniTask<int> task = HotUpdateLikeService.FailAsync("模拟热更侧故障");

            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Faulted));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => task.GetAwaiter().GetResult());
            Assert.That(exception.Message, Is.EqualTo("模拟热更侧故障"));
        }

        [Test]
        public void Samples_CanceledTask_ReportsCanceled()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();

                UniTask<int> task = HotUpdateLikeService.CancelAsync(cancellation.Token);

                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Canceled));
            }
        }

        [Test]
        public void Samples_GenericPayload_KeepsTypeIdentityAcrossAssemblies()
        {
            var payload = new Payload(7);

            UniTask<Payload> task = HotUpdateLikeService.CreatePayloadAsync(payload);
            Payload result = task.GetAwaiter().GetResult();

            Assert.That(result, Is.SameAs(payload), "泛型负载应原样传递，不得被复制或包装。");
            Assert.That(result.Describe(), Is.EqualTo("payload:7"));
        }
    }
}