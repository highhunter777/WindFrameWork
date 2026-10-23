# UniTask 桥接

`Kits/UniTask`，程序集 `WindFrameWork.Kits.UniTask`，命名空间 `WindFrameWork.Kits.Tasks`。本文仅描述当前最新设计。

## 目标与边界

把第三方异步运行时 UniTask 纳入框架的依赖图，并让它与热更通道的形态对齐。**不做**的事同样重要：不替换框架的异步契约、不把 `UniTask` 泄漏进 `Core`、不代为管理 UniTask 的生命周期。

依赖方向遵守既有分层：`App → Modules → Kits / Core → Shared`。UniTask 属工具集，故落在 `Kits`；`WindFrameWork.Core` 的 `references` 保持为空，不因某个子系统将来要用异步而让内核绑定第三方运行时。

## 版本固定与离线可复现

依赖以 Git URL 引入：

```json
"com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask"
```

**实际生效的版本固定来自 `Packages/packages-lock.json` 中的 `hash`（commit 级），而不是 manifest 里的 tag。** 当前锁定 `a77a1b4a89824fb6df293099de3060daf2a432c1`（= 2.5.11）。commit 锁定比 tag 更严格——tag 可被移动，commit 不可。

### 否决 `#2.5.11` tag 的实测理由

曾把 URL 改为 `...#2.5.11`，结果比不加 tag **更危险**：

1. UPM 判定依赖已变更，**先删除 `Library/PackageCache/com.cysharp.unitask@a77a1b4a89/`**；
2. 随即尝试按 tag 重新解析，而本机对 `github.com:443` 直连不通（`git ls-remote` 超时约 21s）；
3. 新包未下载回来，工程直接进入编译失败：`error CS2001: Source file '.../Runtime/Internal/Error.cs' could not be found`，波及所有引用该程序集的脚本。

即：**加 tag 把「版本漂移」这个慢性问题，换成了「删缓存 + 下载失败」的急性故障。** 结论是在网络可达性没有保障前，Git 依赖一律走 lock 的 commit 锁定。

### 缓存与离线还原

`Library/` 已被 `.gitignore` 排除，因此 `Library/PackageCache` 天然不入库；但它也是**唯一可用副本**——一旦被 UPM 删除而远端不可达，只能靠它或备份恢复。两个脚本把这件事固化下来（遵循「PowerShell 脚本统一使用 pwsh」，脚本入库，产物不入库）：

| 脚本 | 作用 |
| --- | --- |
| `scripts/packages/export-package-cache.ps1` | 把 `packages-lock.json` 中全部 `source: git` 的包从 `Library/PackageCache` 导出到 `.cache/packages`，并生成含 commit 标记的 `cache-manifest.json` |
| `scripts/packages/restore-package-cache.ps1` | 校验 commit 标记与 lock 的 hash 一致后，还原回 `Library/PackageCache` |

本工程的三个 Git 依赖（`hybridclr`、`luban`、`unitask`）均在 `.cache/packages` 中留有副本。**还原后还需让 UPM 重新链接**，否则 AssetDatabase 仍视包为不存在——用 `unity command package_resolve`（需配 `--proxy-disable`）重链，UniTask 的 6 个程序集才会重新产出。

`.cache/` 已加入 `.gitignore`。

## 程序集划分与命名空间陷阱

| 程序集 | 命名空间 | 职责 |
| --- | --- | --- |
| `WindFrameWork.Kits.UniTask` | `WindFrameWork.Kits.Tasks` | 桥接层 |
| `WindFrameWork.Kits.UniTask.Samples` | `WindFrameWork.Kits.UniTaskSamples` | 扮演「热更侧」，只引用 UniTask，不引用框架程序集 |

**命名空间刻意不等于程序集名**：若命名空间取 `WindFrameWork.Kits.UniTask`，则裸记号 `UniTask` 在本程序集内会解析到命名空间而非 `Cysharp.Threading.Tasks.UniTask` 类型。这与 `Core/ObjectPool` 记录在案的「命名空间与类型同名邻接」是同一类陷阱（参见 [ObjectPool.md](ObjectPool.md) 末节）。

