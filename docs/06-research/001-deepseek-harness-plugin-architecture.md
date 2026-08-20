# 调研：deepseek-harness 插件化设计 + Cordis 内核机制（DeepWiki）

> 本文定位：**调研依据（为什么这样设计）**，是 `../15-roadmap/plugin-architecture.md`（执行手册）的支撑文档。本文保留 dsh 机制细节与源码证据，落地步骤已全部并入 roadmap。
> 来源：DeepWiki `deepseek-ai/deepseek-harness` 全量文档（466KB，已分段读取核心章节 2.1 / 2.2 / 2.3 / 8 测试）+ DeepWiki `cordiverse/cordis`（上游框架源码机制，2026-08-19 补查：reflect.ts / fiber.ts / events.ts / loader / hmr，见 §4-§6）。
> 对照：本项目「任务 6」三缺口（文件锁 / 运行时无 DI·路由 / 前端未插件化）。
> 结论先行：**dsh 的「可卸载性」来自「一切皆 effect、卸载即反向回滚」，而非靠文件锁技巧**——这正是本项目热更新最该借鉴的底层心智模型。
> 实施防偏差指引：**动手改内核/插件互通前必读 §6（偏差清单与执行 FAQ）**——实现与调研方案出现分歧时以 §6 对照表裁决。

---

## 1. dsh 是怎么做插件化的（核心机制）

### 1.1 everything-is-a-plugin
`dsh` 用 **Cordis 框架**（Node/TS 生态成熟的 DI + 生命周期框架），把从 LLM 适配器、工具注册表到 **agent loop 本身** 全部做成插件，通过配置即可替换或扩展 [docs/architecture.md:9-14]。
关键点：没有「特权内核」，任何部件都可被配置替换。

### 1.2 Fiber 生命周期 = 「可卸载」的真正底座
每个插件实例是一个 **Fiber**，自带资源与 effect 管理 [vendor/cordis/src/fiber.ts]：
- 插件注册的一切（service、event、route 在 Cordis 语境下即 effect）都是**可逆的**；
- 卸载时 `dispose()` → 状态置 `UNLOADING`（拒绝新 effect 注册）→ **按注册逆序执行清理回调** → 状态置 `DISPOSED` [vendor/cordis/src/fiber.ts:1-100]。
- 这使得「换插件」= 拆掉旧 Fiber + 建新 Fiber，框架保证残留被自动收回，**不依赖手动释放文件句柄**。

### 1.3 Capability Seam（能力缝）= 解耦扩展点
架构围绕「Seam」：一个可替换能力 = 服务定义（接口）+ 服务提供者（实现）+ 消费者 [docs/architecture.md:98-103]。
| Seam (`ctx.x`) | 用途 | 实现可切换 |
|---|---|---|
| `ctx.llm` | LLM 抽象 | `llm-deepseek` / `llm-pi-ai` |
| `ctx.fs` | 文件系统 | `fs-local` / `fs-e2b`（沙箱） |
| `ctx.shell` / `ctx.subprocess` | 命令执行 | `pwsh-local` / `e2b` |
| `ctx.tools` | 工具注册表 | `tools` |

消费者只依赖接口，不依赖具体 provider [packages/README.md:67]。这是「运行时换实现不重启」的前提。

### 1.4 Event Bus + Waterfall（事件驱动 + 拦截链）
Cordis 类型化事件总线分三类 [docs/architecture.md:55-61]：Session 事件（持久事实）、Agent 事件（生命周期钩子）、Capability 事件（策略/适配器）。
`waterfall` 事件（如 `agent/pre-step`、`tools/pre-execute`）带 `next` 回调，监听器可拦截/改写模型请求或工具执行流 [docs/architecture.md:84-88]——这提供了「不改核心、只在边上挂逻辑」的扩展范式。

