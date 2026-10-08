# WindFrameWork

> 生产级通用「Unity 客户端 + .NET 服务端」开发框架 —— 追求极致的开发效率、扩展性与通用性。

WindFrameWork 是一套面向生产环境的通用「客户端 + 服务端」开发框架：客户端基于 Unity，服务端基于 .NET，两端通过纯 .NET 共享层（`Shared`）共用协议与确定性内核。它不绑定任何特定玩法或项目类型，而是把开发中最稳定、最高频的基础能力沉淀为可复用的核心模块，让开发者把精力集中在业务本身；同时以统一的工程规范与配套的测试工程、性能工程，保障框架在真实项目中的质量、性能与长期演进能力。

## 设计目标

| 目标 | 说明 |
| --- | --- |
| 极致开发效率 | 开箱即用的模块化能力、统一的命名与目录规范、双端共享的协议与数据结构，减少样板代码与重复实现 |
| 高扩展性 | 遵循开闭原则（对修改关闭、对扩展开放），模块可独立替换、组合与裁剪，两端共用同一套分层与模块规范 |
| 通用性 | 不依赖具体业务与玩法，客户端与服务端可独立接入，也支持一体化使用，适用于不同类型、不同规模的项目 |
| 生产级标准 | 按上线标准实现：单一职责、禁止硬编码、注重稳定性与可维护性 |

## 环境要求

- Unity **2022.3.55 LTS**（推荐使用 Unity Hub 安装对应版本）
- 渲染管线：Universal Render Pipeline（URP 14）
- 输入：Unity Input System
- 其他核心依赖：TextMeshPro、Cinemachine、Timeline、Unity Test Framework
- 热更新：HybridCLR（C# 程序集热更）、XLua（Lua 热修）
- 服务端：.NET 8（`Server/` 解决方案，独立于 Unity 工程）

## 工程结构

工程采用「客户端 + 共享层 + 服务端」三段式：Unity 工程位于仓库根，`Shared/` 与 `Server/` 为平级目录（Unity 不导入 Assets 之外的目录）。依赖方向：客户端 `App → Modules → Kits / Core → Shared`，服务端 `Server.* → Shared`；客户端与服务端互相不可见，唯一交集是 `Shared`。

```
WindFrameWork/                            # 仓库根（同时是 Unity 工程）
├── Assets/                               # 客户端
├── Shared/                               # 纯 .NET 共享层：协议、确定性内核、双端共享数据
├── Server/                               # .NET 服务端解决方案
├── HybridCLRData/                        # HybridCLR 构建缓存（生成物，不进版本管理）
├── Packages/                             # UPM 依赖清单（含 Shared 本地包引用）
├── ProjectSettings/                      # 工程配置
└── .AGENTS/                              # 代码 / 文档 / 资源 / 架构规范
```

### 客户端（Assets/）

```
Assets/
├── WindFrameWork/                        # 框架主体（全部 AOT，稳定基座）
│   ├── Core/                             #   内核：Container（容器契约 + 定位器实现）、ModuleSystem、Bootstrap、5 个基础系统
│   ├── Kits/                             #   工具集：通用、近无状态（Logging、Timer、Serialization…）
│   ├── Modules/                          #   功能模块：自包含、可替换、可裁剪
│   │   ├── HotUpdate/                    #     热更基础设施：检查 / 下载 / 校验、HybridCLR 程序集加载
│   │   ├── Lua/                          #     XLua 运行时：VM、脚本加载、热修注入
│   │   └── Resource/  Audio/  UI/  SceneFlow/  Network/  …
│   ├── Tests/                            #   跨模块测试与性能基准
│   └── Editor/                           #   框架工具与热更构建管线
│
├── App/                                  # 宿主（示例游戏；真实项目 = 游戏工程）
│   ├── AOT/                              #   不可热更：Launcher、Boot 场景、补充元数据引用
│   ├── HotUpdate/                        #   可热更程序集：Bootstrap（组合根）、Modules、Simulation
│   ├── Scenes/  Content/                 #   业务资源（随热更包分发）
│   └── Lua/                              #   XLua 脚本：Entry、Hotfix、Modules
│
├── XLua/                                 # 第三方：Lua 热修运行时
├── Plugins/                              # 第三方：原生插件
└── Settings/                             # URP 等全局工程配置
```

### 共享层（Shared/）

一个目录、两种身份：对 Unity 是本地 UPM 包，对服务端是 csproj——一份代码，双端共享（纯 .NET，零 UnityEngine 依赖）。

```
Shared/
├── package.json / *.asmdef / *.csproj    # 双身份三件套
├── ECS/                                  # 确定性内核：World / Entity / Query / System / 存储 / 确定性随机
├── Protocol/                             # 协议基座：帧、消息头、编解码契约、错误码、版本协商
└── Game/                                 # 示例宿主共享：消息、组件、规则（真实项目 = 游戏侧共享包）
```

### 服务端（Server/）

