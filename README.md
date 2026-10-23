# WindFrameWork

> 生产级通用「Unity 客户端 + .NET 服务端」开发框架 —— 追求极致的开发效率、扩展性与通用性。

WindFrameWork 把开发中最稳定、最高频的基础能力沉淀为可复用的核心模块：客户端基于 Unity，服务端基于 .NET，两端通过纯 .NET 共享层（`Shared`）共用协议与确定性内核。它不绑定任何特定玩法或项目类型，以统一的工程规范与配套的测试工程、性能工程，保障框架在真实项目中的质量、性能与长期演进能力。

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

「客户端 + 共享层 + 服务端」三段式，依赖方向由 asmdef / csproj 强制；目录详情、模块骨架与容器契约见 [架构说明](Docs/项目说明/架构说明.md)。

```
WindFrameWork/
├── Assets/            # 客户端（Unity 工程）：WindFrameWork 框架主体 + App 宿主
├── Shared/            # 纯 .NET 共享层：协议、确定性内核、双端共享数据（本地 UPM 包 + csproj 双身份）
├── Server/            # .NET 服务端解决方案
├── Docs/              # 文档：设计文档 / 施工记录 / 技术总结 / 项目说明
├── Packages/          # UPM 依赖清单（含 Shared 本地包引用）
└── .AGENTS/           # 代码 / 架构 / 文档 / 工具 / 资源规范
```

## 当前进度

版本 **0.1.0**，骨架搭建阶段：对象池、引用池已完成；容器与模块系统、命令系统、事件中心、有限状态机、异步运行时（UniTask 桥接）搭建中；ECS、热更通道、UI 模块、调试平台、开发工具平台、工具集规划中。

最新进度表见 [Docs/施工记录/进度.md](Docs/施工记录/进度.md)（唯一进度记录，代码完成后同步回写）。

## 快速开始

**客户端**

1. 使用 Unity Hub 打开本工程（版本 2022.3.55f1c1），等待依赖解析与编译完成；
2. 打开示例场景 `Assets/Scenes/SampleScene.unity`；
3. 运行测试：菜单 `Window > General > Test Runner`，选择 EditMode / PlayMode 执行。

**服务端**（`Server/` 解决方案落地后启用）

1. 构建：`dotnet build Server/WindFrameWork.Server.sln`；
2. 运行测试：`dotnet test Server/WindFrameWork.Server.sln`；
3. 运行宿主：`dotnet run --project Server/src/Host`。

## 文档索引

| 文档 | 职责 |
| --- | --- |
| [.AGENTS/AGENTS.md](.AGENTS/AGENTS.md) | 代码 / 架构 / 文档 / 工具 / 资源规范（唯一规范来源） |
| [Docs/项目说明/架构说明.md](Docs/项目说明/架构说明.md) | 工程结构详情、分层与依赖、模块标准骨架、容器与注入 |
| [Docs/项目说明/工程化.md](Docs/项目说明/工程化.md) | 测试工程（EditMode / PlayMode / xUnit）与性能工程 |
| [Docs/施工记录/进度.md](Docs/施工记录/进度.md) | 核心模块状态表（仅保留最新进度） |
| [Docs/技术总结/核心模块用法.md](Docs/技术总结/核心模块用法.md) | 对象池、引用池、异步运行时的用法与行为要点 |
| [Docs/设计文档/](Docs/设计文档) | Container / ObjectPool / ReferencePool / UniTaskBridge / 调试平台 / 开发工具平台 / UI 模块 / UI 开发工具 设计细节 |
| [Docs/UNITY-GUIDE.md](Docs/UNITY-GUIDE.md) | Unity 操作规范：工具选择、操作边界与验证要求 |
| [Docs/UnityPipeline本地补丁.md](Docs/UnityPipeline本地补丁.md) | unity-pipeline 包的本地补丁说明 |