### 1.5 Profiles & Bundles（分层组合 + 配置即组装）
- **Profile**：入口（CLI / Web），决定初始化哪个环境；
- **Bundle**：一组相关插件的发行单元，`cordis.patch.yml` 提供基础配置（如 `dsh-base` 是所有实例的基座）[packages/bundle/base]；
- 运行实例 = `cordis.yml` + 多层 `cordis.patch.yml` 合并出的**服务/事件动态树**，同一代码库靠换 bundle/profile 即可跑 CLI、Web、headless [apps/cli/src/profile-boot.ts]。

### 1.6 HMR（热重载）= Fiber.update() + 强约束
- Cordis 自带 `vendor/hmr`，`loader` 插件负责加载/配置/HMR [vendor/README.md:18]；
- `Fiber.update()` 被改造为返回 waterfall 结果，调用方可 `await` 重启同时保留同步校验 [vendor/README.md:38]；
- **HMR 安全铁律**：每个包必须有 `./src/invariant.ts`，运行时验证该包「可被安全 dispose」（HMR 安全）[packages/AGENTS.md:17-18]；
- **测试门禁**：每个 registry 必须有一条测试——dispose 掉贡献该 registry 的 fiber 并断言清理成功 [docs/testing.md:9]。

---

## 2. 对照本项目「任务 6」三缺口的可借鉴点

| 本项目缺口（任务 6） | dsh 做法 | 借鉴点 | 落地可行性 |
|---|---|---|---|
| **缺口 1·文件锁**：原地覆盖 ALC 锁定的 DLL | 不依赖「原地覆盖」。Fiber 卸载=逆序回滚 effect，释放程序集引用后才可回收；换版本=新 Fiber 加载新程序集 | ① 放弃「覆盖式更新」，改 **side-by-side 版本目录**（`Plugins/<id>/versions/<ver>/`），旧版本等 ALC 真正 Unload + GC 后再删；② `Unload()` 后主动 `GC.Collect()` 两代并等待 `Finalizers`，确认句柄释放再动旧文件 | 高（纯工程，不改变架构） |
| **缺口 2·运行时无 DI/路由**：`AppBuilder:88/351` 仅启动注册一次；插件控制器不在 `ApplicationPartManager` | Cordis 的 service/event 都是运行期 effect，Fiber 重建即重新注册；provider 在 profile 启动时按 `cordis.yml` 组装 | ① 后端引入**可变 DI 容器 wrapper**：插件 `Initialize` 拿到的是可在运行期 `AddSingleton/AddScoped` 的 builder，卸载时逆序移除；② 端点用 **minimal API 动态 `app.Map*`（返回 `IEndpointRouteBuilder` 句柄）** 替代 `MapControllers()` 一次性注册，卸载时 `DataSource.Consumers` 移除对应 `ApplicationPart` | 中（需把 `AddControllers` 改成 `AddControllers().AddApplicationPart` + 运行期 `ApplicationPartManager` 增删） |
| **缺口 3·前端未插件化**：静态视图 + `features.ts` SSOT | dsh 前端（`client/*`）也是 Cordis 插件树，能力缝（如 `ctx.tools`）驱动 UI 组件挂载 | 前端引入**按扩展点动态 `import()`**：插件声明 `contributes.views / contributes.menus`，宿主用 `defineAsyncComponent` + 动态路由挂到 tabs；`features.ts` 退化为「已加载插件清单」而非硬编码真源 | 中（需前端运行时扩展点协议） |

### 2.1 最值得借鉴的一条心智模型
dsh 的热更新之所以稳，不是因为它解决了「文件锁」这个表象问题，而是因为它的**每一处注册都是可逆 effect，卸载即自动回滚**。本项目当前插件只做 `IPlugin` 元数据 + 菜单/工具薄覆盖，没有「可逆注册」概念——这正是热更新做不下去的根因。
→ **优先补「可逆注册」语义**（DI 服务、端点、前端视图都支持注册/注销配对），文件锁问题会自然退化为「等 ALC 回收」的工程细节。

