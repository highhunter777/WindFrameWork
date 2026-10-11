# Unity Pipeline 包本地补丁（com.unity.pipeline 跑在 Unity 2022.3）

> 更新：**2026-10-11**（首次：2026-09-15）。目的：把 **Unity 官方 CLI（`unity`）** 的"连接运行中编辑器"能力接入本工程。
> 现状：**本工程已跑通** —— 包编译成功、Pipeline HTTP 服务在 **端口 7802** 运行、CLI 可执行 **151 条命令**（见 §3）。
> 历程：2026-09-15 先在 `E:\unityProject\Test` 工程首次跑通；2026-10-11 从 Unity Registry 重抓同版本源码落位本工程 `E:\unityProject\WindFrameWork`，按 §2 重放补丁后**一次性通过**（未出现新的 API 缺口）。
> ⚠️ 前提认知：上游包 **只支持 Unity 6000.0**（registry 现有 **8 个版本**，全部声明 `"unity": "6000.0"`）；本工程是 **2022.3.55f1**，因此下列补丁是**为 2022.3 定制的兼容层**。升级到 Unity 6 后应还原上游原包（补丁均带 `#if` 守门或可逆）。

## 0. 来源与落位

| 项 | 值 |
| --- | --- |
| 来源 | `https://packages.unity.com/com.unity.pipeline`（**直连可达，无需代理**；`packages.unity.cn` 无此包，返回 404） |
| 查询 | `Invoke-RestMethod https://packages.unity.com/com.unity.pipeline` → `versions` / `dist-tags` |
| 版本 | **0.7.0-exp.1**（与 Test 工程副本同版本，故 §2 补丁可直接重放）<br>⚠️ 2026-10-11 查得 registry `latest` 已为 **0.8.0-exp.1**；**未升级**，升级需重新核验 §2 的 8 处补丁是否仍够 |
| 包体 | `https://download.packages.unity.com/com.unity.pipeline/-/com.unity.pipeline-0.7.0-exp.1.tgz`（4,981,043 B） |
| 校验 | **sha1 `ab05e9cd7f74cc7e8d9dee579ca5a122192e9335`** —— 与 registry `dist.shasum` 一致（下载后必须比对；registry 未给 `dist.integrity`） |
| 抓取方式 | `Invoke-WebRequest` 下 tgz → `tar -xzf` → 得到顶层 `package/`（821 文件，**自带 .meta**） |
| 落位 | **`Packages/com.unity.pipeline/`**（embedded；打完补丁后 **822 文件**；**已加入 `.gitignore`**，见 §5） |

### 0.1 重抓的标准动作（可直接复制执行）

```powershell
$tar = Join-Path $env:TEMP 'com.unity.pipeline-0.7.0-exp.1.tgz'
Invoke-WebRequest 'https://download.packages.unity.com/com.unity.pipeline/-/com.unity.pipeline-0.7.0-exp.1.tgz' -OutFile $tar
(Get-FileHash $tar -Algorithm SHA1).Hash   # 应等于 registry dist.shasum
$src = Join-Path $env:TEMP 'pipeline_src'; tar -xzf $tar -C $src       # 解出 $src\package
Move-Item $src\package 'Packages\com.unity.pipeline'                   # 落位为 embedded 包
```
然后按 §2 重放补丁 → 删除改名后残留的 `Tests.meta` 与 Unity 重建的空 `Tests/` 目录 → Unity 自动刷新即编译。

## 1. 必需依赖（UPM 自动解析）

`com.unity.nuget.mono-cecil 1.11.6`（registry）、`com.unity.nuget.newtonsoft-json`（工程已有 3.2.1）、`com.unity.test-framework 1.1.33`（已有）、`com.unity.inputsystem`（已有 1.11.2）。

## 2. 补丁清单（7 处；重抓后按此重放）

