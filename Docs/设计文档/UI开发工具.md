# UI 开发工具设计

`Modules/UI/Editor/`，程序集 `WindFrameWork.Modules.UI.Editor`（Editor-only，命名空间 `WindFrameWork.Modules.UIEditor`——沿用 EditorTooling 的同名邻接规避）。本模块编辑器工具挂开发工具平台基建（ToolRegistry / ValidationRunner / 工具中心），基建形态见 [开发工具平台.md](开发工具平台.md)；服务的本体设计见 [UI模块.md](UI模块.md)，本文处处引用其契约。

## 目标与边界

一条工作流闭环，工具长在闭环环节上：

**Prefab 是单一事实源 → 生成（代码）→ 校验（规范）→ 预览（免播放迭代）**

不做：运行态观测（面板栈、层级选中同步归调试平台，消费 `IUIDiagnostics`）；图集与精灵审计（归资源模块工具，与 YooAsset 收集规则同源）。

## 技术栈归属（已定）

- 工具的**生成与校验对象**是 UGUI prefab（业务 UI 用 UGUI，判定见 [UI模块.md](UI模块.md)）；
- 工具**自身界面**用 UIElements（UIToolkit 编辑器窗口），与工具中心同源。

## 面板脚手架

输入面板名，产出三件：

1. **prefab 骨架**：层 Canvas 占位、安全区容器（挂 `SafeAreaFitter`）、按约定命名的常用节点；
2. **Panel 逻辑类**（热更侧）：实现 `IPanelLifecycle` 的空壳，落位 `App/HotUpdate/Modules`；
3. **Binder 引用类**（生成物）：见下节。

生成前命名预检：面板名与知名类型同名（`Debug` / `Editor` / `UniTask` 等）时拒绝并说明。与工具平台的模块脚手架同机制、不同粒度，同属第一批。

## Binder 生成（已定：命名约定扫描式）

- 按命名前缀扫描 prefab 节点，前缀 → 类型映射经配置资产承载（内置出厂表，可扩展，禁止硬编码在生成器里）：`_btn` → Button、`_txt` → TMP_Text、`_img` → Image、`_raw` → RawImage、`_toggle` → Toggle、`_slider` → Slider、`_input` → TMP_InputField、`_scroll` → ScrollRect、`_node` → RectTransform；
- 产物为带生成标记的独立 Binder 类，Panel 持有；prefab 变更后重生成即达（含重命名）；
- **决策记录——否决引用收集组件式**：命名即契约，零手工步骤、重命名后重生成即达、命名规范由校验规则盯住；组件式每面板多一步手工挂载且序列化引用易被误改。代价（改名需重跑生成）由保存时自动重生成消化。

## 校验规则（挂 ValidationRunner，规则集系于 UGUI）

| 类别 | 规则 |
| --- | --- |
| 性能向 | 非交互节点 raycast target 审计、Canvas 嵌套与 Rebuild 风险、UI 纹理尺寸超标 |
| 规范向 | 节点命名符合前缀表、排序层级落在所属层区间、全屏面板锚点纪律、TMP 字体资产一致性（禁止动态默认字体兜底） |
| 引用向 | Binder 缺引用、prefab 与生成代码失配、面板删除后的孤儿 Binder |

规则实现放本模块 `Editor/`，注册进统一 Runner；CI 侧随工具平台第一批的 BatchEntry 生效。

## PanelPreviewWindow 与设备模拟

- **免 PlayMode 预览**：实例化面板 prefab、走 `IPanelService` 的真实生命周期（Fake 加载器注入），面板列表来自面板登记表；
- **mock 注入（已定）**：面板经自有 Context 接口取数据，预览窗口注入热更侧 `Testing/` 的 Fake——面板代码不知道自己在预览，细节见「mock 注入边界（已定）」；
- **设备模拟**：常见分辨率、刘海 / 安全区、折叠态叠加显示，`SafeAreaFitter` 的编辑器预演；设备预设表经配置资产承载。

## 分期路线

| 期 | 内容 | 依赖 |
| --- | --- | --- |
| 第一批 | 面板脚手架 + Binder 生成 + 首批校验规则（命名 / raycast / 锚点） | 与 UI 模块骨架同步动工；生成即合规 |
| 第二批 | PanelPreviewWindow + 设备模拟 | UI 模块核心可用（生命周期与层级） |
| 随调试平台 P2 | 运行时观测面板（面板栈 / 选中同步） | `IUIDiagnostics` 落地 |
| 随资源模块 | 图集与精灵审计合流 | YooAsset 收集规则 |

## mock 注入边界（已定）

预览与单测共用同一套 Fake 体系，三个决策：

1. **每面板 Context 接口 + Fake 注入为骨架**：面板把数据依赖契约化为自有 Context 接口，预览窗口经注册表取 Fake 注入；Fake 与单元测试替身是同一套东西（`Testing/` 目录的存在意义即此，预览只是多一个消费方）。
2. **序列化资产降级为 Fake 的数据来源**：Fake 的初始数据允许从面板同名资产读取，保留策划可编辑性；但资产不是独立注入通道，体系只有一个。
3. **否决全局 mock 服务表**：它构成第二个注册源，「真实 / mock」的分叉点藏在解析器里，违反容器「注册只发生在组合根与模块注册阶段」的规则。

**Fake 归置（已定）**：随业务面板放热更侧 `App/HotUpdate/Modules/<游戏模块>/Testing/`——业务数据归业务；UI 模块 `Testing/` 只放框架级替身（`FakePanelService` 等）。热更侧游戏模块遵循与框架模块同构的骨架，`Testing/` 天然在位；预览注册表在编辑器态发现各 `Testing/` 程序集注册的 Fake。

**空 Context 强制（已定）**：无数据依赖的纯展示面板同样强制声明空 Context——注入路径对所有面板一致，「每面板必有 Context 契约」可成为校验规则，杜绝隐式数据依赖钻空子；代价是纯展示面板多一层空声明，接受其为统一规则的显式成本。

## 已知限制（如实记录）

- 本设计与 UI 模块本体同步规划，均未动工；首块代码落地时回写本节与进度表。
- 预览依赖 UI 模块生命周期落地，故排第二批；首批规则对存量 prefab 无追溯义务（当前尚无业务面板）。
- 设备模拟的安全区数值为预设近似，真机差异以实测为准。
