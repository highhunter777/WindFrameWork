# 引用池设计

`Core/ReferencePool`，命名空间 `WindFrameWork.Core.ReferencePool`。本文仅描述当前最新设计。

## 目标与边界

复用纯 C# 引用类型实例（`List<T>`、字典、协议包、命令缓冲、事件对象等），把「每帧新建短命对象」改成「租还复用」，从而降低 GC 压力。同时提供三项可观测保障：

- 并发上限与空闲上限
- 可观测的命中率与峰值
- 租约校验：重复归还与陈旧凭证 O(1) 被拒

不在本模块范围内：`UnityEngine.Object` 的复用（属 `Core/ObjectPool`）、池与作用域的绑定（属未来的 `PoolScope`）、异步与分帧创建。

### 与 Core/ObjectPool 的分工

| | `Core/ObjectPool` | `Core/ReferencePool` |
| --- | --- | --- |
| 元素 | `UnityEngine.Object` | 任意 `class` |
| 线程 | 仅主线程，全链路不加锁 | 默认可跨线程，可选单线程亲和 |
| 溢出多余对象 | 调用 `Destroy` / `DestroyImmediate` | 解除引用，交还 GC |
| 失效来源 | 外部销毁形成 fake-null，需三条容错路径 | 无 fake-null，不需要容错路径 |
| 生命周期回调 | `IPoolable`（组件层级，多个钩子） | `IPoolable`（单钩子）+ 重置 / 释放委托 |

「两类池是否合并」的答案是否：合并后差异只会变成遍布实现分支的类型判断，违反单一职责；而两侧共用的是**同一种槽位表结构**，不必通过继承共享。

## 数据结构：槽位表 + 侵入式空闲链表

```
_items[]            slot -> T 实例
_hookBySlot[]       slot -> IPoolable（至多一个）
_nextFreeBySlot[]   slot -> next free slot   空闲链表，存放于并行 int[]
_stateBySlot[]      slot -> Free / InUse / Dead
_generationBySlot[] slot -> 租出代数
_freeHead           -1 表示空闲链表为空
```

出栈即 `slot = _freeHead; _freeHead = _nextFreeBySlot[slot];`，O(1)、无分配、无哈希。与 `Core/ObjectPool` 的槽位表同构，差异只有两处，均由「元素是纯托管对象」推出：

1. **空闲链表取出时不做存活性检查**。托管对象不会在池外被销毁，`_items[slot]` 只可能为 null（Dead 槽位不入链）。
2. **钩子按单引用缓存而非数组**。托管元素没有 `GameObject` 那样的组件层级，一个槽位至多一个钩子，用数组只是多付一次分配。

### 否决的替代方案

- **`Stack<T>` / `Queue<T>`**：预分配后确实无分配，但**无法支撑租约校验**——`Return(对象)` 需要 `对象 -> slot` 反查，而这正是本模块要避免的路径。此为正确性否决。
- **`Dictionary<T, int>` 反查以支持 `Release(实例)` 形式**：每一次租还都要写哈希桶，比一次数组访问贵一个数量级；更重要的是它会把「归还」从 O(1) 链表操作变成哈希表维护，使稳态零分配依赖字典容量而非结构。**因此本模块不支持按实例归还**，一律交回 `ReferenceHandle<T>`。
- **`ConcurrentBag<T>` / 无锁并发队列**：线程局部存储会为每个线程复制容器，`Clear` 与统计需要遍历所有线程局部段，且同样无法承载代数校验。
- **`LinkedList<T>`**：每次归还分配一个节点，是对象池最经典的 GC 陷阱。

## 容量策略：解除引用式溢出

归还时若空闲数已达 `MaxIdleSize`，则解除引用、推进释放计数，把对象交还 GC。

与 `Core/ObjectPool`（销毁式）同源同理，区别仅在于「昂贵操作」的定义：对 Unity 对象是 `Instantiate` / `Destroy`，对托管对象是分配本身——少一次分配不可能比多持有一次引用更差，所以这里也不需要 LRU。理由同样适用于确定性的内存边界：`MaxIdleSize` 是硬上限，`ReleasedCount` 与 `CreateCount` 的差值即池外持有量，是泄漏的可审计证据。

**并发超限抛 `InvalidOperationException`**，不返回 null。池耗尽意味着调用方漏归还，是缺陷；返回 null 会把明确缺陷变成后续某处的 `NullReferenceException`。

## 线程模型

`ReferencePoolConfig.ThreadMode` 二选一：

| 模式 | 行为 | 适用 |
| --- | --- | --- |
| `Synchronized`（默认） | 以池自身为互斥对象加锁，覆盖租出、归还、清空、释放、统计与计数读取 | 多线程共享的池 |
| `SingleThread` | 不加锁；开发构建下（`UNITY_EDITOR || DEVELOPMENT_BUILD`）断言调用线程与创建线程一致 | 明确只在一个线程流转、需要最短路径的池 |

