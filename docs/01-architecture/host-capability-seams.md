# 01-architecture — 宿主→插件能力供给设计（能力接缝三层模型）

> 状态：设计提案（待拍板实施）
> 最后更新：2026-08-31
> 关联：调研依据 [`06-research/001-deepseek-harness-plugin-architecture.md`](../06-research/001-deepseek-harness-plugin-architecture.md)（§5.5 软依赖判例 / §5.6 裁决 A-F / §6 偏差清单与 FAQ Q1-Q6）；内核设计 [`cordis-kernel.md`](cordis-kernel.md)（§3.6 插件间服务互通）；首个落地候选 = sems 项目登记接缝

本文回答：**宿主（Agent 等内置高级功能）如何设计完整流程，让插件直接取用宿主/其他插件的能力**，取代「共享 JSON 文件」这类隐式契约。

---

## 1. 问题（为什么改）

现状（2026-08-31 代码核实）：

- AIAgent 选定工作目录 → `ProjectRegistryService` 写 `Shared/sems-projects.json`；sems `ProjectService` 读同一文件展示项目列表。
- 两插件通过**文件路径 + 文件格式**耦合（`SemsShared` 常量），绕过了内核的服务定位机制（`IContext.Register/Get`）。
- `IProjectRegistryService` 接口定义在 AIAgent 插件**内部**，不是契约层能力接缝。

偏差影响：

1. **隐式契约**：文件格式即契约，改字段要两端同步改，编译器无法发现漂移（`PathExists/IsGitRepo` 误标 `[JsonIgnore]` 导致 sems 误渲染「不可达」即实证）。
2. **无生命周期语义**：AIAgent 卸载/热重载时，文件仍在——sems 拿到的是「死数据」，无法感知提供方是否存活。
3. **宿主没有「完整流程」**：Agent 工作区是宿主级核心流程，但流程（选目录→登记→通知消费方）没有任何一环由宿主定义，全靠两个插件私下约定。

## 2. 调研给出的答案（不自作设计）

deepseek-harness / Cordis 调研已裁决此类问题（`06-research/001`）：

| 问题 | 权威答案 | 出处 |
|------|----------|------|
| 插件 A 的能力给插件 B 用，怎么接？ | A **eager 构造**实例 + `ctx.Register<TContract>()` 写入 root 共享表（对标 `provide()`）；B 构造注入 `IContext` + `ctx.Get<TContract>()` 消费 | §6 FAQ Q1 |
| 可选增强要不要走事件？ | **默认不要**。服务契约 + 软依赖 `Get` 已完整覆盖「有则增强、无则降级」（dsh `tool-fs-search` 判例）；事件 waterfall 只用于「多方竞争改写/中间件链」 | §5.5 / FAQ Q4 |
| 消费方要注意什么？ | **禁止缓存 Get 到的实例为字段**（每次用每次 Get），提供者热重载后共享表自动摘除，null 走降级 | FAQ Q2 |
| 宿主 seed 与插件提供的关系？ | 同一共享表两个来源：宿主 seed（root，常驻 app 生命周期）；插件提供（随 Fiber 摘除） | §5.6 裁决 B/C、FAQ Q6 |
| 事件跨插件广播现在能用吗？ | **不能**。每个 `Context` 持有独立 `EventBus`（`Context.cs:24`），互不传播——偏差#5，修正方案 = 派生上下文共享根总线/父子链冒泡（TODO 进行中②） | §6 偏差#5 |

## 3. 设计：能力供给三层模型

宿主负责**定义**（契约 + 事件族 + 核心流程编排），插件负责**提供实现**与**消费**。按使用场景分三层，默认用最简单的：

```
L1 服务契约（pull，默认主力）
   契约定义在 ForgeSelf.Abstractions（宿主设计）
   Provider：eager 构造 + ctx.Register<T>()（提供即 effect，卸载自动摘除）
   消费者：ctx.Get<T>() 软依赖，null → 降级；禁止缓存实例
   适用：一切「有则增强、无则降级」的能力取用（90% 场景）

L2 事件通知（push，补充）
   宿主定义事件族常量（命名空间/动作），核心流程节点 emit
   插件 On 订阅（包进 ctx.Effect 保证卸载摘除）
   前提：事件传播修正（偏差#5，方案B 父子链冒泡）落地后才跨插件可用
   适用：需要实时响应的流程通知（如项目登记后刷新 UI、审计/遥测）

L3 拦截管道（waterfall/serial，特例）
   多方按序竞争改写/中间件链（tools/pre-execute 拒绝门为既有判例）
   适用：需要拦截、改写、否决宿主流程的场景
```