### 2.2 不应照搬的部分
- dsh 是 **TypeScript/Node 运行时**，Cordis 是 JS 生态框架，**不能 1:1 移植到 .NET**。本项目应借鉴其*模式*（effect 可逆、seam 解耦、配置即组装、HMR 安全约束），而非引入 Cordis 本身。
- dsh 的 `invariant.ts` / 每 registry 一条 dispose 测试，对应到 .NET 即：**每个插件必须实现 `IDisposable` 且 `DestroyPlugin` 后断言资源释放（文件句柄、定时器、连接）**——建议写进插件契约。

---

## 3. 推荐落地动作（轻量起步，不触发大重构）

1. **先破文件锁（低风险）**：`PluginVersionService.Update` 改 side-by-side 版本目录；`Unload()` 后强制 `GC.Collect(2, GCCollectionMode.Forced)` + `GC.WaitForPendingFinalizers()` 两次，再删旧版本目录。
2. **补「可逆注册」契约（中风险）**：定义 `IPlugin.RegisterServices(IServiceCollection)` / `UnregisterServices(...)` 配对；端点改用 minimal API 动态注册并持有 `EndpointDataSource` 句柄以便卸载移除。
3. **HMR 安全测试（中风险）**：每个插件加一条 xUnit——`DestroyPlugin` 后断言 `AssemblyLoadContext.IsCollectible` 已 true 且文件锁释放（用 `File.Open(..., FileShare.None)` 验证 DLL 可删）。
4. **前端扩展点（中风险）**：在 `features.ts` 之上加 `contributes` 协议 + 动态 `import()`，先把一个现有面板（如 Memory）试点改造。

> 以上属「任务 6 正式 spec/plan」的范畴，仍需用户授权后再起草（高风险大重构，按 AGENTS.md 升级规则须先确认）。

---

## 4. Cordis 内核完整设计（上游 cordiverse/cordis 源码机制，2026-08-19 补查）

> 来源：DeepWiki `cordiverse/cordis`（dsh vendor 内嵌的上游框架，dsh 用的是 4.0.0-rc.7 + 本地硬化）。
> 本节是「.NET 移植该照抄什么语义」的权威依据；`packages/core/src/` 下四个文件即全部核心：`reflect.ts`（服务/DI）、`fiber.ts`（生命周期）、`events.ts`（事件）、`registry.ts`（插件注册）。

### 4.1 服务系统（ReflectService，packages/core/src/reflect.ts）

- **全局共享 store**：`ReflectService.store` 是「symbol → `Impl`」字典；`Impl` = `{ name, fiber（提供者）, value, check? }`。服务名 → symbol 的映射（`symbols.isolate`）建立在 **root 上下文**上。**服务默认全局可见**，不属某个 fiber 私有。
- **`provide()` 注册路径**：校验服务名已声明 → 在 root isolation map 建/取 symbol → `Impl` 存入全局 store，同时挂到提供者 `fiber.store`；fiber 处于 active 时立即 `notify(name)` 通知依赖者。
- **解析路径（`ctx[name]`，Proxy `handler.get`）**：取该服务名的 isolation key → 从**当前 fiber 的 store 沿父 fiber 链向上**查找 `Impl` → 命中即返回 `value`；若该 fiber `inject` 声明了此服务但找不到 → **抛错**（硬依赖缺失是错误，不是 null）；isolation key 不匹配则停止向上（隔离边界）。
- **卸载摘除**：`provide()` 内部把「删除 store 条目 + `notify(name)` + 清 `fiber.store`」注册为 fiber 的 effect——fiber dispose 逆序执行 effect 时自动摘除，**无需手动注销**。
- **`notify(name)`**：遍历所有 active fiber，对 `inject` 了该服务的 fiber 调 `_checkImpl(name)` + `_refresh()` 重新评估状态；同时 emit `internal/service` 事件。
- **隔离（`isolate`）**：`ctx.isolate(name)` 派生子上下文，把服务名映射到**新 symbol** → 该服务在此子树内私有，父级与其他分支不可见。这是「全局默认、私有显式」的设计。

### 4.2 Fiber 生命周期与 inject（packages/core/src/fiber.ts）