```
Server/
├── WindFrameWork.Server.sln
├── src/
│   ├── Host/                             # 宿主入口：Kestrel / 控制台 + DI 装配 + 部署
│   ├── Net/                              # 连接与协议栈：会话、编解码、心跳、重连
│   ├── Game/                             # 玩法权威：房间 / 对局推进（引用 Shared/ECS）
│   └── Persistence/                      # 存储适配：DB / 缓存
└── tests/                                # Shared 契约测试（xUnit）与玩法测试
```

### 容器与注入

容器访问只经两个契约：`IServiceRegistry`（注册，仅组合根与模块注册阶段可见）与 `IServiceResolver`（解析）。默认实现为服务定位器（Service Locator），可整体替换为 DI 容器——适配器按变体约定独立成程序集，`Core` 本体保持零依赖。

```
Core/Container/                          # 容器契约 + 默认实现（零依赖，同属 Core 程序集）
├── IServiceRegistry.cs                  #   写侧：注册
├── IServiceResolver.cs                  #   读侧：解析
├── ServiceLifetime.cs                   #   Singleton / Scoped / Transient
├── ServiceScope.cs                      #   作用域：全局 / 场景 / 关卡
└── ServiceLocator.cs                    #   默认实现（可被替换）
```

将来接入 DI 容器时，适配器作为变体独立成程序集（如 `Core/Container.VContainer/`，引用 Core + 第三方容器）；第三方自行桥接则放宿主侧 `App/HotUpdate/Modules/`。

`ServiceLocator` 是普通类而非静态单例：静态全局状态不可测试，且作用域需要持有各自的实例状态，二者都要求以实例形式存在。`ServiceKey` 在服务类型之外携带一个可选名称，使同一契约的多个平台变体（见下方骨架的 `Runtime/Unity/`、`Runtime/Steam/`）得以并存。

`Core/Container` 与 `Core/ModuleSystem` 的源码按约定零 `UnityEngine` 引用，虽与其余 Core 子系统同属一个程序集，但这一约束使日后将容器整体抽离（若出现第二个使用方）无需拆分程序集。设计细节见 [Docs/Design/Container.md](Docs/Design/Container.md)。

### 模块标准骨架

功能模块自包含，其余模块与 Audio 同构：

```
Modules/Audio/
├── Contracts/                            # 唯一对外依赖面：接口按能力细分，对外事件在 Contracts/Events/
├── Runtime/                              # 实现：XxxModule 自注册，平台变体按目录分组（Unity/、Steam/…）
├── Testing/                              # Fake 实现：消费方的测试替换点
├── Content/                              # 模块资源与代码同目录
├── Tests/                                # 模块自测（EditMode / PlayMode）
└── Editor/                               # 模块编辑器扩展（可选）
```

> 注意：模块资源目录不可命名为 `Resources`（Unity 特殊目录，会全量进包且只能按字符串加载）。

> 说明：目录骨架已按此结构建立；各模块代码随建设逐步落地，进度见下方「核心模块」表。

## 核心模块

| 模块 | 目录 | 说明 | 状态 |
| --- | --- | --- | --- |
| 容器与模块系统 | `Core/Container`、`Core/ModuleSystem`、`Core/Bootstrap` | 读写分离的容器契约（默认定位器实现，可替换为 DI 容器）、模块生命周期与启动编排，由装配清单驱动、可裁剪 | 🚧 搭建中 |
| 命令系统 | `Core/CommandSystem` | 以命令为单元封装操作，支持命令的统一调度、撤销 / 重做等能力 | 🚧 搭建中 |
| 事件中心 | `Core/EventCenter` | 全局事件分发中枢，实现模块间松耦合通信 | 🚧 搭建中 |
| 有限状态机 | `Core/FiniteStateMachine` | 通用状态机，支撑角色 AI、流程控制等状态驱动场景 | 🚧 搭建中 |
| 对象池 | `Core/ObjectPool` | `UnityEngine.Object` 级别的对象复用，降低频繁实例化 / 销毁的开销 | ✅ 已完成 |
| 引用池 | `Core/ReferencePool` | 纯 C# 类对象的引用复用，减少 GC 分配 | 🚧 搭建中 |
| ECS 运行时 | `Shared/ECS` | 可选机制：World / Entity / Query / System、存储策略与确定性随机 | 📋 规划中 |
| 热更通道 | `Modules/HotUpdate`、`Modules/Lua` | HybridCLR 程序集热更与 XLua 热修双通道 | 📋 规划中 |
| 工具集 | `Kits` | 通用工具与编辑器扩展，持续沉淀 | 📋 规划中 |

> 状态说明：✅ 已完成 = 代码与测试已落地；🚧 搭建中 = 目录与设计已就绪，代码开发中；📋 规划中 = 已列入路线图。模块完成后请同步更新本表。

## 工程化配套

框架按生产级标准建设，配套两大专项工程：