默认取加锁而非只用断言，是因为纯托管元素没有 Unity 的主线程约束，把「只能在主线程用」换成「不限线程」才是本模块的形态；而 `SingleThread` 作为显式选项存在，是因为确有独占场景值得省掉一次 `Monitor`。

锁的粒度是**整个池对象**，不是细粒度结构：锁保护好的是「计数 + 空闲链表 + 槽位状态」这组不变量，任何细分都会在 Clear / 释放路径上留下不一致窗口。工厂与业务回调仍在调用线程上执行——池只保证自身状态一致，不保证调用方的代码线程安全。

## 生命周期钩子与回调

- **`IPoolable`**：复用 `Core/ObjectPool` 的同名契约，而非另立一套——两者语义完全相同（「被租出 / 被归还」），重复定义只会让同一概念出现两个名字。它是对象池模块的公开契约类型，本模块引用它符合「跨模块只依赖对方 Contracts」。
- **`onReset`（归还时）**：清理容器内容、还原字段值等。典型用法是 `list => list.Clear()`。
- **`onRelease`（对象离开池时）**：空闲溢出、`Clear`、`Dispose` 三条路径都会触发，用于释放非托管资源或回写指标。

固定顺序：`IPoolable.OnReturnToPool` → `onReset`，使实现类得以在外部回调介入前完成自身一致性收敛。

**池不对 `T` 做任何隐式 `IDisposable` 处理**。原因与对象池一致：池不是对象生命周期的所有者，静默调用 `Dispose` 会把「业务是否允许 Dispose 后可复用」这一只有调用方知道的问题，变成框架的隐式约定。需要释放语义时显式传 `onRelease`。

同理，池不自动重置业务字段——只有调用方知道「干净」的定义。

## 统计

`ReferencePoolStatistics` 为按值返回的结构体快照，诊断用途，不建议进每帧路径。字段：`RentCount`、`HitCount`、`MissCount`、`ReturnCount`、`CreateCount`、`ReleasedCount`、`IdleCount`、`LiveCount`、`PeakLiveCount`，派生 `TotalCount`、`HitRate`。

`ReleasedCount` 与 `CreateCount` 之差即为「池外持有且未归还」的对象数，持续增长意味调用方漏归还。

## 静态入口 `ReferencePools`

按「类型 + 可选业务键」持有共享实例，服务于「临时取一个 `List<int>` 用完还回」这类散点需求。

它不是唯一用法，也不鼓励作为唯一用法：有明确归属与生命周期的池应当自行持有一个 `ReferencePool<T>` 实例。查找经过字符串组合键，每次调用都要重算哈希，因此**不得放在每帧路径上**——正确用法与容器规则同构：初始化阶段取出池引用缓存起来，热路径只调用 `Rent` / `Return`。

## 注册表与生命周期

`ReferencePoolRegistry` 在池构造时自注册，`[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` 阶段统一解除引用并清空注册表，覆盖域重载与「关闭 Reload Domain 进入播放态」两种情况。

与 `Core/ObjectPool` 相比少一层复杂度：托管对象的引用解除在任何时机都合法，不像 `Destroy` 那样受「非播放态不得销毁」约束，因此不需要 `Application.isPlaying` 守卫，也不需要在编辑器程序集重载前另做一次清理。

## 已知限制（如实记录）

- `Clear` / `Dispose` 会解除全部对象引用，此后在外的凭证一律失效并被拒绝归还。它不是「温和的空闲清理」。
- 元素类型必须是引用类型（`where T : class`）；值类型请直接使用 `Span` / 数组池一类的方案。
- 不支持按实例归还；请以 `ReferenceHandle<T>` 归还。
- 无异步与分帧创建：工厂是同步的，预热在构造期一次性完成。

## 程序集归属

与其余 Core 子系统同属 `WindFrameWork.Core` 单个程序集，不新增 asmdef。测试落在 `Tests/EditMode/ReferencePool/`（151 项程序集中的 fixture）与 `Tests/PlayMode/ReferencePool/`（零分配基准，`Performance` 分类）。

## 命名空间与类型同名邻接

命名空间 `WindFrameWork.Core.ReferencePool` 与类型 `ReferencePool<T>` 邻接，与 `Core/ObjectPool` 的形态一致，也与 Unity 内置的 `UnityEngine.Pool.ObjectPool<T>` 一致。因此裸记号 `ReferencePool` 在仅 `using WindFrameWork.Core;` 的文件中会绑定到命名空间；须始终带类型实参写作 `ReferencePool<T>`。静态入口因此命名为 `ReferencePools`（复数）以避开歧义。