- **六状态**：`PENDING`（依赖未就绪，等待）→ `LOADING` → `ACTIVE` → `UNLOADING` → `DISPOSED`；异常进 `FAILED`。
- **inject 是「持续生效的依赖关系」，不是一次性注入**：
  - `_refresh()` 逐项检查 `inject` 数组：任一服务的 `Impl` 不可得 → epoch 置 `INACTIVE`；全部就绪 → epoch = 各提供者 fiber UID 的组合串。
  - `_setEpoch()` 检测 epoch 变化：`INACTIVE → 有效` 触发 `_reload()`（加载并转 ACTIVE）；`有效 → INACTIVE` 触发 `_unload()`（卸载转 PENDING/UNLOADING）。
  - 效果：**提供者热重载/卸载 → 依赖者自动卸载；提供者回归 → 依赖者自动重启**。加载顺序是依赖图的拓扑序，无人工编排（dsh 219 个包即如此）。
- **`inertia`**：异步状态迁移期间锁住 fiber 的 promise，防竞争。
- **`await ctx.plugin(...)`**：等待 fiber 到达 `ACTIVE` 或 `FAILED` 稳态。
- **dispose 语义**：逆序执行该 fiber 注册的全部 effect（事件监听、定时器、服务提供一并收回）。

### 4.3 事件系统（EventsService，packages/core/src/events.ts）

- **五种分发模式**（`isBailed`：返回值非 `null/false/undefined` 即视为短路值）：

| 模式 | 执行 | 返回 | 短路 |
|---|---|---|---|
| `emit` | 同步按序 | void | 无（监听器抛错会中断后续） |
| `parallel` | 异步并发（`Promise.allSettled`） | `Promise<void>` | 无（有拒绝则抛 `AggregateError`） |
| `serial` | 异步按序 | 首个非空值 | 有 |
| `bail` | 同步按序 | 首个非空值 | 有 |
| `waterfall` | 按序中间件链，监听器收 `next` | 链最终结果 | 不调 `next()` 即短路 |

- **注册**：`on(name, listener, { prepend, global })` / `once(...)`；注册时 `ctx.fiber.assertActive()` 校验；listener 经 `ctx.reflect.bind` 绑定 this 到所属上下文。
- **注销与 fiber 绑定**：注册走 `fiber.effect` → **fiber 卸载时事件监听自动移除**，无手动 off。
- **传播规则（沿上下文树，非 DOM 冒泡）**：子上下文 emit 的事件，**父上下文上的监听器默认接收**；`Context.filter` symbol 可让上下文自定义过滤逻辑；`global: true` 注册的监听器无视 filter。
- **internal 事件族**（框架自用的拦截/观察点）：`internal/plugin`（fiber 创建/销毁）、`internal/status`（fiber 状态变化）、`internal/service`（服务注册/注销）、`internal/update`（配置更新 waterfall）、`internal/get` / `internal/set`（上下文属性读写拦截）、`internal/listener`（新监听注册，bail 分发）、`internal/dispatch`（任何事件分发前钩子）。
- **类型安全**：TS declaration merging——各包向 `Events` 接口合并自己的事件名与签名，`emit/on` 全链路类型检查。

### 4.4 配置加载与 HMR（loader：Entry/EntryTree + packages/hmr）

- **Entry/EntryTree**：每个插件一行配置（Entry），EntryTree 层级管理。`entry.update()` 时分两条路：
  - **原地更新**：仅 config 变化且插件是 group → `_patchContext` → `fiber.update()` → 发 `internal/update` waterfall（系统可拦截）→ 插件不重建即响应新配置；
  - **全量重载**：其他选项变化 → dispose 旧 fiber + `init()` 建新 fiber。
- **HMR（packages/hmr）**：`partialReload()` 定位受影响插件 → 清模块缓存 → 重新 import → 成功则 dispose 旧实例、建新实例；**re-import/初始化出错则整体回滚**。改到框架外层模块（cordis 核心等）不属 HMR 安全范围 → 触发全进程重启。
- **HMR 安全判据**：包必须能干净 dispose + 重新初始化（dsh 用 `invariant.ts` + dispose 测试门禁强制，见 §5.3）。

