# 调研：deepseek-ai/deepseek-harness 的插件化设计（DeepWiki）

> 本文定位：**调研依据（为什么这样设计）**，是 `../15-roadmap/plugin-architecture.md`（执行手册）的支撑文档。本文保留 dsh 机制细节与源码证据，落地步骤已全部并入 roadmap。
> 来源：DeepWiki `deepseek-ai/deepseek-harness` 全量文档（466KB，已分段读取核心章节 2.1 / 2.2 / 2.3 / 8 测试）。
> 对照：本项目「任务 6」三缺口（文件锁 / 运行时无 DI·路由 / 前端未插件化）。
> 结论先行：**dsh 的「可卸载性」来自「一切皆 effect、卸载即反向回滚」，而非靠文件锁技巧**——这正是本项目热更新最该借鉴的底层心智模型。

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

## 4. 验证记录
- DeepWiki 文档读取比例：核心插件化章节（§2.1 Cordis / §2.2 Profiles&Bundles / §2.3 Event Bus&Seams / §8 测试 HMR 安全）**100% 已读**；其余（Agent System / Execution Environment / API 层 / Web UI）为概览级，未逐行精读。
- 本项目三缺口现状已用 Grep 源码确认：`AppBuilder.cs:88/351`、`OpenForgeSelf.Backend.csproj` 仅 `None Update="Plugins\**\plugin.json"`（未 `Compile Remove`）。
