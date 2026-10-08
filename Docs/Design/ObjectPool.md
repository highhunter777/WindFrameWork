# 对象池设计

`Core/ObjectPool`，命名空间 `WindFrameWork.Core.ObjectPool`。本文仅描述当前最新设计。

## 目标与边界

复用 `UnityEngine.Object`（`GameObject` / `Component` / `ScriptableObject`）实例，降低频繁实例化与销毁的开销，并让对象池的运行状况可观测、可诊断。

不在本模块范围内：纯 C# 对象的引用复用（属 `Core/ReferencePool`）、池与作用域的绑定（属未来的 `PoolScope`）、异步与分帧创建。

## 为何手写而非包装 `UnityEngine.Pool`

Unity 2022.3 内置的 `UnityEngine.Pool.ObjectPool<T>` 已具备 `maxSize`、`CountAll`、`CountActive`、`CountInactive`、`collectionCheck`、`defaultCapacity` 与 `Clear` / `Dispose`。因此手写的理由不是「缺少容量上限与统计」，而是内置版本缺少以下四项：

| 缺口 | 本模块的应对 |
| --- | --- |
| 无预热：`defaultCapacity` 仅预分配内部 `List<T>` 容量，不创建任何实例 | `ObjectPoolConfig.PrewarmCount` 在构造时真实创建并归还 N 个实例 |
| 无累计统计：仅有当前值量表 | 累计命中 / 未命中 / 归还 / 创建 / 销毁 / 峰值与命中率 |
| 无条件 O(1) 归还校验：`collectionCheck` 用 `List.Contains`，为 O(n) 且约定上在发布版关闭 | 租约凭证 + 槽位代数，O(1) 且不受配置开关影响 |
| 无已销毁对象容错：已销毁对象留在池中并被交付，在使用点引发 `NullReferenceException` | 三条fake-null 路径均被检测、计数且不抛出 |

## 数据结构：槽位表 + 侵入式空闲链表

```
_objectBySlot[]     slot -> UnityEngine.Object
_hooksBySlot[]      slot -> IPoolable[]        创建时解析一次
_nextFreeBySlot[]   slot -> next free slot      空闲链表，存放于并行 int[]
_stateBySlot[]      slot -> Free / InUse / Dead
_generationBySlot[] slot -> 租出代数
_freeHead           -1 表示空闲链表为空
```

出栈即 `slot = _freeHead; _freeHead = _nextFreeBySlot[slot];`，O(1)、无分配、无哈希。

### 否决的替代方案

- **`LinkedList<T>`**：每次 `AddLast` 分配一个 `LinkedListNode<T>`，即归还路径上的一次堆分配，且节点分散、IL2CPP 下各带GC 句柄。这是对象池最经典的 GC 陷阱。
- **`Stack<T>` / `Queue<T>`**：预分配容量后确实无分配，但**无法支撑租约校验**——`Return(obj)` 需要 `object -> slot` 反查，而 `Dictionary<Object,int>` 的 `EqualityComparer<Object>.Default` 会调用Unity 对已销毁对象重载的 `Equals`（fake-null 键行为微妙且随版本变化），也无法区分 `Dead` 与 `Free` 槽位。此为正确性否决，非性能取舍。
- **`List<T>` + `Remove`**：移除为 O(n)。

### 计数不变量

租出计数与并发峰值统一由 `MarkRented` 负责，`PopFree` 只调整空闲计数；预热走 `MarkIdle`，不经过租出路径，因此不污染峰值统计。槽位失效按「是否已计入租出」分为 `MarkDead`（扣减租出）与 `MarkIdleSlotDead`（不调整），避免重复或遗漏计数。

## 容量策略：销毁式溢出，而非 LRU

归还时若空闲数已达 `MaxIdleSize`，销毁多余对象并累加销毁计数。

否决 LRU 的四条理由：

1. **稳态成本**：LRU 需在每次归还时写入时间戳，溢出路径上持续付出缓存行抖动；销毁式的溢出路径只是一次计数比较，且 99.9% 不会命中。
2. **成本模型不匹配**：对 `UnityEngine.Object` 而言昂贵操作是 `Instantiate` / `Destroy`，而非「保留哪一个」。LRU 仅在「创建昂贵 **且** 复用距离长」时获胜——这是需按项目实测的属性，不应由框架假设。
3. **确定性的内存边界**：`MaxIdleSize` 成为可审计的硬上限，这是性能与线上排查需要的。
4. **开闭原则仍然成立**：溢出逻辑收敛在 `HandleOverflow` 单方法，日后可加 `PoolOverflowPolicy` 枚举与LRU 空闲存储而不改动任何公开签名。现在就加入单值枚举属投机式泛化。

**并发超限抛 `InvalidOperationException`**，不返回 null。池耗尽意味着调用方漏归还，是缺陷；返回 null 会把一个明确的缺陷变成几帧之后无关代码行上的 `NullReferenceException`。

空闲上限与并发上限是两个独立配置：`MaxIdleSize` 是内存占用上限，`MaxLiveSize` 是并发上限。合并为单个数值将迫使在「保持大热缓存」与「禁止超过 N 并发」之间取舍，而二者互不相关。

## 已销毁对象（fake null）处理

必须使用 Unity 重载的 `==`（原生 `CompareBaseObjects` 调用）判断销毁状态。**`ReferenceEquals` 免费但错误**：它对已销毁对象仍返回 true。

三条路径均被检测、计数，且**不抛异常**——池不是对象生命周期的所有者，被销毁是预期事件而非契约违约；抛异常会让正确的清理代码（例如场景拆卸）崩溃。