---

## 5. dsh 应用层模式（deepseek-harness 如何用 Cordis，2026-08-19 补查）

### 5.1 能力缝（Seam）的定义与多 Provider

- **定义**：用 TS declaration merging 把服务接口合入 `Context`（如 `ctx.llm`/`ctx.tools`/`ctx.fs`/`ctx.shell`/`ctx.sessions`/`ctx.systemPrompt`），消费者编译期只见接口。
- **Provider 实现**：插件写 `Service` 子类，`super(ctx, 'name')` 即注册。一个 seam 可挂**多个 provider**——如 `llm-deepseek` 把 `'deepseek-official'` 注册进 `ctx.llm` 的 provider 列表，dispose 时该注册被 unwound（HMR 安全）。
- 各 seam 的 owner 包：`dsh-llm-*`（LLM 适配）、`dsh-fs-local/sandbox/e2b`（文件系统）、`dsh-bash-*/dsh-pwsh-local`（shell）、`dsh-session`（会话日志）、`dsh-system-prompt`（提示装配）、`dsh-tools`（工具注册表 + 守卫执行管道）。

### 5.2 消费者与工具注册

- **硬依赖**：消费者插件 `inject: ['tools']`——未就绪不启动（PENDING），消失即卸载、回归即重启。
- **工具插件**：`ctx.tools.register(defineTool({...}))`——注册是 effect，卸载自动摘除；`defineTool` 把参数转 JSON Schema 并校验入参。

### 5.3 HMR 安全约定与测试门禁

- **`invariant.ts`**：每个包必须提供，注册 invariant companion（`ctx.invariants.register`），运行时验证该包可被安全 dispose。
- **dispose 测试门禁**：每个贡献 registry 的包必须有一条测试——dispose 掉贡献 fiber，断言清理成功。范例：`LlmDeepSeek` 测试 `fiber.dispose()` 后 `ctx.llm.listProviders()` 返回空数组。

### 5.4 Profiles / Bundles（配置即组装）

- **Bundle** = npm 包 + `package.json` 声明 `"dsh": { "bundle": { "patch": "./cordis.patch.yml" } }`；`dsh-base` 是所有实例的基座（模型适配、工具、持久化、策略）。
- **Profile** = `$DSH_HOME/profiles/<name>/`（`dsh.profile` 清单 + `cordis.patch.yml`）。
- **合成顺序**：`dsh.profile.bundles` 各 bundle patch → profile 自身 patch → `--patch` 覆盖层；**后层按 id 覆盖前层整个 config 值**。同一代码库换 bundle/profile 即跑 CLI/Web/headless。

### 5.5 软依赖（可选能力）范式 —— 关键判例

- **硬依赖用 `inject`，软依赖用 `ctx.get(name)`**：`ctx.get` 运行期探测服务存在性，缺席返回 `undefined`，插件照常工作、不被阻塞。
- **官方先例**：`tool-fs-search` 插件把 `ctx.spillStore` 作可选依赖——经 `ctx.get` 读取，缺席不影响该工具的 schema 与功能。
- **推论**：「有则增强、无则降级」的可选能力**不需要事件拦截点**，服务契约 + `ctx.get` 即完整表达；事件 waterfall 只用于「多方竞争改写/中间件链」场景。

### 5.6 服务可见性与生命周期——权威裁决（2026-08-19 DeepWiki 三连查）

> 解决「服务全局 vs 本地」「解析顺序」「生命周期/作用域」三个实施必答问题。以下为 `cordiverse/cordis` 源码级答案，是本项目实现 `Context` 语义修正的唯一依据（不自作设计）。