- **测试工程**：客户端基于 Unity Test Framework，覆盖 EditMode 单元测试与 PlayMode 集成测试；共享层与服务端基于 xUnit（`dotnet test`）建立契约测试，核心模块与双端接口以测试驱动保障行为正确与重构安全。
- **性能工程**：建立性能基准与量化指标（GC 分配、耗时、内存等），通过基准测试与 Profiler 实测持续监控框架的性能表现，防止性能退化。

客户端测试工程位置与运行方式：

| 套件 | 目录 | 程序集 | 运行方式 |
| --- | --- | --- | --- |
| EditMode 单元测试 | `Assets/WindFrameWork/Tests/EditMode/` | `WindFrameWork.Tests.EditMode` | `Window > General > Test Runner > EditMode` |
| PlayMode 集成测试 | `Assets/WindFrameWork/Tests/PlayMode/` | `WindFrameWork.Tests.PlayMode` | `Window > General > Test Runner > PlayMode` |

两个测试程序集均以 `UNITY_INCLUDE_TESTS` 为编译约束，因此在正式构建中不会被编入。性能基准目前以 `GC.GetAllocatedBytesForCurrentThread()` 的零分配断言形式落在 `Tests/PlayMode/ObjectPool/`（标记 `Performance` 分类）；待引入 `com.unity.test-framework.performance` 后迁至 `Tests/Benchmarks/`。

> 服务端与共享层的 xUnit 测试工程随 `Shared` / `Server` 落地时补充。

### 对象池

`Core/ObjectPool` 提供 `UnityEngine.Object` 级别的对象复用。要点：

```csharp
// 配置集中承载全部可调项，无散落的魔法数字
var config = new ObjectPoolConfig("Bullet", prewarmCount: 16, maxIdleSize: 64, maxLiveSize: 256);
var pool = new ObjectPool<GameObject>(() => Instantiate(bulletPrefab), config, poolRoot);

// 租出得到租约凭证，归还必须交回同一凭证
PooledHandle<GameObject> handle = pool.Rent();
handle.Object.transform.position = muzzle.position;
pool.Return(handle);
```

- **必须归还凭证而非对象**：归还时凭槽位下标与代数做 O(1) 校验，无需任何哈希表。重复归还、陈旧凭证与跨池凭证一律抛异常——这类错误会静默破坏空闲链表并引发远更难排查的故障。此行为与 `UnityEngine.Pool` 的 `collectionCheck` 不同（后者默认关闭且为 O(n)），属有意选择的快速失败。
- **状态重写由业务实现**：池不修改位置与旋转。需要重置的池化组件实现 `IPoolable`，在 `OnRentFromPool` 中还原自身状态。
- **三个值得关注的统计量**：`HitRate`（命中率，过低说明预热不足）、`PeakLiveCount`（并发峰值，用于 sizing）、`DiscardedDestroyedCount`（生产环境非零意味着池外代码在直接销毁池内对象，属需追查的缺陷）。
- **仅主线程可用**：池内 Unity 对象操作要求主线程，故全链路不加锁，只在开发构建下断言线程归属。
- **并发超限抛异常而非返回 null**：池耗尽意味着调用方漏归还，属应立即暴露的缺陷。

设计细节见 [Docs/Design/ObjectPool.md](Docs/Design/ObjectPool.md)。

## 快速开始

**客户端**

1. 使用 Unity Hub 打开本工程（版本 2022.3.55f1c1），等待依赖解析与编译完成；
2. 打开示例场景 `Assets/Scenes/SampleScene.unity`；
3. 运行测试：菜单 `Window > General > Test Runner`，选择 EditMode / PlayMode 执行。

**服务端**（`Server/` 解决方案落地后启用）

1. 构建：`dotnet build Server/WindFrameWork.Server.sln`；
2. 运行测试：`dotnet test Server/WindFrameWork.Server.sln`；
3. 运行宿主：`dotnet run --project Server/src/Host`。

## 开发规范

完整规范见 [.AGENTS/AGENTS.md](.AGENTS/AGENTS.md)，要点如下：

- 代码文件与类定义遵循单一职责原则，目录结构清晰、职责明确；
- 遵循开闭原则：对修改关闭，对扩展开放；
- 禁止硬编码；除 ECS 架构外遵循面向对象设计规范；
- 业务模块的代码文件与相关美术资源放在同一模块目录内统一管理；
- 统一命名规范，减少重复实现；
- 注释仅描述当前设计与实现，不记录无关内容；
- 分层与依赖由 asmdef / csproj 强制：客户端 `App → Modules → Kits / Core → Shared`、服务端 `Server.* → Shared`，客户端与服务端互相不可见；
- 容器访问只经 `IServiceRegistry` / `IServiceResolver`，实现可替换（DI 等变体独立成程序集，不并入 Core）；
- 热更边界：框架全部 AOT，热更仅覆盖热更侧程序集与 Lua 脚本，AOT 不得引用热更侧。

### 文档规范

- 设计文档与施工进度记录分离；
- 设计文档仅保留最新设计，进度记录仅保留最新进度；
- 代码完成后，同步回写对应文档（含本 README 的模块状态表）。

## 版本

当前版本：**0.1.0**（框架骨架搭建阶段）