| 路径 | 处置 |
| --- | --- |
| 租出期间被外部销毁后归还 | 标记 `Dead`、清引用、扣减租出、不入空闲链表、丢弃计数 +1、记警告、不抛 |
| 空闲期间被外部销毁 | `Rent` 循环中跳过该槽位继续取下一个，丢弃计数 +1。这是快路径允许非平凡的唯一理由，也是失效对象绝不会流到业务代码的唯一保证 |
| 工厂返回已销毁对象 | 视为创建失败并抛出指明池名的诊断——这是工厂缺陷，不是运行时状况 |

生产环境中 `DiscardedDestroyedCount` 非零意味着池外代码在直接销毁池内对象，属需追查的缺陷，应在调试面板中显著呈现。

## 生命周期与编辑器跨会话残留

三层处理：

1. **`HideFlags.DontSave`**：池根与池内对象均带此标记，避免写入场景存档并跨会话残留。
2. **`ObjectPoolRegistry` + `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`**：池在构造时自注册。该阶段执行 `RecycleAll()`——清空集合与引用而**不调用 `Destroy`**（此阶段非播放态，销毁非法）。域重载与「关闭 Reload Domain 进入播放态」两种情况均被覆盖。
3. **编辑器程序集重载前**（`AssemblyReloadEvents.beforeAssemblyReload`，`#if UNITY_EDITOR`）：以 `Application.isPlaying` 为守卫销毁全部对象，避免孤立对象。

**已知限制（如实记录）**：`DontSave` 池根不随场景卸载存活，场景卸载会销毁其 `DontSave` 子对象。跨场景池需由调用方自行挂到 `DontDestroyOnLoad` 对象下，本模块不提供该行为。在此之前，上述 fake-null 路径使其「可生存」：失效对象被静默丢弃重建。

## 线程模型

仅主线程可用，**全链路不加锁**。加锁只能让数据结构线程安全，而 `Instantiate` / `SetActive` 等 Unity 调用仍要求主线程，加锁反而制造虚假的安全感。`PoolThreadGuard` 在构造时记录线程标识，于 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 下断言，发布构建中编译期消除。

## 扩展点

`IPoolLifecycleAdapter<TObject>` 抽象类型特定的状态变更。这一层是必需的：`SetActive` 与 `transform` 只存在于 `GameObject`，泛型池无法直接调用，而在池内部做类型判断会同时违反单一职责与开闭原则。内置实现 `UnityObjectLifecycleAdapter<TObject>` 无状态且以静态单例共享，不为每个池产生额外分配。

`IPoolable` 供业务组件重置自身状态。钩子在**对象创建时解析一次**并按槽位缓存（`GetComponents<IPoolable>()` 每次调用都分配数组，逐次调用将破坏零分配保证）。`ObjectPoolConfig.CollectHooksFromChildren` 默认为 `false`：`GetComponentsInChildren` 显著更贵，且静默扫描整棵层级是「钩子为何触发两次」的常见来源。

**池不修改 `localPosition` / `localRotation`**：重置变换属于业务策略，应在 `IPoolable.OnRentFromPool` 中实现。

## 对象池注册表

`ObjectPoolProvider` 按名称管理池，`IObjectPoolProvider` 为其契约。名称是业务选取的池标识，**不从prefab 或类型派生**——具体创建什么由工厂决定，故同一套注册表可同时承载不同类型的池（如 `"Shell"` 从 prefab 创建、`"RuntimeCurve"` 由 `CreateInstance` 创建）。同名不同元素类型是配置错误并抛异常。

四个易犯错误，均已规避：

1. `Dictionary<Type, object>` 会丢失类型身份，使类型不符退化为使用点的类型转换异常。改为单一 `Dictionary<string, PoolRegistration>`，记录 `ElementType` 并在 `Get<T>` 中精确比对，异常信息指明键、已注册类型与请求类型。一次字典、零装箱、零二次查找。
2. `PooledHandle<T>` 为只读结构体，**严禁存入 `object` 类型的字段或容器**，否则每次存取都装箱。此为该类型的硬性使用约束。
3. 字符串键每次调用都重算哈希。**热路径应缓存 `IObjectPool<T>` 引用**，热路径是 `pool.Rent()` 而非 `provider.Get()`；这与「解析仅允许出现在表现层边界类型」的容器规则同构。
4. `Dictionary` 枚举顺序无契约保证，`GetPools()` 显式按名称排序，使测试与日志可复现。

## 程序集归属

`Core/ObjectPool` 与其余 Core 子系统同属 `WindFrameWork.Core` 单个程序集，依据是 README 已声明的「同属 Core 程序集」。`SetParent` 使用 name-based `references`；每个填入内容的 `WindFrameWork/**` 目录都需配置 asmdef，否则会逃逸进 `Assembly-CSharp` 而脱离依赖图约束。

> 另注：`Assets/XLua/Src/ObjectPool.cs` 是腾讯 XLua 的同名非泛型槽位分配器（MIT，第三方），既非本设计的先例，也不应复制或并入本框架。本模块类型为 `WindFrameWork.Core.ObjectPool.ObjectPool<T>`，与之无命名冲突。

## 命名空间与类型同名邻接

命名空间 `WindFrameWork.Core.ObjectPool` 与类型 `ObjectPool<T>` 邻接（与 Unity 内置的 `UnityEngine.Pool.ObjectPool<T>` 形态一致）。因此裸记号 `ObjectPool` 在仅 `using WindFrameWork.Core;` 的文件中会绑定到命名空间；须始终带类型实参写作 `ObjectPool<T>`。