**裁决 A —— 服务全局 vs 本地值如何区分**（`reflect.ts` handler.set / props / store）：
- Cordis 区分两类：**声明服务**（`ctx.reflect.provide(name, value)` 或 `Service` 子类 `super(ctx,'name')`）→ 进全局 `store`，所有上下文可见；**本地值**（直接 `ctx[name]=value` 且 name 非声明服务/accessor）→ 仅存于该 `Context` 实例（`target`），**不参与全局 store、`ctx.get` 解析不到**。
- **区分机制** = 是否经 `provide()` 声明，而非命名约定。`props` 字典记录哪些属性是服务；`set` 处理器据此分流。
- **本项目推论**：`IContext.Register<T>` 应对标「`provide()`」= 提供全局服务；fiber 私有框架对象（`PluginMetadata`/`IServiceCollection`）用**独立的本地值 API**（如 `RegisterLocal`/`SetLocal`），不再复用 `Register`。

**裁决 D —— 解析顺序**（`reflect.ts` handler.get）：
1. 特殊属性（symbol/保留字/`_` 开头）→ 直接 `Reflect.get`；
2. **本地直接属性**（`Reflect.has(target, prop)`）→ 命中即返回（**本地优先于同名全局服务**）；
3. accessor 属性 → 调其 get；
4. 全局服务 → 沿 fiber 链向上查 `store`（经 `internal/get` waterfall 可拦截）。
- **本项目推论**：`Context.Get<T>()` 解析顺序 = 本地值 → 全局共享表；本地值可遮蔽同名全局服务。

**裁决 F —— 服务生命周期**（`fiber.ts` / `reflect.ts`）：
- 服务 = **每上下文单例**：`provide` 存单个实例，所有消费者共享；**无 per-request/per-session 作用域**。子上下文 `extend`/`isolate` 可覆盖同名服务实现（即作用域=上下文层级，非请求级）。
- **eager 构造**：`provide(name, value)` 直接收实例，无内置懒工厂；昂贵/异步资源用 `ctx.fiber.effect(() => { 构造; return dispose; })` 包裹构造与清理。
- **提供即 effect**：`provide()` 本身注册为 fiber 的 effect，fiber dispose 自动从全局 store 摘除并 `notify` 依赖者。
- **本项目推论（推翻此前「懒解析委托」妥协）**：`IWorkflowAIAdvisor` 应为**eager 单例**（AIAgent 在 `Apply` 时构造并 `ctx.Register`），非懒解析委托；需要跨请求共享单例依赖（如 `IAIAgentService`）就让它本身是单例/有状态安全，不做 Scoped 语义伪实现。

**裁决 B/C —— 宿主 root 契约生命周期**：
- root context 有专属 fiber（`uid=0`, runtime=null），**永不随插件卸载**；root 提供的服务经 `provide` 关联该 root fiber，其 dispose 是 `() => restart()`（整进程重启才清）。
- **本项目推论**：`PluginManager.ProvideHostServices` seed 进 root 的宿主契约（`IConfigurationService`/`IToolRegistry` 等）**常驻 app 生命周期**，正确，不需特殊处理；插件提供的服务随其 fiber 摘除。

**裁决 E —— PluginServiceRegistry 与共享表的关系**：
- 采用「共享表 + eager 单例 + `ctx.Get`」后，消费方不再经 `_serviceProvider.GetService(ISomePluginContract)` 走 `PluginServiceRegistry` 的宿主转发；该类型从 `CollectForwardDescriptors` 转发集合移除，避免双通道解析同一契约。
- **本项目推论**：`IWorkflowAIAdvisor`（以及后续一切「插件→插件」契约）**不进 `CollectForwardDescriptors`**，仅走 `ctx.Get`；`PluginServiceRegistry` 只保留各插件「自身服务」的子容器解析（供插件内部 MS DI 构造注入用）。

---

## 6. 本项目 .NET 移植的偏差清单与执行指导（防偏差对照表）

> 用途：实施内核/插件互通改动时逐条对照；实现与本文出现分歧时，以本表「Cordis 原案」列为准（除非有明确 ADR 记录偏离理由）。

