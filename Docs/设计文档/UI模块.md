# UI 模块设计

`Modules/UI`，程序集 `WindFrameWork.Modules.UI.Contracts`（契约）与 `WindFrameWork.Modules.UI`（运行时，引用契约 + Core）。本文仅描述当前最新设计；当前状态为规划中，代码未动工，进度见 [../施工记录/进度.md](../施工记录/进度.md)。编辑器侧工具见姊妹篇 [UI开发工具.md](UI开发工具.md)。

## 目标与边界

框架侧的 UGUI 面板基础设施：生命周期状态机、层级管理、开合调度、安全区适配，契约先行。业务面板本身、场景级流转（归 SceneFlow）、转场动画的实现细节（业务侧用 DOTween 等自行实现）均不在本模块。

## 技术栈（已定：双栈各司其职）

- **业务 UI 用 UGUI**：`com.unity.ugui` + TextMeshPro + DOTween 生态齐备，2022.3 业务 UI 的主流与热更生态都在这边；Binder 生成与校验规则集系于此选择（见姊妹篇）。
- **工具与调试用 UIToolkit**：工具中心、预览窗口、调试面板的呈现层，与调试平台、开发工具平台的选型一致。
- 本模块 Runtime 只依赖 UGUI / TMP，不引 UIElements。

## 契约（Contracts）

沿用容器「读写分离」的纪律，UI 侧同样分注册与使用两面：

| 契约 | 侧 | 职责 |
| --- | --- | --- |
| `IPanelRegistry` | 写 | 注册 `PanelDescriptor`（稳定字符串 Id + 层 + 行为标志 + 工厂委托），仅组合根与模块注册阶段可见 |
| `IPanelService` | 读 | 打开 / 关闭 / 隐藏 / 查询，按能力细分的接口，表现层边界类型 |
| `IPanelLifecycle` | 实现契约 | 业务面板实现：初始化、开、关、焦点变化等钩子 |
| `IUIDiagnostics` | 诊断 | 面板栈与各面板状态快照，调试平台消费 |
| `Contracts/Events/` | 事件 | `PanelOpened` / `PanelClosed` / 状态变化，供跨模块松耦合（分发随事件中心落地） |
| `IPanelContext` | 数据契约 | 面板的数据依赖接口；纯展示面板强制声明空实现（已定）；真实实现由业务侧提供，Fake 随业务面板放热更侧 `<游戏模块>/Testing/`，单测与预览共用（见姊妹篇） |

- **面板以稳定字符串 Id 标识**：AOT 侧不可能知道热更面板类型，字符串 Id + 工厂委托是两侧的唯一接缝（与 `ReferencePools` 的组合键思路同构）；类型化门面（泛型 `Open<T>`）由热更侧扩展方法提供，不进 AOT。
- **异步边界**：开合含资源加载与转场动画，生命周期以同步状态机为唯一事实；可等待（await 开合）扩展由 UniTask 变体程序集提供，遵循 UniTask 桥接的既定接入建议——本模块 Contracts 不引 UniTask。

## 生命周期状态机

```
None → Loading → Loaded → Opening → Open → Closing → Closed
                                  ↘ Hidden（隐藏不销毁时经 Hide/Show 往返）
```

- 重复打开同一 Id 的策略（置顶 / 忽略 / 断言）由 `PanelDescriptor` 行为标志决定；
- 关闭后缓存还是销毁同样由描述符决定（缓存上限可审计，超限逐出）；
- 转场动画作为生命周期钩子里的可等待步骤，由业务实现；状态机不感知动画技术。

## 层级

- 层级集合（如 Background / HUD / Popup / Guide / Loading / Topmost）经配置资产定义于模块 `Content/`，内置集仅是出厂值——禁止硬编码层名；
- 每层一个独立 Canvas 挂在 UI Root 下，层内排序由面板动态占位；
- 层 Canvas 的 Sorting Order 区间由配置资产分配，预留给工具窗口与调试叠加层的高位段与业务层隔离。

## 热更边界（本模块最关键的一条）

- **框架侧（AOT）**：`UIManager`、状态机、层级、安全区组件与全部契约；
- **业务面板（热更侧）**：实现 `IPanelLifecycle`，由热更侧组合根经 `IPanelRegistry` 注册工厂——热更引用 AOT 契约是允许方向，**无需反射、无需 `IHotEntry` 桥接**；
- 面板 prefab 属热更资源，加载经资源模块契约；AOT 不引用任何具体面板类型。

## 安全区

`SafeAreaFitter`（UGUI 组件）驱动于 `Screen.safeArea`，挂全屏面板根节点；平台差异（刘海 / 圆角 / 折叠 / 手势条）按骨架的平台变体目录分组（`Runtime/Unity/` 为首个变体候选）。编辑器侧的设备模拟归开发工具姊妹篇。

## 与相邻模块的关系

| 模块 | 关系 |
| --- | --- |
| Resource | 面板 prefab 加载经其契约；资源模块落地前，加载器以委托注入形态先行（测试与预览用） |
| SceneFlow | 场景流转不属 UI 模块，两模块经事件解耦 |
| EventCenter | 面板事件已在 `Contracts/Events/` 定义，分发随事件中心落地接入 |
| Debugging | `IUIDiagnostics` 是其 UI 观测面板的数据源 |
| 工具平台 | 本模块 `Editor/` 的工具注册进 ToolRegistry（见姊妹篇） |

## Testing 与 Tests

- `Testing/`：框架级替身（`FakePanelService` 等），消费方测试与预览窗口共用；面板 Context 的 Fake 不在此处，随业务面板放热更侧 `<游戏模块>/Testing/`（归置决策见姊妹篇）；
- `Tests/`：EditMode 覆盖状态机与注册表；PlayMode 覆盖层级、安全区与生命周期推进。

## 已知限制（如实记录）

- 本设计未动工；资源模块未落地前以委托注入形态承接加载，落好后回写本节。
- awaitable 扩展的变体程序集形态待首个异步消费方出现时定稿。
- 面板返回栈（导航）策略——栈深度、引导互斥、跨层返回——随首个业务面板需求细化，当前只承诺单层弹窗栈。
