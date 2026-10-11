using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using WindFrameWork.Kits.UniTaskSamples;

namespace WindFrameWork.Tests.PlayMode.Kits
{
    /// <summary>
    /// 跨帧路径：UniTask 的续体依赖 PlayerLoop 推进，只能在播放态验证。
    /// </summary>
    /// <remarks>
    /// 置于播放态而非编辑态，因为编辑态的 NUnit 用例不会推进帧，跨帧 await 将永久挂起。
    /// </remarks>
    [TestFixture]
    public sealed class UniTaskDeferredTests
    {
        [UnityTest]
        public IEnumerator DeferredTask_CompletesOnNextFrame()
        {
            UniTask<int> task = HotUpdateLikeService.ComputeDeferredAsync(21);

            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending), "未推进帧前应处于挂起状态。");

            yield return null;

            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(task.GetAwaiter().GetResult(), Is.EqualTo(42));
        }

        [UnityTest]
        public IEnumerator DeferredTask_ResumesEachLoop()
        {
            var counter = 0;

            async UniTask RunAsync()
            {
                for (int index = 0; index < 3; index++)
                {
                    await UniTask.Yield();
                    counter++;
                }
            }

            UniTask run = RunAsync();

            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            Assert.That(run.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(counter, Is.EqualTo(3), "每次 Yield 应恰好推进一次续体。");
        }

        [UnityTest]
        public IEnumerator DeferredTask_CanDriveTestCoroutine()
        {
            // ToCoroutine 是 PlayMode 下把 UniTask 接回 NUnit 协程机的桥：
            // 它让断言能等到任务真正完成，而不必靠手写轮询帧数。
            UniTask<int> task = HotUpdateLikeService.ComputeDeferredAsync(10);
            int observed = 0;

            yield return task.ToCoroutine(
                result => observed = result,
                exception => Assert.Fail(exception.ToString()));

            Assert.That(observed, Is.EqualTo(20));
        }
    }
}