| # | 维度 | Cordis 原案 | 本项目现状（2026-08-19 代码核实） | 偏差影响 | 修正方向 |
|---|---|---|---|---|---|
| 1 | 服务可见性 | 写入全局共享 store（root isolation map），**默认全局可见**；本地值经直接赋值（非 `provide`）留在本地 | `Context.Register` 只写**自身字典**，兄弟 fiber 不可见（`Context.cs:33-38/56-63`） | 插件间接缝无法互通（`IWorkflowAIAdvisor` 案例：AIAgent 提供、WorkflowEngine 消费，解析恒为 null） | `Register` 对标 `provide()` 提升到 root 共享服务表（见 §5.6 裁决 A）；fiber 私有框架对象改用独立本地值 API（`RegisterLocal`），不复用 `Register` |
| 2 | 服务摘除 | `provide` 本身是 effect，dispose 自动摘条目 + `notify` 依赖者 | 无共享表，卸载不涉及服务摘除 | 若只提升不摘除 → 卸载后残留旧实例（悬空引用） | 共享表注册走 `ctx.Effect`，Fiber 逆序回滚即摘除（见 §5.6 裁决 F） |
| 3 | 硬依赖 | `inject` 持续生效：PENDING 等待 + `notify→_refresh→_setEpoch` 自动卸载/重启 | 无 inject 机制（`PluginMetadata.Provides/Consumes` 字段预留但无人消费）；加载顺序靠静态拓扑排序 | 强依赖无法表达；提供者热重载后依赖者持旧实例悬空 | 后续按 `Provides/Consumes` 实现 PENDING/notify/refresh（对标 `_refresh`/`_setEpoch`）；首个强依赖接缝出现时落地 |
| 4 | 软依赖 | `ctx.get(name)` 探测，缺席照常工作（dsh `tool-fs-search` 判例） | 无明确范式 | 可选增强类接缝实现路径不统一 | **明确约定：可选 = `ctx.Get<T>()` 不声明任何依赖，null 即降级**；禁止缓存实例（每次用每次 Get），防热重载悬空 |
| 5 | 事件传播 | 沿上下文树：子 emit 父默认收到；`filter` 过滤；`global` 无视 filter | 每个 `Context` 持有**独立 `EventBus`**（`Context.cs:21`），互不传播 | 跨插件事件根本不通——插件间 waterfall 拦截点不可用（目前仅平台级 `IEventBus` 单例接线 `tools/*`） | 派生上下文共享根 `EventBus` 实例（`Derive` 时传递），或按 Cordis filter 语义实现传播 |
| 6 | 监听注销 | 注册走 `fiber.effect`，卸载自动移除 | `EventBus.On` 返回 `IDisposable`，靠插件自觉配对 | 泄漏/ALC 回收失败风险 | 惯例：Apply 内所有 `On` 必须包进 `ctx.Effect`；保留「卸载后断言清理成功」测试门禁 |
| 7 | HMR 粒度 | `partialReload` 出错回滚；`fiber.update` 原地更新走 `internal/update` waterfall | `PluginHotReloadWatcher` 全量拆建 fiber，无原地更新、无失败回滚 | 热重载失败态处理弱 | 现状可接受（全量重建语义简单）；原地更新列为增强项，非必需 |

### 6.1 执行 FAQ（实施遇阻先查这里）

**Q1：插件 A 的服务要给插件 B 用，怎么接？**
A：A 在 `Apply` 里**eager 构造**服务实例，再 `ctx.Register<TContract>(实例)` 提供到共享表（对标 `provide()`，见 §5.6 裁决 A/F）；B 构造注入 `IContext`，用 `ctx.Get<TContract>()` 消费。可选能力 → B 不声明任何依赖，null 走降级；必备能力 → 待 inject 机制（偏差#3）落地后声明 `Consumes` 获得 PENDING 等待语义。当前一律按可选处理。

**Q2：提供者热重载/卸载后，消费者怎么办？**
A：共享表条目随 Fiber 回滚自动摘除（`provide` 即 effect，见 §5.6 裁决 F）→ 消费者下次 `Get` 得 null → 走降级分支。**消费者禁止把 Get 到的实例缓存为字段**（每次使用每次 Get），这是防悬空引用的硬约束。