## 桥接层做了什么、刻意不做什么

**做了**：把 UniTask 的运行事实暴露为可查询快照——主线程标识、当前是否在主线程、未完成任务计数（`UniTaskBridge.CaptureSnapshot`）。

**不做生命周期干预**：UniTask 自带 `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` 注入 PlayerLoop（`PlayerLoopHelper.Init`），域重载与播放态切换也自行处理。框架再插一手只会制造「谁负责初始化」的第二种答案。**桥接层因此不含任何 `Initialize` 方法**——这是有意的设计缺口，不是遗漏。

同理，2.5.11 **没有** `PooledUniTask` 与 `IObjectPool<T>`（全包零命中），因此不存在「把 UniTask 的池接到框架池上」这条桥接路径；该版本唯一的池是内部 `ArrayPool<T>`（固定容量数组），与 `IReferencePool<T>` 不同构。

## 跨程序集契约验证

`Samples` 程序集扮演将来的热更侧，提供三类方法：同步完成（`await UniTask.CompletedTask`）、跨帧挂起（`await UniTask.Yield()`）、以及失败 / 取消 / 泛型负载。

EditMode 6 项已全部通过（同步完成路径）；PlayMode 3 项覆盖跨帧续体与 `ToCoroutine` 桥，待在 Test Runner 中手动执行（当前 Pipeline 命令行对 PlayMode 返回空结果集，见 §已知限制）。

取值一律经 `GetAwaiter().GetResult()`：`UniTask<T>` 本身不暴露 `Result` / `Exception` 属性，只有 awaiter 提供取值入口。

## IL2CPP + HybridCLR 验证清单（尚未执行）

编辑器（Mono）下两侧都能即时编译，**AOT 泛型缺口在编辑器里不可见**。热更侧的 `async` 方法会生成该侧独有的状态机泛型类型，AOT dll 中不存在对应实例——这才是 UniTask 接入热更通道时唯一的真实风险，而 UniTask 2.5.11 **不附带 `link.xml`**，裁剪后的行为必须实测。

上线前需逐项确认（当前均未验证）：

1. 开启 IL2CPP + 裁剪，构建含 `Samples` 的程序集，确认 `AsyncUniTaskMethodBuilder<T>` 相关泛型未被裁掉，必要时自备 `link.xml`；
2. 把 `Samples` 转为 HybridCLR 热更程序集，验证热更侧 `async UniTask<T>` 能被 AOT 侧 await；
3. 验证 `UniTask.Yield` / `NextFrame` 在热更侧跨域后的 PlayerLoop 续体仍能推进；
4. 确认 `TaskTracker` 在 Development Build 下无副作用（跟踪数据仅编辑器内有效，发布构建恒为 0，属预期）。

## 已知限制（如实记录）

- **Pipeline 命令行跑不了 PlayMode**：`run_tests --mode playmode` 返回 `Total=0`，本工程的 PlayMode 验证需在 Test Runner 中手动执行。
- **测试框架不支持 `async Task` 测试方法**（UTF 1.1.33 所用 NUnit 版本）：失败信息为 `Method has non-void return value, but no result is expected`。EditMode 的异步断言只能同步取值或放 PlayMode。
- **`ActiveTaskCount` 需手动开启**：数据来自 `TaskTracker`，仅在编辑器内且开启跟踪选项后有值，其余环境恒为 0。
- **桥接层目前只提供诊断**：真正的消费方（资源加载、模块初始化的 UniTask 实现变体）尚未出现，因此没有「框架契约 + UniTask 实现」这层抽象。按既有纪律，投机性抽象不提前落地。

## 后续接入建议

待 `Modules` 中出现需要异步的模块（如资源加载、启动编排）时，正确形态是：**模块声明自有契约（如 `IAsyncLoader`），UniTask 版实现作为变体独立成程序集**——与 DI 容器「实现变体独立成程序集、不并入 Core 本体」的处理一致。届时本桥接层扩展为该变体的公共设施，而不是让模块直接依赖 UniTask。