**选型决策树**（架构设计时逐层问）：

1. 消费者拿不到能力时能降级吗？→ 能：**L1 契约 + Get**（停，不要用事件）
2. 消费者必须在能力变化**发生时**实时动作？→ 是：补 **L2 事件**（L1 仍保留作拉取通道）
3. 需要多方按序拦截/改写/否决？→ 是：**L3 waterfall/serial**
4. 能力缺失时消费者**根本不该启动**？→ 硬依赖 `Consumes` + PENDING（机制缓建，首个强依赖接缝出现时落地，偏差#3）

## 4. 首个落地：项目登记接缝（sems 共享 JSON 的替代）

### 4.1 谁提供？——宿主 seed（推荐）

两个候选：

| 方案 | 提供方 | 优点 | 缺点 |
|------|--------|------|------|
| A. AIAgent 插件提供 | AIAgent `Apply` 时 `ctx.Register<IProjectRegistry>` | 符合「一切皆插件」纯化 | sems 强耦合 AIAgent 在场；AIAgent 卸载 → sems 数据空洞；其他来源（手动添加、第三方插件登记）无处挂 |
| B. **宿主 seed（推荐）** | 宿主实现 `IProjectRegistry`，启动时 `ProvideHostServices` 写 root | 项目工作区是**宿主级核心流程**（Agent 内置高级功能的底座），服务与数据同寿命、常驻；AIAgent/sems/未来任何插件都是平等的消费方/登记方 | 宿主多一个服务（代价极小） |

**推荐 B**：与用户定位一致——「Agent 功能作为内置高级功能，理应由宿主设计完整流程」。宿主定义并持有登记流程，AIAgent 的「选目录」只是登记动作的一个触发源。

### 4.2 改造点（阶段 1）

1. **契约入 Abstractions**：`IProjectRegistry`（`Register(root, out error)` / `GetAll()`），`ProjectRecord` 已在 Abstractions 复用。
2. **宿主实现 + seed**：`HostProjectRegistry`（实现即现在的 JSON 持久化逻辑，迁入宿主 `Services/`），`PluginManager.ProvideHostServices` seed 进 root（对标裁决 B/C，常驻）。
3. **AIAgent 改消费**：`ProjectController`/`ProjectRegistryService` 改 `ctx.Get<IProjectRegistry>().Register(...)`；删除插件内部 `IProjectRegistryService` 接口与文件直写逻辑。
4. **sems 改消费**：`ProjectService` 改 `ctx.Get<IProjectRegistry>().GetAll()`（每次用每次 Get，不缓存）；删除文件直读逻辑。`PathExists/IsGitRepo` 派生计算保留在 sems 端。
5. **共享 JSON 降级**：从「跨插件契约」降为宿主实现的**持久化细节**（跨重启保留数据），文件路径/格式可自由演进。
6. **测试**：契约测试（提供后可解析、注册往返）；sems 消费降级测试（Get null → 空列表不崩）；`SemsSharedContractTests` 随迁改。

### 4.3 阶段划分

| 阶段 | 内容 | 风险 | 依赖 |
|------|------|------|------|
| 1 | 项目登记接缝：契约 + 宿主 seed + 两插件改消费（§4.2） | 中（公共接口变更，执行后全量 Verify） | 无，可立即做 |
| 2 | 事件族 `project/registered` 等：宿主在核心流程节点 emit；插件订阅 | 中 | **事件传播修正（偏差#5，TODO 进行中②）先落地**，否则跨插件收不到 |
| 3 | 硬依赖 inject（`Consumes` + PENDING 自动重启） | 高 | 首个强依赖接缝出现时（缓建，台账） |

## 5. 边界与不做事决策

- **不做事件优先架构**：L2 事件不是 L1 的替代品，而是补充；sems 首页这类「请求时拉取」场景用 Get 已够（调研 Q4）。触发条件：出现「必须实时响应」的消费方再启用 L2。
- **不动态下发 Scoped 语义**：共享表服务 = eager 单例（裁决 F），不做 per-request 伪 Scoped。
- **不删除共享 JSON 文件本身**：它是持久层，删的是「文件当契约」的用法。既有数据自然保留。

## 6. 验证（实施时）

- `dotnet build` 0 错误 + `dotnet test` 全绿（含新增契约/降级测试）。
- 运行时：宿主 51888 冷启动 → AIAgent 选目录 → sems `/api/projects` 200 且数据一致（Playwright）。