**Q3：服务实例的 Scoped 依赖（如 DbContext）怎么处理？**
A：Cordis 服务 = **每上下文单例**，无 per-request 作用域（§5.6 裁决 F）。服务在 `Apply` 时 eager 构造，依赖的宿主 Scoped 服务（如 DbContext）需在构造时经 `ctx.Get` 取得可用实例；若确需请求级上下文，用「每次 Get 每次新建的轻量包装」而非伪 Scoped。**禁止**把依赖 Scoped 服务的实例 seed 成永久单例（captive dependency）；不采用懒解析委托（Cordis 无此机制）。

**Q4：可选增强要不要改走事件 waterfall？**
A：默认不要。服务契约 + 软依赖 Get 已完整覆盖「有则增强、无则降级」（§5.5 判例）。只有需要多方按序竞争改写/中间件链时才用 waterfall 事件——且须先完成偏差#5（事件传播）修正。

**Q5：`IWorkflowAIAdvisor` 接线的标准答案？**
A：①AIAgent `Apply` 补注册 `IAIWorkflowAssistant`/`IWorkflowAIAdvisor` 进子容器（供插件内部构造注入），**eager 构造** `AIWorkflowAdvisor` 实例并 `ctx.Register<IWorkflowAIAdvisor>(实例)` 提供到共享表（§5.6 裁决 F）；②`WorkflowExecutor` 构造注入 `IContext`，`GetAIAdviceAsync` 改 `_ctx.Get<IWorkflowAIAdvisor>()`，null → 默认重试（**保留现有降级语义**）；③`IWorkflowAIAdvisor` **不进 `CollectForwardDescriptors`**（§5.6 裁决 E）；④测试：提供后可解析 → 卸载后解析为 null 且默认重试 → 重挂载恢复（dispose 门禁）。

**Q6：宿主 seed 契约（`ProvideHostServices`）与插件提供服务的关系？**
A：同一套共享表，两个来源。宿主 seed（`IConfigurationService`/`IToolRegistry` 等「宿主→插件」契约）在启动时写入 root，root fiber 永不卸载 → **常驻 app 生命周期**（§5.6 裁决 B/C）；插件提供（「插件→插件」契约）在 Apply 时写入、随 Fiber 摘除。`HostProvidedServiceContracts` 清单只管前者。

---

## 7. 验证记录
- DeepWiki `deepseek-ai/deepseek-harness` 文档读取比例：核心插件化章节（§2.1 Cordis / §2.2 Profiles&Bundles / §2.3 Event Bus&Seams / §8 测试 HMR 安全）**100% 已读**；其余（Agent System / Execution Environment / API 层 / Web UI）为概览级，未逐行精读。
- DeepWiki `cordiverse/cordis`（2026-08-19 补查）：wiki 结构 9 章全览 + **七轮**定向问答——ReflectService 服务机制、Fiber 生命周期/HMR、事件系统、dsh 应用层模式（含软依赖判例 `tool-fs-search`）、**服务全局/本地区分与解析顺序、服务生命周期（单例/eager/effect）、provide 即 effect 与宿主 root 生命周期**（§4-§6 内容均出自各轮源码级回答）。
- 本项目偏差现状已用源码核实（2026-08-19）：`Context.cs:21/33-38/56-63`（独立 EventBus、Register 写自身字典、Get 仅父链）、`PluginServiceRegistry.cs`（无宿主转发）、`PluginManager.cs:129-171`（root seed 仅宿主契约）、`AIAgentPlugin.cs:23-30`（Advisor/Assistant 未注册）、`WorkflowEnginePlugin.cs:37-39`（Executor 持 fiber ctx）、`WorkflowExecutor.cs:281`（GetService 查 IWorkflowAIAdvisor）。
- 早前核实：`AppBuilder.cs:88/351`、`OpenForgeSelf.Backend.csproj` 仅 `None Update="Plugins\**\plugin.json"`（未 `Compile Remove`，11 插件拆程序集后已更新）。