| # | 文件 | 改动 | 原因 |
| --- | --- | --- | --- |
| 1 | `Runtime/Plugins/CodeAnalysis/*.dll.meta`（5 个）与 `Runtime/Analyzers/IlInterpreterAnalyzer.dll.meta` | **转为 2022.3 原生 v2 格式**（`serializedVersion: 2`）+ 平台对齐（**Editor ✓ / Standalone Win·Win64·Linux64·OSX ✓**） | 上游 meta 用 `serializedVersion: 3`（Unity 6 格式），**2022.3 误读为全平台禁用**（`GetCompatibleWithEditor()=False`）→ 插件 DLL 未生效 → `IlInterpreter` 找不到 `UnityPipeline.*` 命名空间。修法：`PluginImporter.SetCompatibleWithEditor(true)` + `SetCompatibleWithPlatform(Standalone…)` + `SaveAndReimport()` —— Unity 随之把 meta **重写为 v2 格式**（已核验 6/6 均为 `serializedVersion: 2`，Editor/Win64 兼容均为 ✓） |
| 2 | `Tests/` → **`Tests~`** | 重命名目录 | 包自带测试程序集引用 nunit.framework，2022.3 下解析失败；`~` 后缀被 Unity 忽略（源码保留） |
| 3 | **新增** `Editor/AnalyticInfoCompat_2022.cs` | `#if !UNITY_6000_0_OR_NEWER` 下提供 `UnityEngine.Analytics.IAnalytic`（含嵌套 `IData`）与 `AnalyticInfoAttribute` | 这两个类型是 Unity 6 新增，2022.3 编辑器内**不存在**（实测全程序集扫描为 0） |
| 4 | `Editor/PipelineAnalytics.cs` | `s_Send` 在 2022.3 下置空（`analytic => { }`） | 2022.3 的 `EditorAnalytics` 只有 `SendEventWithLimit` 系列，**没有 `SendAnalytic(IAnalytic)`** |
| 5 | `Editor/Console/EditorConsoleGroundTruth.cs` | `ConsoleWindowUtility.consoleLogsChanged` 订阅与 `GetConsoleLogCounts(...)` 在 2022.3 下 `#if` 排除（计数置零） | `ConsoleWindowUtility` 为 Unity 6 API |
| 6 | `Editor/Commands/Materials/MaterialCommands.cs` | `mat.rawRenderQueue` → 2022.3 用 `mat.renderQueue` | Unity 6 新增 `rawRenderQueue`（-1=继承）；2022.3 无 → **读回值退化为有效值**（写入侧 `renderQueue:-1` 仍可用） |
| 7 | `Editor/Commands/Assets/AssetCommands.cs` | 文件头加 `using PhysicsMaterial = UnityEngine.PhysicMaterial;`（`#if !UNITY_6000_0_OR_NEWER`） | Unity 6 更名 `PhysicMaterial` → `PhysicsMaterial` |
| 8 | `Runtime/Analyzers/IlInterpreterAnalyzer.dll` | **移入 `Runtime/Analyzers/Disabled2022~/`**（`~` 目录 Unity 不导入）并删除其 `.meta` | 该分析器 DLL 引用 `Microsoft.CodeAnalysis`/`System.Collections.Immutable`，2022.3 的 Mono **解析不了这些引用** → Unity 每次导入都抛 "Assembly ... will not be loaded due to errors"。**仅移除 RoslynAnalyzer 标签不够**——Unity 仍会把它当普通插件程序集做加载校验。藏出导入管线后错误永久消失（Unity 6 恢复包原样时移回即可） |

**副作用（可接受）**：分析遥测（#3/#4）与 console 计数对账（#5）在 2022.3 下不生效；材料 renderQueue 读回语义降级（#6）。**Pipeline 服务/命令能力不受影响。**

## 3. 验证记录

### 3.1 本工程 WindFrameWork（2026-10-11，当前基准）

| 检查 | 结果 |
| --- | --- |
| 重抓校验 | tgz sha1 `ab05e9cd…9335` == registry `dist.shasum` ✅ |
| 补丁一致性 | 重放后与 Test 工程已验证副本做**文件集合比对**：**822/822 完全一致**（无单侧多出/缺失） |
| 程序集产物 | `Unity.Pipeline.dll`(362KB) / `Unity.Pipeline.Editor.dll`(**688KB**) / `Unity.Pipeline.IlInterpreter.dll`(369KB) / `Attributes.dll`(6KB) / `CodeGen.dll`(18KB) —— **全部产出**（`Library/ScriptAssemblies/`，3:30:40–54） |
| 服务 | `Library/Pipeline/.unity-pipeline-port` → **port 7802 / pid 40008 / mode=editor / unityVersion 2022.3.55f1c1**；`unity pipeline list` → WindFrameWork 行 `Pipeline=true`、`Server Reachable=true` |
| CLI 往返 | `unity command recompile_status --project-path …` → `{"status":"idle","failed":false,"errors":[],"compilationFailed":false}` ✅ |
| 命令目录 | `unity list --project-path …` → **151 条命令** |
| 副作用清理 | 目录改名后 Unity 会依 `Tests.meta` 重建空 `Tests/` 目录并告警——**已删除残留 `Tests.meta` 与空 `Tests/`** |
| 分析器告警 | 日志中已**查不到** `IlInterpreterAnalyzer` 加载错误（补丁 #8 生效） |

