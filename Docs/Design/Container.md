# 容器与模块契约设计

`Core/Container`、`Core/ModuleSystem`，命名空间 `WindFrameWork.Core.Container` 与 `WindFrameWork.Core.ModuleSystem`。本文仅描述当前最新设计。

## 读写分离

容器访问只经两个契约，二者互不继承：

- `IServiceRegistry`（写侧）仅暴露注册与注销，不含任何解析能力。
- `IServiceResolver`（读侧）仅暴露解析，不含任何注册能力。

不继承使读写分离在**类型系统层面**强制：持有注册契约的代码无法解析服务。`ServiceLocatorRegistrationTests` 中以反射锁定该约束（断言两个接口均不含对方的成员前缀），使规则防回归。

## 服务键

`ServiceKey` 由服务类型与可选名称组成。名称支持同一契约的多个实现并存——这正是模块骨架中 `Runtime/` 按平台分组（`Unity/`、`Steam/`）所要求的：仅按类型索引会使后注册者静默覆盖先注册者，与已发布的模块结构直接矛盾。因此该结构自第一天起即必要，而非过度设计。

## 生命周期与作用域

`ServiceLifetime` 三态：`Singleton` 全局唯一；`Scoped` 作用域内唯一；`Transient` 每次解析新建且不被持有。

`ServiceScope` 为作用域内 `Scoped` 实例的持有者与释放者。作用域解析时先查自身持有的作用域实例，未命中则按生命周期分派：`Scoped` 在本作用域创建，`Singleton` 与 `Transient` 回退到根定位器。`ServiceScopeKind`（`Global` / `Scene` / `Level`）本轮仅作为被记录的元数据——在何处开启作用域属于 `Core/Bootstrap` 的职责，尚未落地；先固化契约可避免日后改动已发布的 API。

作用域释放时释放其持有的全部作用域实例，并从定位器注销以免长期持有失效引用。

## 异常类型的选择

- **注册期误用**（重复键、实例与 `Transient` 矛盾）使用 `InvalidOperationException`：注册只发生在组合根与模块注册阶段，该处错误是立即可见的程序错误，用标准异常即可，不值得为其单设类型。
- **解析期未找到**使用自定义 `ServiceResolutionException`：解析失败可能在场景流转中浮现，需要一个可捕获且自描述（含 `Key`）的类型。

## 为何 `ServiceLocator` 是类而非静态单例

静态全局状态不可测试，且作用域需要持有各自的实例状态——二者都要求以实例形式存在。因此不提供 `Current` 属性。模块测试可直接 `new ServiceLocator()`、注册替身、释放。

## 解析的合法位置

| 位置 | 是否允许 | 机制 |
| --- | --- | --- |
| 组合根 | 允许 | 唯一可见全部 `Runtime` 并调用解析的位置 |
| 模块 `Register(IServiceRegistry)` | 允许（仅写） | 该契约不含解析能力 |
| 表现层边界类型 | 允许 | 注入 `IServiceResolver` 或直接注入契约 |
| 领域类、`Shared/`、`Server/` | 不允许 | 仅构造注入 |

`IModule` 因此只接受 `IServiceRegistry`。模块的 `Initialize()` **不接收** `IServiceResolver`——否则就在错误分层引入解析点；跨模块 `Contracts` 由组合根构造注入。

## 模块能力接口

`ModuleBase` 提供可直接使用的默认实现：名称取类型名、顺序为 0、注册为空操作，最小模块无需重写任何成员。

`IInitializable` 与 `IShutdownable` 拆为独立的能力接口，对应模块骨架中「接口按能力细分」：模块只实现自身需要的能力，启动编排日后以类型检查发现能力，无需任何模块编写空方法。`Order` 由组合根按装配清单配置，模块自身不决定全局顺序。

## DI 变体与抽离

DI 容器适配器作为变体独立成程序集（如 `Core/Container.VContainer/`），引用 Core 加第三方容器，`Core` 本体保持零依赖。

`Core/Container` 与 `Core/ModuleSystem` 的源码**零 `UnityEngine` 引用**，尽管与其余 Core 子系统同属一个程序集（依据 README 已声明的「同属 Core 程序集」）。该约束使日后若出现第二个使用方，可整体抽离容器而无需拆分程序集；由代码评审保障。

服务端不使用本容器：`Shared` 与 `Server/` 采用 .NET 原生 DI 与生命周期托管。