### 3.2 首次验证 @ `E:\unityProject\Test`（2026-09-15）

| 检查 | 结果 |
| --- | --- |
| 程序集产物 | `Unity.Pipeline.dll`(354KB) / `Unity.Pipeline.Editor.dll`(**672KB**) / `IlInterpreter.dll`(361KB) / `Attributes.dll` / `CodeGen.dll` —— **全部产出** |
| 程序集加载 | 5 个 `Unity.Pipeline*` 全部载入 AppDomain，`PipelineServerStartup` 类型可达 |
| 服务 | `Library/Pipeline/.unity-pipeline-port` → **port 7800 / pid 215852 / mode=editor**；`unity status` → `7800 ready`；`unity pipeline list` → **Server Reachable=true** |
| CLI 往返 | `unity command console_status` → `success:true`（返回 `compilationFailed:false`、`counts{error:1,warn:8}`）；`unity command recompile_status` → `{"status":"idle","failed":false}` |
| 命令目录 | `unity list` → **151 条命令**（GameObject/组件/prefab/Animator/Timeline/build/tests/截图/console/audit/navmesh/batch…） |
| 控制台 | 编译 error **0** |

## 4. 使用方式与注意事项

```bash
# 前置：Unity CLI 已在 PATH（C:\Users\hunter\AppData\Local\Unity\bin\unity）
unity status                                              # 看已连接编辑器与端口
unity list    --project-path "E:\unityProject\WindFrameWork"        # 列可用命令（151 条）
unity command console_status   --project-path "E:\unityProject\WindFrameWork"
unity command recompile_status --project-path "E:\unityProject\WindFrameWork"
```

- ⚠️ **本机必须带 `--project-path`**：本机常同时跑两个装了 Pipeline 的编辑器（**Test 占 7801 / WindFrameWork 占 7802**，端口随可用槽位分配，实测也出现过 7800），不带参数会因多实例而报错
- ⚠️ **本机必须加 `--proxy-disable`**：否则 CLI 会走代理，**服务端點明明在监听（`curl` 直连返回 401 未授权）却报 `502 Bad Gateway`**，且 `unity status` 把端口判为 unreachable。现象极具迷惑性——看起来像 Pipeline 服务挂了，实际是出网环节的问题。排查方式：先用 `Invoke-WebRequest http://127.0.0.1:<端口>/api/status` 确认服务活着（返回 401 即为健康），再加 `--proxy-disable` 重试
- ✅ 推荐用法组合：`unity command <命令> --project-path "E:\unityProject\WindFrameWork" --proxy-disable`（跑测试加 `--mode editor --filter_type assembly --filter WindFrameWork.Tests.EditMode`）
- ⚠️ **多实例共用同一份 `Editor.log`**：`%LOCALAPPDATA%\Unity\Editor\Editor.log` 会被两个实例交错写入，读日志判编译结果时**必须按 Assets/Packages 路径归属区分**，否则会把另一个工程的编译错误算到本工程头上
- ⚠️ 服务由包的 `[InitializeOnLoad]`（`PipelineServerStartup`）**自动启动**；本工程落位后是 Unity 自动刷新即自动拉起的（未手工调用 `EnsureServerStarted()`），编辑器重启后同样自动就绪
- ⚠️ 描述符自带提示：编辑器**非自动化模式**启动时可能卡在模态对话框；无人值守场景建议用 `unity open` 启动编辑器
- ⚠️ 已知无害告警：`Runtime/Analyzers/IlInterpreterAnalyzer.dll` 加载失败（Roslyn 分析器，2022.3 下版本不匹配）——补丁 #8 已把该 DLL 藏到 `Disabled2022~/`，告警随之消失

## 5. git 策略

`Packages/com.unity.pipeline/` **不入库**（第三方包 + 本地补丁，与 `Packages/MCPForUnity/` 同策略）；**本文档入库**——重抓包后按 §2 重放补丁即可复原。

- 忽略规则位于本工程 `.gitignore` 末尾（`# Unity Pipeline：…` 段）：`/Packages/com.unity.pipeline/`
- 校验方式：`git check-ignore -v Packages/com.unity.pipeline/package.json` 应命中上述规则
- ⚠️ 依赖 `com.unity.nuget.mono-cecil 1.11.6` 由 UPM 从 registry 自动解析并写入 `Packages/packages-lock.json`；**embedded 包本身无需写进 `Packages/manifest.json`**（`Packages/` 下子目录自动识别为 embedded 包）
